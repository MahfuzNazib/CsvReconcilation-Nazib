using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CsvReconcile.Application;

/// <summary>
/// Handles parallel processing of multiple file pairs
/// </summary>
public class FileProcessor : IFileProcessor
{
    private readonly IReconciliationEngine _reconciliationEngine;
    private readonly ILogger _logger;

    public FileProcessor(
        IReconciliationEngine reconciliationEngine,
        ILogger logger)
    {
        _reconciliationEngine = reconciliationEngine ?? throw new ArgumentNullException(nameof(reconciliationEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes all file pairs between two folders in parallel
    /// </summary>
    public async Task<ReconciliationResult> ProcessAllFilesAsync(
        ReconciliationConfig config,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var result = new ReconciliationResult();

        _logger.Information("Starting reconciliation process");
        _logger.Information("FolderA: {FolderA}", config.FolderA);
        _logger.Information("FolderB: {FolderB}", config.FolderB);
        _logger.Information("File matching mode: {MatchingMode}", config.MatchingMode);
        _logger.Information("Degree of Parallelism: {DegreeOfParallelism}", config.DegreeOfParallelism);
        _logger.Information("Streaming output: {EnableStreamingOutput}", config.EnableStreamingOutput);
        _logger.Information("Max memory usage: {MaxMemoryMB}MB (0 = auto)", config.MaxMemoryUsageMB);

        // Log initial memory usage
        LogMemoryUsage("Initial");

        try
        {
            // Get all CSV files from both folders
            var filesA = GetCsvFiles(config.FolderA);
            var filesB = GetCsvFiles(config.FolderB);

            _logger.Information("Found {CountA} CSV files in FolderA", filesA.Count);
            _logger.Information("Found {CountB} CSV files in FolderB", filesB.Count);

            // Create file pairs
            var filePairs = CreateFilePairs(filesA, filesB, config);

            _logger.Information("Processing {PairCount} file pairs", filePairs.Count);

            // Use ConcurrentBag for thread-safe result collection
            var fileResults = new ConcurrentBag<FileComparisonResult>();

            // Process file pairs in parallel using Parallel.ForEachAsync
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = config.DegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(filePairs, parallelOptions, async (pair, ct) =>
            {
                var threadId = Environment.CurrentManagedThreadId;

                try
                {
                    _logger.Information("Processing file pair: {FileName} (Thread: {ThreadId})",
                        pair.FileName, threadId);

                    // Show processing start
                    Cli.ConsoleDisplay.ShowProcessingEvent(
                        threadId,
                        pair.FileName,
                        "Processing...",
                        "Reading records");

                    var fileResult = await _reconciliationEngine.ReconcileFilesAsync(
                        pair.FileAPath,
                        pair.FileBPath,
                        config,
                        ct);

                    fileResults.Add(fileResult);

                    // Show completion with results
                    var details = $"Matched={fileResult.MatchedCount}, OnlyA={fileResult.OnlyInACount}, OnlyB={fileResult.OnlyInBCount}";

                    if (!fileResult.Success)
                    {
                        Cli.ConsoleDisplay.ShowProcessingEvent(
                            threadId,
                            pair.FileName,
                            "✗ Error",
                            fileResult.Errors.FirstOrDefault() ?? "Unknown error");
                    }
                    else if (!fileResult.ExistsInA || !fileResult.ExistsInB)
                    {
                        var missingIn = !fileResult.ExistsInB ? "FolderB" : "FolderA";
                        Cli.ConsoleDisplay.ShowProcessingEvent(
                            threadId,
                            pair.FileName,
                            "Warning",
                            $"File missing in {missingIn}");
                    }
                    else if (fileResult.Errors.Any())
                    {
                        Cli.ConsoleDisplay.ShowProcessingEvent(
                            threadId,
                            pair.FileName,
                            "Warning",
                            "Completed with warnings");
                        Cli.ConsoleDisplay.ShowProcessingEvent(
                            threadId,
                            pair.FileName,
                            "Completed",
                            details);
                    }
                    else
                    {
                        Cli.ConsoleDisplay.ShowProcessingEvent(
                            threadId,
                            pair.FileName,
                            "Completed",
                            details);
                    }

                    _logger.Information("Completed file pair: {FileName} (Thread: {ThreadId})",
                        pair.FileName, threadId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing file pair: {FileName}", pair.FileName);

                    Cli.ConsoleDisplay.ShowProcessingEvent(
                        threadId,
                        pair.FileName,
                        "Error",
                        ex.Message.Length > 26 ? ex.Message.Substring(0, 23) + "..." : ex.Message);

                    var errorResult = new FileComparisonResult
                    {
                        FileName = pair.FileName,
                        FileAPath = pair.FileAPath,
                        FileBPath = pair.FileBPath,
                        ExistsInA = File.Exists(pair.FileAPath),
                        ExistsInB = File.Exists(pair.FileBPath)
                    };
                    errorResult.Errors.Add($"Processing error: {ex.Message}");
                    fileResults.Add(errorResult);
                }
            });

            // Aggregate results
            result.FileResults = fileResults.OrderBy(r => r.FileName).ToList();
            result.TotalProcessingTime = totalStopwatch.Elapsed;

            // Log final memory usage
            LogMemoryUsage("Final");

            _logger.Information("=== Reconciliation Complete ===");
            _logger.Information("Total files processed: {TotalFiles}", result.FileResults.Count);
            _logger.Information("Successful: {Successful}, Failed: {Failed}",
                result.SuccessfulFiles, result.FailedFiles);
            _logger.Information("Total records - In A: {InA}, In B: {InB}, Matched: {Matched}",
                result.TotalRecordsInA, result.TotalRecordsInB, result.TotalMatched);
            _logger.Information("Total processing time: {Time:F2}s", result.TotalProcessingTime.TotalSeconds);

            if (result.MissingInA.Any())
            {
                _logger.Warning("Files missing in FolderA: {Files}", string.Join(", ", result.MissingInA));
            }

            if (result.MissingInB.Any())
            {
                _logger.Warning("Files missing in FolderB: {Files}", string.Join(", ", result.MissingInB));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during reconciliation process");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Gets all CSV files from a directory
    /// </summary>
    private List<string> GetCsvFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.Warning("Folder does not exist: {FolderPath}", folderPath);
            return new List<string>();
        }

        return Directory.GetFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Creates file pairs for comparison
    /// </summary>
    private List<FilePair> CreateFilePairs(List<string> filesA, List<string> filesB, ReconciliationConfig config)
    {
        var pairs = new List<FilePair>();

        if (config.MatchingMode == FileMatchingMode.AllAgainstAll)
        {
            // All-against-all mode: compare every file in A against every file in B
            foreach (var fileAPath in filesA)
            {
                foreach (var fileBPath in filesB)
                {
                    var fileNameA = Path.GetFileNameWithoutExtension(fileAPath);
                    var fileNameB = Path.GetFileName(fileBPath);
                    var compositeFileName = $"{fileNameA}_vs_{fileNameB}";

                    pairs.Add(new FilePair
                    {
                        FileName = compositeFileName,
                        FileAPath = fileAPath,
                        FileBPath = fileBPath
                    });
                }
            }
        }
        else
        {
            // One-to-one mode: match files by filename (existing behavior)
            var filesBDict = filesB.ToDictionary(
                path => Path.GetFileName(path),
                path => path,
                StringComparer.OrdinalIgnoreCase);

            // Pair files from FolderA
            foreach (var fileAPath in filesA)
            {
                var fileName = Path.GetFileName(fileAPath);
                var fileBPath = filesBDict.GetValueOrDefault(fileName) ?? string.Empty;

                pairs.Add(new FilePair
                {
                    FileName = fileName,
                    FileAPath = fileAPath,
                    FileBPath = fileBPath
                });

                // Remove from dictionary to track unmatched files
                if (!string.IsNullOrEmpty(fileBPath))
                {
                    filesBDict.Remove(fileName);
                }
            }

            // Add remaining files from FolderB that don't have matches in FolderA
            foreach (var kvp in filesBDict)
            {
                pairs.Add(new FilePair
                {
                    FileName = kvp.Key,
                    FileAPath = string.Empty,
                    FileBPath = kvp.Value
                });
            }
        }

        return pairs;
    }

    /// <summary>
    /// Logs current memory usage
    /// </summary>
    private void LogMemoryUsage(string stage)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);
            var privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
            var gcMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

            _logger.Information(
                "Memory usage ({Stage}): WorkingSet={WorkingSetMB}MB, PrivateMemory={PrivateMemoryMB}MB, GC={GCMemoryMB}MB",
                stage, workingSetMB, privateMemoryMB, gcMemoryMB);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to log memory usage");
        }
    }

    /// <summary>
    /// Internal class to represent a file pair
    /// </summary>
    private class FilePair
    {
        public string FileName { get; set; } = string.Empty;
        public string FileAPath { get; set; } = string.Empty;
        public string FileBPath { get; set; } = string.Empty;
    }
}

