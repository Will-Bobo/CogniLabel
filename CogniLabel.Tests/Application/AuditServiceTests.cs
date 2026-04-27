using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Tests.Application;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task Excel_validation_duplicate_sn_should_stop_pipeline_and_return_audit_result()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = " A " },
        });

        var service = new AuditService(excel);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "in-memory.xlsx",
            ImageFolderPath = null,
            TemplatePath = null,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Meta.Stages);
        Assert.True(result.Meta.Stages.Last().ShouldStop);
        Assert.Contains(result.Errors, e => e.Type == ErrorType.Duplicate);
    }

    [Fact]
    public async Task Excel_validation_ok_should_not_stop_pipeline_even_if_later_stages_not_implemented()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "B" },
        });

        var service = new AuditService(excel);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "in-memory.xlsx",
            ImageFolderPath = null,
            TemplatePath = null,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Meta.Stages);
        Assert.False(result.Meta.Stages.Last().ShouldStop);
        Assert.DoesNotContain(result.Errors, e => e.Type == ErrorType.Duplicate);
        // Phase 4: Summary.Total means processed images count (not Excel row count)
        Assert.Equal(0, result.Summary.Total);
    }

    private sealed class FakeExcelReader : IExcelReader
    {
        private readonly IReadOnlyList<Dictionary<string, string>> _rows;

        public FakeExcelReader(IReadOnlyList<Dictionary<string, string>> rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken)
            => Task.FromResult(_rows);
    }
}

