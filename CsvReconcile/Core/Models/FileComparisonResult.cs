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

    public List<CsvRecord> MatchedRecords { get; set; } = new();

    public List<CsvRecord> OnlyInARecords { get; set; } = new();

    public List<CsvRecord> OnlyInBRecords { get; set; } = new();

    public TimeSpan ProcessingTime { get; set; }

    public List<string> Errors { get; set; } = new();

    public bool Success => Errors.Count == 0;
}

