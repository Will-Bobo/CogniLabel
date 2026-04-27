using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using System.Globalization;
using System.IO;

namespace CogniLabel.Application.Export;

public sealed class ExportService
{
    private readonly IExcelWriter _excelWriter;
    private readonly IFileSystemService _fs;
    private readonly IClock _clock;

    public ExportService(IExcelWriter excelWriter, IFileSystemService fs, IClock clock)
    {
        _excelWriter = excelWriter;
        _fs = fs;
        _clock = clock;
    }

    public ExportResult Export(AuditResult auditResult, string? outputRoot = null)
    {
        try
        {
            var items = auditResult.Items.Cast<AuditItem>().ToList();

            var root = outputRoot ?? Strings.Export.OutputRootFolder;
            var folderName = _clock.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var outputPath = EnsureUniqueDirectory(Path.Combine(root, folderName));

            try
            {
                _fs.CreateDirectory(outputPath);
            }
            catch
            {
                return new ExportResult { IsSuccess = false, OutputPath = null, Message = Strings.Messages.ExportFailed };
            }

            var reportPath = Path.Combine(outputPath, Strings.Export.ReportFileName);
            try
            {
                _excelWriter.WriteReport(reportPath, auditResult, items);
            }
            catch
            {
                return new ExportResult { IsSuccess = false, OutputPath = outputPath, Message = Strings.Messages.ExportFailed };
            }

            var imageCopyResult = ExportErrorImages(outputPath, items);
            if (!imageCopyResult.IsSuccess)
            {
                return new ExportResult
                {
                    IsSuccess = false,
                    OutputPath = outputPath,
                    Message = imageCopyResult.Message,
                };
            }

            return new ExportResult { IsSuccess = true, OutputPath = outputPath, Message = Strings.Messages.ExportSuccess };
        }
        catch
        {
            return new ExportResult { IsSuccess = false, OutputPath = null, Message = Strings.Messages.ExportFailed };
        }
    }

    private ExportResult ExportErrorImages(string outputPath, IReadOnlyList<AuditItem> items)
    {
        foreach (var item in items.Where(i => !i.IsPass))
        {
            if (string.IsNullOrWhiteSpace(item.Image.ImagePath) || !_fs.FileExists(item.Image.ImagePath))
                return new ExportResult { IsSuccess = false, OutputPath = null, Message = Strings.Messages.ExportImageNotAccessible };

            var dest = GetImageDestination(outputPath, item.ErrorType);
            var destPath = Path.Combine(dest, Path.GetFileName(item.Image.ImagePath));
            try
            {
                _fs.CopyFile(item.Image.ImagePath, destPath, overwrite: true);
            }
            catch
            {
                return new ExportResult { IsSuccess = false, OutputPath = null, Message = Strings.Messages.ExportFailed };
            }
        }

        return new ExportResult { IsSuccess = true, OutputPath = outputPath, Message = Strings.Messages.ExportSuccess };
    }

    private static string GetImageDestination(string outputPath, ErrorType errorType)
    {
        var imagesRoot = Path.Combine(outputPath, Strings.Export.ImagesFolder);
        return errorType switch
        {
            ErrorType.Duplicate => Path.Combine(imagesRoot, Strings.Export.DuplicateFolder),
            ErrorType.NotFound => Path.Combine(imagesRoot, Strings.Export.ErrorFolder, Strings.Export.NotFoundFolder),
            ErrorType.Mismatch => Path.Combine(imagesRoot, Strings.Export.ErrorFolder, Strings.Export.MismatchFolder),
            ErrorType.Unreadable => Path.Combine(imagesRoot, Strings.Export.ErrorFolder, Strings.Export.UnreadableFolder),
            _ => Path.Combine(imagesRoot, Strings.Export.ErrorFolder),
        };
    }

    private string EnsureUniqueDirectory(string candidate)
    {
        if (!_fs.DirectoryExists(candidate))
            return candidate;

        for (var i = 1; i <= 999; i++)
        {
            var next = candidate + "_" + i.ToString("D3", CultureInfo.InvariantCulture);
            if (!_fs.DirectoryExists(next))
                return next;
        }

        return candidate + "_" + Guid.NewGuid().ToString("N");
    }
}

