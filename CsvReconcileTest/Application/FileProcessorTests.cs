using CsvReconcile.Application;
using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using Moq;
using Serilog;

namespace CsvReconcileTest.Application;

public class FileProcessorTests
{
    private readonly Mock<IReconciliationEngine> _mockReconciliationEngine;
    private readonly Mock<ILogger> _mockLogger;
    private readonly FileProcessor _fileProcessor;

    public FileProcessorTests()
    {
        _mockReconciliationEngine = new Mock<IReconciliationEngine>();
        _mockLogger = new Mock<ILogger>();
        _fileProcessor = new FileProcessor(_mockReconciliationEngine.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessAllFilesAsync_WithOneToOneMode_CreatesMatchingPairs()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            FolderA = Path.Combine("TestData", "FolderA"),
            FolderB = Path.Combine("TestData", "FolderB"),
            MatchingMode = FileMatchingMode.OneToOne,
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            DegreeOfParallelism = 1
        };

        _mockReconciliationEngine
            .Setup(x => x.ReconcileFilesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReconciliationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileA, string fileB, ReconciliationConfig cfg, CancellationToken ct) =>
            {
                return new FileComparisonResult
                {
                    FileName = Path.GetFileName(fileA) ?? Path.GetFileName(fileB) ?? "Unknown",
                    FileAPath = fileA,
                    FileBPath = fileB,
                    ExistsInA = !string.IsNullOrEmpty(fileA) && File.Exists(fileA),
                    ExistsInB = !string.IsNullOrEmpty(fileB) && File.Exists(fileB),
                    TotalInA = 0,
                    TotalInB = 0,
                    MatchedCount = 0,
                    OnlyInACount = 0,
                    OnlyInBCount = 0,
                    ProcessingTime = TimeSpan.FromMilliseconds(1)
                };
            });

        // Act
        var result = await _fileProcessor.ProcessAllFilesAsync(config, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileResults);
        // Should have 3 pairs: file1 (both exist), file2 (only in A), file3 (only in B)
        Assert.Equal(3, result.FileResults.Count);
        
        var file1Result = result.FileResults.FirstOrDefault(f => f.FileName == "file1.csv");
        Assert.NotNull(file1Result);
        Assert.True(file1Result.ExistsInA);
        Assert.True(file1Result.ExistsInB);

        var file2Result = result.FileResults.FirstOrDefault(f => f.FileName == "file2.csv");
        Assert.NotNull(file2Result);
        Assert.True(file2Result.ExistsInA);
        Assert.False(file2Result.ExistsInB);

        var file3Result = result.FileResults.FirstOrDefault(f => f.FileName == "file3.csv");
        Assert.NotNull(file3Result);
        Assert.False(file3Result.ExistsInA);
        Assert.True(file3Result.ExistsInB);
    }

    [Fact]
    public async Task ProcessAllFilesAsync_WithAllAgainstAllMode_CreatesAllCombinations()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            FolderA = Path.Combine("TestData", "FolderA"),
            FolderB = Path.Combine("TestData", "FolderB"),
            MatchingMode = FileMatchingMode.AllAgainstAll,
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            DegreeOfParallelism = 1
        };

        _mockReconciliationEngine
            .Setup(x => x.ReconcileFilesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReconciliationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileA, string fileB, ReconciliationConfig cfg, CancellationToken ct) =>
            {
                var fileNameA = Path.GetFileNameWithoutExtension(fileA);
                var fileNameB = Path.GetFileName(fileB);
                return new FileComparisonResult
                {
                    FileName = $"{fileNameA}_vs_{fileNameB}",
                    FileAPath = fileA,
                    FileBPath = fileB,
                    ExistsInA = !string.IsNullOrEmpty(fileA) && File.Exists(fileA),
                    ExistsInB = !string.IsNullOrEmpty(fileB) && File.Exists(fileB),
                    TotalInA = 0,
                    TotalInB = 0,
                    MatchedCount = 0,
                    OnlyInACount = 0,
                    OnlyInBCount = 0,
                    ProcessingTime = TimeSpan.FromMilliseconds(1)
                };
            });

        // Act
        var result = await _fileProcessor.ProcessAllFilesAsync(config, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileResults);
        // Should have 2 files in A × 2 files in B = 4 pairs
        Assert.Equal(4, result.FileResults.Count);
        
        // Verify all combinations exist
        var pairs = result.FileResults.Select(f => f.FileName).ToList();
        Assert.Contains("file1_vs_file1.csv", pairs);
        Assert.Contains("file1_vs_file3.csv", pairs);
        Assert.Contains("file2_vs_file1.csv", pairs);
        Assert.Contains("file2_vs_file3.csv", pairs);
    }

    [Fact]
    public async Task ProcessAllFilesAsync_WithEmptyFolders_ReturnsEmptyResults()
    {
        // Arrange
        var emptyFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyFolder);

        try
        {
            var config = new ReconciliationConfig
            {
                FolderA = emptyFolder,
                FolderB = emptyFolder,
                MatchingMode = FileMatchingMode.OneToOne,
                MatchingRule = new MatchingRule
                {
                    MatchingFields = new List<string> { "Id" },
                    CaseSensitive = false,
                    Trim = true
                },
                DegreeOfParallelism = 1
            };

            // Act
            var result = await _fileProcessor.ProcessAllFilesAsync(config, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.FileResults);
            Assert.Empty(result.FileResults);
        }
        finally
        {
            Directory.Delete(emptyFolder, true);
        }
    }
}


