# CogniLabel

CogniLabel 是一个 **本地运行** 的标签图片自动复核工具（WPF / .NET 8）。  
用于在标签打印前，对供应商提供的标签图片电子档进行 **条码/二维码识别**，并与 Excel 生产资料进行匹配校验，自动发现异常并输出报告，显著降低人工核对工作量。

## 核心能力（V1）

- **一图一设备一行**：每张图片文件 ↔ 一个设备 ↔ Excel 一行（强制一一映射）
- **唯一主键**：主键锁死为 `SN`（来源：条码），并且仅此一个主键
- **模板 ROI 识别**：按模板中定义的相对比例 ROI 裁剪后识别（适配不同分辨率）
- **异常分类收敛**（仅四类）：
  - `NOT_FOUND`：识别出的 `SN` 不在 Excel 中
  - `MISMATCH`：字段值与 Excel 值不一致（`Trim` 后字符串完全比较）
  - `DUPLICATE`：本次运行批次内 `SN` 重复（基于图片识别结果统一统计）
  - `UNREADABLE`：识别失败（`SN` 失败则整图 `UNREADABLE`；普通字段失败仅字段级 `UNREADABLE`）
- **报告导出 + 错误图片导出**：输出到时间戳目录，避免覆盖；导出失败可重试且不丢失内存结果

## V1 边界（明确不做）

- 不做 OCR 文字识别、不做全图自动找码、不做模板自动识别/自适应
- 不做 WiFi/URL 等内容解析与格式规则校验（仅字符串比较）
- 不使用数据库（仅本地文件）
- 不引入 OpenCV / AI 识别库
- 不支持中断恢复（但支持取消：返回已完成部分结果）

## 技术栈

- **.NET 8 / WPF（WinExe）**
- **MVVM**
- 条码/二维码：`ZXing.Net`
- Excel 读取/导出：`ClosedXML`
- JSON：`System.Text.Json`

项目目标框架：`net8.0-windows`（见 `CogniLabel/CogniLabel.csproj`）。

## 架构分层（Clean Architecture）

严格分层与职责边界：

- **Presentation（WPF / MVVM）**：UI、数据绑定、命令与展示；不写业务逻辑
- **Application**：流程编排（唯一入口 `RunAudit`）、并发控制、进度回调、错误/状态汇总
- **Core**：确定性业务规则（ROI 换算与 Clamp、匹配/比对、重复检测、错误分类）；不依赖 UI / IO / 第三方库
- **Infrastructure**：外部依赖封装（ZXing、ClosedXML、文件系统、图片加载/裁剪）

代码位置（以当前仓库为准）：

- `CogniLabel/Application/`：`AuditService`、Pipeline、DTO、单图处理
- `CogniLabel/Core/`：Engines、ROI、校验等纯逻辑
- `CogniLabel/Infrastructure/`：Excel/模板/图片/条码/导出等外部能力
- `CogniLabel/Shared/`：字符串与展示映射（如 `Strings`）

## 输入与配置

### 1) Excel（`.xlsx`）

- Excel **全按字符串读取**（避免科学计数法导致 SN 失真）
- 比对前做 `Trim`（仅前后空格）
- 审核前必须执行 Excel 预校验（阻断执行）：
  - `SN` **非空**（含 `Trim` 后为空）
  - `SN` **唯一**（发现重复直接中断，不进入图片识别）

### 2) 图片目录

- 文件夹批量导入
- 支持格式：`jpg / png / bmp`

### 3) 模板（JSON，ROI 为相对比例）

模板定义图片中各字段的 ROI 区域。示例（来自 PRD）：

```json
{
  "TemplateName": "MIFI_A_V1",
  "Fields": [
    { "Name": "SN", "Type": "BARCODE", "X": 0.1, "Y": 0.7, "W": 0.6, "H": 0.15 },
    { "Name": "QR1", "Type": "QR", "X": 0.05, "Y": 0.05, "W": 0.4, "H": 0.4 }
  ]
}
```

ROI 边界策略：

- 由比例坐标换算像素坐标后，**超出边界自动 Clamp**
- Clamp 后若 ROI 宽/高为 0：判定为 `UNREADABLE`（`SN` 则整图 `UNREADABLE`）

### 4) 字段映射（模板字段 ↔ Excel 表头）

审核时需要把“模板字段名”映射到“Excel 列名（表头）”。  
示例：`SN → SN_CODE`，`QR1 → WIFI_INFO`。

> 约束：`SN` 是唯一主键字段，必须参与匹配与重复检测，禁止使用 QR 作为主键。

## 运行方式（开发）

本仓库未包含 `.sln`，可直接用 `dotnet` 基于 `csproj` 构建/测试。

### 构建

```powershell
dotnet restore .\CogniLabel\CogniLabel.csproj
dotnet build   .\CogniLabel\CogniLabel.csproj -c Release
```

### 运行（WPF）

```powershell
dotnet run --project .\CogniLabel\CogniLabel.csproj
```

也可以用 Visual Studio 打开 `CogniLabel/CogniLabel.csproj` 直接启动。

### 运行测试

```powershell
dotnet test .\CogniLabel.Tests\CogniLabel.Tests.csproj -c Release
```

## 审核主流程（Pipeline）

唯一入口：`AuditService.RunAudit(...)`（或 `RunAuditSafe(...)`）。  
阶段顺序锁死（与文档一致）：Excel 读取 → Excel 预校验（阻断）→ 模板加载 → 图片识别（并发）→ SN 匹配 → 字段比对 → 重复检测 → 汇总。

并发策略（确定性）：

- 仅在“图片批量识别（单图处理）”阶段并发
- 并发模型：`Task` + `SemaphoreSlim`
- 并发数：`max(1, CPU 逻辑核心数 - 1)`（V1 不提供 UI 配置入口）

## 输出说明

默认输出根目录：`output/`（见 `CogniLabel/Shared/Strings.cs`）。

每次导出写入时间戳文件夹：

```text
output/yyyyMMdd_HHmmss/
  report.xlsx
  images/
    duplicate/                 # DUPLICATE
    error/
      not_found/               # NOT_FOUND
      mismatch/                # MISMATCH
      unreadable/              # UNREADABLE
```

导出策略：

- 导出失败（文件占用/权限/路径等）会提示错误
- **内存中的审核结果不清空**，可直接再次点击导出重试，不需要重跑审核

## UI 规范（摘要）

UI 目标：批处理效率优先、异常优先、单一状态源。页面结构固定为“配置区（可折叠）→状态区→结果区（DataGrid）→操作区（Sticky）”，并要求异常行高亮与 Quick Preview（选中刷新预览/双击大图预览并对照“识别 vs 预期”）。

详细规范见：`docs/UI 设计规范.md`。

## 文档索引

- `docs/PRD.md`：产品需求与业务规则（V1 边界、异常定义、报告结构等）
- `docs/技术约束与骨架说明.md`：强制技术约束与分层/并发/错误收敛规则
- `docs/开发计划-总体技术方案.md`：总体方案与阶段化交付/测试策略
- `docs/UI 设计规范.md`：UI 结构、DataGrid 规范、交互与 DoD

