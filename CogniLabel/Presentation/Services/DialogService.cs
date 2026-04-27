using Microsoft.Win32;

namespace CogniLabel.Presentation.Services;

public interface IDialogService
{
    string? PickExcelFile();
    string? PickTemplateFile();
    string? PickSaveTemplateFile();
    string? PickImageFolder();
}

public sealed class DialogService : IDialogService
{
    public string? PickExcelFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            CheckFileExists = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickTemplateFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "模板 (*.json)|*.json",
            CheckFileExists = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickSaveTemplateFile()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "模板 (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickImageFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog();
        return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
    }
}

