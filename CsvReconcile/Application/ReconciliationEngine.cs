using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CsvReconcile.Application;

/// <summary>
/// Core engine for reconciling CSV data between two files
/// </summary>
public class ReconciliationEngine : IReconciliationEngine
{
    private readonly ICsvReader _csvReader;
    private readonly IRecordMatcher _recordMatcher;
    private readonly ILogger _logger;

    public ReconciliationEngine(
        ICsvReader csvReader,
        IRecordMatcher recordMatcher,
        ILogger logger)
    {
        _csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));
        _recordMatcher = recordMatcher ?? throw new ArgumentNullException(nameof(recordMatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reconciles two CSV files and returns the comparison result
    /// </summary>
    public async Task<FileComparisonResult> ReconcileFilesAsync(
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var fileName = Path.GetFileName(fileAPath);

        var result = new FileComparisonResult
        {
            FileName = fileName,
            FileAPath = fileAPath,
            FileBPath = fileBPath,
            ExistsInA = File.Exists(fileAPath),
            ExistsInB = File.Exists(fileBPath)
        };

        _logger.Information("Starting reconciliation for file: {FileName}", fileName);

        try
        {
            // Handle missing file scenarios
            if (!result.ExistsInA)
            {
                _logger.Warning("File missing in FolderA: {FileName}", fileName);
                result.Errors.Add($"File not found in FolderA: {fileAPath}");
                
                if (result.ExistsInB)
                {
                    // All records in B are unmatched
                    var recordsB = await _csvReader.ReadAllAsync(
                        fileBPath, config.Delimiter, config.HasHeaderRow, cancellationToken);
                    result.OnlyInBRecords = recordsB;
                    result.TotalInB = recordsB.Count;
                    result.OnlyInBCount = recordsB.Count;
                }
                
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }

            if (!result.ExistsInB)
            {
                _logger.Warning("File missing in FolderB: {FileName}", fileName);
                result.Errors.Add($"File not found in FolderB: {fileBPath}");
                
                // All records in A are unmatched
                var recordsA = await _csvReader.ReadAllAsync(
                    fileAPath, config.Delimiter, config.HasHeaderRow, cancellationToken);
                result.OnlyInARecords = recordsA;
                result.TotalInA = recordsA.Count;
                result.OnlyInACount = recordsA.Count;
                
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }

            // Both files exist - perform reconciliation
            await PerformReconciliationAsync(result, fileAPath, fileBPath, config, cancellationToken);

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            _logger.Information(
                "Completed reconciliation for {FileName}: " +
                "Matched={Matched}, OnlyInA={OnlyInA}, OnlyInB={OnlyInB}, Time={Time:F2}s",
                fileName, result.MatchedCount, result.OnlyInACount, result.OnlyInBCount, 
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reconciling file: {FileName}", fileName);
            result.Errors.Add($"Reconciliation error: {ex.Message}");
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Performs the actual reconciliation logic using dictionary-based matching
    /// </summary>
    private async Task PerformReconciliationAsync(
        FileComparisonResult result,
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        CancellationToken cancellationToken)
    {
        // Build dictionary from FolderA records for fast lookup
        var dictionaryA = new ConcurrentDictionary<string, CsvRecord>();
        
        _logger.Debug("Reading records from FolderA file: {FileName}", result.FileName);
        
        await foreach (var record in _csvReader.ReadStreamAsync(
            fileAPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
        {
            try
            {
                var key = _recordMatcher.GenerateKey(record, config.MatchingRule);
                
                // Handle duplicate keys in file A
                if (!dictionaryA.TryAdd(key, record))
                {
                    _logger.Warning(
                        "Duplicate key found in FolderA file {FileName} at line {LineNumber}: {Key}",
                        result.FileName, record.LineNumber, key);
                }
                
                result.TotalInA++;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing record from FolderA at line {LineNumber}", 
                    record.LineNumber);
                result.Errors.Add($"Error in FolderA line {record.LineNumber}: {ex.Message}");
            }
        }

        _logger.Debug("Reading records from FolderB file: {FileName}", result.FileName);

        // Stream through FolderB records and match against dictionary
        var tempOnlyInB = new ConcurrentBag<CsvRecord>();
        var tempMatched = new ConcurrentBag<CsvRecord>();

        await foreach (var recordB in _csvReader.ReadStreamAsync(
            fileBPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
        {
            try
            {
                var key = _recordMatcher.GenerateKey(recordB, config.MatchingRule);
                result.TotalInB++;

                if (dictionaryA.TryRemove(key, out var recordA))
                {
                    // Match found - merge records from both sides
                    var mergedRecord = MergeRecords(recordA, recordB);
                    tempMatched.Add(mergedRecord);
                    result.MatchedCount++;
                }
                else
                {
                    // No match - only in B
                    tempOnlyInB.Add(recordB);
                    result.OnlyInBCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing record from FolderB at line {LineNumber}", 
                    recordB.LineNumber);
                result.Errors.Add($"Error in FolderB line {recordB.LineNumber}: {ex.Message}");
            }
        }

        // Remaining records in dictionary are only in A
        result.OnlyInARecords = dictionaryA.Values.ToList();
        result.OnlyInACount = result.OnlyInARecords.Count;

        result.OnlyInBRecords = tempOnlyInB.ToList();
        result.MatchedRecords = tempMatched.ToList();
    }

    /// <summary>
    /// Merges two matching records from FolderA and FolderB
    /// </summary>
    private CsvRecord MergeRecords(CsvRecord recordA, CsvRecord recordB)
    {
        var merged = new CsvRecord
        {
            SourceFile = $"{recordA.SourceFile} + {recordB.SourceFile}",
            LineNumber = recordA.LineNumber
        };

        // Add all fields from A with "_A" suffix if there's a conflict
        foreach (var field in recordA.Fields)
        {
            merged.SetField(field.Key, field.Value);
        }

        // Add fields from B, handling conflicts
        foreach (var field in recordB.Fields)
        {
            if (merged.Fields.ContainsKey(field.Key))
            {
                // If values are different, add B's value with suffix
                if (merged.GetField(field.Key) != field.Value)
                {
                    merged.SetField($"{field.Key}_B", field.Value);
                }
            }
            else
            {
                merged.SetField(field.Key, field.Value);
            }
        }

        return merged;
    }
}

