using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Text.Json;

namespace CsvReconcile.Application;

/// <summary>
/// Generates output files and reports from reconciliation results
/// </summary>
public class OutputGenerator : IOutputGenerator
{
    private readonly ICsvWriter _csvWriter;
    private readonly ILogger _logger;

    public OutputGenerator(
        ICsvWriter csvWriter,
        ILogger logger)
    {
        _csvWriter = csvWriter ?? throw new ArgumentNullException(nameof(csvWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates output files for a single file comparison
    /// </summary>
    public async Task GenerateFileOutputsAsync(
        FileComparisonResult result,
        string outputFolder,
        string delimiter = ",",
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        // Create output subfolder for this file
        var fileBaseName = Path.GetFileNameWithoutExtension(result.FileName);
        var fileOutputFolder = Path.Combine(outputFolder, fileBaseName);
        
        if (!Directory.Exists(fileOutputFolder))
        {
            Directory.CreateDirectory(fileOutputFolder);
        }

        _logger.Information("Generating outputs for {FileName} in {Folder}",
            result.FileName, fileOutputFolder);

        // Check if streaming mode was used (temp files exist)
        var useStreaming = !string.IsNullOrEmpty(result.MatchedRecordsFilePath) ||
                          !string.IsNullOrEmpty(result.OnlyInARecordsFilePath) ||
                          !string.IsNullOrEmpty(result.OnlyInBRecordsFilePath);

        if (useStreaming)
        {
            // Copy temp files to final output location
            if (!string.IsNullOrEmpty(result.MatchedRecordsFilePath) && File.Exists(result.MatchedRecordsFilePath))
            {
                var matchedPath = Path.Combine(fileOutputFolder, "matched.csv");
                File.Copy(result.MatchedRecordsFilePath, matchedPath, overwrite: true);
                _logger.Debug("Copied matched records from temp file to {Path}", matchedPath);
            }

            if (!string.IsNullOrEmpty(result.OnlyInARecordsFilePath) && File.Exists(result.OnlyInARecordsFilePath))
            {
                var onlyInAPath = Path.Combine(fileOutputFolder, "only-in-folderA.csv");
                File.Copy(result.OnlyInARecordsFilePath, onlyInAPath, overwrite: true);
                _logger.Debug("Copied only-in-A records from temp file to {Path}", onlyInAPath);
            }

            if (!string.IsNullOrEmpty(result.OnlyInBRecordsFilePath) && File.Exists(result.OnlyInBRecordsFilePath))
            {
                var onlyInBPath = Path.Combine(fileOutputFolder, "only-in-folderB.csv");
                File.Copy(result.OnlyInBRecordsFilePath, onlyInBPath, overwrite: true);
                _logger.Debug("Copied only-in-B records from temp file to {Path}", onlyInBPath);
            }

            // Cleanup temp files
            CleanupTempFiles(result);
        }
        else
        {
            // Generate matched records CSV (in-memory mode)
            if (result.MatchedRecords.Any())
            {
                var matchedPath = Path.Combine(fileOutputFolder, "matched.csv");
                await _csvWriter.WriteAsync(matchedPath, result.MatchedRecords, delimiter, cancellationToken);
                _logger.Debug("Wrote {Count} matched records to {Path}", 
                    result.MatchedRecords.Count, matchedPath);
            }

            // Generate only-in-A records CSV
            if (result.OnlyInARecords.Any())
            {
                var onlyInAPath = Path.Combine(fileOutputFolder, "only-in-folderA.csv");
                await _csvWriter.WriteAsync(onlyInAPath, result.OnlyInARecords, delimiter, cancellationToken);
                _logger.Debug("Wrote {Count} only-in-A records to {Path}", 
                    result.OnlyInARecords.Count, onlyInAPath);
            }

            // Generate only-in-B records CSV
            if (result.OnlyInBRecords.Any())
            {
                var onlyInBPath = Path.Combine(fileOutputFolder, "only-in-folderB.csv");
                await _csvWriter.WriteAsync(onlyInBPath, result.OnlyInBRecords, delimiter, cancellationToken);
                _logger.Debug("Wrote {Count} only-in-B records to {Path}", 
                    result.OnlyInBRecords.Count, onlyInBPath);
            }
        }

        // Generate per-file JSON summary
        await GenerateFileSummaryAsync(result, fileOutputFolder, cancellationToken);
    }

    /// <summary>
    /// Generates JSON summary for a single file comparison
    /// </summary>
    private async Task GenerateFileSummaryAsync(
        FileComparisonResult result,
        string outputFolder,
        CancellationToken cancellationToken)
    {
        var summary = new
        {
            fileName = result.FileName,
            existsInA = result.ExistsInA,
            existsInB = result.ExistsInB,
            totalInA = result.TotalInA,
            totalInB = result.TotalInB,
            matched = result.MatchedCount,
            onlyInA = result.OnlyInACount,
            onlyInB = result.OnlyInBCount,
            processingTimeSeconds = result.ProcessingTime.TotalSeconds,
            success = result.Success,
            errors = result.Errors
        };

        var summaryPath = Path.Combine(outputFolder, "reconcile-summary.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(summary, options);
        await File.WriteAllTextAsync(summaryPath, json, cancellationToken);

        _logger.Debug("Wrote summary to {Path}", summaryPath);
    }

    /// <summary>
    /// Generates global summary report across all file comparisons
    /// </summary>
    public async Task GenerateGlobalSummaryAsync(
        ReconciliationResult result,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        _logger.Information("Generating global summary in {Folder}", outputFolder);

        var summary = new
        {
            timestamp = DateTime.UtcNow,
            totalProcessingTimeSeconds = result.TotalProcessingTime.TotalSeconds,
            summary = new
            {
                totalFilesProcessed = result.FileResults.Count,
                successfulFiles = result.SuccessfulFiles,
                failedFiles = result.FailedFiles,
                totalRecordsInA = result.TotalRecordsInA,
                totalRecordsInB = result.TotalRecordsInB,
                totalMatched = result.TotalMatched,
                totalOnlyInA = result.TotalOnlyInA,
                totalOnlyInB = result.TotalOnlyInB
            },
            missingFiles = new
            {
                missingInA = result.MissingInA,
                missingInB = result.MissingInB
            },
            fileBreakdown = result.FileResults.Select(f => new
            {
                fileName = f.FileName,
                existsInA = f.ExistsInA,
                existsInB = f.ExistsInB,
                totalInA = f.TotalInA,
                totalInB = f.TotalInB,
                matched = f.MatchedCount,
                onlyInA = f.OnlyInACount,
                onlyInB = f.OnlyInBCount,
                processingTimeSeconds = f.ProcessingTime.TotalSeconds,
                success = f.Success,
                errorCount = f.Errors.Count
            }).ToList()
        };

        var summaryPath = Path.Combine(outputFolder, "global-summary.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(summary, options);
        await File.WriteAllTextAsync(summaryPath, json, cancellationToken);

        _logger.Information("Wrote global summary to {Path}", summaryPath);
    }

    /// <summary>
    /// Cleans up temporary files used in streaming mode
    /// </summary>
    private void CleanupTempFiles(FileComparisonResult result)
    {
        try
        {
            var tempFiles = new[]
            {
                result.MatchedRecordsFilePath,
                result.OnlyInARecordsFilePath,
                result.OnlyInBRecordsFilePath
            };

            foreach (var tempFile in tempFiles)
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try
                    {
                        var tempDir = Path.GetDirectoryName(tempFile);
                        File.Delete(tempFile);
                        
                        // Try to delete temp directory if empty
                        if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                        {
                            try
                            {
                                if (!Directory.EnumerateFileSystemEntries(tempDir).Any())
                                {
                                    Directory.Delete(tempDir);
                                }
                            }
                            catch
                            {
                                // Ignore directory deletion errors
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to delete temp file: {FilePath}", tempFile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during temp file cleanup");
        }
    }
}

