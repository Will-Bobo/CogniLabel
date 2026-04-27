using CogniLabel.Core.Engines;

namespace CogniLabel.Tests.Core;

public sealed class DuplicateEngineTests
{
    [Fact]
    public void No_duplicates_should_return_empty()
    {
        var d = DuplicateEngine.FindDuplicates(new[] { "A", "B", "C" });
        Assert.Empty(d);
    }

    [Fact]
    public void Has_duplicates_should_mark_duplicate_sn()
    {
        var d = DuplicateEngine.FindDuplicates(new[] { "A", "B", "A" });
        Assert.Contains("A", d);
        Assert.Single(d);
    }

    [Fact]
    public void Null_should_be_ignored()
    {
        var d = DuplicateEngine.FindDuplicates(new string?[] { null, "A", "A" });
        Assert.Contains("A", d);
        Assert.Single(d);
    }

    [Fact]
    public void Order_should_not_change_result()
    {
        var a = DuplicateEngine.FindDuplicates(new[] { "A", "B", "A" });
        var b = DuplicateEngine.FindDuplicates(new[] { "B", "A", "A" });
        Assert.Equal(a.OrderBy(x => x), b.OrderBy(x => x));
    }
}

