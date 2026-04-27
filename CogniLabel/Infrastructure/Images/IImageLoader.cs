namespace CogniLabel.Infrastructure.Images;

public interface IImageLoader
{
    LoadedImage Load(string path);
}

