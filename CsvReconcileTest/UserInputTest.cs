using CsvReconcile.Cli;
using CsvReconcile.Core.Models;
using System.Text.Json;

namespace CsvReconcileTest;

public class UserInputTest
{
    [Fact]
    public void ParseConfiguration_WithValidInputs_CreatesValidConfig()
    {
        // Arrange
        var folderA = "TestData/FolderA";
        var folderB = "TestData/FolderB";
        var configPath = Path.Combine("TestConfigs", "test-config.json");
        var output = "TestOutput";
        var parallelism = 4;
        var delimiter = ",";
        var noHeader = false;

        // Act
        var config = CommandLineInterface.ParseConfiguration(
            folderA, folderB, configPath, output, parallelism, delimiter, noHeader);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(folderA, config.FolderA);
        Assert.Equal(folderB, config.FolderB);
        Assert.Equal(output, config.OutputFolder);
        Assert.Equal(parallelism, config.DegreeOfParallelism);
        Assert.Equal(delimiter, config.Delimiter);
        Assert.True(config.HasHeaderRow);
        Assert.NotNull(config.MatchingRule);
        Assert.NotEmpty(config.MatchingRule.MatchingFields);
    }

    [Fact]
    public void ParseConfiguration_WithNonExistentConfigFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var folderA = "TestData/FolderA";
        var folderB = "TestData/FolderB";
        var configPath = "NonExistentConfig.json";
        var output = "TestOutput";
        var parallelism = 4;
        var delimiter = ",";
        var noHeader = false;

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            CommandLineInterface.ParseConfiguration(
                folderA, folderB, configPath, output, parallelism, delimiter, noHeader));
    }

    [Fact]
    public void ParseConfiguration_WithNoHeaderFlag_SetsHasHeaderRowToFalse()
    {
        // Arrange
        var folderA = "TestData/FolderA";
        var folderB = "TestData/FolderB";
        var configPath = Path.Combine("TestConfigs", "test-config.json");
        var output = "TestOutput";
        var parallelism = 4;
        var delimiter = ",";
        var noHeader = true;

        // Act
        var config = CommandLineInterface.ParseConfiguration(
            folderA, folderB, configPath, output, parallelism, delimiter, noHeader);

        // Assert
        Assert.False(config.HasHeaderRow);
    }
}