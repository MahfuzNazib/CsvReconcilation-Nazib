namespace CsvReconcile.Core.Models;

/// <summary>
/// Configuration for the reconciliation process
/// </summary>
public class ReconciliationConfig
{
    public string FolderA { get; set; } = string.Empty;

    public string FolderB { get; set; } = string.Empty;

    public string OutputFolder { get; set; } = "Output";

    public MatchingRule MatchingRule { get; set; } = new();

    public FileMatchingMode MatchingMode { get; set; } = FileMatchingMode.OneToOne;

    public int DegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    public string Delimiter { get; set; } = ",";

    public bool HasHeaderRow { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FolderA))
            throw new InvalidOperationException("FolderA path is required.");

        if (string.IsNullOrWhiteSpace(FolderB))
            throw new InvalidOperationException("FolderB path is required.");

        if (!Directory.Exists(FolderA))
            throw new DirectoryNotFoundException($"FolderA does not exist: {FolderA}");

        if (!Directory.Exists(FolderB))
            throw new DirectoryNotFoundException($"FolderB does not exist: {FolderB}");

        if (DegreeOfParallelism < 1)
            throw new InvalidOperationException("DegreeOfParallelism must be at least 1.");

        MatchingRule?.Validate();
    }
}

