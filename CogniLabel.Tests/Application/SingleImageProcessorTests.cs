using CogniLabel.Application.SingleImage;
using CogniLabel.Core.Roi;
using CogniLabel.Infrastructure.Barcode;
using CogniLabel.Infrastructure.Images;

namespace CogniLabel.Tests.Application;

public sealed class SingleImageProcessorTests
{
    [Fact]
    public void Multi_code_strategy_should_take_first_value()
    {
        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition(name: "SN", roi: new RelativeRoi(0,0,1,1), isSn: true),
        });

        var loader = new FakeImageLoader();
        var barcode = new BarcodeReaderWrapper(_ => new List<string> { "A", "B" });
        var sut = new SingleImageProcessor(template, loader, barcode);

        var result = sut.ProcessSingleImage("c:\\fake\\img.png");

        Assert.Equal("c:\\fake\\img.png", result.ImagePath);
        Assert.Equal("A", result.Fields["SN"]);
        Assert.False(result.IsUnreadable);
    }

    [Fact]
    public void No_recognition_result_should_set_field_to_null()
    {
        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition(name: "QR1", roi: new RelativeRoi(0,0,1,1), isSn: false),
        });

        var sut = new SingleImageProcessor(template, new FakeImageLoader(), new BarcodeReaderWrapper(_ => new List<string>()));
        var result = sut.ProcessSingleImage("c:\\fake\\img.png");

        Assert.Null(result.Fields["QR1"]);
        Assert.False(result.IsUnreadable);
    }

    [Fact]
    public void Sn_not_recognized_should_mark_image_unreadable()
    {
        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition(name: "SN", roi: new RelativeRoi(0,0,1,1), isSn: true),
        });

        var sut = new SingleImageProcessor(template, new FakeImageLoader(), new BarcodeReaderWrapper(_ => new List<string>()));
        var result = sut.ProcessSingleImage("c:\\fake\\img.png");

        Assert.True(result.IsUnreadable);
        Assert.Null(result.Fields["SN"]);
    }

    [Fact]
    public void Exceptions_should_be_isolated_and_return_unreadable_without_throwing()
    {
        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition(name: "SN", roi: new RelativeRoi(0,0,1,1), isSn: true),
        });

        var sut = new SingleImageProcessor(template, new FakeImageLoader(), new BarcodeReaderWrapper(_ => throw new InvalidOperationException("boom")));

        var result = sut.ProcessSingleImage("c:\\fake\\img.png");

        Assert.True(result.IsUnreadable);
        Assert.Null(result.Fields["SN"]);
    }

    [Fact]
    public void Partial_field_failure_should_not_make_whole_image_unreadable_when_sn_success()
    {
        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition(name: "SN", roi: new RelativeRoi(0,0,1,1), isSn: true),
            new TemplateFieldDefinition(name: "QR1", roi: new RelativeRoi(0,0,0,1), isSn: false), // invalid roi -> field unreadable
        });

        var barcode = new BarcodeReaderWrapper(_ => new List<string> { "OK" });
        var sut = new SingleImageProcessor(template, new FakeImageLoader(), barcode);
        var result = sut.ProcessSingleImage("c:\\fake\\img.png");

        Assert.False(result.IsUnreadable);
        Assert.Equal("OK", result.Fields["SN"]);
        Assert.Null(result.Fields["QR1"]);
    }

    private sealed class FakeImageLoader : IImageLoader
    {
        public LoadedImage Load(string path)
        {
            return LoadedImage.FromFake(width: 1000, height: 1000);
        }
    }
}

