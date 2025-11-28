namespace CsvReconcile.Core.Models;

/// <summary>
/// Defines how files are paired for comparison
/// </summary>
public enum FileMatchingMode
{
    /// <summary>
    /// Matches files by filename (one-to-one pairing)
    /// </summary>
    OneToOne,

    /// <summary>
    /// Compares every file in FolderA against every file in FolderB (all-against-all)
    /// </summary>
    AllAgainstAll
}

