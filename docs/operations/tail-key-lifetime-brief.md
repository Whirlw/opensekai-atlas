# 尾键生命周期离开 `State`：任务书

> **状态：已完成（历史文档，不再更新）。** 现行行为见 `tail-classes.md`。

> 目的：把「尾键什么时候显示、跟随、下移、回收」从 `Note.State` 与六个布尔开关里解耦出来，收敛成一处纯函数 `TailKeyLifetime`。
> 适用读者：接手本任务的外部执行者。
> 前置条件：读过 [判定通道与解耦收尾](judgment-channel-brief.md)（尤其 §5、§5.1、§11）与 [判定与窗口](../reference/judging.md)。
> 最后更新：2026-11-07

## 1. 一句话目标

尾键（长条末端那枚键）的可见性只由三个输入决定——**尾键时间、判定结果、条是否已结束**；不再读 `Note.State`，不再依赖 `KeepsViewUntilParentEnds` / `ShouldDeferViewRemoval` / `onRetireView` / `SuppressView` / `IsViewConsumed` / `MarkViewConsumed` 这六个开关。**不改任何判定数值**。

## 2. 项目与运行环境

- 仓库：`E:\DeepseekHarness\OpenSekai`（Open Sekai Atlas）；工作分支从 `codex/long-hold-pickup-fix` 拉出，命名 `codex/tail-key-lifetime`。
- Unity `6000.6.7f1`，仅 Android；真机 OPPO `PLQ110`（165 Hz）。
- EditMode：编辑器内 `OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<name>.xml")`；CLI 见 `Tools/run-editmode-tests.ps1`。**读报告先看 `LastWriteTime` 与 `testcasecount`**——编译失败时 Unity 会静默保留旧程序集。
- 当前基线：**430 条 EditMode 全绿 + 4 条停放**（HEAD `021a0ac`）。
- 出包：`AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` → `OpenSekaiAssetBundleBuildPipeline.BuildForTarget(BuildTarget.Android, true)` → `BuildPipeline.BuildPlayer`；构建耗时约 60 s，`script eval` 有 30 s 超时，504 超时不代表失败。

## 3. 现状（不要重复调研）

| 位置 | 现状 |
|---|---|
| `Assets/Game/Runtime/Sekai/Live/NoteBase.cs` | `KeepsViewUntilParentEnds` / `ShouldDeferViewRemoval` / `onRetireView` / `SuppressView` / `IsViewConsumed` / `MarkViewConsumed` 六个开关 |
| 六个音符类（`NormalNote` / `FlickNote` / `FrictionNote` / `FrictionFlickNote` / `GuideNote` / `FrictionHideLongNote`） | 各自 override `State`，`Done` 分支里各自决定「这一刻要不要 `OnUnSpawnNote`」 |
| `Assets/Game/Runtime/Sekai/View/` | 现有视图层不知道「尾键该不该还留着」，只能问判定层要状态 |
| `Assets/Tests/EditMode/Live/LongNoteViewLifetimeTests.cs` | 5 条用例，目前通过 `State` 与开关间接断言 |
| `Assets/Tests/EditMode/Live/` | 缺 `LiveHarness.cs`（[judgment-channel-brief](judgment-channel-brief.md) §4 WP7 的产物） |

历史教训：这六个开关与 `State` 的耦合，曾造成「长条瞬间消失」「尾键提前回收」「按上出判定特效」三类缺陷，每次都只能靠真机录像定位。

## 4. 工作包

### WP0 先把测试脚手架补上（前置，必做）

- 新增 `Assets/Tests/EditMode/Live/LiveHarness.cs`：封装「造 `LiveLogic` + 塞 `_musicScore` / `_noteArray` / `_noteDisplayTimeOffset` / `_liveViews` + 按帧 `OnUpdate` + `source.Add(Touch(...))` + `OnInput`」。
- 文件头注释**明令禁止**「`new LongNote(...)` 后反射调内部阶段」写判定用例，并写明原因（子音符的进度、视图、候选只在 `OnUpdate` 的整条扫描里推进，缺了它 `IsJudgment` 恒为假）。
- 需要真实音符图时，用 `ChartJson.Read` → `RuntimeChartConverter.Convert`，参考 `Assets/Tests/EditMode/Live/DecorationTailFromChartTests.cs` 与夹具 `Tools/fixtures/deco-tail-test10/score.json`。
- 判据：`LiveHarness` 自带 1 条冒烟用例（普通单键被按下后 `Result != None`），且 `DecorationTailFromChartTests` 能用它改写且仍全绿。

