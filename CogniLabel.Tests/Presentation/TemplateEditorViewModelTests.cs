using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Application;
using CogniLabel.Core.Roi;
using CogniLabel.Presentation.Services;
using CogniLabel.Presentation.ViewModels;

namespace CogniLabel.Tests.Presentation;

public sealed class TemplateEditorViewModelTests
{
    [Fact]
    public void Create_mode_default_row_passes_validation()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        Assert.True(vm.ValidateForTest());
        Assert.True(vm.TryBuildDefinitionForTest(out var def));
        Assert.NotNull(def);
        Assert.Single(def!.Fields);
        Assert.True(def.Fields[0].IsSn);
        Assert.Equal("SN", def.Fields[0].Name);
    }

    [Fact]
    public void Create_mode_should_select_first_row_by_default()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        Assert.NotNull(vm.SelectedField);
        Assert.Same(vm.Fields[0], vm.SelectedField);
    }

    [Fact]
    public void Roi_out_of_range_fails_validation()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        vm.RoiState.SetRoi(vm.Fields[0].RowId, new RelativeRoi(0, 0, 1.01, 0), RoiWriteSource.TestInject);
        // RoiStateService 会强制 clamp，非法值不会进入 VM（Single Source of Truth）
        Assert.True(vm.ValidateForTest());
        Assert.Equal(1.0, vm.Fields[0].RoiW, 6);
    }

    [Fact]
    public void Drag_pixels_should_convert_to_relative_roi_on_selected_field()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        vm.SelectedField = vm.Fields[0];

        vm.ApplyDragToSelectedPixels(startX: 20, startY: 10, endX: 120, endY: 60, canvasWidth: 200, canvasHeight: 100);

        Assert.Equal(0.1, vm.Fields[0].RoiX, 6);
        Assert.Equal(0.1, vm.Fields[0].RoiY, 6);
        Assert.Equal(0.5, vm.Fields[0].RoiW, 6);
        Assert.Equal(0.5, vm.Fields[0].RoiH, 6);
    }

    [Fact]
    public void Roi_change_should_reflect_in_canvas_rect_calculation()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        vm.SelectedField = vm.Fields[0];

        vm.RoiState.SetRoi(vm.Fields[0].RowId, new RelativeRoi(0.25, 0.1, 0.5, 0.2), RoiWriteSource.TestInject);

        var r = vm.GetCanvasRect(vm.Fields[0], canvasWidth: 400, canvasHeight: 200);
        Assert.Equal(100, r.X, 6);
        Assert.Equal(20, r.Y, 6);
        Assert.Equal(200, r.W, 6);
        Assert.Equal(40, r.H, 6);
    }

    [Fact]
    public void ApplyTemplateDefinition_should_reuse_existing_rows_and_preserve_roi_when_incoming_is_zero_rect()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        var snRow = vm.Fields[0];

        vm.RoiState.SetRoi(snRow.RowId, new RelativeRoi(0.2, 0.3, 0.4, 0.5), RoiWriteSource.TestInject);

        // incoming template has SN but ROI is 0x0 => should preserve existing roi
        var incoming = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition("SN", new CogniLabel.Core.Roi.RelativeRoi(0,0,0,0), isSn: true),
        });

        vm.ApplyTemplateDefinitionForTest(incoming, overwriteRoi: false);

        Assert.Same(snRow, vm.Fields[0]); // instance reused
        Assert.Equal(0.2, vm.Fields[0].RoiX, 6);
        Assert.Equal(0.3, vm.Fields[0].RoiY, 6);
        Assert.Equal(0.4, vm.Fields[0].RoiW, 6);
        Assert.Equal(0.5, vm.Fields[0].RoiH, 6);
    }

    [Fact]
    public void Duplicate_non_sn_names_fail_validation()
    {
        var vm = CreateVm(TemplateEditorMode.Create);
        vm.Fields.Add(new TemplateEditorFieldRowViewModel
        {
            Name = "A",
            IsSn = false,
        });
        vm.Fields.Add(new TemplateEditorFieldRowViewModel
        {
            Name = "A",
            IsSn = false,
        });
        Assert.False(vm.ValidateForTest());
    }

    [Fact]
    public void Save_when_valid_writes_and_requests_close_with_path()
    {
        var writer = new SpyTemplateWriter();
        var dialogs = new SpyDialogs { SavePath = @"c:\fake\out.json" };
        var vm = new TemplateEditorViewModel(new ThrowingLoader(), writer, dialogs, new RoiStateService(), TemplateEditorMode.Create);

        var closed = false;
        bool? ok = null;
        vm.CloseRequested += success =>
        {
            closed = true;
            ok = success;
        };

        vm.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.True(ok);
        Assert.Single(writer.Saves);
        Assert.Equal(@"c:\fake\out.json", writer.Saves[0].path);
        Assert.Equal(@"c:\fake\out.json", vm.SavedTemplatePath);
    }

    [Fact]
    public void Save_when_user_cancels_save_dialog_does_not_close()
    {
        var writer = new SpyTemplateWriter();
        var dialogs = new SpyDialogs { SavePath = null };
        var vm = new TemplateEditorViewModel(new ThrowingLoader(), writer, dialogs, new RoiStateService(), TemplateEditorMode.Create);

        var closed = false;
        vm.CloseRequested += _ => closed = true;
        vm.SaveCommand.Execute(null);
        Assert.False(closed);
        Assert.Empty(writer.Saves);
    }

    private static TemplateEditorViewModel CreateVm(TemplateEditorMode mode)
        => new(new ThrowingLoader(), new SpyTemplateWriter(), new SpyDialogs { SavePath = null }, new RoiStateService(), mode);

    private sealed class ThrowingLoader : ITemplateLoader
    {
        public TemplateDefinition Load(string templatePath)
            => throw new InvalidOperationException();
    }

    private sealed class SpyTemplateWriter : ITemplateWriter
    {
        public List<(string path, TemplateDefinition def)> Saves { get; } = new();

        public void Save(string templatePath, TemplateDefinition template)
            => Saves.Add((templatePath, template));
    }

    private sealed class SpyDialogs : IDialogService
    {
        public string? SavePath { get; set; }

        public string? PickExcelFile() => null;
        public string? PickImageFolder() => null;
        public string? PickTemplateFile() => null;
        public string? PickSaveTemplateFile() => SavePath;
    }
}
