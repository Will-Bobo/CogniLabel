using CogniLabel.Core.Roi;
using CogniLabel.Infrastructure.Barcode;
using CogniLabel.Infrastructure.Images;
using System.IO;

namespace CogniLabel.Application.SingleImage;

public sealed class SingleImageProcessor
{
    private readonly TemplateDefinition _template;
    private readonly IImageLoader _imageLoader;
    private readonly IBarcodeReader _barcodeReader;

    public SingleImageProcessor(TemplateDefinition template, IImageLoader imageLoader, IBarcodeReader barcodeReader)
    {
        _template = template;
        _imageLoader = imageLoader;
        _barcodeReader = barcodeReader;
    }

    public ImageProcessResult ProcessSingleImage(string imagePath)
    {
        var imageName = Path.GetFileName(imagePath);
        var fields = _template.Fields.ToDictionary(f => f.Name, _ => (string?)null, StringComparer.Ordinal);
        var rawValues = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        try
        {
            using var img = _imageLoader.Load(imagePath);

            foreach (var field in _template.Fields)
            {
                var roi = RoiMath.ToClampedPixelRect(img.Width, img.Height, field.Roi);
                if (!roi.IsValid)
                {
                    fields[field.Name] = null;
                    continue;
                }

                var cropped = ImageCropper.Crop(img, roi.Rect);
                var codes = _barcodeReader.ReadAllCodes(cropped) ?? Array.Empty<string>();
                rawValues[field.Name] = codes.ToList();

                var first = codes.FirstOrDefault();
                fields[field.Name] = string.IsNullOrEmpty(first) ? null : first;
            }

            var isUnreadable = _template.Fields.Any(f => f.IsSn && fields[f.Name] is null);
            return new ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = imageName,
                Fields = fields,
                RawValues = rawValues,
                IsUnreadable = isUnreadable,
            };
        }
        catch
        {
            // 异常隔离：任何异常都不允许向上抛出，统一转为 UNREADABLE
            return new ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = imageName,
                Fields = fields,
                RawValues = rawValues,
                IsUnreadable = true,
            };
        }
    }
}

