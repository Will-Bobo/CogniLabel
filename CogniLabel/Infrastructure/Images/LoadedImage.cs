using System.Windows.Media.Imaging;

namespace CogniLabel.Infrastructure.Images;

public sealed class LoadedImage : IDisposable
{
    private LoadedImage(BitmapSource source)
    {
        Source = source;
    }

    public BitmapSource Source { get; }
    public int Width => Source.PixelWidth;
    public int Height => Source.PixelHeight;

    public void Dispose()
    {
        // BitmapSource is managed; nothing to dispose.
    }

    public static LoadedImage FromFake(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var src = BitmapSource.Create(
            pixelWidth: width,
            pixelHeight: height,
            dpiX: 96,
            dpiY: 96,
            pixelFormat: System.Windows.Media.PixelFormats.Bgra32,
            palette: null,
            pixels: pixels,
            stride: width * 4);
        return new LoadedImage(src);
    }

    internal static LoadedImage FromSource(BitmapSource source) => new(source);
}

