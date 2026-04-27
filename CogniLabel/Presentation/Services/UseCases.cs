using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;

namespace CogniLabel.Presentation.Services;

public interface IAuditUseCase
{
    Task<AuditResult> RunAudit(AuditRequest request, IProgress<ProgressInfo> progress, CancellationToken token);
}

public interface IExportUseCase
{
    ExportResult Export(AuditResult auditResult);
}

public sealed class AuditUseCase : IAuditUseCase
{
    private readonly AuditService _service;
    public AuditUseCase(AuditService service) => _service = service;

    public Task<AuditResult> RunAudit(AuditRequest request, IProgress<ProgressInfo> progress, CancellationToken token)
        => _service.RunAuditSafe(request, progress, token);
}

public sealed class ExportUseCase : IExportUseCase
{
    private readonly ExportService _service;
    public ExportUseCase(ExportService service) => _service = service;

    public ExportResult Export(AuditResult auditResult) => _service.Export(auditResult);
}

