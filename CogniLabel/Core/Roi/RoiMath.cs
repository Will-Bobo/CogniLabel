namespace CogniLabel.Core.Roi;

public static class RoiMath
{
    public static RoiCalcResult ToClampedPixelRect(int imageWidth, int imageHeight, RelativeRoi roi)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return new RoiCalcResult(IsValid: false, Rect: default);

        var x1 = (int)Math.Floor(roi.X * imageWidth);
        var y1 = (int)Math.Floor(roi.Y * imageHeight);
        var x2 = (int)Math.Ceiling((roi.X + roi.W) * imageWidth);
        var y2 = (int)Math.Ceiling((roi.Y + roi.H) * imageHeight);

        x1 = Clamp(x1, 0, imageWidth);
        y1 = Clamp(y1, 0, imageHeight);
        x2 = Clamp(x2, 0, imageWidth);
        y2 = Clamp(y2, 0, imageHeight);

        var w = x2 - x1;
        var h = y2 - y1;
        if (w <= 0 || h <= 0)
            return new RoiCalcResult(IsValid: false, Rect: new PixelRect(x1, y1, 0, 0));

        return new RoiCalcResult(IsValid: true, Rect: new PixelRect(x1, y1, w, h));
    }

    private static int Clamp(int v, int min, int max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}

