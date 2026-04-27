using CogniLabel.Application.SingleImage;

namespace CogniLabel.Application.Pipeline;

public interface ITemplateWriter
{
    void Save(string templatePath, TemplateDefinition template);
}
