using CsvReconcile.Core.Models;
using System.Text.Json;

namespace CsvReconcileTest.Core.Models;

public class ConfigurationTests
{
    [Fact]
    public void MatchingRule_FromSingleFieldJson_DeserializesCorrectly()
    {
        // Arrange
        var json = """
            {
              "matchingFields": ["InvoiceId"],
              "caseSensitive": false,
              "trim": true
            }
            """;
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var matchingRule = JsonSerializer.Deserialize<MatchingRule>(json, jsonOptions);

        // Assert
        Assert.NotNull(matchingRule);
        Assert.Single(matchingRule.MatchingFields);
        Assert.Equal("InvoiceId", matchingRule.MatchingFields[0]);
        Assert.False(matchingRule.CaseSensitive);
        Assert.True(matchingRule.Trim);
    }

    [Fact]
    public void MatchingRule_FromCompositeFieldJson_DeserializesCorrectly()
    {
        // Arrange
        var json = """
            {
              "matchingFields": ["FirstName", "LastName"],
              "caseSensitive": false,
              "trim": true
            }
            """;
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var matchingRule = JsonSerializer.Deserialize<MatchingRule>(json, jsonOptions);

        // Assert
        Assert.NotNull(matchingRule);
        Assert.Equal(2, matchingRule.MatchingFields.Count);
        Assert.Equal("FirstName", matchingRule.MatchingFields[0]);
        Assert.Equal("LastName", matchingRule.MatchingFields[1]);
        Assert.False(matchingRule.CaseSensitive);
        Assert.True(matchingRule.Trim);
    }

    [Fact]
    public void MatchingRule_Validate_WithEmptyFields_ThrowsException()
    {
        // Arrange
        var matchingRule = new MatchingRule
        {
            MatchingFields = new List<string>(),
            CaseSensitive = false,
            Trim = true
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => matchingRule.Validate());
    }

    [Fact]
    public void MatchingRule_Validate_WithValidFields_Passes()
    {
        // Arrange
        var matchingRule = new MatchingRule
        {
            MatchingFields = new List<string> { "Id" },
            CaseSensitive = false,
            Trim = true
        };

        // Act
        var exception = Record.Exception(() => matchingRule.Validate());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void MatchingRule_FromJsonFile_WithCaseInsensitiveProperties_DeserializesCorrectly()
    {
        // Arrange
        var json = """
            {
              "matchingfields": ["OrderId"],
              "casesensitive": true,
              "TRIM": false
            }
            """;
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var matchingRule = JsonSerializer.Deserialize<MatchingRule>(json, jsonOptions);

        // Assert
        Assert.NotNull(matchingRule);
        Assert.Single(matchingRule.MatchingFields);
        Assert.Equal("OrderId", matchingRule.MatchingFields[0]);
        Assert.True(matchingRule.CaseSensitive);
        Assert.False(matchingRule.Trim);
    }
}


