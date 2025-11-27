using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Abstraction for reading CSV files
/// </summary>
public interface ICsvReader
{
    Task<List<CsvRecord>> ReadAllAsync(
        string filePath,
        string delimiter = ",",
        bool hasHeaderRow = true,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CsvRecord> ReadStreamAsync(
        string filePath,
        string delimiter = ",",
        bool hasHeaderRow = true,
        CancellationToken cancellationToken = default);
}

