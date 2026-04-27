using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Excel;

namespace CogniLabel.Application.Pipeline;

public interface ITemplateLoader
{
    TemplateDefinition Load(string templatePath);
}

public interface IImageEnumerator
{
    IReadOnlyList<string> Enumerate(string imageFolderPath);
}

public interface ISingleImageProcessor
{
    ImageProcessResult ProcessSingleImage(string imagePath);
}

public interface ISingleImageProcessorFactory
{
    ISingleImageProcessor Create(TemplateDefinition template);
}

public sealed class DefaultSingleImageProcessorFactory : ISingleImageProcessorFactory
{
    private readonly Func<TemplateDefinition, ISingleImageProcessor> _factory;

    public DefaultSingleImageProcessorFactory(Func<TemplateDefinition, ISingleImageProcessor> factory)
    {
        _factory = factory;
    }

    public ISingleImageProcessor Create(TemplateDefinition template) => _factory(template);
}

