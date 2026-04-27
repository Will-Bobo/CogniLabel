using CogniLabel.Core.Roi;

namespace CogniLabel.Tests.Core;

public sealed class RoiMathTests
{
    [Fact]
    public void Roi_out_of_bounds_should_be_clamped_and_truncated()
    {
        var roi = new RelativeRoi(X: 0.9, Y: 0.9, W: 0.5, H: 0.5);
        var result = RoiMath.ToClampedPixelRect(imageWidth: 1000, imageHeight: 1000, roi);

        Assert.True(result.IsValid);
        Assert.True(result.Rect.X >= 0);
        Assert.True(result.Rect.Y >= 0);
        Assert.True(result.Rect.Width > 0);
        Assert.True(result.Rect.Height > 0);
        Assert.True(result.Rect.X + result.Rect.Width <= 1000);
        Assert.True(result.Rect.Y + result.Rect.Height <= 1000);

        // Must be truncated because w/h exceed boundary
        Assert.True(result.Rect.Width < 500);
        Assert.True(result.Rect.Height < 500);
    }

    [Theory]
    [InlineData(0.1, 0.1, 0.0, 0.2)]
    [InlineData(0.1, 0.1, 0.2, 0.0)]
    public void Roi_zero_width_or_height_should_be_invalid(double x, double y, double w, double h)
    {
        var roi = new RelativeRoi(x, y, w, h);
        var result = RoiMath.ToClampedPixelRect(1000, 1000, roi);

        Assert.False(result.IsValid);
    }
}

