using System.IO;
using System.Windows.Media.Imaging;

namespace CogniLabel.Infrastructure.Images;

public sealed class ImageLoader : IImageLoader
{
    public LoadedImage Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("图片文件不存在", path);

        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return LoadedImage.FromSource(frame);
    }
}

