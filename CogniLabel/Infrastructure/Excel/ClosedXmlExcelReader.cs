using ClosedXML.Excel;

namespace CogniLabel.Infrastructure.Excel;

public sealed class ClosedXmlExcelReader : IExcelReader
{
    public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheets.First();

        var used = ws.RangeUsed();
        if (used is null)
            return Task.FromResult<IReadOnlyList<Dictionary<string, string>>>(Array.Empty<Dictionary<string, string>>());

        var firstRow = used.FirstRowUsed();
        var headerCells = firstRow.CellsUsed().ToList();
        var headers = headerCells.Select(c => c.GetString()).ToList();

        var result = new List<Dictionary<string, string>>();

        foreach (var row in used.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var cell = row.Cell(i + 1);
                dict[header] = cell.GetString();
            }
            result.Add(dict);
        }

        return Task.FromResult<IReadOnlyList<Dictionary<string, string>>>(result);
    }
}

