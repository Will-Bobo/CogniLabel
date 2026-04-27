namespace CogniLabel.Infrastructure.Barcode;

public interface IBarcodeReader
{
    IReadOnlyList<string> ReadAllCodes(object image);
}

