namespace CogniLabel.Infrastructure.IO;

public sealed class ImageEnumerator : CogniLabel.Application.Pipeline.IImageEnumerator
{
    public IReadOnlyList<string> Enumerate(string imageFolderPath)
    {
        if (string.IsNullOrWhiteSpace(imageFolderPath))
            throw new ArgumentException("图片目录为空", nameof(imageFolderPath));

        if (!System.IO.Directory.Exists(imageFolderPath))
            throw new System.IO.DirectoryNotFoundException(imageFolderPath);

        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

        return System.IO.Directory.EnumerateFiles(imageFolderPath, "*.*", System.IO.SearchOption.TopDirectoryOnly)
            .Where(p => exts.Contains(System.IO.Path.GetExtension(p)))
            .ToList();
    }
}

