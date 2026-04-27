using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;

namespace CogniLabel.Infrastructure.Export;

public interface IExcelWriter
{
    void WriteReport(string filePath, AuditResult auditResult, IReadOnlyList<AuditItem> items);
}

