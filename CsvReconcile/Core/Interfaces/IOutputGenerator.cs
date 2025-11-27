using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Generates output files and reports from reconciliation results
/// </summary>
public interface IOutputGenerator
{
    Task GenerateFileOutputsAsync(
        FileComparisonResult result,
        string outputFolder,
        string delimiter = ",",
        CancellationToken cancellationToken = default);

    Task GenerateGlobalSummaryAsync(
        ReconciliationResult result,
        string outputFolder,
        CancellationToken cancellationToken = default);
}

