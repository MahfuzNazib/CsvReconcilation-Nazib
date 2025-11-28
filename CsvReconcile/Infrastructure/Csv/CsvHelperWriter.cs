using CsvHelper;
using CsvHelper.Configuration;
using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Globalization;

namespace CsvReconcile.Infrastructure.Csv;

/// <summary>
/// CSV writer implementation using CsvHelper library
/// </summary>
public class CsvHelperWriter : ICsvWriter
{
  private readonly ILogger _logger;

  public CsvHelperWriter(ILogger logger)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <summary>
  /// Writes records to a CSV file
  /// </summary>
  public async Task WriteAsync(
      string filePath,
      IEnumerable<CsvRecord> records,
      string delimiter = ",",
      CancellationToken cancellationToken = default)
  {
    if (records == null)
      throw new ArgumentNullException(nameof(records));

    var recordsList = records.ToList();
    if (recordsList.Count == 0)
    {
      _logger.Debug("No records to write to {FilePath}", filePath);
      return;
    }

    // Ensure directory exists
    var directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
      Delimiter = delimiter
    };

    await using var writer = new StreamWriter(filePath);
    await using var csv = new CsvWriter(writer, config);

    // Collect all unique headers from all records
    var allHeaders = recordsList
        .SelectMany(r => r.Fields.Keys)
        .Distinct()
        .OrderBy(h => h)
        .ToList();

    // Write header
    foreach (var header in allHeaders)
    {
      csv.WriteField(header);
    }
    await csv.NextRecordAsync();

    // Write records
    foreach (var record in recordsList)
    {
      cancellationToken.ThrowIfCancellationRequested();

      foreach (var header in allHeaders)
      {
        var value = record.GetField(header);
        csv.WriteField(value);
      }
      await csv.NextRecordAsync();
    }

    _logger.Debug("Wrote {RecordCount} records to {FilePath}", recordsList.Count, filePath);
  }

  /// <summary>
  /// Writes a single record to a CSV file (for error logging)
  /// </summary>
  public async Task WriteAsync(
      string filePath,
      CsvRecord record,
      string delimiter = ",",
      CancellationToken cancellationToken = default)
  {
    await WriteAsync(filePath, new[] { record }, delimiter, cancellationToken);
  }

  /// <summary>
  /// Writes records from an async enumerable stream to a CSV file with buffering
  /// </summary>
  public async Task WriteStreamAsync(
      string filePath,
      IAsyncEnumerable<CsvRecord> records,
      string delimiter = ",",
      CancellationToken cancellationToken = default)
  {
    if (records == null)
      throw new ArgumentNullException(nameof(records));

    // Ensure directory exists
    var directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
      Delimiter = delimiter
    };

    await using var writer = new StreamWriter(filePath);
    await using var csv = new CsvWriter(writer, config);

    var headersWritten = false;
    var allHeaders = new HashSet<string>();
    var buffer = new List<CsvRecord>();
    const int bufferSize = 10000; // Buffer 10K records at a time

    await foreach (var record in records)
    {
      cancellationToken.ThrowIfCancellationRequested();

      // Collect headers from records
      foreach (var key in record.Fields.Keys)
      {
        allHeaders.Add(key);
      }

      buffer.Add(record);

      // Write when buffer is full
      if (buffer.Count >= bufferSize)
      {
        if (!headersWritten)
        {
          foreach (var header in allHeaders.OrderBy(h => h))
          {
            csv.WriteField(header);
          }
          await csv.NextRecordAsync();
          headersWritten = true;
        }

        foreach (var bufferedRecord in buffer)
        {
          foreach (var header in allHeaders.OrderBy(h => h))
          {
            var value = bufferedRecord.GetField(header);
            csv.WriteField(value);
          }
          await csv.NextRecordAsync();
        }

        buffer.Clear();
      }
    }

    // Write remaining records
    if (buffer.Count > 0)
    {
      if (!headersWritten)
      {
        foreach (var header in allHeaders.OrderBy(h => h))
        {
          csv.WriteField(header);
        }
        await csv.NextRecordAsync();
      }

      foreach (var bufferedRecord in buffer)
      {
        foreach (var header in allHeaders.OrderBy(h => h))
        {
          var value = bufferedRecord.GetField(header);
          csv.WriteField(value);
        }
        await csv.NextRecordAsync();
      }
    }

    _logger.Debug("Finished streaming records to {FilePath}", filePath);
  }

  /// <summary>
  /// Opens a streaming writer for incremental writes
  /// </summary>
  public async Task<IStreamingCsvWriter> OpenStreamWriterAsync(
      string filePath,
      string delimiter = ",",
      CancellationToken cancellationToken = default)
  {
    // Ensure directory exists
    var directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    return new StreamingCsvWriter(filePath, delimiter, _logger);
  }

  /// <summary>
  /// Internal implementation of streaming CSV writer
  /// </summary>
  private class StreamingCsvWriter : IStreamingCsvWriter
  {
    private readonly StreamWriter _writer;
    private readonly CsvWriter _csv;
    private readonly ILogger _logger;
    private readonly HashSet<string> _headers = new();
    private bool _headersWritten = false;
    private readonly string _delimiter;

    public StreamingCsvWriter(string filePath, string delimiter, ILogger logger)
    {
      _delimiter = delimiter;
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      var config = new CsvConfiguration(CultureInfo.InvariantCulture)
      {
        Delimiter = delimiter
      };

      _writer = new StreamWriter(filePath);
      _csv = new CsvWriter(_writer, config);
    }

    public async Task WriteRecordAsync(CsvRecord record, CancellationToken cancellationToken = default)
    {
      if (record == null)
        return;

      cancellationToken.ThrowIfCancellationRequested();

      // Collect headers
      foreach (var key in record.Fields.Keys)
      {
        _headers.Add(key);
      }

      // Write headers if not written yet
      if (!_headersWritten)
      {
        foreach (var header in _headers.OrderBy(h => h))
        {
          _csv.WriteField(header);
        }
        await _csv.NextRecordAsync();
        _headersWritten = true;
      }

      // Write record
      foreach (var header in _headers.OrderBy(h => h))
      {
        var value = record.GetField(header);
        _csv.WriteField(value);
      }
      await _csv.NextRecordAsync();
    }

    public async Task WriteRecordsAsync(IEnumerable<CsvRecord> records, CancellationToken cancellationToken = default)
    {
      if (records == null)
        return;

      foreach (var record in records)
      {
        await WriteRecordAsync(record, cancellationToken);
      }
    }

    public async ValueTask DisposeAsync()
    {
      if (_csv != null)
      {
        await _csv.DisposeAsync();
      }

      if (_writer != null)
      {
        await _writer.DisposeAsync();
      }
    }
  }
}

