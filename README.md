# CSV Reconciliation Tool

C# console application that compares CSV files between two folders and identifies matching records, records unique to each folder, and generates detailed reconciliation reports. Perfect for data validation, migration verification, and finding discrepancies between datasets.

## What It Does

Ever had two folders full of CSV files and needed to figure out what's the same, what's different, and what's missing? This tool does exactly that. It:

- **Compares** CSV files with matching names across two folders
- **Identifies** records that match between both files
- **Finds** records that exist only in Folder A
- **Finds** records that exist only in Folder B
- **Generates** organized output files with all the results
- **Processes** multiple files in parallel for speed

## Features

**Flexible Matching Rules** - Match on single fields or composite keys (e.g., FirstName + LastName)  
**Parallel Processing** - Process multiple file pairs simultaneously for better performance  
**Interactive Mode** - User-friendly menu-driven interface for easy operation  
**Command-Line Interface** - Full CLI support for automation and scripting  
**Detailed Reports** - JSON summaries and organized CSV outputs  
**Configurable Options** - Case-sensitive/insensitive matching, whitespace trimming, custom delimiters  
**Comprehensive Logging** - Detailed logs with Serilog for troubleshooting

## Requirements

- .NET 8.0 SDK or later
- Windows, Linux, or macOS

## Installation

1. Clone the repository:

```bash
git clone https://github.com/MahfuzNazib/CsvReconcilation-Nazib
cd CsvReconcile
```

2. Restore dependencies:

```bash
dotnet restore
```

3. Build the project:

```bash
dotnet build
```

## Quick Start

### Interactive Mode (Recommended for First-Time Users)

Simply run the application without any arguments:

```bash
dotnet run
```

You'll see a friendly menu with predefined scenarios:

- Orders Reconciliation
- Customers Reconciliation
- Products Reconciliation
- Transactions Reconciliation
- Custom Configuration

Just follow the prompts!

### Command-Line Mode

For automation or when you know exactly what you need:

```bash
dotnet run -- --folderA "TestData/FolderA" --folderB "TestData/FolderB" --config "Configs/single-field-config.json" --output "Output"
```

## Configuration

Matching rules are defined in JSON configuration files. Here's what they look like:

### Single Field Matching

```json
{
  "matchingFields": ["InvoiceId"],
  "caseSensitive": false,
  "trim": true
}
```

### Composite Field Matching

```json
{
  "matchingFields": ["FirstName", "LastName"],
  "caseSensitive": false,
  "trim": true
}
```

### Case-Sensitive Matching

```json
{
  "matchingFields": ["ProductCode"],
  "caseSensitive": true,
  "trim": true
}
```

**Configuration Options:**

- `matchingFields`: Array of field names to use for matching (can be one or multiple)
- `caseSensitive`: Whether matching should be case-sensitive (default: `false`)
- `trim`: Whether to trim whitespace before matching (default: `true`)

## Command-Line Options

```
Required:
  --folderA, -a <path>      Path to folder A containing CSV files
  --folderB, -b <path>      Path to folder B containing CSV files
  --config, -c <path>       Path to JSON configuration file

Optional:
  --output, -o <path>       Output folder (default: 'Output')
  --parallelism, -p <num>   Degree of parallelism (default: CPU count)
  --delimiter, -d <char>    CSV delimiter (default: ',')
  --no-header               CSV files do not have header row
  --verbose, -v             Enable verbose logging
```

## Output Structure

After running a reconciliation, you'll find organized output in your specified output folder:

```
Output/
├── Customers/
│   ├── matched.csv              # Records found in both files
│   ├── only-in-folderA.csv      # Records only in Folder A
│   ├── only-in-folderB.csv      # Records only in Folder B
│   └── reconcile-summary.json   # Detailed statistics for this file
├── Orders/
│   ├── matched.csv
│   ├── only-in-folderA.csv
│   ├── only-in-folderB.csv
│   └── reconcile-summary.json
└── global-summary.json           # Overall statistics across all files
```

Each `reconcile-summary.json` contains:

- Total records in each file
- Number of matched records
- Number of records only in A
- Number of records only in B
- Processing time
- Any errors encountered

## Examples

### Example 1: Compare Customer Files

```bash
dotnet run -- \
  --folderA "Data/Source" \
  --folderB "Data/Target" \
  --config "Configs/composite-config.json" \
  --output "Results/Customers"
```

This will match customers based on FirstName + LastName.

### Example 2: Case-Sensitive Product Matching

```bash
dotnet run -- \
  -a "Products/Source" \
  -b "Products/Target" \
  -c "Configs/case-sensitive-config.json" \
  -o "Results/Products" \
  -v
```

The `-v` flag enables verbose logging for detailed information.

### Example 3: Custom Delimiter

If your CSV files use semicolons instead of commas:

```bash
dotnet run -- \
  --folderA "Data/A" \
  --folderB "Data/B" \
  --config "Configs/single-field-config.json" \
  --delimiter ";"
```

## How It Works

1. **File Discovery**: The tool finds all CSV files in both folders and pairs them by filename
2. **Dictionary Building**: For each file pair, it reads Folder A and builds a lookup dictionary using the matching key
3. **Matching**: It then reads Folder B and matches records against the dictionary
4. **Categorization**: Records are categorized as:
   - **Matched**: Found in both files
   - **Only in A**: Remaining records from Folder A after matching
   - **Only in B**: Records from Folder B that weren't found in A
5. **Output Generation**: Results are written to organized CSV files and JSON summaries

The algorithm uses a dictionary-based approach for O(n + m) time complexity, making it efficient even for large files.

## Project Structure

```
CsvReconcile/
├── Application/          # Core business logic
│   ├── FileProcessor.cs
│   ├── OutputGenerator.cs
│   └── ReconciliationEngine.cs
├── Cli/                  # User interfaces
│   ├── CommandLineInterface.cs
│   ├── ConsoleDisplay.cs
│   └── InteractiveMenu.cs
├── Configs/              # Example configuration files
│   ├── single-field-config.json
│   ├── composite-config.json
│   └── case-sensitive-config.json
├── Core/                 # Domain models and interfaces
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
├── Infrastructure/       # External dependencies
│   ├── Csv/
│   └── Logging/
└── TestData/             # Sample data for testing
```

## Logging

Logs are automatically saved to the output folder with timestamps. Each run creates a log file like:

```
reconciliation-20251127-09140120251127.log
```

Use the `--verbose` flag for more detailed logging during troubleshooting.

## Performance Tips

- **Parallelism**: Adjust `--parallelism` based on your CPU cores and I/O capacity
- **Large Files**: The tool uses streaming I/O, so it can handle large files efficiently
- **Memory**: For very large datasets, consider processing files individually rather than all at once

## Troubleshooting

**Problem**: "Configuration file not found"  
**Solution**: Make sure the config file path is correct and the JSON is valid

**Problem**: "FolderA does not exist"  
**Solution**: Check that your folder paths are correct (use absolute paths if needed)

**Problem**: No matches found  
**Solution**: Verify your matching fields exist in both CSV files and check case sensitivity settings

**Nazib Mahfuz**
