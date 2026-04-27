using CogniLabel.Core.Engines;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Tests.Core;

public sealed class MatchEngineTests
{
    [Fact]
    public void Normal_match_should_return_row()
    {
        var excel = new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A", ["X"] = "1" },
        };

        var outcome = MatchEngine.MatchBySn("A", excel, "SN");

        Assert.True(outcome.IsMatched);
        Assert.NotNull(outcome.Row);
        Assert.Equal("1", outcome.Row!["X"]);
        Assert.Equal(ErrorType.None, outcome.Error);
    }

    [Fact]
    public void Not_found_should_return_not_found_error()
    {
        var excel = new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "B" },
        };

        var outcome = MatchEngine.MatchBySn("A", excel, "SN");

        Assert.False(outcome.IsMatched);
        Assert.Equal(ErrorType.NotFound, outcome.Error);
    }

    [Fact]
    public void Multi_row_match_should_return_duplicate_error()
    {
        var excel = new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "A" },
        };

        var outcome = MatchEngine.MatchBySn("A", excel, "SN");

        Assert.False(outcome.IsMatched);
        Assert.Equal(ErrorType.Duplicate, outcome.Error);
    }

    [Fact]
    public void Sn_null_should_be_skipped_and_not_produce_match_error()
    {
        var excel = new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
        };

        var outcome = MatchEngine.MatchBySn(null, excel, "SN");

        Assert.True(outcome.IsSkipped);
        Assert.Equal(ErrorType.None, outcome.Error);
    }
}

