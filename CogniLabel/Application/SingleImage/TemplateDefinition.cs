using CogniLabel.Core.Roi;

namespace CogniLabel.Application.SingleImage;

public sealed class TemplateDefinition
{
    public TemplateDefinition(IReadOnlyList<TemplateFieldDefinition> fields)
    {
        Fields = fields;
    }

    public IReadOnlyList<TemplateFieldDefinition> Fields { get; }
}

public sealed class TemplateFieldDefinition
{
    public TemplateFieldDefinition(string name, RelativeRoi roi, bool isSn)
    {
        Name = name;
        Roi = roi;
        IsSn = isSn;
    }

    public string Name { get; }
    public RelativeRoi Roi { get; }
    public bool IsSn { get; }
}

