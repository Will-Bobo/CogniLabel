using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Core.Engines;
using CogniLabel.Core.Validation;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using System.IO;

namespace CogniLabel.Application;

public sealed class AuditService
{
    private readonly IExcelReader _excelReader;
    private readonly ITemplateLoader _templateLoader;
    private readonly IImageEnumerator _imageEnumerator;
    private readonly ISingleImageProcessorFactory _processorFactory;
    private readonly IConcurrencyProvider _concurrency;

    public AuditService(IExcelReader excelReader)
    {
        _excelReader = excelReader;
        _templateLoader = new NullTemplateLoader();
        _imageEnumerator = new NullImageEnumerator();
        _processorFactory = new NullProcessorFactory();
        _concurrency = new DefaultConcurrencyProvider();
    }

    public AuditService(
        IExcelReader excelReader,
        ITemplateLoader templateLoader,
        IImageEnumerator imageEnumerator,
        ISingleImageProcessorFactory processorFactory,
        IConcurrencyProvider? concurrencyProvider = null)
    {
        _excelReader = excelReader;
        _templateLoader = templateLoader;
        _imageEnumerator = imageEnumerator;
        _processorFactory = processorFactory;
        _concurrency = concurrencyProvider ?? new DefaultConcurrencyProvider();
    }

