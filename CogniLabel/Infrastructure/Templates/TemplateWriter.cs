using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using System.IO;
using System.Text.Json;

namespace CogniLabel.Infrastructure.Templates;

public sealed class TemplateWriter : ITemplateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Save(string templatePath, TemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("模板路径为空", nameof(templatePath));

        ArgumentNullException.ThrowIfNull(template);

        var doc = new TemplateDocument
        {
            Fields = template.Fields.Select(f => new TemplateFieldRecord
            {
                Name = f.Name,
                X = f.Roi.X,
                Y = f.Roi.Y,
                W = f.Roi.W,
                H = f.Roi.H,
                IsSn = f.IsSn,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(doc, JsonOptions);
        File.WriteAllText(templatePath, json);
    }

    private sealed class TemplateDocument
    {
        public List<TemplateFieldRecord>? Fields { get; set; }
    }

    private sealed class TemplateFieldRecord
    {
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public bool IsSn { get; set; }
    }
}
