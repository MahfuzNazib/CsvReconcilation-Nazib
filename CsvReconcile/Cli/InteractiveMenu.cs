using CsvReconcile.Core.Models;
using System.Text.Json;

namespace CsvReconcile.Cli;

/// <summary>
/// Interactive menu for user-friendly operation
/// </summary>
public static class InteractiveMenu
{
  /// <summary>
  /// Displays the main menu and gets user's configuration choice
  /// </summary>
  public static ReconciliationConfig? ShowMainMenu()
  {
    while (true)
    {
      Console.Clear();
      DisplayMenuBanner();

      Console.WriteLine("  Please select an option:");
      Console.WriteLine();
      Console.WriteLine("  [1] --> Orders Reconciliation (Single Field: InvoiceId)");
      Console.WriteLine("  [2] --> Customers Reconciliation (Composite: FirstName + LastName)");
      Console.WriteLine("  [3] --> Products Reconciliation (Case Sensitive: ProductCode)");
      Console.WriteLine("  [4] --> Transactions Reconciliation (Single Field: TransactionId)");
      Console.WriteLine("  [5] --> All-Against-All Mode (Compare every file in A vs every file in B)");
      Console.WriteLine("  [6] --> Large File Processing (Optimized for files > 1GB with memory management)");
      Console.WriteLine("  [7] --> Custom Configuration (Specify your own paths and config)");
      Console.WriteLine();
      Console.WriteLine("  [H] Help - View Command Line Usage");
      Console.WriteLine("  [Q] Quit");
      Console.WriteLine();
      Console.Write("  Enter your choice: ");

      var choice = Console.ReadKey().KeyChar.ToString().ToUpper();
      Console.WriteLine();
      Console.WriteLine();

      switch (choice)
      {
        case "1":
          return CreateConfig("Orders", "single-field-config.json");
        case "2":
          return CreateConfig("Customers", "composite-config.json");
        case "3":
          return CreateConfig("Products", "case-sensitive-config.json");
        case "4":
          return CreateConfig("Transactions", "single-field-config.json");
        case "5":
          return CreateConfig("All-Against-All", "all-againest-all.json");
        case "6":
          return GetLargeFileConfiguration();
        case "7":
          return GetCustomConfiguration();
        case "H":
          ShowHelp();
          break;
        case "Q":
          Console.WriteLine("  Exiting application. Goodbye!");
          return null;
        default:
          Console.WriteLine("  Invalid choice. Press any key to try again...");
          Console.ReadKey();
          break;
      }
    }
  }

