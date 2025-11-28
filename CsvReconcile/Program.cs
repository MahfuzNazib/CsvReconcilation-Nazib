using CsvReconcile.Application;
using CsvReconcile.Cli;
using CsvReconcile.Core.Models;
using CsvReconcile.Core.Services;
using CsvReconcile.Infrastructure.Csv;
using Serilog;
using Serilog.Events;

namespace CsvReconcile;

internal class Program
{
    static async Task<int> Main()
    {
        // Display banner
        CommandLineInterface.DisplayBanner();

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
                Console.WriteLine($"  Results saved to: {Path.GetFullPath(config.OutputFolder)}");
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
