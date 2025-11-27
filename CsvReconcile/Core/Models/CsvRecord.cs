namespace CsvReconcile.Core.Models;

/// <summary>
/// Represents a single CSV record with dynamic fields
/// </summary>
public class CsvRecord
{
  public Dictionary<string, string> Fields { get; set; } = new();

  public string SourceFile { get; set; } = string.Empty;

  public int LineNumber { get; set; }

  public string GetField(string fieldName)
  {
    return Fields.TryGetValue(fieldName, out var value) ? value : string.Empty;
  }

  public void SetField(string fieldName, string value)
  {
    Fields[fieldName] = value;
  }

  public CsvRecord Clone()
  {
    return new CsvRecord
    {
      Fields = new Dictionary<string, string>(Fields),
      SourceFile = SourceFile,
      LineNumber = LineNumber
    };
  }
}

