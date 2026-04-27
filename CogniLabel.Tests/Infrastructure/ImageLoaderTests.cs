using CogniLabel.Infrastructure.Images;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CogniLabel.Tests.Infrastructure;

public sealed class ImageLoaderTests
{
    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.png")]
    [InlineData("test.bmp")]
    public void Image_loader_should_load_and_return_dimensions(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        var filePath = Path.Combine(path, fileName);
        SaveTestImage(filePath, width: 10, height: 20);

        var loader = new ImageLoader();
        using var loaded = loader.Load(filePath);

        Assert.Equal(10, loaded.Width);
        Assert.Equal(20, loaded.Height);
    }

    [Fact]
    public void Image_loader_file_not_found_should_throw()
    {
        var loader = new ImageLoader();
        Assert.Throws<FileNotFoundException>(() => loader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png")));
    }

    private static void SaveTestImage(string filePath, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bmp = BitmapSource.Create(
            pixelWidth: width,
            pixelHeight: height,
            dpiX: 96,
            dpiY: 96,
            pixelFormat: PixelFormats.Bgra32,
            palette: null,
            pixels: pixels,
            stride: width * 4);

        BitmapEncoder encoder = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder(),
        };

        encoder.Frames.Add(BitmapFrame.Create(bmp));

        using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(fs);
    }
}

