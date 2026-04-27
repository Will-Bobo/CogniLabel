using System.Windows;
using Xunit;

namespace CogniLabel.Tests.Presentation;

public sealed class ImageDisplayRectCalcTests
{
    [Fact]
    public void Uniform_should_center_letterbox_vertically()
    {
        // control 400x400, image 800x400 => scale=0.5 => display 400x200, y offset 100
        var r = Invoke(400, 400, 800, 400);
        Assert.Equal(0, r.X, 6);
        Assert.Equal(100, r.Y, 6);
        Assert.Equal(400, r.Width, 6);
        Assert.Equal(200, r.Height, 6);
    }

    [Fact]
    public void Uniform_should_center_letterbox_horizontally()
    {
        // control 400x400, image 400x800 => scale=0.5 => display 200x400, x offset 100
        var r = Invoke(400, 400, 400, 800);
        Assert.Equal(100, r.X, 6);
        Assert.Equal(0, r.Y, 6);
        Assert.Equal(200, r.Width, 6);
        Assert.Equal(400, r.Height, 6);
    }

    private static Rect Invoke(double cw, double ch, double pw, double ph)
    {
        // keep in sync with TemplateEditorWindow.GetImageDisplayRect
        var scale = Math.Min(cw / pw, ch / ph);
        var w = pw * scale;
        var h = ph * scale;
        var x = (cw - w) / 2;
        var y = (ch - h) / 2;
        return new Rect(x, y, w, h);
    }
}