    public async Task<AuditResult> RunAuditSafe(
        AuditRequest? request,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.ExcelPath) ||
                string.IsNullOrWhiteSpace(request.ImageFolderPath) ||
                string.IsNullOrWhiteSpace(request.TemplatePath))
            {
                return BuildSafeStopResult(Strings.Messages.InvalidRequest);
            }

            return await RunAudit(request, progress, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return BuildSafeStopResult(Strings.Messages.RunAuditUnexpectedError);
        }
    }

    public async Task<AuditResult> RunAudit(
        AuditRequest request,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.UtcNow;
        var stages = new List<StageResult>(capacity: 8);
        var errors = new List<AuditError>();
        var cancelled = false;

        Report(progress, AuditStage.ExcelLoading, 0, 0, message: string.Empty);
        if (cancellationToken.IsCancellationRequested)
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: true);

        // 1) ExcelLoading
        IReadOnlyList<Dictionary<string, string>> excelRows;
        try
        {
            excelRows = await _excelReader.ReadAsStringTableAsync(request.ExcelPath, cancellationToken);
            stages.Add(StageResult.Success(message: string.Empty, payload: new { RowCount = excelRows.Count }));
        }
        catch (Exception ex)
        {
            stages.Add(StageResult.Stop(ErrorType.Unreadable, Strings.Messages.ExcelValidationFailed, payload: ex));
            errors.Add(new AuditError { Type = ErrorType.Unreadable, Message = Strings.Messages.ExcelValidationFailed });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }

        // 2) ExcelValidating (blocking)
        Report(progress, AuditStage.ExcelValidating, 0, excelRows.Count, message: string.Empty);
        if (cancellationToken.IsCancellationRequested)
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: true);

        var excelSnColumn = ResolveSnColumn(request.FieldMappings);
        var sns = excelRows.Select(r => r.TryGetValue(excelSnColumn, out var v) ? v : null);
        var snResult = SnValidator.Validate(sns);
        if (snResult.HasEmpty)
        {
            stages.Add(StageResult.Stop(ErrorType.Unreadable, Strings.Messages.ExcelEmptySn));
            errors.Add(new AuditError { Type = ErrorType.Unreadable, Message = Strings.Messages.ExcelEmptySn });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }

        if (snResult.Duplicates.Count > 0)
        {
            stages.Add(StageResult.Stop(ErrorType.Duplicate, Strings.Messages.ExcelDuplicateSn, payload: snResult.Duplicates));
            errors.Add(new AuditError { Type = ErrorType.Duplicate, Message = Strings.Messages.ExcelDuplicateSn });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }

        stages.Add(StageResult.Success(message: string.Empty));

        // 3) TemplateLoading
        Report(progress, AuditStage.TemplateLoading, 0, 0, message: string.Empty);
        if (cancellationToken.IsCancellationRequested)
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: true);

        TemplateDefinition template;
        try
        {
            template = _templateLoader.Load(request.TemplatePath ?? string.Empty);
            stages.Add(StageResult.Success(message: string.Empty));
        }
        catch (Exception ex)
        {
            stages.Add(StageResult.Stop(ErrorType.Unreadable, Strings.Messages.TemplateLoadFailed, payload: ex));
            errors.Add(new AuditError { Type = ErrorType.Unreadable, Message = Strings.Messages.TemplateLoadFailed });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }

        // 4) ImageProcessing (concurrent)
        Report(progress, AuditStage.ImageProcessing, 0, 0, message: string.Empty);
        if (cancellationToken.IsCancellationRequested)
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: true);

        var imageFolder = request.ImageFolderPath ?? string.Empty;
        List<string> imagePaths;
        try
        {
            imagePaths = _imageEnumerator.Enumerate(imageFolder)
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            stages.Add(StageResult.Stop(ErrorType.Unreadable, Strings.Messages.ImageFolderNotFound, payload: ex));
            errors.Add(new AuditError { Type = ErrorType.Unreadable, Message = Strings.Messages.ImageFolderNotFound });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }

        var totalImages = imagePaths.Count;
        var maxConcurrency = _concurrency.GetMaxConcurrency();
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        ISingleImageProcessor processor;
        try
        {
            processor = _processorFactory.Create(template);
        }
        catch (Exception ex)
        {
            stages.Add(StageResult.Stop(ErrorType.Unreadable, Strings.Messages.RunAuditUnexpectedError, payload: ex));
            errors.Add(new AuditError { Type = ErrorType.Unreadable, Message = Strings.Messages.RunAuditUnexpectedError });
            return BuildResult(start, stages, errors, items: Array.Empty<AuditItem>(), cancelled: false);
        }
        var processed = new ImageProcessResult[totalImages];

        var completed = 0;
        var tasks = imagePaths.Select((path, index) => Task.Run(async () =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (cancellationToken.IsCancellationRequested) return;
                try
                {
                    processed[index] = processor.ProcessSingleImage(path);
                }
                catch
                {
                    processed[index] = new CogniLabel.Application.SingleImage.ImageProcessResult
                    {
                        ImagePath = path,
                        ImageName = Path.GetFileName(path),
                        Fields = new Dictionary<string, string?>(),
                        IsUnreadable = true,
                    };
                }
            }
            finally
            {
                semaphore.Release();
                var now = Interlocked.Increment(ref completed);
                if (now % 5 == 0 || now == totalImages)
                    Report(progress, AuditStage.ImageProcessing, now, totalImages, message: string.Empty);
            }
        }, cancellationToken)).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        if (cancellationToken.IsCancellationRequested)
            cancelled = true;

        var imagesProcessed = processed.Where(p => p is not null).ToList();
        stages.Add(StageResult.Success(message: string.Empty, payload: new { ImageCount = imagesProcessed.Count, MaxConcurrency = maxConcurrency }));

        if (cancelled)
            return BuildResult(start, stages, errors, BuildItemsMinimal(imagesProcessed), cancelled: true);

        // 5) Matching
        Report(progress, AuditStage.Matching, 0, imagesProcessed.Count, message: string.Empty);
        var matches = imagesProcessed.Select(img =>
        {
            img.Fields.TryGetValue("SN", out var sn);
            return MatchEngine.MatchBySn(sn, excelRows, excelSnColumn);
        }).ToList();
        stages.Add(StageResult.Success(message: string.Empty));

        // 6) Comparing
        Report(progress, AuditStage.Comparing, 0, imagesProcessed.Count, message: string.Empty);
        var fieldResults = imagesProcessed.Zip(matches, (img, match) =>
        {
            if (!match.IsMatched || match.Row is null)
                return Array.Empty<FieldCompareResult>();
            return CompareEngine.Compare(img.Fields, match.Row, request.FieldMappings);
        }).ToList();
        stages.Add(StageResult.Success(message: string.Empty));

        // 7) Deduplicating (image SN duplicates)
        Report(progress, AuditStage.Deduplicating, 0, imagesProcessed.Count, message: string.Empty);
        var duplicateSns = DuplicateEngine.FindDuplicates(imagesProcessed.Select(img => img.Fields.TryGetValue("SN", out var sn) ? sn : null));
        stages.Add(StageResult.Success(message: string.Empty, payload: new { DuplicateCount = duplicateSns.Count }));

        // 8) Summary
        Report(progress, AuditStage.Summary, imagesProcessed.Count, imagesProcessed.Count, message: string.Empty);
        var items = new List<AuditItem>(imagesProcessed.Count);
        for (var i = 0; i < imagesProcessed.Count; i++)
        {
            var img = imagesProcessed[i];
            img.Fields.TryGetValue("SN", out var sn);
            var eval = ErrorClassifier.Evaluate(
                isImageUnreadable: img.IsUnreadable,
                match: matches[i],
                fieldResults: fieldResults[i],
                duplicateSns: duplicateSns,
                sn: sn);

            items.Add(new AuditItem
            {
                Image = img,
                IsPass = eval.IsPass,
                ErrorType = eval.Error,
                ExcelValues = matches[i].Row,
                FieldIssues = eval.FieldResults
                    .Where(r => r.ErrorType is not null)
                    .Select(r => new FieldIssue { FieldName = r.FieldName, ErrorType = r.ErrorType!.Value })
                    .ToList(),
            });
        }

        var pass = items.Count(i => i.IsPass);
        var fail = items.Count - pass;

        return new AuditResult
        {
            Items = items,
            Summary = new AuditSummary { Total = items.Count, Pass = pass, Fail = fail },
            Errors = errors,
            Meta = new AuditMeta { StartTime = start, Stages = stages, Cancelled = false },
        };
    }

    private static string ResolveSnColumn(IReadOnlyDictionary<string, string> mappings)
    {
        if (mappings.TryGetValue("SN", out var col) && !string.IsNullOrWhiteSpace(col))
            return col.Trim();
        return "SN";
    }

    private static AuditResult BuildResult(
        DateTimeOffset start,
        IReadOnlyList<StageResult> stages,
        IReadOnlyList<AuditError> errors,
        IReadOnlyList<AuditItem> items,
        bool cancelled)
    {
        var pass = items.Count(i => i.IsPass);
        var fail = items.Count - pass;
        return new AuditResult
        {
            Items = items,
            Summary = new AuditSummary { Total = items.Count, Pass = pass, Fail = fail },
            Errors = errors,
            Meta = new AuditMeta { StartTime = start, Stages = stages, Cancelled = cancelled },
        };
    }

    private static IReadOnlyList<AuditItem> BuildItemsMinimal(IReadOnlyList<ImageProcessResult> imagesProcessed)
    {
        return imagesProcessed.Select(img => new AuditItem
        {
            Image = img,
            IsPass = !img.IsUnreadable,
            ErrorType = img.IsUnreadable ? ErrorType.Unreadable : ErrorType.None,
            FieldIssues = Array.Empty<FieldIssue>(),
        }).ToList();
    }

    private static void Report(IProgress<ProgressInfo>? progress, AuditStage stage, int current, int total, string message)
    {
        progress?.Report(new ProgressInfo { Current = current, Total = total, Stage = stage, Message = message });
    }

    private static AuditResult BuildSafeStopResult(string message)
    {
        return new AuditResult
        {
            Items = Array.Empty<object>(),
            Summary = new AuditSummary { Total = 0, Pass = 0, Fail = 0 },
            Errors = new[] { new AuditError { Type = ErrorType.Unreadable, Message = message } },
            Meta = new AuditMeta
            {
                StartTime = DateTimeOffset.UtcNow,
                Stages = new[] { StageResult.Stop(ErrorType.Unreadable, message) },
                Cancelled = false,
            },
        };
    }

    private sealed class NullTemplateLoader : ITemplateLoader
    {
        public CogniLabel.Application.SingleImage.TemplateDefinition Load(string templatePath)
            => new CogniLabel.Application.SingleImage.TemplateDefinition(Array.Empty<CogniLabel.Application.SingleImage.TemplateFieldDefinition>());
    }

    private sealed class NullImageEnumerator : IImageEnumerator
    {
        public IReadOnlyList<string> Enumerate(string imageFolderPath) => Array.Empty<string>();
    }

    private sealed class NullProcessorFactory : ISingleImageProcessorFactory
    {
        public ISingleImageProcessor Create(CogniLabel.Application.SingleImage.TemplateDefinition template)
            => new NullProcessor();

        private sealed class NullProcessor : ISingleImageProcessor
        {
            public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath)
                => new CogniLabel.Application.SingleImage.ImageProcessResult
                {
                    ImagePath = imagePath,
                    ImageName = Path.GetFileName(imagePath),
                    Fields = new Dictionary<string, string?>(),
                    IsUnreadable = true,
                };
        }
    }
}

