using CogniLabel.Application.Pipeline;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Presentation.ViewModels;

public sealed class AuditItemViewModel
{
    private readonly AuditItem _item;

    public AuditItemViewModel(AuditItem item)
    {
        _item = item;
    }

    public string ImageName => _item.Image.ImageName;
    public string ImagePath => _item.Image.ImagePath;
    public string? SN => _item.Image.Fields.TryGetValue("SN", out var sn) ? sn : null;
    public bool IsPass => _item.IsPass;
    public string Status => _item.IsPass ? "PASS" : "FAIL";
    public ErrorType ErrorType => _item.ErrorType;
    public string ErrorTypeText => ErrorTypeDisplay.GetErrorDisplay(_item.ErrorType);
}

