using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CsvReconcile.Application;

public class ReconciliationEngine : IReconciliationEngine
{
    private readonly ICsvReader _csvReader;
    private readonly IRecordMatcher _recordMatcher;
    private readonly ICsvWriter _csvWriter;
    private readonly ILogger _logger;

    public ReconciliationEngine(
        ICsvReader csvReader,
        IRecordMatcher recordMatcher,
        ILogger logger)
        : this(csvReader, recordMatcher, null, logger)
    {
    }

    public ReconciliationEngine(
        ICsvReader csvReader,
        IRecordMatcher recordMatcher,
        ICsvWriter? csvWriter,
        ILogger logger)
    {
        _csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));
        _recordMatcher = recordMatcher ?? throw new ArgumentNullException(nameof(recordMatcher));
        _csvWriter = csvWriter!;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FileComparisonResult> ReconcileFilesAsync(
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        string fileName;
        if (config.MatchingMode == FileMatchingMode.AllAgainstAll && !string.IsNullOrEmpty(fileAPath) && !string.IsNullOrEmpty(fileBPath))
        {
            var fileNameA = Path.GetFileNameWithoutExtension(fileAPath);
            var fileNameB = Path.GetFileName(fileBPath);
            fileName = $"{fileNameA}_vs_{fileNameB}";
        }
        else
        {
            fileName = !string.IsNullOrEmpty(fileAPath) ? Path.GetFileName(fileAPath) : Path.GetFileName(fileBPath);
        }

        var result = new FileComparisonResult
        {
            FileName = fileName,
            FileAPath = fileAPath,
            FileBPath = fileBPath,
            ExistsInA = File.Exists(fileAPath),
            ExistsInB = File.Exists(fileBPath)
        };

        _logger.Information("Starting reconciliation for file: {FileName}", fileName);

        LogMemoryUsage("Before reconciliation");

        try
        {
            if (!result.ExistsInA)
            {
                _logger.Warning("File missing in FolderA: {FileName}", fileName);
                result.Errors.Add($"File not found in FolderA: {fileAPath}");

                if (result.ExistsInB)
                {
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

                var recordsA = await _csvReader.ReadAllAsync(
                    fileAPath, config.Delimiter, config.HasHeaderRow, cancellationToken);
                result.OnlyInARecords = recordsA;
                result.TotalInA = recordsA.Count;
                result.OnlyInACount = recordsA.Count;

                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }

            await PerformReconciliationAsync(result, fileAPath, fileBPath, config, cancellationToken);

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            LogMemoryUsage("After reconciliation");

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

    private async Task PerformReconciliationAsync(
        FileComparisonResult result,
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        CancellationToken cancellationToken)
    {
        var useChunked = ShouldUseChunkedProcessing(fileAPath, fileBPath, config);
        var useStreaming = config.EnableStreamingOutput || useChunked;

        if (useStreaming && _csvWriter == null)
        {
            _logger.Warning("Streaming output requested but ICsvWriter not provided. Falling back to in-memory mode.");
            useStreaming = false;
            useChunked = false;
        }

        if (useChunked)
        {
            _logger.Information("Using chunked processing mode for large files");
            var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcile", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                await PerformReconciliationChunkedAsync(result, fileAPath, fileBPath, config, tempDir, cancellationToken);
            }
            catch
            {
                // Cleanup on error
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                throw;
            }
        }
        else if (useStreaming)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcile", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                await PerformReconciliationStreamingAsync(result, fileAPath, fileBPath, config, tempDir, cancellationToken);
            }
            catch
            {
                // Cleanup on error
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                throw;
            }
        }
        else
        {
            await PerformReconciliationInMemoryAsync(result, fileAPath, fileBPath, config, cancellationToken);
        }
    }

    private bool ShouldUseChunkedProcessing(string fileAPath, string fileBPath, ReconciliationConfig config)
    {
        try
        {
            var fileAInfo = new FileInfo(fileAPath);
            var fileBInfo = new FileInfo(fileBPath);
            var totalSizeMB = (fileAInfo.Length + fileBInfo.Length) / (1024 * 1024);

            var thresholdMB = config.MaxMemoryUsageMB > 0
                ? config.MaxMemoryUsageMB
                : config.ChunkSizeMB * 2;

            if (totalSizeMB > thresholdMB)
            {
                _logger.Information(
                    "Files exceed memory threshold ({TotalSizeMB}MB > {ThresholdMB}MB). Enabling chunked processing.",
                    totalSizeMB, thresholdMB);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking file sizes for chunked processing. Using standard mode.");
            return false;
        }
    }

    private async Task PerformReconciliationChunkedAsync(
        FileComparisonResult result,
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        string tempDir,
        CancellationToken cancellationToken)
    {
        _logger.Information("Starting chunked reconciliation for {FileName}", result.FileName);

        var matchedFilePath = Path.Combine(tempDir, "matched.csv");
        var onlyInBFilePath = Path.Combine(tempDir, "only-in-B.csv");
        var onlyInAFilePath = Path.Combine(tempDir, "only-in-A.csv");

        var matchedWriter = await _csvWriter!.OpenStreamWriterAsync(matchedFilePath, config.Delimiter, cancellationToken);
        var onlyInBWriter = await _csvWriter.OpenStreamWriterAsync(onlyInBFilePath, config.Delimiter, cancellationToken);
        IStreamingCsvWriter? onlyInAWriter = null;

        try
        {
            var chunkSizeMB = config.ChunkSizeMB;
            var chunkSizeBytes = chunkSizeMB * 1024 * 1024;
            var fileAInfo = new FileInfo(fileAPath);
            var estimatedChunks = (int)Math.Ceiling((double)fileAInfo.Length / chunkSizeBytes);

            _logger.Information("Processing FileA in approximately {ChunkCount} chunks of {ChunkSizeMB}MB each",
                estimatedChunks, chunkSizeMB);

            var chunkIndex = 0;
            var currentChunkSize = 0L;
            var chunkDictionary = new Dictionary<string, CsvRecord>();
            var onlyInAKeys = new HashSet<string>();
            var matchedBKeys = new HashSet<string>();

            await foreach (var record in _csvReader.ReadStreamAsync(
                fileAPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
            {
                try
                {
                    var key = _recordMatcher.GenerateKey(record, config.MatchingRule);
                    chunkDictionary[key] = record;
                    currentChunkSize += EstimateRecordSize(record);
                    result.TotalInA++;

                    if (currentChunkSize >= chunkSizeBytes)
                    {
                        await ProcessChunkAsync(
                            chunkDictionary,
                            fileBPath,
                            config,
                            matchedWriter,
                            onlyInBWriter,
                            onlyInAKeys,
                            matchedBKeys,
                            result,
                            chunkIndex++,
                            cancellationToken);

                        chunkDictionary.Clear();
                        currentChunkSize = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing record from FolderA at line {LineNumber}",
                        record.LineNumber);
                    result.Errors.Add($"Error in FolderA line {record.LineNumber}: {ex.Message}");
                }
            }

            if (chunkDictionary.Count > 0)
            {
                await ProcessChunkAsync(
                    chunkDictionary,
                    fileBPath,
                    config,
                    matchedWriter,
                    onlyInBWriter,
                    onlyInAKeys,
                    matchedBKeys,
                    result,
                    chunkIndex++,
                    cancellationToken);
            }

            _logger.Debug("Processing FileB to find records only in B");
            await foreach (var recordB in _csvReader.ReadStreamAsync(
                fileBPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
            {
                try
                {
                    var key = _recordMatcher.GenerateKey(recordB, config.MatchingRule);
                    if (!matchedBKeys.Contains(key))
                    {
                        await onlyInBWriter.WriteRecordAsync(recordB, cancellationToken);
                        result.OnlyInBCount++;
                        matchedBKeys.Add(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing record from FolderB at line {LineNumber}",
                        recordB.LineNumber);
                    result.Errors.Add($"Error in FolderB line {recordB.LineNumber}: {ex.Message}");
                }
            }

            if (onlyInAKeys.Count > 0)
            {
                onlyInAWriter = await _csvWriter.OpenStreamWriterAsync(onlyInAFilePath, config.Delimiter, cancellationToken);

                var onlyInARecords = await GetRecordsByKeysAsync(fileAPath, onlyInAKeys, config, cancellationToken);

                foreach (var record in onlyInARecords)
                {
                    await onlyInAWriter.WriteRecordAsync(record, cancellationToken);
                    result.OnlyInACount++;
                }
            }

            result.MatchedRecordsFilePath = matchedFilePath;
            result.OnlyInBRecordsFilePath = onlyInBFilePath;
            if (onlyInAWriter != null)
            {
                result.OnlyInARecordsFilePath = onlyInAFilePath;
            }

            if (config.EnableRecordStorage)
            {
                _logger.Warning("EnableRecordStorage is true for large file. This may cause high memory usage.");
                result.MatchedRecords = await LoadRecordsFromFileAsync(matchedFilePath, cancellationToken);
                result.OnlyInBRecords = await LoadRecordsFromFileAsync(onlyInBFilePath, cancellationToken);
                if (result.OnlyInARecordsFilePath != null)
                {
                    result.OnlyInARecords = await LoadRecordsFromFileAsync(onlyInAFilePath, cancellationToken);
                }
            }
        }
        finally
        {
            await matchedWriter.DisposeAsync();
            await onlyInBWriter.DisposeAsync();
            if (onlyInAWriter != null)
            {
                await onlyInAWriter.DisposeAsync();
            }
        }
    }

    private async Task ProcessChunkAsync(
        Dictionary<string, CsvRecord> chunkDictionary,
        string fileBPath,
        ReconciliationConfig config,
        IStreamingCsvWriter matchedWriter,
        IStreamingCsvWriter onlyInBWriter,
        HashSet<string> onlyInAKeys,
        HashSet<string> matchedBKeys,
        FileComparisonResult result,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        _logger.Debug("Processing chunk {ChunkIndex} with {RecordCount} records", chunkIndex, chunkDictionary.Count);

        var chunkKeys = new HashSet<string>(chunkDictionary.Keys);

        await foreach (var recordB in _csvReader.ReadStreamAsync(
            fileBPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
        {
            try
            {
                var key = _recordMatcher.GenerateKey(recordB, config.MatchingRule);
                result.TotalInB++;

                if (chunkDictionary.TryGetValue(key, out var recordA))
                {
                    var mergedRecord = MergeRecords(recordA, recordB);
                    await matchedWriter.WriteRecordAsync(mergedRecord, cancellationToken);
                    result.MatchedCount++;
                    chunkKeys.Remove(key);
                    matchedBKeys.Add(key);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing record from FolderB at line {LineNumber}",
                    recordB.LineNumber);
                result.Errors.Add($"Error in FolderB line {recordB.LineNumber}: {ex.Message}");
            }
        }

        foreach (var key in chunkKeys)
        {
            onlyInAKeys.Add(key);
        }
    }

    private long EstimateRecordSize(CsvRecord record)
    {
        long size = 0;
        foreach (var field in record.Fields)
        {
            size += field.Key.Length * 2;
            size += field.Value.Length * 2;
        }
        return size + 100;
    }

    private async Task<List<CsvRecord>> GetRecordsByKeysAsync(
        string fileAPath,
        HashSet<string> keys,
        ReconciliationConfig config,
        CancellationToken cancellationToken)
    {
        var records = new List<CsvRecord>();
        var foundKeys = new HashSet<string>();

        await foreach (var record in _csvReader.ReadStreamAsync(
            fileAPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
        {
            var key = _recordMatcher.GenerateKey(record, config.MatchingRule);
            if (keys.Contains(key) && foundKeys.Add(key))
            {
                records.Add(record);
                if (foundKeys.Count >= keys.Count)
                {
                    break;
                }
            }
        }

        return records;
    }

    private async Task PerformReconciliationStreamingAsync(
        FileComparisonResult result,
        string fileAPath,
        string fileBPath,
        ReconciliationConfig config,
        string tempDir,
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

        var matchedFilePath = Path.Combine(tempDir, "matched.csv");
        var onlyInBFilePath = Path.Combine(tempDir, "only-in-B.csv");
        var onlyInAFilePath = Path.Combine(tempDir, "only-in-A.csv");

        var matchedWriter = await _csvWriter.OpenStreamWriterAsync(matchedFilePath, config.Delimiter, cancellationToken);
        var onlyInBWriter = await _csvWriter.OpenStreamWriterAsync(onlyInBFilePath, config.Delimiter, cancellationToken);
        IStreamingCsvWriter? onlyInAWriter = null;

        try
        {
            await foreach (var recordB in _csvReader.ReadStreamAsync(
                fileBPath, config.Delimiter, config.HasHeaderRow, cancellationToken))
            {
                try
                {
                    var key = _recordMatcher.GenerateKey(recordB, config.MatchingRule);
                    result.TotalInB++;

                    if (dictionaryA.TryRemove(key, out var recordA))
                    {
                        var mergedRecord = MergeRecords(recordA, recordB);
                        await matchedWriter.WriteRecordAsync(mergedRecord, cancellationToken);
                        result.MatchedCount++;
                    }
                    else
                    {
                        await onlyInBWriter.WriteRecordAsync(recordB, cancellationToken);
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

            if (dictionaryA.Count > 0)
            {
                onlyInAWriter = await _csvWriter.OpenStreamWriterAsync(onlyInAFilePath, config.Delimiter, cancellationToken);

                foreach (var recordA in dictionaryA.Values)
                {
                    await onlyInAWriter.WriteRecordAsync(recordA, cancellationToken);
                    result.OnlyInACount++;
                }
            }

            result.MatchedRecordsFilePath = matchedFilePath;
            result.OnlyInBRecordsFilePath = onlyInBFilePath;
            if (onlyInAWriter != null)
            {
                result.OnlyInARecordsFilePath = onlyInAFilePath;
            }

            if (config.EnableRecordStorage)
            {
                result.MatchedRecords = await LoadRecordsFromFileAsync(matchedFilePath, cancellationToken);
                result.OnlyInBRecords = await LoadRecordsFromFileAsync(onlyInBFilePath, cancellationToken);
                if (result.OnlyInARecordsFilePath != null)
                {
                    result.OnlyInARecords = await LoadRecordsFromFileAsync(onlyInAFilePath, cancellationToken);
                }
            }
        }
        finally
        {
            await matchedWriter.DisposeAsync();
            await onlyInBWriter.DisposeAsync();
            if (onlyInAWriter != null)
            {
                await onlyInAWriter.DisposeAsync();
            }
        }
    }

    private async Task PerformReconciliationInMemoryAsync(
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
                    var mergedRecord = MergeRecords(recordA, recordB);
                    tempMatched.Add(mergedRecord);
                    result.MatchedCount++;
                }
                else
                {
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

        result.OnlyInARecords = dictionaryA.Values.ToList();
        result.OnlyInACount = result.OnlyInARecords.Count;

        result.OnlyInBRecords = tempOnlyInB.ToList();
        result.MatchedRecords = tempMatched.ToList();
    }

    private async Task<List<CsvRecord>> LoadRecordsFromFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var records = new List<CsvRecord>();
        await foreach (var record in _csvReader.ReadStreamAsync(filePath, ",", true, cancellationToken))
        {
            records.Add(record);
        }
        return records;
    }

    private CsvRecord MergeRecords(CsvRecord recordA, CsvRecord recordB)
    {
        var merged = new CsvRecord
        {
            SourceFile = $"{recordA.SourceFile} + {recordB.SourceFile}",
            LineNumber = recordA.LineNumber
        };

        foreach (var field in recordA.Fields)
        {
            merged.SetField(field.Key, field.Value);
        }

        foreach (var field in recordB.Fields)
        {
            if (merged.Fields.ContainsKey(field.Key))
            {
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

    private void LogMemoryUsage(string stage)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);
            var privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
            var gcMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

            _logger.Debug(
                "Memory usage ({Stage}): WorkingSet={WorkingSetMB}MB, PrivateMemory={PrivateMemoryMB}MB, GC={GCMemoryMB}MB",
                stage, workingSetMB, privateMemoryMB, gcMemoryMB);

            if (workingSetMB > 4096)
            {
                _logger.Warning(
                    "High memory usage detected: {WorkingSetMB}MB. Consider enabling streaming output or chunked processing.",
                    workingSetMB);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to log memory usage");
        }
    }
}

