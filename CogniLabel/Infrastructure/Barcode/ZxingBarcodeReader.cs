using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace CogniLabel.Infrastructure.Barcode;

public sealed class ZxingBarcodeReader : IBarcodeReader
{
    private readonly BarcodeReader _reader;

    public ZxingBarcodeReader()
    {
        _reader = new BarcodeReader
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                TryHarder = false,
                ReturnCodabarStartEnd = false,
            },
        };
    }

    public IReadOnlyList<string> ReadAllCodes(object image)
    {
        if (image is not BitmapSource bmp)
            return Array.Empty<string>();

        var results = _reader.DecodeMultiple(bmp);
        if (results is null || results.Length == 0)
            return Array.Empty<string>();

        return results.Select(r => r.Text ?? string.Empty).ToList();
    }
}

