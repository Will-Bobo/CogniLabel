using CogniLabel.Infrastructure.Barcode;

namespace CogniLabel.Tests.Infrastructure;

public sealed class BarcodeReaderWrapperTests
{
    [Fact]
    public void Multi_code_should_return_raw_list_without_selection()
    {
        var sut = new BarcodeReaderWrapper(_ => new List<string> { "A", "B", "C" });
        var codes = sut.ReadAllCodes(image: new object());

        Assert.Equal(new[] { "A", "B", "C" }, codes);
    }

    [Fact]
    public void Empty_result_should_return_empty_list_not_null()
    {
        var sut = new BarcodeReaderWrapper(_ => new List<string>());
        var codes = sut.ReadAllCodes(image: new object());

        Assert.NotNull(codes);
        Assert.Empty(codes);
    }
}

