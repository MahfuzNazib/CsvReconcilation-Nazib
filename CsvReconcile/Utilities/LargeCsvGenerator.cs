using System.Diagnostics;
using System.Text;

namespace CsvReconcile.Utilities;

/// <summary>
/// Utility for generating large CSV files for testing memory optimization features
/// </summary>
public class LargeCsvGenerator
{
  private readonly Random _random = new Random();

  /// <summary>
  /// Generates a large CSV file with configurable parameters
  /// </summary>
  /// <param name="outputPath">Path where the CSV file will be created</param>
  /// <param name="targetSizeMB">Target file size in MB</param>
  /// <param name="numFields">Number of fields per record</param>
  /// <param name="matchingFieldIndex">Index of the field to use for matching (0-based)</param>
  /// <param name="matchPercentage">Percentage of records that should have matching keys (0-100)</param>
  /// <param name="cancellationToken">Cancellation token</param>
  public async Task GenerateLargeCsvAsync(
      string outputPath,
      int targetSizeMB = 100,
      int numFields = 10,
      int matchingFieldIndex = 0,
      double matchPercentage = 80.0,
      CancellationToken cancellationToken = default)
  {
    var targetSizeBytes = (long)targetSizeMB * 1024 * 1024;
    var directory = Path.GetDirectoryName(outputPath);

    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    Console.WriteLine($"Generating large CSV file: {outputPath}");
    Console.WriteLine($"Target size: {targetSizeMB}MB");
    Console.WriteLine($"Fields per record: {numFields}");
    Console.WriteLine($"Matching field index: {matchingFieldIndex}");
    Console.WriteLine();

    var stopwatch = Stopwatch.StartNew();
    var recordCount = 0L;
    var currentSize = 0L;

    // Generate field names
    var fieldNames = Enumerable.Range(0, numFields)
        .Select(i => $"Field{i + 1}")
        .ToArray();

    // Ensure matching field has a meaningful name
    fieldNames[matchingFieldIndex] = "Id";

    await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize: 65536);

    // Write header
    var headerLine = string.Join(",", fieldNames);
    await writer.WriteLineAsync(headerLine);
    currentSize += Encoding.UTF8.GetByteCount(headerLine + Environment.NewLine);

    // Generate records until target size is reached
    var matchingKeys = new HashSet<string>();
    var recordsToMatch = (long)((targetSizeBytes * matchPercentage / 100.0) / (targetSizeBytes / 1000000.0)); // Rough estimate

    while (currentSize < targetSizeBytes && !cancellationToken.IsCancellationRequested)
    {
      var record = GenerateRecord(fieldNames, matchingFieldIndex, recordCount, matchingKeys, recordsToMatch);
      var recordLine = string.Join(",", record);
      await writer.WriteLineAsync(recordLine);

      currentSize += Encoding.UTF8.GetByteCount(recordLine + Environment.NewLine);
      recordCount++;

      // Progress update every 100K records
      if (recordCount % 100000 == 0)
      {
        var currentSizeMB = currentSize / (1024.0 * 1024.0);
        var progress = (currentSize / (double)targetSizeBytes) * 100.0;
        Console.WriteLine($"Progress: {progress:F1}% | Records: {recordCount:N0} | Size: {currentSizeMB:F2}MB");
      }
    }

    stopwatch.Stop();
    var finalSizeMB = currentSize / (1024.0 * 1024.0);

    Console.WriteLine();
    Console.WriteLine($"Generation complete!");
    Console.WriteLine($"Records generated: {recordCount:N0}");
    Console.WriteLine($"File size: {finalSizeMB:F2}MB");
    Console.WriteLine($"Time taken: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"Records/second: {recordCount / stopwatch.Elapsed.TotalSeconds:F0}");
  }

  /// <summary>
  /// Generates a pair of CSV files for testing reconciliation
  /// </summary>
  public async Task GenerateTestPairAsync(
      string folderA,
      string folderB,
      string fileName,
      int targetSizeMB = 100,
      int numFields = 10,
      int matchingFieldIndex = 0,
      double matchPercentage = 80.0,
      double overlapPercentage = 70.0,
      CancellationToken cancellationToken = default)
  {
    Console.WriteLine("Generating test file pair for reconciliation testing...");
    Console.WriteLine();

    // Generate shared matching keys
    var sharedKeys = new HashSet<string>();
    var totalRecords = EstimateRecordCount(targetSizeMB, numFields);
    var sharedKeyCount = (int)(totalRecords * overlapPercentage / 100.0);

    for (int i = 0; i < sharedKeyCount; i++)
    {
      sharedKeys.Add($"KEY-{i:D10}");
    }

    // Generate FileA
    var fileAPath = Path.Combine(folderA, $"{fileName}.csv");
    Console.WriteLine($"Generating FileA: {fileAPath}");
    await GenerateLargeCsvWithKeysAsync(
        fileAPath,
        targetSizeMB,
        numFields,
        matchingFieldIndex,
        sharedKeys,
        cancellationToken);

    // Generate FileB with overlapping keys
    var fileBPath = Path.Combine(folderB, $"{fileName}.csv");
    Console.WriteLine();
    Console.WriteLine($"Generating FileB: {fileBPath}");
    await GenerateLargeCsvWithKeysAsync(
        fileBPath,
        targetSizeMB,
        numFields,
        matchingFieldIndex,
        sharedKeys,
        cancellationToken);
  }

