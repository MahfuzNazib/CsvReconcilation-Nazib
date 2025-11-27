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
}

