namespace CogniLabel.Presentation.Services;

public interface ITemplateEditorDialogService
{
    /// <summary>返回用户保存后的路径；取消则为 null。</summary>
    string? ShowCreate();

    /// <summary>加载指定路径的模板进行编辑；返回保存后的路径；取消则为 null。</summary>
    string? ShowEdit(string templatePathToLoad);
}
