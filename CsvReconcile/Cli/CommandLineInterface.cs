using CsvReconcile.Core.Models;
using System.CommandLine;
using System.Text.Json;

namespace CsvReconcile.Cli;

/// <summary>
/// Command-line interface configuration using System.CommandLine
/// </summary>
public static class CommandLineInterface
{
    /// <summary>
    /// Creates the root command with all options
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("CSV Reconciliation Tool - Compares CSV files between two folders");

        // Define options
        var folderAOption = new Option<string>(
            aliases: new[] { "--folderA", "-a" },
            description: "Path to folder A containing CSV files")
        {
            IsRequired = true
        };

        var folderBOption = new Option<string>(
            aliases: new[] { "--folderB", "-b" },
            description: "Path to folder B containing CSV files")
        {
            IsRequired = true
        };

        var configOption = new Option<string>(
            aliases: new[] { "--config", "-c" },
            description: "Path to JSON configuration file with matching rules")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "Output",
            description: "Output folder for reconciliation results (default: 'Output')");

        var parallelismOption = new Option<int>(
            aliases: new[] { "--parallelism", "-p" },
            getDefaultValue: () => Environment.ProcessorCount,
            description: $"Degree of parallelism (default: {Environment.ProcessorCount})");

        var delimiterOption = new Option<string>(
            aliases: new[] { "--delimiter", "-d" },
            getDefaultValue: () => ",",
            description: "CSV delimiter character (default: ',')");

        var noHeaderOption = new Option<bool>(
            aliases: new[] { "--no-header" },
            getDefaultValue: () => false,
            description: "CSV files do not have header row (default: false)");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            getDefaultValue: () => false,
            description: "Enable verbose logging (default: false)");

        // Add options to root command
        rootCommand.AddOption(folderAOption);
        rootCommand.AddOption(folderBOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(parallelismOption);
        rootCommand.AddOption(delimiterOption);
        rootCommand.AddOption(noHeaderOption);
        rootCommand.AddOption(verboseOption);

        return rootCommand;
    }

    /// <summary>
    /// Parses and validates the reconciliation configuration from command-line arguments
    /// </summary>
    public static ReconciliationConfig ParseConfiguration(
        string folderA,
        string folderB,
        string configPath,
        string output,
        int parallelism,
        string delimiter,
        bool noHeader)
    {
        // Read matching rule from JSON config file
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var configJson = File.ReadAllText(configPath);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        var matchingRule = JsonSerializer.Deserialize<MatchingRule>(configJson, jsonOptions);

        if (matchingRule == null)
        {
            throw new InvalidOperationException("Failed to parse matching rule from configuration file.");
        }

        // Parse matchingMode from JSON if present
        FileMatchingMode matchingMode = FileMatchingMode.OneToOne;
        using (var doc = JsonDocument.Parse(configJson))
        {
            if (doc.RootElement.TryGetProperty("matchingMode", out var modeElement))
            {
                var modeString = modeElement.GetString();
                if (Enum.TryParse<FileMatchingMode>(modeString, ignoreCase: true, out var parsedMode))
                {
                    matchingMode = parsedMode;
                }
            }
        }

        var config = new ReconciliationConfig
        {
            FolderA = folderA,
            FolderB = folderB,
            OutputFolder = output,
            MatchingRule = matchingRule,
            MatchingMode = matchingMode,
            DegreeOfParallelism = parallelism,
            Delimiter = delimiter,
            HasHeaderRow = !noHeader
        };

        // Validate configuration
        config.Validate();

        return config;
    }

    /// <summary>
    /// Displays a welcome banner
    /// </summary>
    public static void DisplayBanner()
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          CSV Reconciliation Tool v1.0                     ║");
        Console.WriteLine("║          Multithreaded CSV Data Comparison                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }
}

