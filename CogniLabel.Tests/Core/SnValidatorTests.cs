using CogniLabel.Core.Validation;

namespace CogniLabel.Tests.Core;

public sealed class SnValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sn_non_empty_validation_should_fail_for_null_or_whitespace(string? sn)
    {
        var result = SnValidator.Validate(new[] { sn });
        Assert.True(result.HasEmpty);
    }

    [Fact]
    public void Sn_uniqueness_validation_should_detect_duplicates()
    {
        var result = SnValidator.Validate(new[] { "A", "A" });
        Assert.Contains("A", result.Duplicates);
    }

    [Fact]
    public void Trim_rule_should_apply_before_validation()
    {
        var result = SnValidator.Validate(new[] { "  ABC  ", "ABC" });
        Assert.Contains("ABC", result.Duplicates);
    }
}

