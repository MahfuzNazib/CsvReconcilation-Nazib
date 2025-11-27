using CsvReconcile.Core.Interfaces;
using CsvReconcile.Core.Models;
using System.Text;

namespace CsvReconcile.Core.Services;

/// <summary>
/// Implementation of record matching logic
/// </summary>
public class RecordMatcher : IRecordMatcher
{
  /// <summary>
  /// Generates a composite key from a record based on matching fields
  /// </summary>
  public string GenerateKey(CsvRecord record, MatchingRule matchingRule)
  {
    if (record == null)
      throw new ArgumentNullException(nameof(record));

    if (matchingRule == null)
      throw new ArgumentNullException(nameof(matchingRule));

    if (matchingRule.MatchingFields == null || matchingRule.MatchingFields.Count == 0)
      throw new InvalidOperationException("MatchingFields cannot be empty.");

    // For single field, return normalized value directly
    if (matchingRule.MatchingFields.Count == 1)
    {
      var fieldName = matchingRule.MatchingFields[0];
      var value = record.GetField(fieldName);
      return NormalizeValue(value, matchingRule);
    }

    // For composite keys, join normalized values with a delimiter
    // Using pipe (|) as delimiter to minimize collision risk
    var keyBuilder = new StringBuilder();
    for (int i = 0; i < matchingRule.MatchingFields.Count; i++)
    {
      if (i > 0)
        keyBuilder.Append('|');

      var fieldName = matchingRule.MatchingFields[i];
      var value = record.GetField(fieldName);
      keyBuilder.Append(NormalizeValue(value, matchingRule));
    }

    return keyBuilder.ToString();
  }

  /// <summary>
  /// Normalizes a field value based on matching rules
  /// </summary>
  public string NormalizeValue(string value, MatchingRule matchingRule)
  {
    if (value == null)
      return string.Empty;

    // Apply trimming if enabled
    if (matchingRule.Trim)
    {
      value = value.Trim();
    }

    // Apply case normalization if not case-sensitive
    if (!matchingRule.CaseSensitive)
    {
      value = value.ToLowerInvariant();
    }

    return value;
  }
}

