using CsvReconcile.Application;
using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using CsvReconcile.Core.Services;
using Moq;
using Serilog;

namespace CsvReconcileTest.Application;

public class ReconciliationEngineTests
{
    private readonly Mock<ICsvReader> _mockCsvReader;
    private readonly IRecordMatcher _recordMatcher;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ReconciliationEngine _reconciliationEngine;

    public ReconciliationEngineTests()
    {
        _mockCsvReader = new Mock<ICsvReader>();
        _recordMatcher = new RecordMatcher();
        _mockLogger = new Mock<ILogger>();
        _reconciliationEngine = new ReconciliationEngine(
            _mockCsvReader.Object,
            _recordMatcher,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ReconcileFilesAsync_WithMatchingRecords_ReturnsCorrectCounts()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            Delimiter = ",",
            HasHeaderRow = true,
            MatchingMode = FileMatchingMode.OneToOne
        };

        var recordsA = new List<CsvRecord>
        {
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "1" }, { "Name", "Alice" } }, LineNumber = 1 },
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "2" }, { "Name", "Bob" } }, LineNumber = 2 },
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "3" }, { "Name", "Charlie" } }, LineNumber = 3 }
        };

        var recordsB = new List<CsvRecord>
        {
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "1" }, { "Name", "Alice" } }, LineNumber = 1 },
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "2" }, { "Name", "Bob" } }, LineNumber = 2 },
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "4" }, { "Name", "David" } }, LineNumber = 3 }
        };

        // Create temporary files for the test
        var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcileTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFileA = Path.Combine(tempDir, "fileA.csv");
        var tempFileB = Path.Combine(tempDir, "fileB.csv");
        File.WriteAllText(tempFileA, "Id,Name\n1,Alice\n2,Bob\n3,Charlie");
        File.WriteAllText(tempFileB, "Id,Name\n1,Alice\n2,Bob\n4,David");

        _mockCsvReader
            .Setup(x => x.ReadAllAsync(tempFileA, config.Delimiter, config.HasHeaderRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordsA);

        _mockCsvReader
            .Setup(x => x.ReadAllAsync(tempFileB, config.Delimiter, config.HasHeaderRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordsB);

        try
        {
            // Act
            var result = await _reconciliationEngine.ReconcileFilesAsync(
                tempFileA, tempFileB, config, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.ExistsInA);
            Assert.True(result.ExistsInB);
            Assert.Equal(3, result.TotalInA);
            Assert.Equal(3, result.TotalInB);
            Assert.Equal(2, result.MatchedCount); // Records 1 and 2 match
            Assert.Equal(1, result.OnlyInACount); // Record 3 is only in A
            Assert.Equal(1, result.OnlyInBCount); // Record 4 is only in B
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReconcileFilesAsync_WithFileMissingInA_ReturnsOnlyInBRecords()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            Delimiter = ",",
            HasHeaderRow = true,
            MatchingMode = FileMatchingMode.OneToOne
        };

        var recordsB = new List<CsvRecord>
        {
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "1" }, { "Name", "Alice" } }, LineNumber = 1 }
        };

        // Create temporary file for B only
        var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcileTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFileA = Path.Combine(tempDir, "nonexistentA.csv");
        var tempFileB = Path.Combine(tempDir, "fileB.csv");
        File.WriteAllText(tempFileB, "Id,Name\n1,Alice");

        _mockCsvReader
            .Setup(x => x.ReadAllAsync(tempFileB, config.Delimiter, config.HasHeaderRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordsB);

        try
        {
            // Act
            var result = await _reconciliationEngine.ReconcileFilesAsync(
                tempFileA, tempFileB, config, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ExistsInA);
            Assert.True(result.ExistsInB);
            Assert.Contains(result.Errors, e => e.Contains("not found in FolderA"));
            Assert.Equal(0, result.TotalInA);
            Assert.Equal(1, result.TotalInB);
            Assert.Equal(1, result.OnlyInBCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReconcileFilesAsync_WithFileMissingInB_ReturnsOnlyInARecords()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            Delimiter = ",",
            HasHeaderRow = true,
            MatchingMode = FileMatchingMode.OneToOne
        };

        var recordsA = new List<CsvRecord>
        {
            new CsvRecord { Fields = new Dictionary<string, string> { { "Id", "1" }, { "Name", "Alice" } }, LineNumber = 1 }
        };

        // Create temporary file for A only
        var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcileTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFileA = Path.Combine(tempDir, "fileA.csv");
        var tempFileB = Path.Combine(tempDir, "nonexistentB.csv");
        File.WriteAllText(tempFileA, "Id,Name\n1,Alice");

        _mockCsvReader
            .Setup(x => x.ReadAllAsync(tempFileA, config.Delimiter, config.HasHeaderRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordsA);

        try
        {
            // Act
            var result = await _reconciliationEngine.ReconcileFilesAsync(
                tempFileA, tempFileB, config, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ExistsInA);
            Assert.False(result.ExistsInB);
            Assert.Contains(result.Errors, e => e.Contains("not found in FolderB"));
            Assert.Equal(1, result.TotalInA);
            Assert.Equal(0, result.TotalInB);
            Assert.Equal(1, result.OnlyInACount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReconcileFilesAsync_WithAllAgainstAllMode_SetsCorrectFileName()
    {
        // Arrange
        var config = new ReconciliationConfig
        {
            MatchingRule = new MatchingRule
            {
                MatchingFields = new List<string> { "Id" },
                CaseSensitive = false,
                Trim = true
            },
            Delimiter = ",",
            HasHeaderRow = true,
            MatchingMode = FileMatchingMode.AllAgainstAll
        };

        _mockCsvReader
            .Setup(x => x.ReadAllAsync(It.IsAny<string>(), config.Delimiter, config.HasHeaderRow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CsvRecord>());

        // Create temporary files
        var tempDir = Path.Combine(Path.GetTempPath(), "CsvReconcileTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFileA = Path.Combine(tempDir, "orders.csv");
        var tempFileB = Path.Combine(tempDir, "customers.csv");
        File.WriteAllText(tempFileA, "Id,Name");
        File.WriteAllText(tempFileB, "Id,Name");

        try
        {
            // Act
            var result = await _reconciliationEngine.ReconcileFilesAsync(
                tempFileA, tempFileB, config, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("orders_vs_customers.csv", result.FileName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

