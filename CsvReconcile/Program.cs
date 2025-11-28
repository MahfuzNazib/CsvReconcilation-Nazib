using CsvReconcile.Application;
using CsvReconcile.Cli;
using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using CsvReconcile.Core.Services;
using CsvReconcile.Infrastructure.Csv;
using Serilog;
using Serilog.Events;
using System.CommandLine;

namespace CsvReconcile;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        ILogger? logger = null;

        try
        {
            // Display banner
            CommandLineInterface.DisplayBanner();

            // If no arguments provided, enter interactive mode
            if (args.Length == 0)
            {
                return await RunInteractiveModeAsync();
            }

            // Otherwise, use command-line mode
            // Create root command
            var rootCommand = CommandLineInterface.CreateRootCommand();

            // Extract options
            var folderAOption = rootCommand.Options[0] as System.CommandLine.Option<string>;
            var folderBOption = rootCommand.Options[1] as System.CommandLine.Option<string>;
            var configOption = rootCommand.Options[2] as System.CommandLine.Option<string>;
            var outputOption = rootCommand.Options[3] as System.CommandLine.Option<string>;
            var parallelismOption = rootCommand.Options[4] as System.CommandLine.Option<int>;
            var delimiterOption = rootCommand.Options[5] as System.CommandLine.Option<string>;
            var noHeaderOption = rootCommand.Options[6] as System.CommandLine.Option<bool>;
            var verboseOption = rootCommand.Options[7] as System.CommandLine.Option<bool>;
            var maxMemoryOption = rootCommand.Options[8] as System.CommandLine.Option<int>;
            var chunkSizeOption = rootCommand.Options[9] as System.CommandLine.Option<int>;
            var enableStreamingOption = rootCommand.Options[10] as System.CommandLine.Option<bool>;
            var enableRecordStorageOption = rootCommand.Options[11] as System.CommandLine.Option<bool>;

            // Set handler for the command
            rootCommand.SetHandler(async (
                string folderA,
                string folderB,
                string configPath,
                string output,
                int parallelism,
                string delimiter,
                bool noHeader,
                bool verbose) =>
            {
                // Get memory-related options from parse result
                var parseResult = rootCommand.Parse(args);
                int maxMemoryMB = parseResult.GetValueForOption(maxMemoryOption!);
                int chunkSizeMB = parseResult.GetValueForOption(chunkSizeOption!);
                bool enableStreaming = parseResult.GetValueForOption(enableStreamingOption!);
                bool enableRecordStorage = parseResult.GetValueForOption(enableRecordStorageOption!);

                int exitCode = 0;

                // Configure logging (console enabled for CLI mode)
                var logLevel = verbose ? LogEventLevel.Debug : LogEventLevel.Information;
                logger = Infrastructure.Logging.LoggerConfiguration.CreateLogger(output, logLevel, consoleLogging: false);

                logger.Information("CSV Reconciliation Tool started (CLI Mode)");

                try
                {
                    // Parse configuration
                    var config = CommandLineInterface.ParseConfiguration(
                        folderA, folderB, configPath, output, parallelism, delimiter, noHeader,
                        maxMemoryMB, chunkSizeMB, enableStreaming, enableRecordStorage);

                    logger.Information("Configuration loaded successfully");
                    logger.Information("Matching fields: {Fields}",
                        string.Join(", ", config.MatchingRule.MatchingFields));
                    logger.Information("Case sensitive: {CaseSensitive}", config.MatchingRule.CaseSensitive);
                    logger.Information("Trim: {Trim}", config.MatchingRule.Trim);
                    logger.Information("File matching mode: {MatchingMode}", config.MatchingMode);
                    logger.Information("Streaming output: {EnableStreamingOutput}", config.EnableStreamingOutput);
                    logger.Information("Max memory usage: {MaxMemoryMB}MB (0 = auto)", config.MaxMemoryUsageMB);
                    logger.Information("Chunk size: {ChunkSizeMB}MB", config.ChunkSizeMB);

                    // Show clean progress header
                    ConsoleDisplay.ShowProgressHeader();

                    // Build dependency graph (manual DI for simplicity)
                    var recordMatcher = new RecordMatcher();
                    var csvReader = new CsvHelperReader(logger);
                    var csvWriter = new CsvHelperWriter(logger);
                    var reconciliationEngine = new ReconciliationEngine(csvReader, recordMatcher, csvWriter, logger);
                    var fileProcessor = new FileProcessor(reconciliationEngine, logger);
                    var outputGenerator = new OutputGenerator(csvWriter, logger);

                    // Execute reconciliation
                    var result = await fileProcessor.ProcessAllFilesAsync(config, CancellationToken.None);

                    ConsoleDisplay.ShowProcessingTableFooter();
                    Console.WriteLine();
                    ConsoleDisplay.ShowProcessing("Generating output files...");

                    // Generate outputs
                    logger.Information("Generating output files...");

                    foreach (var fileResult in result.FileResults)
                    {
                        await outputGenerator.GenerateFileOutputsAsync(
                            fileResult, config.OutputFolder, config.Delimiter, CancellationToken.None);
                    }

                    await outputGenerator.GenerateGlobalSummaryAsync(
                        result, config.OutputFolder, CancellationToken.None);

                    ConsoleDisplay.ShowSuccess("Output files generated successfully");
                    logger.Information("Output files generated successfully");
                    logger.Information("Results saved to: {OutputFolder}", Path.GetFullPath(config.OutputFolder));

                    // Display clean summary
                    ConsoleDisplay.ShowFinalSummary(result);

                    Console.WriteLine();
                    Console.WriteLine("  ══════════════════════════════════════════════════════════");
                    Console.WriteLine($"  📁 Results saved to: {Path.GetFullPath(config.OutputFolder)}");
                    Console.WriteLine("  ══════════════════════════════════════════════════════════");
                    Console.WriteLine();

                    exitCode = result.FailedFiles > 0 ? 1 : 0;
                }
                catch (Exception ex)
                {
                    logger?.Error(ex, "Fatal error during reconciliation");
                    Console.Error.WriteLine($"ERROR: {ex.Message}");
                    exitCode = 1;
                }
                finally
                {
                    Log.CloseAndFlush();
                }
            },
            folderAOption!,
            folderBOption!,
            configOption!,
            outputOption!,
            parallelismOption!,
            delimiterOption!,
            noHeaderOption!,
            verboseOption!);

            // Invoke the command
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
            logger?.Error(ex, "Fatal error in main");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Displays a summary of the reconciliation results
    /// </summary>
    private static void DisplaySummary(ReconciliationResult result)
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  RECONCILIATION SUMMARY                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  Files Processed:    {result.FileResults.Count}");
        Console.WriteLine($"  Successful:         {result.SuccessfulFiles}");
        Console.WriteLine($"  Failed:             {result.FailedFiles}");
        Console.WriteLine();
        Console.WriteLine($"  Total Records in A: {result.TotalRecordsInA:N0}");
        Console.WriteLine($"  Total Records in B: {result.TotalRecordsInB:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Matched:            {result.TotalMatched:N0}");
        Console.WriteLine($"  Only in A:          {result.TotalOnlyInA:N0}");
        Console.WriteLine($"  Only in B:          {result.TotalOnlyInB:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Processing Time:    {result.TotalProcessingTime.TotalSeconds:F2} seconds");
        Console.WriteLine();

        if (result.MissingInA.Any())
        {
            Console.WriteLine("  Files missing in Folder A:");
            foreach (var file in result.MissingInA)
            {
                Console.WriteLine($"    - {file}");
            }
            Console.WriteLine();
        }

        if (result.MissingInB.Any())
        {
            Console.WriteLine("  Files missing in Folder B:");
            foreach (var file in result.MissingInB)
            {
                Console.WriteLine($"    - {file}");
            }
            Console.WriteLine();
        }

        if (result.FailedFiles > 0)
        {
            Console.WriteLine("  ⚠ Some files had errors. Check the log file for details.");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Runs the application in interactive mode
    /// </summary>
    private static async Task<int> RunInteractiveModeAsync()
    {
        while (true) // Loop for "Run Again" functionality
        {
            ILogger? logger = null;

            try
            {
                // Show interactive menu and get configuration
                var config = InteractiveMenu.ShowMainMenu();

                if (config == null)
                {
                    // User chose to quit
                    return 0;
                }

                // Validate configuration
                try
                {
                    config.Validate();
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  ERROR: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine("  Press any key to exit...");
                    Console.ReadKey();
                    return 1;
                }

                // Configure logging (file only, suppress console from Serilog)
                logger = Infrastructure.Logging.LoggerConfiguration.CreateLogger(
                    config.OutputFolder, LogEventLevel.Information, consoleLogging: false);

                logger.Information("CSV Reconciliation Tool started (Interactive Mode)");
                logger.Information("Configuration loaded successfully");
                logger.Information("Matching fields: {Fields}",
                    string.Join(", ", config.MatchingRule.MatchingFields));
                logger.Information("Case sensitive: {CaseSensitive}", config.MatchingRule.CaseSensitive);
                logger.Information("Trim: {Trim}", config.MatchingRule.Trim);
                logger.Information("File matching mode: {MatchingMode}", config.MatchingMode);

                // Show clean progress header
                ConsoleDisplay.ShowProgressHeader();

                // Build dependency graph (manual DI for simplicity)
                var recordMatcher = new RecordMatcher();
                var csvReader = new CsvHelperReader(logger);
                var csvWriter = new CsvHelperWriter(logger);
                var reconciliationEngine = new ReconciliationEngine(csvReader, recordMatcher, logger);
                var fileProcessor = new FileProcessor(reconciliationEngine, logger);
                var outputGenerator = new OutputGenerator(csvWriter, logger);

                // Execute reconciliation
                var result = await fileProcessor.ProcessAllFilesAsync(config, CancellationToken.None);

                ConsoleDisplay.ShowProcessingTableFooter();
                Console.WriteLine();
                ConsoleDisplay.ShowProcessing("Generating output files...");

                // Generate outputs
                logger.Information("Generating output files...");

                foreach (var fileResult in result.FileResults)
                {
                    await outputGenerator.GenerateFileOutputsAsync(
                        fileResult, config.OutputFolder, config.Delimiter, CancellationToken.None);
                }

                await outputGenerator.GenerateGlobalSummaryAsync(
                    result, config.OutputFolder, CancellationToken.None);

                ConsoleDisplay.ShowSuccess("Output files generated successfully");
                logger.Information("Output files generated successfully");
                logger.Information("Results saved to: {OutputFolder}", Path.GetFullPath(config.OutputFolder));

                // Display clean summary
                ConsoleDisplay.ShowFinalSummary(result);

                Console.WriteLine();
                Console.WriteLine("  ══════════════════════════════════════════════════════════");
                Console.WriteLine($"  📁 Results saved to: {Path.GetFullPath(config.OutputFolder)}");
                Console.WriteLine("  ══════════════════════════════════════════════════════════");
                Console.WriteLine();

                Log.CloseAndFlush();

                // Ask if user wants to run again
                Console.Write("  Do You Want To Run Again? (Y/N): ");
                var runAgain = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();

                if (runAgain.Key == ConsoleKey.Y)
                {
                    // Loop back to menu
                    continue;
                }
                else
                {
                    // Exit
                    Console.WriteLine("  Thank you for using CSV Reconciliation Tool. Goodbye!");
                    Console.WriteLine();
                    return result.FailedFiles > 0 ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Fatal error during reconciliation");
                Console.WriteLine();
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();

                Log.CloseAndFlush();

                // Ask if user wants to try again after error
                Console.Write("  Do You Want To Try Again? (Y/N): ");
                var tryAgain = Console.ReadKey();
                Console.WriteLine();
                Console.WriteLine();

                if (tryAgain.Key == ConsoleKey.Y)
                {
                    // Loop back to menu
                    continue;
                }
                else
                {
                    // Exit with error
                    Console.WriteLine("  Exiting application.");
                    Console.WriteLine();
                    return 1;
                }
            }
        }
    }
}
