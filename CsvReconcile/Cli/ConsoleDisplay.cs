using CsvReconcile.Core.Models;

namespace CsvReconcile.Cli;

public static class ConsoleDisplay
{
  private static readonly object _consoleLock = new object();

  public static void ShowProgressHeader()
  {
    Console.WriteLine();
    Console.WriteLine("  ╔════════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║                         RECONCILIATION IN PROGRESS                             ║");
    Console.WriteLine("  ╚════════════════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    DrawProcessingTableHeader();
  }

  private static void DrawProcessingTableHeader()
  {
    Console.WriteLine("  ┌──────────┬────────────────────────┬─────────────────────┬────────────────────────────┐");
    Console.WriteLine("  │ Thread   │ File Name              │ Status              │ Details                    │");
    Console.WriteLine("  ├──────────┼────────────────────────┼─────────────────────┼────────────────────────────┤");
  }

  public static void ShowProcessingEvent(int threadId, string fileName, string status, string details)
  {
    lock (_consoleLock)
    {
      var thread = threadId.ToString().PadLeft(8);
      var file = TruncateString(fileName, 22);
      var stat = TruncateString(status, 19);
      var det = TruncateString(details, 26);

      // Color code based on status
      Console.Write("  │ ");
      Console.Write(thread);
      Console.Write(" │ ");
      Console.Write(file);
      Console.Write(" │ ");

      if (status.Contains("Completed"))
      {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(stat);
        Console.ResetColor();
      }
      else if (status.Contains("Warning"))
      {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(stat);
        Console.ResetColor();
      }
      else if (status.Contains("Error"))
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(stat);
        Console.ResetColor();
      }
      else if (status.Contains("Processing"))
      {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(stat);
        Console.ResetColor();
      }
      else
      {
        Console.Write(stat);
      }

      Console.Write(" │ ");
      Console.Write(det);
      Console.WriteLine(" │");
    }
  }

  public static void ShowProcessingTableFooter()
  {
    Console.WriteLine("  └──────────┴────────────────────────┴─────────────────────┴────────────────────────────┘");
  }


  public static void ShowFinalSummary(ReconciliationResult result)
  {
    Console.WriteLine();
    Console.WriteLine("  ╔════════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║                           RECONCILIATION SUMMARY                               ║");
    Console.WriteLine("  ╚════════════════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // Statistics Table
    Console.WriteLine("  ┌─────────────────────────────────────────┬──────────────────────────────────────┐");
    Console.WriteLine("  │ Metric                                  │ Value                                │");
    Console.WriteLine("  ├─────────────────────────────────────────┼──────────────────────────────────────┤");

    ShowSummaryRow("Files Processed", result.FileResults.Count.ToString());
    ShowSummaryRow("Successful", result.SuccessfulFiles.ToString(), ConsoleColor.Green);

    if (result.FailedFiles > 0)
      ShowSummaryRow("Failed", result.FailedFiles.ToString(), ConsoleColor.Red);
    else
      ShowSummaryRow("Failed", "0");

    Console.WriteLine("  ├─────────────────────────────────────────┼──────────────────────────────────────┤");
    ShowSummaryRow("Total Records in Folder A", result.TotalRecordsInA.ToString("N0"));
    ShowSummaryRow("Total Records in Folder B", result.TotalRecordsInB.ToString("N0"));
    Console.WriteLine("  ├─────────────────────────────────────────┼──────────────────────────────────────┤");

    if (result.TotalMatched > 0)
      ShowSummaryRow("Matched Records", result.TotalMatched.ToString("N0"), ConsoleColor.Green);
    else
      ShowSummaryRow("Matched Records", "0");

    if (result.TotalOnlyInA > 0)
      ShowSummaryRow("Only in Folder A", result.TotalOnlyInA.ToString("N0"), ConsoleColor.Yellow);
    else
      ShowSummaryRow("Only in Folder A", "0");

    if (result.TotalOnlyInB > 0)
      ShowSummaryRow("Only in Folder B", result.TotalOnlyInB.ToString("N0"), ConsoleColor.Yellow);
    else
      ShowSummaryRow("Only in Folder B", "0");

    Console.WriteLine("  ├─────────────────────────────────────────┼──────────────────────────────────────┤");
    ShowSummaryRow("Processing Time", $"{result.TotalProcessingTime.TotalSeconds:F2} seconds");

    Console.WriteLine("  └─────────────────────────────────────────┴──────────────────────────────────────┘");

    // Missing Files Warning
    if (result.MissingInA.Any() || result.MissingInB.Any())
    {
      Console.WriteLine();
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine("  WARNING: Missing Files Detected");
      Console.ResetColor();

      if (result.MissingInA.Any())
      {
        Console.WriteLine("  Missing in Folder A:");
        foreach (var file in result.MissingInA)
          Console.WriteLine($"    • {file}");
      }

      if (result.MissingInB.Any())
      {
        Console.WriteLine("  Missing in Folder B:");
        foreach (var file in result.MissingInB)
          Console.WriteLine($"    • {file}");
      }
    }

    // Errors Warning
    if (result.FailedFiles > 0)
    {
      Console.WriteLine();
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine("  WARNING: Some files had errors. Check the log file for details.");
      Console.ResetColor();
    }
  }

  private static void ShowSummaryRow(string label, string value, ConsoleColor? color = null)
  {
    var paddedLabel = label.PadRight(39);
    var paddedValue = value.PadRight(36);

    if (color.HasValue)
    {
      Console.Write("  │ ");
      Console.Write(paddedLabel);
      Console.Write(" │ ");
      Console.ForegroundColor = color.Value;
      Console.Write(paddedValue);
      Console.ResetColor();
      Console.WriteLine(" │");
    }
    else
    {
      Console.WriteLine($"  │ {paddedLabel} │ {paddedValue} │");
    }
  }

  private static string TruncateString(string str, int maxLength)
  {
    if (string.IsNullOrEmpty(str))
      return new string(' ', maxLength);

    if (str.Length <= maxLength)
      return str.PadRight(maxLength);

    return str.Substring(0, maxLength - 3) + "...";
  }

  public static void ShowProcessing(string message)
  {
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  {message}");
    Console.ResetColor();
  }

  public static void ShowSuccess(string message)
  {
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  {message}");
    Console.ResetColor();
  }

  public static void ShowError(string message)
  {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  {message}");
    Console.ResetColor();
  }

  public static void ShowWarning(string message)
  {
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  {message}");
    Console.ResetColor();
  }
}

