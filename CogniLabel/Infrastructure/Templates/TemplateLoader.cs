using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Core.Roi;
using System.Text.Json;
using System.IO;

namespace CogniLabel.Infrastructure.Templates;

public sealed class TemplateLoader : ITemplateLoader
{
    public TemplateDefinition Load(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("模板路径为空", nameof(templatePath));

        var json = File.ReadAllText(templatePath);
        var doc = JsonSerializer.Deserialize<TemplateDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("模板解析失败");

        var fields = doc.Fields?.Select(f =>
        {
            var isSn = f.IsSn ?? string.Equals(f.Name, "SN", StringComparison.OrdinalIgnoreCase);
            return new TemplateFieldDefinition(
                name: f.Name ?? string.Empty,
                roi: new RelativeRoi(f.X, f.Y, f.W, f.H),
                isSn: isSn);
        }).ToList()
            ?? new List<TemplateFieldDefinition>();

        return new TemplateDefinition(fields);
    }

    private sealed class TemplateDocument
    {
        public List<TemplateField>? Fields { get; set; }
    }

    private sealed class TemplateField
    {
        public string? Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public string? Type { get; set; }
        public bool? IsSn { get; set; }
    }
}

