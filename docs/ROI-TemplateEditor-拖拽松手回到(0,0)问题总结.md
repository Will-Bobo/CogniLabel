## 背景

在 `TemplateEditor` 中，拖拽 ROI 时“拖拽框显示正确”，但松手后用户肉眼观察 ROI 框“回到左上角 (0,0)”。  
该现象经过多轮排查，一度怀疑为 ROI 写回/回灌导致的 state 回退。

## 结论（最终根因）

**最终确认这不是 ROI state 回退问题，而是 WPF `ItemsControl + Canvas` 的布局定位绑定位置错误导致“渲染看起来在 (0,0)”**。

- `RoiStateService` 的 Mouse 写入值是非零且正确
- `RoiChanged` → `ApplyRoiFromService` 后 `RowVM` 的 `RoiX/Y/W/H` 与 `EditableRoiX/Y/W/H` 都更新为非零
- 视觉上“回到 (0,0)”的 ROI 框，来自 `ItemsControl` 在 Canvas 中布局时始终把 item container 放在 (0,0)

核心原因是：

- `ItemsControl.ItemsPanel` 使用 `<Canvas/>`
- Canvas 布局读取的是 **ItemContainer（默认是 `ContentPresenter`）上的 `Canvas.Left/Canvas.Top` 附加属性**
- 之前把 `Canvas.Left/Top` 的绑定写在了 `DataTemplate` 内部的 `<Canvas>` 上
  - 这不会被外层 `ItemsPanel` Canvas 用来定位 item container
  - 结果：**宽高（W/H）正确，但 Left/Top 被布局为默认值 0**

## 证据链（日志要点）

一次完整 Mouse 松手写入链路中，关键日志满足：

- `[ROI MOUSE FINAL WRITE] ... roi=(非零)`
- `[ROI WRITE] source=Mouse ... old=(0,0,0,0) -> roi=(非零)`
- `[ROI BROADCAST] source=Mouse ... roi=(非零)`
- `[ROI CHANGED EVENT] ... roi=(非零)`
- `[ROI APPLY] ... roi=(非零)`
- `[ROI CHANGE] RoiX/Y/W/H ... -> 非零`
- `[ROI CHANGE] EditableRoiX/Y/W/H ... -> 非零`

即：**state → event → apply → row 属性**全链路一致，不存在“第二次写 0”或“第二次 apply 0”的证据。

因此，“回到 (0,0)”只能来自 **渲染定位**（Canvas.Left/Top）而非 ROI state。

## 最小修复（不改 ROI 业务逻辑）

文件：`CogniLabel/Presentation/Views/TemplateEditorWindow.xaml`

把 ROI 定位绑定移动到 `ItemsControl.ItemContainerStyle`（Target=`ContentPresenter`），而不是放在 `DataTemplate` 内部元素上：

- `Canvas.Left = RoiX * Overlay.ActualWidth`
- `Canvas.Top  = RoiY * Overlay.ActualHeight`

并移除原先 DataTemplate 内部的 `Canvas.Left/Top` 绑定（避免误导和重复）。

这样 `ItemsControl` 的 item container 会被 Canvas 正确定位，ROI 框松手后不会“看起来回到 (0,0)”。

## 额外排查与收敛（工程化措施）

为证明“不是 state 回退”，排查过程做了以下收敛：

- **ROI 单写入口**：ROI 修改统一经 `RoiStateService`（Mouse/DataGrid/Move）
- **去噪与追踪**：在 `RoiStateService` 写入链路加入 `traceId/source/rowId` 追踪日志
- **Row 生命周期诊断**：增加 RowVM 创建/缓存/重复实例检测日志，用于排除“Row 重建导致 Init 覆盖”
- **Apply 无 cache**：`ApplyRoiFromService` 只做 UI 同步，不做 diff/cache（避免生命周期噪声掩盖事实）

## 验证

- 单元测试：`dotnet test` 通过（70/70）
- UI 行为：松手后 ROI 框保持在拖拽位置；DataGrid 值与画面一致

