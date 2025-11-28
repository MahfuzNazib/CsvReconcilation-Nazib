using System.Diagnostics;

namespace CsvReconcile.Core.Models;

/// <summary>
/// Result of comparing a single file pair
/// </summary>
public class FileComparisonResult
{
    public string FileName { get; set; } = string.Empty;

    public string FileAPath { get; set; } = string.Empty;

    public string FileBPath { get; set; } = string.Empty;

    public bool ExistsInA { get; set; }

    public bool ExistsInB { get; set; }

    public int TotalInA { get; set; }

    public int TotalInB { get; set; }

    public int MatchedCount { get; set; }

    public int OnlyInACount { get; set; }

    public int OnlyInBCount { get; set; }

    /// <summary>
    /// Matched records. Only populated if EnableRecordStorage is true.
    /// Otherwise, use MatchedRecordsFilePath for streaming mode.
    /// </summary>
    public List<CsvRecord> MatchedRecords { get; set; } = new();

    /// <summary>
    /// Records only in FolderA. Only populated if EnableRecordStorage is true.
    /// Otherwise, use OnlyInARecordsFilePath for streaming mode.
    /// </summary>
    public List<CsvRecord> OnlyInARecords { get; set; } = new();

    /// <summary>
    /// Records only in FolderB. Only populated if EnableRecordStorage is true.
    /// Otherwise, use OnlyInBRecordsFilePath for streaming mode.
    /// </summary>
    public List<CsvRecord> OnlyInBRecords { get; set; } = new();

    /// <summary>
    /// Path to temporary file containing matched records (streaming mode)
    /// </summary>
    public string? MatchedRecordsFilePath { get; set; }

    /// <summary>
    /// Path to temporary file containing records only in FolderA (streaming mode)
    /// </summary>
    public string? OnlyInARecordsFilePath { get; set; }

    /// <summary>
    /// Path to temporary file containing records only in FolderB (streaming mode)
    /// </summary>
    public string? OnlyInBRecordsFilePath { get; set; }

    public TimeSpan ProcessingTime { get; set; }

    public List<string> Errors { get; set; } = new();

    public bool Success => Errors.Count == 0;
}

