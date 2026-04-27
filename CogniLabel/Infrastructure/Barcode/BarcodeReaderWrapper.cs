namespace CogniLabel.Infrastructure.Barcode;

public sealed class BarcodeReaderWrapper : IBarcodeReader
{
    private readonly Func<object, List<string>> _decode;

    public BarcodeReaderWrapper(Func<object, List<string>> decode)
    {
        _decode = decode;
    }

    public IReadOnlyList<string> ReadAllCodes(object image)
    {
        var codes = _decode(image) ?? new List<string>();
        return codes;
    }
}

