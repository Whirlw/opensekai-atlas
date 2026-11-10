# 中间点判定窗口交付报告

日期：2026-11-10。权威任务书：[interior-point-window.md](interior-point-window.md)。

## 改动与边界

生产代码仅改 `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs` 的 `ConnectionNoteJudgment` 中间点循环：读取一次 `MusicScore.CurrentFrameInfo.time`，只在 `-0.125 <= now - note.MusicScoreInfo.time <= 0.1417` 时调用原 `Judgment`。常量复用 `LongEndJudgement.EarlyGoodMs / LateGoodMs`，不放宽窗口。

现场确认 `NoteBase.JudgeOffsetAt(time)` 返回 `time - MusicScoreInfo.time`。本次直接写时间差，避免判定表辅助函数的方向约定混用。没有使用 `Max(touch.musicTime, frameTime)`：触摸戳不能使已过期或未到窗口的点获准。

选择任务书允许的全局帧时钟方案，避免扩大生产改动范围。`MusicScoreInfo` 实际是 struct，未初始化时安全退到默认帧时间 0，不存在空引用；不以触摸戳兜底。非有限时间无法通过区间比较。测试保存、初始化并恢复全局帧，避免污染后续用例。

既有车道覆盖、长条状态前置和单点 Judgment 均保留。未改 `_barWasInPlay`、`JudgeDueComboPoints`、长条生命周期和三帧宽限、划键尾判、谱面转换、`ConnectionNote` 或 `LongHoldCombo` 的语义。

## 14 条既有契约测试

所有原断言保留，只改驱动帧时钟；触摸时间戳仍为 0。摘要中的文件位置有一处与现场不同：第 2 条实际位于 `LongNoteHeadTests.cs`，其中点时间为 1 秒。

| # | 用例 | 时钟修正与保留断言 |
|---|---|---|
| 1 | `SustainedHoldStillJudgesItsInteriorTicks` | Refresh 前将 logic 与全局帧都设为 0.125；保留 JustPerfect 和持握者断言 |
| 2 | `SustainedRefreshKeepsOffsetAndInteriorParentWithoutRepublishingHead` | 两次直接 RefreshHeldInput 前设全局帧为 1；保留 offset、父条、角色、JustPerfect、点回调 1 次、头回调 0 次 |
| 3 | `OverlappingHoldsJudgeBothInteriorComboPoints(Long, false, false)` | Judgment 前 logic 与全局帧为 0.125；保留两个 JustPerfect 和各自持握者 |
| 4 | `OverlappingHoldsJudgeBothInteriorComboPoints(Long, true, false)` | 同上，反向输入顺序 |
| 5 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionLong, false, false)` | 同上，摩擦条 |
| 6 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionLong, true, false)` | 同上，摩擦条反向输入 |
| 7 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionHideLong, false, false)` | 同上，隐藏摩擦条 |
| 8 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionHideLong, true, false)` | 同上，隐藏摩擦条反向输入 |
| 9 | `OverlappingHoldsJudgeBothInteriorComboPoints(Long, false, true)` | 同上，嵌套车道 |
| 10 | `OverlappingHoldsJudgeBothInteriorComboPoints(Long, true, true)` | 同上，嵌套车道反向输入 |
| 11 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionLong, false, true)` | 同上，摩擦条嵌套车道 |
| 12 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionLong, true, true)` | 同上，摩擦条嵌套车道反向输入 |
| 13 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionHideLong, false, true)` | 同上，隐藏摩擦条嵌套车道 |
| 14 | `OverlappingHoldsJudgeBothInteriorComboPoints(FrictionHideLong, true, true)` | 同上，隐藏摩擦条嵌套车道反向输入 |

## 新增两条回归测试

文件：`Assets/Tests/EditMode/Live/InteriorPointWindowTests.cs`（附 Unity 生成的 `.meta`）。

- `AnExpiredInteriorPointIsNotJudgedByALatePress`：真实摩擦条中放入 0.125、0.25、0.375 秒三个积压点，以及 1 秒当前点。更新一次使点进入 Last，再以帧 1 秒、触摸戳 1 秒调用摩擦条 Judgment 和 RefreshSustainedHolds，并再次刷新。三个过期点均保持 `Result == None` 且 `State != Done`；当前点为 JustPerfect；点判定回调总计严格为 **1**。这给出 friction 不再整批补判的数字证明：积压 3 点、当前 1 点 → 仅 +1。
- `ATouchStampedEarlyStillJudgesAPointThatHasArrived`：先启动摩擦条，再推进到 0.125 秒点。分别以触摸戳 0、-1 驱动，帧始终为 0.125；断言点 JustPerfect、Done、重复刷新后回调仍只有 1 次。0 恰在 125 ms 早窗边界上，额外 -1 场景确保误用触摸时间戳时确实会失败，不增加测试条数。

窗口外选择“不判、不在此处直接 Miss”，与任务书第一种允许语义一致；既有后续执行与回收路径仍负责 Miss/清理，本次不改其行为。

## 验证

最终全量 EditMode：**total 518 / passed 514 / failed 0 / skipped 4**。对比任务书基线 516 / 512 / 0 / 4，恰好新增两条通过用例。

验收 XML：[interior-point-window-final.xml](../../TestResults/interior-point-window-final.xml)，根节点 `test-run` 的开始/结束时间为 2026-11-10 15:04:53Z / 15:04:55Z。两条 `InteriorPointWindowTests` 均为 Passed，14 条既有契约用例全部通过。XML 位于测试输出目录，不作为源代码提交。

验证过程：

1. 经端口 7800 的已运行 Unity 6000.6.7f1，执行 `AssetDatabase.Refresh(ForceSynchronousImport)`，再执行 `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`。测试 DLL 从最初 14:51:19 更新，最终为 **15:03:50**（客户端同为 UTC）。
2. 等待导入/重编结束后，以指定 DotNetSdk 的 dotnet、SDK 8.0.318 的 csc，分别使用 `Library/Bee/artifacts/1300b0aE.dag/OpenSekai.Game.rsp` 与 `OpenSekai.Tests.EditMode.rsp` 编译：**两者 exit 0，无编译诊断**。清缓存刚启动时的首次探测曾因引用 DLL 尚未生成而失败；等待重编完成后重新校验通过。
3. 通过 `OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/interior-point-window-final.xml")` 跑全量，读取根节点结果，并核对新增两条 Passed。
4. `git diff --check` 通过。最终生产代码只改上述一个文件的循环门禁。

首轮测试曾为 518 / 513 / 1 / 4，唯一失败为新增早触摸戳用例缺少摩擦条启动步骤（尚处于 Scrolling）；补齐启动步骤后，保留全部断言，得到上述最终结果。已有 14 条用例在首轮亦均通过。

## 遗留风险与工作区说明

- 未跑真机 friction 谱面；本次证据为真实摩擦条 Judgment、持续刷新路径和点回调计数。真机仍应核对连击逐点增长、换手、普通长条和划键尾判。
- 全局时钟方案依赖运行时在判定前更新 MusicScore 帧。当前任务书允许该方案；独立调用者和测试必须显式设置帧，默认 0 不能代表任意当前时间。多会话的全局帧隔离不在本次范围。
- 本次新增的重点是过期积压与触摸时钟回归，没有声称穷尽所有浮点边界、车道形变与设备输入组合。
- 开始时工作区干净，实际 HEAD 为 `09e5c79`，实际分支为 `codex/long-note-head-split`，与用户描述的 `publish` 不同。本次保留现有分支，没有切换分支、提交或推送；若后续推送，用户指定目标为 `origin/main`。
