using CogniLabel.Core.Engines;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Tests.Core;

public sealed class ErrorClassifierTests
{
    [Fact]
    public void Priority_case1_sn_unreadable_should_return_image_unreadable_and_stop()
    {
        var match = MatchOutcome.Skipped();
        var fields = new[] { FieldCompareResult.Fail("SN", ErrorType.Unreadable) };
        var eval = ErrorClassifier.Evaluate(
            isImageUnreadable: true,
            match: match,
            fieldResults: fields,
            duplicateSns: new HashSet<string>(),
            sn: null);

        Assert.False(eval.IsPass);
        Assert.Equal(ErrorType.Unreadable, eval.Error);
    }

    [Fact]
    public void Priority_case2_not_found_should_return_not_found_and_not_require_compare()
    {
        var match = MatchOutcome.Fail(ErrorType.NotFound);
        var eval = ErrorClassifier.Evaluate(
            isImageUnreadable: false,
            match: match,
            fieldResults: Array.Empty<FieldCompareResult>(),
            duplicateSns: new HashSet<string>(),
            sn: "A");

        Assert.False(eval.IsPass);
        Assert.Equal(ErrorType.NotFound, eval.Error);
    }

    [Fact]
    public void Priority_case3_duplicate_should_return_duplicate()
    {
        var match = MatchOutcome.Success(new Dictionary<string, string> { ["SN"] = "A" });
        var eval = ErrorClassifier.Evaluate(
            isImageUnreadable: false,
            match: match,
            fieldResults: Array.Empty<FieldCompareResult>(),
            duplicateSns: new HashSet<string> { "A" },
            sn: "A");

        Assert.False(eval.IsPass);
        Assert.Equal(ErrorType.Duplicate, eval.Error);
    }

    [Fact]
    public void Priority_case4_mismatch_should_return_mismatch()
    {
        var match = MatchOutcome.Success(new Dictionary<string, string> { ["SN"] = "A" });
        var fields = new[] { FieldCompareResult.Fail("X", ErrorType.Mismatch) };
        var eval = ErrorClassifier.Evaluate(
            isImageUnreadable: false,
            match: match,
            fieldResults: fields,
            duplicateSns: new HashSet<string>(),
            sn: "A");

        Assert.False(eval.IsPass);
        Assert.Equal(ErrorType.Mismatch, eval.Error);
    }

    [Fact]
    public void Priority_case5_field_unreadable_should_fail_with_unreadable()
    {
        var match = MatchOutcome.Success(new Dictionary<string, string> { ["SN"] = "A" });
        var fields = new[] { FieldCompareResult.Fail("QR1", ErrorType.Unreadable) };
        var eval = ErrorClassifier.Evaluate(
            isImageUnreadable: false,
            match: match,
            fieldResults: fields,
            duplicateSns: new HashSet<string>(),
            sn: "A");

        Assert.False(eval.IsPass);
        Assert.Equal(ErrorType.Unreadable, eval.Error);
    }
}

