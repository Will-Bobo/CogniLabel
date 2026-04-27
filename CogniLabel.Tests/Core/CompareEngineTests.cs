using CogniLabel.Core.Engines;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Tests.Core;

public sealed class CompareEngineTests
{
    [Fact]
    public void Fully_equal_should_pass()
    {
        var r = CompareEngine.CompareOne("F", "ABC", "ABC");
        Assert.True(r.IsPass);
        Assert.Null(r.ErrorType);
    }

    [Fact]
    public void Trim_equal_should_pass()
    {
        var r = CompareEngine.CompareOne("F", " ABC ", "ABC");
        Assert.True(r.IsPass);
        Assert.Null(r.ErrorType);
    }

    [Fact]
    public void Mismatch_should_return_mismatch_error()
    {
        var r = CompareEngine.CompareOne("F", "ABC", "ABD");
        Assert.False(r.IsPass);
        Assert.Equal(ErrorType.Mismatch, r.ErrorType);
    }

    [Fact]
    public void Field_unreadable_should_return_unreadable_error()
    {
        var r = CompareEngine.CompareOne("F", null, "ABC");
        Assert.False(r.IsPass);
        Assert.Equal(ErrorType.Unreadable, r.ErrorType);
    }
}

