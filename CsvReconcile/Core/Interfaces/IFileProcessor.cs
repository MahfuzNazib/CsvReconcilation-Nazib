using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Handles parallel processing of multiple file pairs
/// </summary>
public interface IFileProcessor
{
    Task<ReconciliationResult> ProcessAllFilesAsync(
        ReconciliationConfig config,
        CancellationToken cancellationToken = default);
}

