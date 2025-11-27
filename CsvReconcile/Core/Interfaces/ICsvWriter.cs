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
}

