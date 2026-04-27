using System.Configuration;
using System.Data;
using System.Windows;
using CogniLabel.Application;
using CogniLabel.Application.Export;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Infrastructure.Templates;
using CogniLabel.Infrastructure.Barcode;
using CogniLabel.Infrastructure.Images;
using CogniLabel.Application.Pipeline;
using CogniLabel.Presentation.Services;
using CogniLabel.Presentation.ViewModels;
using CogniLabel.Presentation.Views;

namespace CogniLabel
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var excelReader = new ClosedXmlExcelReader();
            var templateLoader = new TemplateLoader();
            var templateWriter = new TemplateWriter();
            var imageEnumerator = new ImageEnumerator();

            var processorFactory = new DefaultSingleImageProcessorFactory(template =>
            {
                var loader = new ImageLoader();
                var barcode = new ZxingBarcodeReader();
                var proc = new CogniLabel.Application.SingleImage.SingleImageProcessor(template, loader, barcode);
                return new SingleImageAdapter(proc);
            });

            var auditService = new AuditService(excelReader, templateLoader, imageEnumerator, processorFactory);
            var auditUseCase = new AuditUseCase(auditService);

            var exportService = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), new SystemClock());
            var exportUseCase = new ExportUseCase(exportService);

            var dialogs = new DialogService();
            var templateEditor = new TemplateEditorDialogService(templateLoader, templateWriter, dialogs);
            var vm = new MainViewModel(auditUseCase, exportUseCase, dialogs, templateEditor);
            var main = new MainWindow(vm);
            main.Show();
        }
	}

}

file sealed class SingleImageAdapter : CogniLabel.Application.Pipeline.ISingleImageProcessor
{
    private readonly CogniLabel.Application.SingleImage.SingleImageProcessor _inner;
    public SingleImageAdapter(CogniLabel.Application.SingleImage.SingleImageProcessor inner) => _inner = inner;
    public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath) => _inner.ProcessSingleImage(imagePath);
}
