using CsvHelper;
using CsvHelper.Configuration;
using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CsvReconcile.Infrastructure.Csv;

/// <summary>
/// CSV reader implementation using CsvHelper library
/// </summary>
public class CsvHelperReader : ICsvReader
{
  private readonly ILogger _logger;

  public CsvHelperReader(ILogger logger)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <summary>
  /// Reads all records from a CSV file
  /// </summary>
  public async Task<List<CsvRecord>> ReadAllAsync(
      string filePath,
      string delimiter = ",",
      bool hasHeaderRow = true,
      CancellationToken cancellationToken = default)
  {
    var records = new List<CsvRecord>();

    await foreach (var record in ReadStreamAsync(filePath, delimiter, hasHeaderRow, cancellationToken))
    {
      records.Add(record);
    }

    return records;
  }

  /// <summary>
  /// Reads CSV records as a stream for large file processing
  /// </summary>
  public async IAsyncEnumerable<CsvRecord> ReadStreamAsync(
      string filePath,
      string delimiter = ",",
      bool hasHeaderRow = true,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(filePath))
    {
      _logger.Error("File path is null or empty");
      throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
    }

    if (!File.Exists(filePath))
    {
      _logger.Warning("File not found: {FilePath}", filePath);
      yield break;
    }

    // Validate file is not empty
    var fileInfo = new FileInfo(filePath);
    if (fileInfo.Length == 0)
    {
      _logger.Warning("File is empty: {FilePath}", filePath);
      yield break;
    }

    StreamReader? reader = null;
    CsvReader? csv = null;
    string[] headers;
    int lineNumber = 1;
    bool isFirstDataRow = false;

    try
    {
      var config = new CsvConfiguration(CultureInfo.InvariantCulture)
      {
        Delimiter = delimiter,
        HasHeaderRecord = hasHeaderRow,
        MissingFieldFound = null, // Don't throw on missing fields
        BadDataFound = context =>
        {
          _logger.Warning("Bad data found in {File}: {RawRecord}",
                    filePath, context.RawRecord);
        },
        TrimOptions = TrimOptions.Trim
      };

      reader = new StreamReader(filePath);
      csv = new CsvReader(reader, config);

      if (hasHeaderRow)
      {
        // Read header row
        if (!await csv.ReadAsync())
        {
          _logger.Warning("File is empty or could not read header row: {FilePath}", filePath);
          yield break;
        }

        csv.ReadHeader();
        var rawHeaders = csv.HeaderRecord ?? Array.Empty<string>();

        if (rawHeaders.Length == 0)
        {
          _logger.Error("No headers found in file (file may be empty or malformed): {FilePath}", filePath);
          throw new InvalidOperationException($"CSV file '{filePath}' has no header row or is malformed.");
        }

        // Validate and normalize headers
        headers = ValidateAndNormalizeHeaders(rawHeaders, filePath);
        isFirstDataRow = false;
      }
      else
      {
        // When no header row, read first data row to determine column count
        if (!await csv.ReadAsync())
        {
          _logger.Warning("File is empty or has no data rows: {FilePath}", filePath);
          yield break;
        }

        // Get the record to determine column count
        var firstRecord = csv.Parser.Record;
        if (firstRecord == null || firstRecord.Length == 0)
        {
          _logger.Error("First data row is empty in file: {FilePath}", filePath);
          throw new InvalidOperationException($"CSV file '{filePath}' first data row is empty.");
        }

        // Generate column names based on column count
        headers = new string[firstRecord.Length];
        for (int i = 0; i < firstRecord.Length; i++)
        {
          headers[i] = $"Column{i + 1}";
        }

        _logger.Debug("Generated {Count} column headers for file without header row: {FilePath}",
            headers.Length, filePath);
        isFirstDataRow = true;
      }
    }
    catch (ReaderException ex)
    {
      _logger.Error(ex, "CSV parsing error in file {FilePath}: {Message}", filePath, ex.Message);
      throw new InvalidOperationException($"Failed to parse CSV file '{filePath}': {ex.Message}. " +
          $"See inner exception for details.", ex);
    }
    catch (ParserException ex)
    {
      _logger.Error(ex, "CSV parsing error in file {FilePath} at line {Line}: {Message}",
          filePath, ex.Context?.Parser?.Row ?? 0, ex.Message);
      throw new InvalidOperationException($"Failed to parse CSV file '{filePath}' at line {ex.Context?.Parser?.Row ?? 0}: {ex.Message}. " +
          $"See inner exception for details.", ex);
    }
    catch (HeaderValidationException ex)
    {
      _logger.Error(ex, "CSV header validation error in file {FilePath}: {Message}", filePath, ex.Message);
      throw new InvalidOperationException($"CSV header validation failed for file '{filePath}': {ex.Message}. " +
          $"See inner exception for details.", ex);
    }
    catch (IOException ex)
    {
      _logger.Error(ex, "I/O error reading file {FilePath}: {Message}", filePath, ex.Message);
      throw new InvalidOperationException($"I/O error reading CSV file '{filePath}': {ex.Message}. " +
          "The file may be locked, inaccessible, or corrupted.", ex);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.Error(ex, "Access denied reading file {FilePath}: {Message}", filePath, ex.Message);
      throw new UnauthorizedAccessException($"Access denied reading CSV file '{filePath}': {ex.Message}.", ex);
    }
    catch (Exception ex) when (!(ex is InvalidOperationException))
    {
      _logger.Error(ex, "Unexpected error initializing CSV reader for file {FilePath}: {Message}", filePath, ex.Message);
      throw new InvalidOperationException($"Unexpected error reading CSV file '{filePath}': {ex.Message}.", ex);
    }

