using CogniLabel.Application.SingleImage;
using CogniLabel.Core.Roi;
using CogniLabel.Infrastructure.Templates;
using System.IO;

namespace CogniLabel.Tests.Infrastructure;

public sealed class TemplateWriterTests
{
    [Fact]
    public void Roundtrip_serializes_and_loader_restores_definition()
    {
        var original = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition("SN", new RelativeRoi(0.1, 0.2, 0.3, 0.4), isSn: true),
            new TemplateFieldDefinition("LOT", new RelativeRoi(0, 0, 1, 1), isSn: false),
        });

        var path = Path.GetTempFileName();
        try
        {
            var writer = new TemplateWriter();
            writer.Save(path, original);

            var loader = new TemplateLoader();
            var loaded = loader.Load(path);

            Assert.Equal(original.Fields.Count, loaded.Fields.Count);
            for (var i = 0; i < original.Fields.Count; i++)
            {
                Assert.Equal(original.Fields[i].Name, loaded.Fields[i].Name);
                Assert.Equal(original.Fields[i].IsSn, loaded.Fields[i].IsSn);
                Assert.Equal(original.Fields[i].Roi.X, loaded.Fields[i].Roi.X);
                Assert.Equal(original.Fields[i].Roi.Y, loaded.Fields[i].Roi.Y);
                Assert.Equal(original.Fields[i].Roi.W, loaded.Fields[i].Roi.W);
                Assert.Equal(original.Fields[i].Roi.H, loaded.Fields[i].Roi.H);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_prefers_explicit_isSn_in_json_over_name_inference()
    {
        var json = """
            {
              "fields": [
                { "name": "Code", "x": 0, "y": 0, "w": 1, "h": 1, "isSn": true }
              ]
            }
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var loader = new TemplateLoader();
            var loaded = loader.Load(path);
            Assert.Single(loaded.Fields);
            Assert.True(loaded.Fields[0].IsSn);
            Assert.Equal("Code", loaded.Fields[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
