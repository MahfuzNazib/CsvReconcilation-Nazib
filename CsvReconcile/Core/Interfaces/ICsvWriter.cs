using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Abstraction for writing CSV files
/// </summary>
public interface ICsvWriter
{
    Task WriteAsync(
        string filePath,
        IEnumerable<CsvRecord> records,
        string delimiter = ",",
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        string filePath,
        CsvRecord record,
        string delimiter = ",",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes records from an async enumerable stream to a CSV file.
    /// This method is memory-efficient for large datasets as it writes incrementally.
    /// </summary>
    Task WriteStreamAsync(
        string filePath,
        IAsyncEnumerable<CsvRecord> records,
        string delimiter = ",",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a streaming writer that can be used to write records incrementally.
    /// Returns a disposable writer that must be disposed after use.
    /// </summary>
    Task<IStreamingCsvWriter> OpenStreamWriterAsync(
        string filePath,
        string delimiter = ",",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for streaming CSV writer that allows incremental writes
/// </summary>
public interface IStreamingCsvWriter : IAsyncDisposable
{
    /// <summary>
    /// Writes a single record to the CSV file
    /// </summary>
    Task WriteRecordAsync(CsvRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple records to the CSV file
    /// </summary>
    Task WriteRecordsAsync(IEnumerable<CsvRecord> records, CancellationToken cancellationToken = default);
}