    // Now read and yield records outside of try-catch to avoid yield-in-try-catch restriction
    if (csv == null || reader == null)
    {
      yield break;
    }

    try
    {
      // Process first row if it was already read (no-header case)
      if (isFirstDataRow)
      {
        cancellationToken.ThrowIfCancellationRequested();
        lineNumber = csv.Parser.Row;

        CsvRecord? firstRecord = null;
        try
        {
          var csvRecord = new CsvRecord
          {
            SourceFile = Path.GetFileName(filePath),
            LineNumber = lineNumber
          };

          // Read by index when no headers
          var record = csv.Parser.Record;
          if (record != null)
          {
            for (int i = 0; i < headers.Length && i < record.Length; i++)
            {
              csvRecord.SetField(headers[i], record[i] ?? string.Empty);
            }
            firstRecord = csvRecord;
          }
        }
        catch (Exception ex)
        {
          _logger.Warning(ex, "Error processing first record at line {LineNumber} in file {FilePath}: {Message}",
              lineNumber, filePath, ex.Message);
          // Continue processing next record
        }

        if (firstRecord != null)
        {
          yield return firstRecord;
        }
      }

      // Read remaining records (for hasHeaderRow=true, this starts at first data row; 
      // for hasHeaderRow=false, this continues from second row)
      while (await csv.ReadAsync())
      {
        cancellationToken.ThrowIfCancellationRequested();

        if (hasHeaderRow)
        {
          lineNumber++; // Data rows start at line 2 when header exists
        }
        else
        {
          // Line number equals current row number when no header
          lineNumber = csv.Parser.Row;
        }

        CsvRecord? csvRecord = null;
        try
        {
          csvRecord = new CsvRecord
          {
            SourceFile = Path.GetFileName(filePath),
            LineNumber = lineNumber
          };

          // Read all fields dynamically
          if (hasHeaderRow)
          {
            foreach (var header in headers)
            {
              try
              {
                var value = csv.GetField(header) ?? string.Empty;
                csvRecord.SetField(header, value);
              }
              catch (Exception ex)
              {
                _logger.Warning(ex, "Error reading field '{Header}' at line {LineNumber} in file {FilePath}: {Message}",
                    header, lineNumber, filePath, ex.Message);
                csvRecord.SetField(header, string.Empty);
              }
            }
          }
          else
          {
            // Read by index when no headers
            var record = csv.Parser.Record;
            if (record != null)
            {
              for (int i = 0; i < headers.Length && i < record.Length; i++)
              {
                csvRecord.SetField(headers[i], record[i] ?? string.Empty);
              }
            }
          }
        }
        catch (Exception ex)
        {
          _logger.Warning(ex, "Error processing record at line {LineNumber} in file {FilePath}: {Message}",
              lineNumber, filePath, ex.Message);
          // Continue processing next record
          csvRecord = null;
        }

        if (csvRecord != null)
        {
          yield return csvRecord;
        }
      }

      var recordCount = hasHeaderRow ? lineNumber - 1 : lineNumber;
      _logger.Debug("Finished reading {RecordCount} records from {FilePath}",
          recordCount, filePath);
    }
    finally
    {
      // Dispose resources
      csv?.Dispose();
      reader?.Dispose();
    }
  }

  /// <summary>
  /// Validates and normalizes CSV headers, handling duplicates and empty names
  /// </summary>
  private string[] ValidateAndNormalizeHeaders(string[] headers, string filePath)
  {
    if (headers == null || headers.Length == 0)
    {
      throw new InvalidOperationException($"CSV file '{filePath}' has no headers.");
    }

    var normalizedHeaders = new string[headers.Length];
    var headerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var invalidHeaders = new List<int>();

    for (int i = 0; i < headers.Length; i++)
    {
      var originalHeader = headers[i];
      var trimmedHeader = string.IsNullOrWhiteSpace(originalHeader) ? null : originalHeader.Trim();

      // Handle empty or whitespace headers
      if (string.IsNullOrWhiteSpace(trimmedHeader))
      {
        trimmedHeader = $"Column{i + 1}";
        invalidHeaders.Add(i);
        _logger.Warning("Header at index {Index} in file {FilePath} is empty or whitespace. Renamed to '{Header}'.",
            i, filePath, trimmedHeader);
      }

      // Handle duplicate headers
      if (headerCounts.ContainsKey(trimmedHeader))
      {
        var count = headerCounts[trimmedHeader] + 1;
        headerCounts[trimmedHeader] = count;
        var duplicateHeader = $"{trimmedHeader}_{count}";
        normalizedHeaders[i] = duplicateHeader;
        _logger.Warning("Duplicate header '{Header}' at index {Index} in file {FilePath}. Renamed to '{DuplicateHeader}'.",
            trimmedHeader, i, filePath, duplicateHeader);
      }
      else
      {
        headerCounts[trimmedHeader] = 1;
        normalizedHeaders[i] = trimmedHeader;
      }
    }

    if (invalidHeaders.Count > 0)
    {
      _logger.Warning("Found {Count} invalid headers (empty or whitespace) in file {FilePath}. They have been renamed.",
          invalidHeaders.Count, filePath);
    }

    return normalizedHeaders;
  }
}