  /// <summary>
  /// Creates a configuration for predefined scenarios
  /// </summary>
  private static ReconciliationConfig CreateConfig(string scenario, string configFile)
  {
    Console.WriteLine($"  Selected: {scenario} Reconciliation");
    Console.WriteLine();

    // Ask for folder paths
    Console.Write("  Enter FolderA path (press Enter for default 'TestData/FolderA'): ");
    var folderA = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderA))
      folderA = "TestData/FolderA";

    Console.Write("  Enter FolderB path (press Enter for default 'TestData/FolderB'): ");
    var folderB = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderB))
      folderB = "TestData/FolderB";

    Console.Write("  Enter Output folder (press Enter for default 'Output'): ");
    var output = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(output))
      output = "Output";

    Console.Write("  Degree of parallelism (press Enter for default CPU count): ");
    var parallelismInput = Console.ReadLine();
    int parallelism = Environment.ProcessorCount;
    if (!string.IsNullOrWhiteSpace(parallelismInput) && int.TryParse(parallelismInput, out var p))
      parallelism = p;

    Console.Write("  Enable verbose logging? (Y/N, press Enter for No): ");
    var verboseInput = Console.ReadKey();
    bool verbose = verboseInput.Key == ConsoleKey.Y;
    Console.WriteLine();
    Console.WriteLine();

    // Load matching rule from config file
    var configPath = Path.Combine("Configs", configFile);
    if (!File.Exists(configPath))
    {
      Console.WriteLine($"  ERROR: Config file not found: {configPath}");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu()!;
    }

    var configJson = File.ReadAllText(configPath);
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var matchingRule = JsonSerializer.Deserialize<MatchingRule>(configJson, jsonOptions);

    if (matchingRule == null)
    {
      Console.WriteLine("  ERROR: Failed to parse configuration file.");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu()!;
    }

    // Parse additional settings from JSON if present
    FileMatchingMode matchingMode = FileMatchingMode.OneToOne;
    int maxMemoryMB = 0;
    int chunkSizeMB = 1024;
    bool enableStreaming = true;
    bool enableRecordStorage = false;

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

      // Parse memory settings from JSON
      if (doc.RootElement.TryGetProperty("maxMemoryUsageMB", out var maxMemElement) && maxMemElement.ValueKind == JsonValueKind.Number)
      {
        maxMemoryMB = maxMemElement.GetInt32();
      }

      if (doc.RootElement.TryGetProperty("chunkSizeMB", out var chunkElement) && chunkElement.ValueKind == JsonValueKind.Number)
      {
        chunkSizeMB = chunkElement.GetInt32();
      }

      if (doc.RootElement.TryGetProperty("enableStreamingOutput", out var streamingElement))
      {
        if (streamingElement.ValueKind == JsonValueKind.True || streamingElement.ValueKind == JsonValueKind.False)
        {
          enableStreaming = streamingElement.GetBoolean();
        }
      }

      if (doc.RootElement.TryGetProperty("enableRecordStorage", out var storageElement))
      {
        if (storageElement.ValueKind == JsonValueKind.True || storageElement.ValueKind == JsonValueKind.False)
        {
          enableRecordStorage = storageElement.GetBoolean();
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
      Delimiter = ",",
      HasHeaderRow = true,
      MaxMemoryUsageMB = maxMemoryMB,
      ChunkSizeMB = chunkSizeMB,
      EnableStreamingOutput = enableStreaming,
      EnableRecordStorage = enableRecordStorage
    };

    // Display configuration summary
    Console.WriteLine("  ──────────────────────────────────────────────────────");
    Console.WriteLine("  Configuration Summary:");
    Console.WriteLine($"  • Folder A: {config.FolderA}");
    Console.WriteLine($"  • Folder B: {config.FolderB}");
    Console.WriteLine($"  • Output: {config.OutputFolder}");
    Console.WriteLine($"  • Matching Fields: {string.Join(", ", config.MatchingRule.MatchingFields)}");
    Console.WriteLine($"  • Case Sensitive: {config.MatchingRule.CaseSensitive}");
    Console.WriteLine($"  • Trim: {config.MatchingRule.Trim}");
    Console.WriteLine($"  • File Matching Mode: {config.MatchingMode}");
    Console.WriteLine($"  • Parallelism: {config.DegreeOfParallelism}");
    Console.WriteLine($"  • Verbose: {verbose}");
    Console.WriteLine("  ──────────────────────────────────────────────────────");
    Console.WriteLine();
    Console.Write("  Proceed with reconciliation? (Y/N): ");

    var confirm = Console.ReadKey();
    Console.WriteLine();

    if (confirm.Key != ConsoleKey.Y)
    {
      Console.WriteLine("  Cancelled. Returning to menu...");
      System.Threading.Thread.Sleep(1000);
      return ShowMainMenu()!;
    }

    return config;
  }

  /// <summary>
  /// Gets configuration for large file processing with memory optimization options
  /// </summary>
  private static ReconciliationConfig? GetLargeFileConfiguration()
  {
    Console.WriteLine("  Large File Processing Mode");
    Console.WriteLine("  Optimized for files > 1GB with streaming and chunked processing");
    Console.WriteLine();

    // Ask for folder paths
    Console.Write("  Enter FolderA path (press Enter for default 'TestData/FolderA'): ");
    var folderA = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderA))
      folderA = "TestData/FolderA";

    Console.Write("  Enter FolderB path (press Enter for default 'TestData/FolderB'): ");
    var folderB = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderB))
      folderB = "TestData/FolderB";

    Console.Write("  Enter Output folder (press Enter for default 'Output'): ");
    var output = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(output))
      output = "Output";

    // Load matching rule from config file
    Console.Write("  Enter Config file path (press Enter for 'Configs/large-file-config.json'): ");
    var configPathInput = Console.ReadLine();
    var configPath = string.IsNullOrWhiteSpace(configPathInput) 
      ? Path.Combine("Configs", "large-file-config.json")
      : configPathInput;

    if (!File.Exists(configPath))
    {
      Console.WriteLine($"  ERROR: Config file not found: {configPath}");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu()!;
    }

    var configJson = File.ReadAllText(configPath);
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var matchingRule = JsonSerializer.Deserialize<MatchingRule>(configJson, jsonOptions);

    if (matchingRule == null)
    {
      Console.WriteLine("  ERROR: Failed to parse configuration file.");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu()!;
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

    // Large file specific settings
    Console.WriteLine();
    Console.WriteLine("  ──────────────────────────────────────────────────────");
    Console.WriteLine("  Large File Memory Settings:");
    Console.WriteLine("  ──────────────────────────────────────────────────────");

    Console.Write("  Max memory usage per file pair in MB (0 = auto-detect, default 0): ");
    var maxMemoryInput = Console.ReadLine();
    int maxMemoryMB = 0;
    if (!string.IsNullOrWhiteSpace(maxMemoryInput) && int.TryParse(maxMemoryInput, out var mm))
      maxMemoryMB = mm;

    Console.Write("  Chunk size in MB for chunked processing (default 1024 = 1GB): ");
    var chunkSizeInput = Console.ReadLine();
    int chunkSizeMB = 1024;
    if (!string.IsNullOrWhiteSpace(chunkSizeInput) && int.TryParse(chunkSizeInput, out var cs))
      chunkSizeMB = cs;

    Console.Write("  Enable streaming output? (Y/N, default Yes): ");
    var streamingInput = Console.ReadKey();
    bool enableStreaming = streamingInput.Key != ConsoleKey.N;
    Console.WriteLine();

    Console.Write("  Store full records in memory? (Y/N, default No - memory efficient): ");
    var recordStorageInput = Console.ReadKey();
    bool enableRecordStorage = recordStorageInput.Key == ConsoleKey.Y;
    Console.WriteLine();

    Console.Write("  Degree of parallelism (press Enter for default CPU count): ");
    var parallelismInput = Console.ReadLine();
    int parallelism = Environment.ProcessorCount;
    if (!string.IsNullOrWhiteSpace(parallelismInput) && int.TryParse(parallelismInput, out var p))
      parallelism = p;

    Console.Write("  Enable verbose logging? (Y/N, press Enter for No): ");
    var verboseInput = Console.ReadKey();
    bool verbose = verboseInput.Key == ConsoleKey.Y;
    Console.WriteLine();
    Console.WriteLine();

    var config = new ReconciliationConfig
    {
      FolderA = folderA,
      FolderB = folderB,
      OutputFolder = output,
      MatchingRule = matchingRule,
      MatchingMode = matchingMode,
      DegreeOfParallelism = parallelism,
      Delimiter = ",",
      HasHeaderRow = true,
      MaxMemoryUsageMB = maxMemoryMB,
      ChunkSizeMB = chunkSizeMB,
      EnableStreamingOutput = enableStreaming,
      EnableRecordStorage = enableRecordStorage
    };

    // Display configuration summary
    Console.WriteLine("  ──────────────────────────────────────────────────────");
    Console.WriteLine("  Configuration Summary:");
    Console.WriteLine($"  • Folder A: {config.FolderA}");
    Console.WriteLine($"  • Folder B: {config.FolderB}");
    Console.WriteLine($"  • Output: {config.OutputFolder}");
    Console.WriteLine($"  • Matching Fields: {string.Join(", ", config.MatchingRule.MatchingFields)}");
    Console.WriteLine($"  • Case Sensitive: {config.MatchingRule.CaseSensitive}");
    Console.WriteLine($"  • Trim: {config.MatchingRule.Trim}");
    Console.WriteLine($"  • File Matching Mode: {config.MatchingMode}");
    Console.WriteLine($"  • Streaming Output: {config.EnableStreamingOutput}");
    Console.WriteLine($"  • Max Memory: {config.MaxMemoryUsageMB}MB (0 = auto)");
    Console.WriteLine($"  • Chunk Size: {config.ChunkSizeMB}MB");
    Console.WriteLine($"  • Store Records: {config.EnableRecordStorage}");
    Console.WriteLine($"  • Parallelism: {config.DegreeOfParallelism}");
    Console.WriteLine($"  • Verbose: {verbose}");
    Console.WriteLine("  ──────────────────────────────────────────────────────");
    Console.WriteLine();
    Console.Write("  Proceed with reconciliation? (Y/N): ");

    var confirm = Console.ReadKey();
    Console.WriteLine();

    if (confirm.Key != ConsoleKey.Y)
    {
      Console.WriteLine("  Cancelled. Returning to menu...");
      System.Threading.Thread.Sleep(1000);
      return ShowMainMenu()!;
    }

    return config;
  }

  /// <summary>
  /// Gets custom configuration from user
  /// </summary>
  private static ReconciliationConfig? GetCustomConfiguration()
  {
    Console.WriteLine("  Custom Configuration Mode");
    Console.WriteLine();

    Console.Write("  Enter FolderA path: ");
    var folderA = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderA))
    {
      Console.WriteLine("  ERROR: FolderA is required.");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu();
    }

    Console.Write("  Enter FolderB path: ");
    var folderB = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(folderB))
    {
      Console.WriteLine("  ERROR: FolderB is required.");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu();
    }

    Console.Write("  Enter Config file path (JSON): ");
    var configPath = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
    {
      Console.WriteLine($"  ERROR: Config file not found: {configPath}");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu();
    }

    Console.Write("  Enter Output folder (default 'Output'): ");
    var output = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(output))
      output = "Output";

    Console.Write("  Degree of parallelism (default CPU count): ");
    var parallelismInput = Console.ReadLine();
    int parallelism = Environment.ProcessorCount;
    if (!string.IsNullOrWhiteSpace(parallelismInput) && int.TryParse(parallelismInput, out var p))
      parallelism = p;

    Console.Write("  CSV delimiter (default ','): ");
    var delimiter = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(delimiter))
      delimiter = ",";

    Console.Write("  Files have header row? (Y/N, default Yes): ");
    var headerInput = Console.ReadKey();
    bool hasHeader = headerInput.Key != ConsoleKey.N;
    Console.WriteLine();

    try
    {
      var configJson = File.ReadAllText(configPath);
      var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
      var matchingRule = JsonSerializer.Deserialize<MatchingRule>(configJson, jsonOptions);

      if (matchingRule == null)
        throw new InvalidOperationException("Failed to parse matching rule.");

      // Parse additional settings from JSON if present
      FileMatchingMode matchingMode = FileMatchingMode.OneToOne;
      int maxMemoryMB = 0;
      int chunkSizeMB = 1024;
      bool enableStreaming = true;
      bool enableRecordStorage = false;

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

        // Parse memory settings from JSON
        if (doc.RootElement.TryGetProperty("maxMemoryUsageMB", out var maxMemElement) && maxMemElement.ValueKind == JsonValueKind.Number)
        {
          maxMemoryMB = maxMemElement.GetInt32();
        }

        if (doc.RootElement.TryGetProperty("chunkSizeMB", out var chunkElement) && chunkElement.ValueKind == JsonValueKind.Number)
        {
          chunkSizeMB = chunkElement.GetInt32();
        }

        if (doc.RootElement.TryGetProperty("enableStreamingOutput", out var streamingElement))
        {
          if (streamingElement.ValueKind == JsonValueKind.True || streamingElement.ValueKind == JsonValueKind.False)
          {
            enableStreaming = streamingElement.GetBoolean();
          }
        }

        if (doc.RootElement.TryGetProperty("enableRecordStorage", out var storageElement))
        {
          if (storageElement.ValueKind == JsonValueKind.True || storageElement.ValueKind == JsonValueKind.False)
          {
            enableRecordStorage = storageElement.GetBoolean();
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
        HasHeaderRow = hasHeader,
        MaxMemoryUsageMB = maxMemoryMB,
        ChunkSizeMB = chunkSizeMB,
        EnableStreamingOutput = enableStreaming,
        EnableRecordStorage = enableRecordStorage
      };

      Console.WriteLine();
      Console.Write("  Proceed with reconciliation? (Y/N): ");
      var confirm = Console.ReadKey();
      Console.WriteLine();

      if (confirm.Key != ConsoleKey.Y)
      {
        Console.WriteLine("  Cancelled. Returning to menu...");
        System.Threading.Thread.Sleep(1000);
        return ShowMainMenu();
      }

      return config;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"  ERROR: {ex.Message}");
      Console.WriteLine("  Press any key to return to menu...");
      Console.ReadKey();
      return ShowMainMenu();
    }
  }

  /// <summary>
  /// Displays the menu banner
  /// </summary>
  private static void DisplayMenuBanner()
  {
    Console.WriteLine();
    Console.WriteLine("  ╔════════════════=══════════════════════════════════════════╗");
    Console.WriteLine("  ║          CSV Reconciliation Tool v1.0                     ║");
    Console.WriteLine("  ║          Interactive Mode                                 ║");
    Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();
  }

  /// <summary>
  /// Shows command-line help
  /// </summary>
  private static void ShowHelp()
  {
    Console.Clear();
    Console.WriteLine();
    Console.WriteLine("  Command-Line Usage:");
    Console.WriteLine("  ═══════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine("  CsvReconcile --folderA <path> --folderB <path> --config <path> [options]");
    Console.WriteLine();
    Console.WriteLine("  Required Options:");
    Console.WriteLine("    --folderA, -a <path>      Path to folder A containing CSV files");
    Console.WriteLine("    --folderB, -b <path>      Path to folder B containing CSV files");
    Console.WriteLine("    --config, -c <path>       Path to JSON configuration file");
    Console.WriteLine();
    Console.WriteLine("  Optional:");
    Console.WriteLine("    --output, -o <path>       Output folder (default: 'Output')");
    Console.WriteLine("    --parallelism, -p <num>   Thread count (default: CPU count)");
    Console.WriteLine("    --delimiter, -d <char>    CSV delimiter (default: ',')");
    Console.WriteLine("    --no-header               Files have no headers");
    Console.WriteLine("    --verbose, -v             Verbose logging");
    Console.WriteLine();
    Console.WriteLine("  Example:");
    Console.WriteLine("    dotnet run -- -a Data/A -b Data/B -c config.json -v");
    Console.WriteLine();
    Console.WriteLine("  Press any key to return to menu...");
    Console.ReadKey();
  }
}

