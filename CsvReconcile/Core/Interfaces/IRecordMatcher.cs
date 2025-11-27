using CsvReconcile.Core.Models;

namespace CsvReconcile.Core.Interfaces;

/// <summary>
/// Service for matching records based on matching rules
/// </summary>
public interface IRecordMatcher
{
    string GenerateKey(CsvRecord record, MatchingRule matchingRule);

    string NormalizeValue(string value, MatchingRule matchingRule);
}

