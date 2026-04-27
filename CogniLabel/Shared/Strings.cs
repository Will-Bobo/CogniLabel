namespace CogniLabel.Shared;

public static class Strings
{
    public static class Messages
    {
        public const string ExcelValidationFailed = "Excel 校验失败，已中断本次审核";
        public const string ExcelDuplicateSn = "Excel 数据存在主键（SN）重复，请修复后再执行";
        public const string ExcelEmptySn = "Excel 数据存在空 SN（主键缺失），请修复后再执行";
        public const string ExportSuccess = "导出成功";
        public const string ExportFailed = "导出失败，请检查文件占用/权限/路径后重试";
        public const string ExportImageNotAccessible = "导出失败：源图片不可访问（不存在/无权限/被占用）";
        public const string RunAuditUnexpectedError = "审核失败：发生未处理异常（请重试或检查输入/权限）";
        public const string InvalidRequest = "请求参数无效，请检查 Excel/图片目录/模板路径";
        public const string ImageFolderNotFound = "图片目录不存在，请检查路径";
        public const string TemplateLoadFailed = "模板加载失败，请检查模板文件";

        public static class TemplateEditor
        {
            public const string ValidationNoFields = "至少添加一个字段";
            public const string ValidationNoSn = "必须指定一个 SN 字段（勾选「是否 SN」）";
            public const string ValidationDuplicateFieldName = "字段名不能重复";
            public const string ValidationEmptyFieldName = "非 SN 字段的名称不能为空";
            public const string ValidationRoiRange = "ROI 须在 0~1 之间";
            public const string LoadFailed = "模板读取失败";
            public const string SaveFailed = "模板保存失败，请检查路径与权限";
        }
    }

    public static class Report
    {
        public const string SheetSummary = "Summary";
        public const string SheetDetails = "Details";
        public const string SheetErrors = "Errors";
        public const string SheetDuplicates = "Duplicates";
        public const string SheetUnreadable = "Unreadable";

        public const string ColTotal = "总数";
        public const string ColPass = "PASS 数";
        public const string ColFail = "FAIL 数";
        public const string ColRunTime = "运行时间";
        public const string ColExcelPath = "Excel 路径";
        public const string ColImageFolder = "图片目录";
        public const string ColTemplatePath = "模板路径";

        public const string ColImageName = "ImageName";
        public const string ColSn = "SN";
        public const string ColMatchStatus = "MatchStatus";
        public const string ColErrorType = "ErrorType";

        public const string ColFieldName = "FieldName";
        public const string ColImageValue = "ImageValue";
        public const string ColExcelValue = "ExcelValue";

        public const string ColCount = "Count";
        public const string ColImages = "Images";
        public const string ColReason = "Reason";
    }

    public static class Export
    {
        public const string OutputRootFolder = "output";
        public const string ReportFileName = "report.xlsx";
        public const string ImagesFolder = "images";
        public const string ErrorFolder = "error";
        public const string DuplicateFolder = "duplicate";
        public const string NotFoundFolder = "not_found";
        public const string MismatchFolder = "mismatch";
        public const string UnreadableFolder = "unreadable";
    }

    public static class UI
    {
        public const string BrowseExcel = "选择 Excel";
        public const string BrowseImageFolder = "选择图片目录";
        public const string BrowseTemplate = "选择模板";
        public const string NewTemplate = "新建模板";
        public const string EditTemplate = "编辑模板";
        public const string SaveTemplate = "保存模板";
        public const string AddTemplateField = "添加字段";
        public const string RemoveTemplateField = "删除字段";
        public const string TemplateEditorTitleCreate = "新建模板";
        public const string TemplateEditorTitleEdit = "编辑模板";
        public const string FieldNameColumn = "字段名";
        public const string IsSnColumn = "是否 SN";
        public const string RoiXColumn = "X";
        public const string RoiYColumn = "Y";
        public const string RoiWColumn = "宽";
        public const string RoiHColumn = "高";
        public const string LoadSampleImage = "加载示例图片";
        public const string StartAudit = "开始审核";
        public const string Cancel = "取消";
        public const string Export = "导出报告";

        public const string StageExcelLoading = "加载 Excel";
        public const string StageExcelValidating = "校验 Excel";
        public const string StageTemplateLoading = "加载模板";
        public const string StageImageProcessing = "处理图片";
        public const string StageMatching = "SN 匹配";
        public const string StageComparing = "字段比对";
        public const string StageDeduplicating = "重复检测";
        public const string StageSummary = "汇总";
    }
}