  private async Task GenerateLargeCsvWithKeysAsync(
      string outputPath,
      int targetSizeMB,
      int numFields,
      int matchingFieldIndex,
      HashSet<string> sharedKeys,
      CancellationToken cancellationToken)
  {
    var targetSizeBytes = (long)targetSizeMB * 1024 * 1024;
    var directory = Path.GetDirectoryName(outputPath);

    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var fieldNames = Enumerable.Range(0, numFields)
        .Select(i => $"Field{i + 1}")
        .ToArray();
    fieldNames[matchingFieldIndex] = "Id";

    var recordCount = 0L;
    var currentSize = 0L;
    var keyIndex = 0;
    var sharedKeysList = sharedKeys.ToList();

    await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize: 65536);

    // Write header
    var headerLine = string.Join(",", fieldNames);
    await writer.WriteLineAsync(headerLine);
    currentSize += Encoding.UTF8.GetByteCount(headerLine + Environment.NewLine);

    while (currentSize < targetSizeBytes && !cancellationToken.IsCancellationRequested)
    {
      string matchingKey;
      if (keyIndex < sharedKeysList.Count)
      {
        matchingKey = sharedKeysList[keyIndex++];
      }
      else
      {
        // Generate unique key for records not in shared set
        matchingKey = $"UNIQUE-{recordCount:D10}";
      }

      var record = GenerateRecordWithKey(fieldNames, matchingFieldIndex, matchingKey);
      var recordLine = string.Join(",", record);
      await writer.WriteLineAsync(recordLine);

      currentSize += Encoding.UTF8.GetByteCount(recordLine + Environment.NewLine);
      recordCount++;

      if (recordCount % 100000 == 0)
      {
        var currentSizeMB = currentSize / (1024.0 * 1024.0);
        var progress = (currentSize / (double)targetSizeBytes) * 100.0;
        Console.WriteLine($"  Progress: {progress:F1}% | Records: {recordCount:N0} | Size: {currentSizeMB:F2}MB");
      }
    }
  }

  private string[] GenerateRecord(
      string[] fieldNames,
      int matchingFieldIndex,
      long recordNumber,
      HashSet<string> matchingKeys,
      long recordsToMatch)
  {
    var record = new string[fieldNames.Length];

    // Generate matching key
    string matchingKey;
    if (recordNumber < recordsToMatch && matchingKeys.Count < recordsToMatch)
    {
      matchingKey = $"KEY-{recordNumber:D10}";
      matchingKeys.Add(matchingKey);
    }
    else
    {
      matchingKey = $"UNIQUE-{recordNumber:D10}";
    }

    record[matchingFieldIndex] = matchingKey;

    // Generate other fields
    for (int i = 0; i < fieldNames.Length; i++)
    {
      if (i != matchingFieldIndex)
      {
        record[i] = GenerateFieldValue(fieldNames[i], recordNumber);
      }
    }

    return record;
  }

  private string[] GenerateRecordWithKey(
      string[] fieldNames,
      int matchingFieldIndex,
      string matchingKey)
  {
    var record = new string[fieldNames.Length];
    record[matchingFieldIndex] = matchingKey;

    // Generate other fields
    for (int i = 0; i < fieldNames.Length; i++)
    {
      if (i != matchingFieldIndex)
      {
        record[i] = GenerateFieldValue(fieldNames[i], _random.Next());
      }
    }

    return record;
  }

  private string GenerateFieldValue(string fieldName, long seed)
  {
    var random = new Random((int)(seed % int.MaxValue));

    // Generate different types of data based on field name
    if (fieldName.Contains("Name", StringComparison.OrdinalIgnoreCase))
    {
      return $"Name{random.Next(100000, 999999)}";
    }
    else if (fieldName.Contains("Date", StringComparison.OrdinalIgnoreCase))
    {
      return DateTime.Now.AddDays(-random.Next(0, 365)).ToString("yyyy-MM-dd");
    }
    else if (fieldName.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
             fieldName.Contains("Price", StringComparison.OrdinalIgnoreCase))
    {
      return (random.NextDouble() * 10000).ToString("F2");
    }
    else if (fieldName.Contains("Email", StringComparison.OrdinalIgnoreCase))
    {
      return $"user{random.Next(100000, 999999)}@example.com";
    }
    else
    {
      return $"Value{random.Next(100000, 999999)}";
    }
  }

  private long EstimateRecordCount(int targetSizeMB, int numFields)
  {
    // Rough estimate: average record size
    var avgFieldSize = 20; // bytes per field
    var avgRecordSize = (numFields * avgFieldSize) + (numFields - 1); // + commas
    var targetSizeBytes = (long)targetSizeMB * 1024 * 1024;
    return targetSizeBytes / avgRecordSize;
  }
}

