# 双长条断连：交给外部执行者的任务书

> **状态：已完成并合入（历史文档，不再更新）。** 现行行为见 `tail-classes.md`。

本文是一份**自包含**的任务书：执行者不需要了解此仓库的历史，只按本文即可开始工作。

- 目标读者：独立实现者（可以是另一个 AI 代理或工程师）
- 前置条件：能读写 `E:\DeepseekHarness\OpenSekai`，能启动 Unity 6000.6.7f1 编辑器
- 最后更新：2026-11-03

## 1. 一句话目标

按住一条长条（LongNote）时，**另一根手指按在同一车道或相邻的重叠车道上**，长条不能断连；两条长条**中途交叉**时也不能断连。修复必须在真机上由人手动确认。

## 2. 项目与运行环境

- 仓库：`E:\DeepseekHarness\OpenSekai`，分支 `codex/player-lite-refactor`，本次任务的起点提交 `94330df`。
- 引擎：**Unity 6000.6.7f1**，目标平台 **Android**（arm64，minSdk 26，IL2CPP + Managed Stripping Low）。
- 代码布局：`Assets/Game/Runtime/{Sekai,OpenSekai,Shared,Platform}`（三个程序集 `OpenSekai.Game` / `OpenSekai.App` / `OpenSekai.Editor`），测试在 `Assets/Tests/{EditMode,PlayMode}`。
- 编辑器内的辅助菜单（都在 `OpenSekai/` 下）：`Library/Play Local Baseline Automatically`（跑门禁）、`Dev/Run EditMode Tests`、`Dev/Install Test Chart`。

## 3. 现象（用户实测描述）

> 「还是断连。只要有一根手指按在重叠处，就立刻断。两根长条中途交叉也会断。」

复现的三个场景：

1. 两条长条**车道重叠**（同一车道或相邻车道起手），左手指按住第一条，右手指按到重叠区域 → 第一条立刻断。
2. 两条长条**中途交叉**（起手车道互换）→ 至少一条断。
3. 单指按长条、另一指按**普通音符**落在重叠车道 → 也会断（用户未逐项枚举，但现象描述覆盖此情形）。

**关键时间特征**：「立刻」= 按下去当帧或 1–2 帧内断，不是过一会儿才断。

## 4. 每帧判定管线（必读）

`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`，`Judgment()` 每帧固定顺序：

```
AllocateInputJudgableEachNotes();   // 把 note 分配成每根手指的候选
OverlapJudgmentSorting();           // 每手指候选排序 + 同判定时间去重
LeaveNotesFastGeneration();         // 剪枝（保留快判的候选）
PickUpNotBeginInputNotes();         // 未起手的长条
EvaluateJudgment();                 // 每手指判第一条候选，判完 break
```

配套文件：

| 文件 | 作用 |
| --- | --- |
| `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs` | 判定主循环、候选分配、归属、剪枝 |
| `Assets/Game/Runtime/Sekai/Live/LongNote.cs` | 长条音符：`Judgment`、`IsJudgment`、`lastInputFrame`、`_holdingTouchId`、释放判定 |
| `Assets/Game/Runtime/Sekai/Live/NoteBase.cs` | 音符基类：`LaneStartF/LaneEndF`、`JudgeLaneStart/End`、`JudgedLaneStart/End`、`LaneDistance` |
| `Assets/Game/Runtime/Shared/Live/CandidateOrder.cs` | 候选排序（result → time → distance，持握优先） |
| `Assets/Game/Runtime/Shared/Live/CandidateOwnership.cs` | 跨手指候选归属（持握优先 / 距离） |
| `Assets/Game/Runtime/Shared/Live/TouchLane.cs` | 触点 → 车道（小数车道、容差、`Outside = -1`） |

## 5. 已经查明的机制（不要重复推导）

1. **`_judgmentNoteList` 不是每帧重建**：`LiveLogic.UpdateJudgmentNoteArray(note)` 只在「按时间窗口推进音符」的两个循环里被调用（`LiveLogic.cs` 约 506-517、519-568），条件 `note.State != NoteState.Done && !note.IsDecoration`。**一旦某条 note 被 `_judgmentNoteList.Remove`，要等那两个循环再次走到它才会被加回。**
2. **长条靠 `lastInputFrame` 续命**：`LongNote.cs` 约 326-331
   ```csharp
   if ((State == NoteState.InputBegan || State == NoteState.Input) && !IsDecoration
       && lastInputFrame + LiveConfig.noteTypeJudgeData.MissMesh < Time.frameCount)
       State = NoteState.Release;
   ```
   而 `lastInputFrame` 只在 `LongNote.Judgment` 与 `LongNote.AutoJudgment` 里刷新。
3. **`EvaluateJudgment` 一次按下只判一条**（按规则）：判完 `_judgmentNoteList.Remove(note)`、`input.CandidateNotes.Clear()`、`_usedFingerList.Add(fingerId)`、`break`。
4. **`LongNote.Judgment` 不校验 `fingerId`**：任何手指调它都会写 `lastInputFrame` 与 `_holdingTouchId`。

## 6. 三次已失败的尝试（含失败原因）

