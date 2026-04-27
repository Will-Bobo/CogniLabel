using CogniLabel.Application.Pipeline;
using CogniLabel.Application;
using CogniLabel.Presentation.ViewModels;
using CogniLabel.Presentation.Views;

namespace CogniLabel.Presentation.Services;

public sealed class TemplateEditorDialogService : ITemplateEditorDialogService
{
    private readonly ITemplateLoader _loader;
    private readonly ITemplateWriter _writer;
    private readonly IDialogService _dialogs;
    private readonly RoiStateService _roiState;

    public TemplateEditorDialogService(ITemplateLoader loader, ITemplateWriter writer, IDialogService dialogs)
    {
        _loader = loader;
        _writer = writer;
        _dialogs = dialogs;
        _roiState = new RoiStateService();
    }

    public string? ShowCreate()
    {
        var vm = new TemplateEditorViewModel(_loader, _writer, _dialogs, _roiState, TemplateEditorMode.Create);
        return ShowInternal(vm);
    }

    public string? ShowEdit(string templatePathToLoad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePathToLoad);
        var vm = new TemplateEditorViewModel(_loader, _writer, _dialogs, _roiState, TemplateEditorMode.Edit, templatePathToLoad);
        return ShowInternal(vm);
    }

    private static string? ShowInternal(TemplateEditorViewModel vm)
    {
        var w = new TemplateEditorWindow(vm);
        if (System.Windows.Application.Current.MainWindow is { } owner)
            w.Owner = owner;

        var ok = w.ShowDialog() == true;
        return ok ? vm.SavedTemplatePath : null;
    }
}
