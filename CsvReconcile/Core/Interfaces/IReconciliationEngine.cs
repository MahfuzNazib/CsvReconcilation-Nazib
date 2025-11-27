using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Core engine for reconciling CSV data between two files
/// </summary>
public interface IReconciliationEngine
{

    Task<FileComparisonResult> ReconcileFilesAsync(
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        CancellationToken cancellationToken = default);
}