### WP1 `TailKeyLifetime`（主体）

- 新增 `Assets/Game/Runtime/Sekai/View/TailKeyLifetime.cs`，纯函数形式，输入只有三个：
  - `tailTime`（尾键的谱面时间）、`now`（当前音乐时间）；
  - `NoteResult result`（`None` 表示还没判）；
  - `bool barEnded`（条是否已经结束，由 `LongNote` 自己的收尾通道给出）。
- 输出枚举 `TailKeyPhase { Hidden, Following, SlidingOut, Retired }`，转换规则：
  - `now < tailTime - lead` → `Hidden`；
  - `now` 落在 `[tailTime - lead, tailTime + grace)` → `Following`；
  - 已出窗口且 `result == None` → `SlidingOut`（继续下移，等判定或兜底超时）；
  - `result != None` 或 `barEnded` → `Retired`。
- 进入 `SlidingOut` 的时长必须与现有观感一致（取当前 `ShouldDeferViewRemoval` 的实际窗口，不许拍脑袋改数值）。
- 判据：不引用 `NoteBase` 任何与视图相关的成员；`grep -c 'Note.State' TailKeyLifetime.cs` 为 0。

### WP2 视图层改用它

- `LongNoteView` / `NoteLineView` 里所有「尾键还在不在、要不要继续移动」的判断改为读 `TailKeyLifetime.Of(...)`。
- 删除 `NoteBase` 上六个开关；六个音符类的 `State` override 恢复统一行为（保存一份 `git show HEAD:<file>` 以便对照）。
- 判据：`grep -rn 'KeepsViewUntilParentEnds\|ShouldDeferViewRemoval\|onRetireView\|SuppressView\|IsViewConsumed\|MarkViewConsumed' Assets/Game` 为 0（`SuppressJudgmentEffect` 不在删除之列，它是特效开关）。

### WP3 用例迁移与新增

- `LongNoteViewLifetimeTests` 的 5 条改为直接驱动 `TailKeyLifetime`，**用例数不得减少**。
- 新增 2 条：`AClearedTailKeyNeverComesBack`（判掉之后不会因为状态回摆重新出现）、`ATailKeyKeepsSlidingWhileUnjudged`（到线未判继续下移，直到兜底）。
- 判据：`LongNoteViewLifetimeTests` ≥ 7 条全绿。

## 5. 硬性约束

1. **不改判定数值**：窗口、尾判毫秒表、3 帧宽限期、松手第 3 帧一律不动；只改「谁决定显示」。
2. 一次只切一个视图，切完立刻全量 + 真机一小段。
3. 绞杀者模式：新实现先并行存在，逐处切换，最后删旧分支。
4. 每个工作包一个提交，提交信息写「行为不变」或「行为变化点」。
5. 不引入新依赖，不做 UI 翻新。

## 6. 验收

1. headless 全量 EditMode 全绿，条数 **不少于 434**（430 + 新增），报告 `LastWriteTime` 是当轮；
2. 至少一条变异实验：把 `SlidingOut` 改成 `Retired`，`ATailKeyKeepsSlidingWhileUnjudged` 必须变红，再恢复；
3. 真机一小段（用 `Tools/push-device-test-chart.ps1` 推测试谱）：普通长条、划键尾长条、friction 尾长条、装饰条（Guide）各一条，确认尾键出现/下落/回收时机与改前一致；
4. 曲库门禁（BOF 自动）仍为 `1,000,000 / S / 1669`。

## 7. 报告格式

- 改了什么（文件 + 行数）；
- 行为不变 / 行为变化点（有变化必须给出前后各一帧的描述）；
- 用例增删（数字与名字）；
- headless 结果与报告时间戳；
- 真机四个片段各一句结论；
- 分支名与提交列表。

## 8. 相关文档

- [判定通道与解耦收尾](judgment-channel-brief.md)（§4 WP3 是本任务书的前身，§5.1 是解析度更高的判据）
- [判定与窗口](../reference/judging.md)
- [长条 v3 交接](long-hold-v3-handoff.md)
- [测试与走查](testing.md)