namespace CogniLabel.Infrastructure.Excel;

public interface IExcelReader
{
    Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken);
}

