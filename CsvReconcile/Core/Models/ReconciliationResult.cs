namespace CsvReconcile.Core.Models;

/// <summary>
/// Aggregated result of reconciling all file pairs
/// </summary>
public class ReconciliationResult
{
    public List<FileComparisonResult> FileResults { get; set; } = new();

    public TimeSpan TotalProcessingTime { get; set; }

    public int TotalRecordsInA => FileResults.Sum(f => f.TotalInA);

    public int TotalRecordsInB => FileResults.Sum(f => f.TotalInB);

    public int TotalMatched => FileResults.Sum(f => f.MatchedCount);

    public int TotalOnlyInA => FileResults.Sum(f => f.OnlyInACount);

    public int TotalOnlyInB => FileResults.Sum(f => f.OnlyInBCount);

    public int SuccessfulFiles => FileResults.Count(f => f.Success);

    public int FailedFiles => FileResults.Count(f => !f.Success);

    public List<string> MissingInB => FileResults
        .Where(f => f.ExistsInA && !f.ExistsInB)
        .Select(f => f.FileName)
        .ToList();

    public List<string> MissingInA => FileResults
        .Where(f => f.ExistsInB && !f.ExistsInA)
        .Select(f => f.FileName)
        .ToList();
}