| 提交 | 改了什么 | 为什么没解决 |
| --- | --- | --- |
| `ffc42cc` | 候选排序与剪枝给「持握中的长条」优先（`CandidateOrder.Compare/KeepsFirst` 加 `isHeld`，`LeaveNotesFastGeneration` 保护持握方） | 只影响**同一根手指**的候选顺序与剪枝；跨手指的归属裁剪没管 |
| `9814956` | `CandidateOwnership.Loser(bool currentHolds, bool otherHolds, …)` + `LongNote.HoldingTouchId`，跨手指归属优先给持握方 | 归属修好了，但**长条仍会被 `_judgmentNoteList.Remove`**；且第二根手指仍会调 `LongNote.Judgment` 改写 `_holdingTouchId` |
| `94330df` | `LongNote.ShouldIgnoreTouch`（持有者新鲜期内忽略外来 touch）、`AllocateInputJudgableEachNotes` 跳过「别人在按」的长条、`EvaluateJudgment` 对未 Done 的长条不 `Remove` | 用户复测**仍然断**。说明断因不在这三处，或还有第四条路径（见 §8） |

三次都跑了 EditMode（当前 **232 passed**）与曲库门禁（`1,000,000 / S / 221`），全部通过——**自动打歌不经过输入路径，门禁无法暴露此缺陷**，只能人工手动验。

## 7. 复现与验证方法

1. 测试谱面：`Tools/fixtures/baseline_tap`（标题 `Test`，221 音符）。用编辑器菜单 `OpenSekai/Dev/Install Test Chart` 装进 `persistentDataPath/CustomMusicScores`，或 `Tools/push-device-test-chart.ps1 -Adb <adb> -Fixture Tools/fixtures/baseline_tap` 推到设备。
2. 编辑器内自测：Play → 曲库出现 `Test` → 点「手动」→ 用鼠标模拟双指不现实，**以设备为准**。
3. 设备：`unity build.run --target Android --outputPath Builds/<name>.apk --confirm true`，然后 `adb install -r`；触摸必须人工操作。
4. 现有测试基线：EditMode **232 passed / 0 failed**；跑法 `TestRunnerBridge.Run("EditMode", "TestResults/editmode.xml")`（编辑器开着时）或关掉编辑器后 `Tools\run-editmode-tests.ps1`。

## 8. 尚未验证的假设（建议从这里入手）

1. **`PickUpNotBeginInputNotes` 与 `_connectionNoteList`**：长条在 `State == Playing` 时是否仍被正确「拾起」；`ConnectionNoteJudgment` / `TryAddConnectionCandidate` 是否会把两条长条互相挤掉。
2. **`IsJudgment` 的判定区间随状态漂移**：`LongNote.IsJudgment` 用 `JudgeInfo.result == NoteResult.None ? JudgeLaneStart/End : JudgedLaneStart/End`；持握后区间换成 `JudgedLane*`（`LaneStartF - LaneOffset` / `LaneEndF + LaneOffset`），交叉或第二根手指介入时，持有手指的触点在下一帧可能落到区间外 → 该帧无候选 → `lastInputFrame` 不刷新。
3. **`ConvertLaneBy` 的镜像/偏移**：交叉时 note 的判定车道会被 `ConvertLaneBy` 改写，若同一根手指的 `InputTmp` 被重算，可能让 note 落到 `Outside`。
4. **`State == NoteState.Last`**：`LongNote.Judgment` 对 `Playing/Last` 走起手分支；如果断连发生在长条尾段，可能是 `Last` 分支的问题。

## 9. 约束（硬性）

- 不要改 public API 形状、不要重命名现有类型/成员（外部有测试与文档引用）。
- 不要动 `Assets/Graphics/**`、`Assets/Game/AssetBundles/**`、构建脚本、`ProjectSettings/**`。
- 不要为测试放宽生产代码可见性；需要时用 `InternalsVisibleTo`（已对 `OpenSekai.Tests.EditMode` 开放）。
- 不要提交 `Builds/`、`TestResults/`、`Library/`。
- 提交信息用英文，一次提交只说一件事。

## 10. 验收标准（全部满足才算完成）

**硬门禁**

1. EditMode 测试 ≥ 232 条且 **0 failed**；新增的判定规则必须有对应单测（把决策抽成纯函数再测，参考 `CandidateOrder`/`CandidateOwnership` 的做法）。
2. 曲库门禁：编辑器 Play → `OpenSekai/Library/Play Local Baseline Automatically` → 结算 `1,000,000 / S / 221`。
3. 真机人工验证（OPPO `PLQ110` / `3B164A00Y5600000`，或小米 `22041216C`）：
   - 两条车道重叠的长条同时按住，**两条都不中断**；
   - 两条长条中途交叉，**两条都不中断**；
   - 单指长条 + 另一指敲重叠处普通音符，**长条不中断**且普通音符正常判定；
   - 长条正常结束、正常 MISS 的行为未被破坏。

**质量判据**

- 断连的根因要能用一句话解释（「哪一帧、哪个列表/状态、谁把它改坏的」），并给出对应代码位置。
- 修复不能靠「多给几帧宽限」这类阈值妥协来掩盖；若必须放宽阈值，要在报告里写出数值依据。
- 不引入新的每帧分配。

## 11. 交付报告格式（六节，缺一不可）

1. **结论**：问题根因一句话 + 修复概述。
2. **分支与提交**：分支名、基点提交、每个提交的 hash 与一行说明。
3. **改动面**：`git diff --stat` 输出 + 逐文件说明改了什么、为什么。
4. **证据**：EditMode 结果（`total/passed/failed` 与报告路径）、门禁截图路径、真机验证的四个场景各一句话（谁按的、按在哪、结果）。
5. **风险与未覆盖**：明确写「未验证」的项，不要写「应该没问题」。
6. **遗留**：仍需后续处理的问题（若有）。

## 12. 相关文档

- [判定窗口与容差](../reference/judging.md)
- [测试与 CI](testing.md)
- [Android 构建与真机门禁](android-build.md)
- [触摸源抽象任务书](touch-source-brief.md)（同类交接文档，可作为格式参考）