namespace CsvReconcile.Core.Models;

/// <summary>
/// Defines the matching rule for comparing CSV records between two files.
/// </summary>
public class MatchingRule
{
    public List<string> MatchingFields { get; set; } = new();

    public bool CaseSensitive { get; set; } = false;

    public bool Trim { get; set; } = true;

    public void Validate()
    {
        if (MatchingFields == null || MatchingFields.Count == 0)
        {
            throw new InvalidOperationException("MatchingFields must contain at least one field name.");
        }

        if (MatchingFields.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("MatchingFields cannot contain null or empty field names.");
        }
    }
}

