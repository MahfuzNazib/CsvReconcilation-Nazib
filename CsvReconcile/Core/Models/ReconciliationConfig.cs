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

    /// <summary>
    /// Maximum memory usage in MB per file pair. When exceeded, chunked processing is enabled.
    /// Default: 0 (auto-detect based on available memory)
    /// </summary>
    public int MaxMemoryUsageMB { get; set; } = 0;

    /// <summary>
    /// Enable streaming output to disk instead of storing records in memory.
    /// When true, records are written to temp files during processing.
    /// Default: true (enabled for memory efficiency)
    /// </summary>
    public bool EnableStreamingOutput { get; set; } = true;

    /// <summary>
    /// Size of chunks in MB for chunked processing mode.
    /// Used when file size exceeds memory threshold.
    /// Default: 1024 (1GB chunks)
    /// </summary>
    public int ChunkSizeMB { get; set; } = 1024;

    /// <summary>
    /// Enable storing full records in FileComparisonResult.
    /// When false, only file paths to temp files are stored (streaming mode).
    /// Default: false (memory efficient)
    /// </summary>
    public bool EnableRecordStorage { get; set; } = false;

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

        if (MaxMemoryUsageMB < 0)
            throw new InvalidOperationException("MaxMemoryUsageMB must be non-negative.");

        if (ChunkSizeMB < 1)
            throw new InvalidOperationException("ChunkSizeMB must be at least 1.");

        MatchingRule?.Validate();
    }
}

