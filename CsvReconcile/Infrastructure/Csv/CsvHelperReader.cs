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
    if (!File.Exists(filePath))
    {
      _logger.Warning("File not found: {FilePath}", filePath);
      yield break;
    }

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

    using var reader = new StreamReader(filePath);
    using var csv = new CsvReader(reader, config);

    // Read header to get field names
    await csv.ReadAsync();
    csv.ReadHeader();
    var headers = csv.HeaderRecord ?? Array.Empty<string>();

    if (headers.Length == 0)
    {
      _logger.Warning("No headers found in file: {FilePath}", filePath);
      yield break;
    }

    int lineNumber = 1; // Header is line 1

    // Read records
    while (await csv.ReadAsync())
    {
      cancellationToken.ThrowIfCancellationRequested();
      lineNumber++;

      var csvRecord = new CsvRecord
      {
        SourceFile = Path.GetFileName(filePath),
        LineNumber = lineNumber
      };

      // Read all fields dynamically
      foreach (var header in headers)
      {
        var value = csv.GetField(header) ?? string.Empty;
        csvRecord.SetField(header, value);
      }

      yield return csvRecord;
    }

    _logger.Debug("Finished reading {RecordCount} records from {FilePath}",
        lineNumber - 1, filePath);
  }
}

