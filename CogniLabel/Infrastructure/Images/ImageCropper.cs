using CogniLabel.Core.Roi;
using System.Windows.Media.Imaging;

namespace CogniLabel.Infrastructure.Images;

public static class ImageCropper
{
    public static BitmapSource Crop(LoadedImage image, PixelRect rect)
    {
        var src = image.Source;
        var cropped = new CroppedBitmap(src, new System.Windows.Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));
        cropped.Freeze();
        return cropped;
    }
}

