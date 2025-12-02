using CsvReconcile.Core.Models;
using CsvReconcile.Core.Services;

namespace CsvReconcileTest.Core.Services;

public class RecordMatcherTests
{
    [Fact]
    public void GenerateKey_WithSingleField_ReturnsNormalizedKey()
    {
        // Arrange
        var matcher = new RecordMatcher();
        var record = new CsvRecord();
        record.SetField("Name", "John Doe");
        var rule = new MatchingRule 
        { 
            MatchingFields = new List<string> { "Name" },
            CaseSensitive = false,
            Trim = true
        };

        // Act
        var key = matcher.GenerateKey(record, rule);

        // Assert
        Assert.Equal("john doe", key);
    }
}


