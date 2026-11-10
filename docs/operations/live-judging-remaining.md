# 判定剩余三件事（交给外部实现者）

状态：2026-11-10，`main = dd1d9ad`，EditMode `522 / 518 通过 / 0 失败 / 4 跳过`。
前序：[judgement-input-split](judgement-input-split.md)（部件 1 已完成头部拆分）、[head-decoupling](head-decoupling.md)、[interior-point-window](interior-point-window.md)。

本文档是三个独立任务的合并任务书。**必须一次只做一个任务，各自单独提交、单独跑全量测试**，不要合并成一个提交。

---

## 0. 环境与工具链

| 项 | 值 |
| --- | --- |
| 仓库 | `E:\DeepseekHarness\OpenSekai`（Unity 6000.6.7f1，C#） |
| 分支 | `main`，提交到 `main` 并 push |
| 编译自检（不用开 Unity） | `& 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe' 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll' "@<最新的 Library\Bee\artifacts\**\OpenSekai.Game.rsp>" -nologo`，过滤 `error CS` |
| Unity 侧重编 | `unity_eval` 端口 7800：`AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`；**先 `unity_command editor focus`**，否则编译请求会被吞 |
| 确认重编落地 | 看 `Library\ScriptAssemblies\OpenSekai.Game.dll` / `OpenSekai.Tests.EditMode.dll` 的 LastWriteTime 变新 |
| 跑测试 | `unity_eval`: `return OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<名字>.xml");`，然后解析 XML 的 `testcasecount/total/passed/failed/skipped` |
| 出包 | `unity command build run --target Android --outputPath "Builds/<名字>-2026-11-10/OpenSekaiAtlas.apk" --confirm --result-only` |

**纪律**：`git diff --stat` 每次改完立刻看，允许的行数是个位数到几十；不允许整文件重写。改文件用锚定的整块替换，**不要按行号**（历史上三次把括号改坏）。

---

## 1. 任务 A：长条体不再表示「按住」（部件 3）

### 目标

`LongNote` 的 `NoteState.InputBegan` / `NoteState.Input` 只用于「某个具体音符被按住」，**条体本身**只用 `Wait / Scrolling / Last / Release / Done` 表达外观，用 `HasFinger`（`LongNote.cs:491`）表达「有手指在条上」。

### 现状（精确行号）

条体写 `InputBegan/Input` 的地方：

- `Assets/Game/Runtime/Sekai/Live/LongNote.cs:568`
- `Assets/Game/Runtime/Sekai/Live/LongNote.cs:1327`
- `Assets/Game/Runtime/Sekai/Live/LongNote.cs:1418`（`RestoreHeldState()`）

条体读自己的 `InputBegan/Input`：

- `LongNote.cs:103`（`WasHoldingWhenTerminated` 赋值）
- `LongNote.cs:399`（refresh 分支）
- `LongNote.cs:458`、`:527`、`:955`、`:1043`、`:1121`、`:1432`

外部按「条是否被按住」读它的地方（这些应改成 `HasFinger`）：

- `Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs:240`
- `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:661`（中间点门禁，`Last` 也算「在打」，注意语义）
- `Assets/Game/Runtime/Sekai/LongTapEffectView.cs:151`
- `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs:83`、`:118`

### 要做的事

1. 新增（或复用）条体的「按住」表达：`HasFinger` 已经是唯一真源，确认没有第二处再写 `_holdingTouchId`。
2. 删除上面三处写 `InputBegan/Input` 的语句，改为不改 `State` 只改 `HasFinger`（`RecordHoldingInput` 已经在做）。
3. 所有「条是否在按住」的判断改读 `HasFinger`；只有真正需要「这个音符自己的状态」的读点保留（`LongNoteHead` 内部对头音符的状态）。`LongNote.cs:661` 那条中间点门禁要保留 `Last` 参与，否则宽限期内的点会失效。
4. 条体进入 `Last` / 回到按住 / 离开的**视觉**必须不变（灰/亮由 `ShowsAsReleased` 决定，`LongNote.cs` 已按 `Release || Last` 实现）。

### 红线

- 不动 `FlickNote` / `FrictionNote` / `FrictionLongNote` 等**自身**的 `State` 用法（它们是真音符）。
- 不删除 `NoteState.InputBegan` / `NoteState.Input` 枚举成员（其它音符在用）。
- 不放宽任何现有测试断言；需要改的只是把「构造按住态」从 `State = InputBegan` 换成 `RecordHoldingInput(...)`。

### 验收

- 三条既有关键测试仍绿：`ALostFingerGreysTheBarAndReleasesThreeFramesLater`、`AFingerReturningInsideTheGraceTakesTheBarBack`、`HasFingerFollowsTheTouchIdOnly`。
- 新增至少 2 条：条体在按住期间 `State` **不再**是 `InputBegan/Input`；`HasFinger` 与 `HoldingTouchId` 一致。
- 全量不降（当前 `522 / 518 / 0 / 4`）。

---

## 2. 任务 B：按下通道只收三类音符（部件 4）

**风险最高，最后做。**

### 目标（用户原始规则）

1. 只有**普通头**（`LongNote.IsOrdinaryHead` 且头部未判）、**普通键**（非装饰 `NormalNote`）、**普通划键的按下步**（`FlickNote`）参与「每指每帧一条」。
2. 一帧里收到多个「按下」事件时：先各自列出能判的三类音符；互相重叠的按**到该音符中心的车道距离最小者**归给某一指（距离相等取 `fingerId` 小者），并从其它指的列表移除；然后每指只判自己列表里**最早**的一条。
3. 其它一切（中间点、尾键、连接键、搓/隐藏、隐藏摩擦、装饰）**完全不参与这套调度**，每帧独立判定。
4. 只有 `Began` 触发这套流程，`Moved` 不算（用户明确要求）。

### 现状

- `Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs`：`Collect` 仍把长条头、连接键、搓键、隐藏键、尾键一起收进 `CandidateNotes`；`Sort` 用 `CandidateRank`/`HeldRank`；`DropExtraFastGeneration` 里还有长条优先级。
- 中间点已经不进候选集（`c42fb7e`、`dc3e107`）。
- 「谁先判」目前由 `CandidateOrder` + `IsReservedForAnotherInput` + `ClosesSlotAfterPress` 混合决定。

### 已知地雷（务必先读）

`LongNote.Judgment` 一帧做五件事（头部结算 / `RecordHoldingInput` / `MarkPressedThisFrame` / `OffsetJudgeTime` 清零 / 内部点与尾键推进）。把条体整体移出按下通道会让这五件事没人做，实测 `510/468/38` 失败。**正确做法是先把这五件事拆到「条自己的每帧驱动」里，再移出候选集**，不要只改候选集。

### 红线

- 不动 `LiveHoldCoordinator`（长条续命/换手/尾 sweep 归它）。
- 不改现有契约测试的**断言**；只允许把测试的「驱动方式」对齐新入口（例如从 `Stage(logic,"Judgment")` 改成同时驱动协调器）。
- 一次只改一个子步，每步全量测试。

### 验收

- 全量不降；新增至少 3 条：两指同时点同一个普通键只判一次；两指分别点在两个普通键上各判一次；一条长条被按住时另一根手指划到它上面不抢候选。
- 真机：普通谱 + 长条密集谱 + 变速谱各一遍。

---

## 3. 任务 C：`NoteSpeedRatio.Resolve` 的护栏测试

只加测试，不改生产代码（除非测试证明是 bug —— 那先报告，不要顺手改）。

- 文件：`Assets/Game/Runtime/Shared/Live/NoteSpeedRatio.cs`，入口 `Resolve(MusicScoreInfo[] scoreInfos, float currentProgress, float noteProgress, float fallbackSpeedRatio)`。
- 构造三段：`speedRatio` 1.0 / 2.0 / 0.5（用 `MusicScoreInfo(bar, barProgress, time, bpm, timeSignature, speedRatio, seVolume)`）。
- 覆盖：音符正好落在段边界；音符跨两段（加权平均）；`currentProgress` 与 `noteProgress` 同段；空数组回退 `fallbackSpeedRatio`。
- 断言写「期望的加权结果」，注释里给出算式。

---

## 4. 我方验收方式（不要替他做）

拿到提交后我按这个顺序复核，所以交付物必须可复核：

1. `git diff --stat <父提交>..<提交>` —— 只碰任务允许的文件，行数合理。
2. 红线文件 diff 必须为空：`LiveHoldCoordinator.cs`（任务 B）、`FlickNote.cs` / `FrictionNote.cs`（任务 A）。
3. 自己重跑一遍全量 EditMode，不看提交者的数字。
4. 逐条比对新增测试是否真的覆盖任务书里的断言。
5. 真机三谱（普通 / 长条密集 / 变速）。
6. 报告里必须写明：改了什么、为什么、失败过什么、还剩什么没做。

---

## 附：任务 A 首轮失败的根因（2026-11-10 定位，第二轮据此修）

首轮 `524 / 517 / 3` 的三条红都是 `LiveLogicInputTests.AuthoredCrossingAllowsFollowingOrExchangingFingers`，症状统一：中间点期望 `JustPerfect`、实得 `None`。

根因不是门槛写错，而是**「按住」这件事在两个世界里表达不同**：

| 世界 | 表达方式 |
| --- | --- |
| 生产 | `_holdingTouchId >= 0`（即 `HasFinger`），由 `RecordHoldingInput` / `AssignHoldingInput` 写入 |
| 该测试 | 只写 `lastInputTime`（`LiveLogicInputTests.cs:104`、`:246`），**从不设 touch id** |

`HasFinger` 是 touch id 的派生值，所以测试里它恒为 `false`。条体退役 `InputBegan/Input` 之后，两处「在打」的门禁都读 `HasFinger`，于是中间点永远不被判 —— 三条红由此而来，与候选排序、窗口都无关。

第二轮请按下面三点改：

1. **生产门禁**（两处，都要保留 `Last`，宽限期内的中间点必须仍能判）：
   - `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs:118`
     `if (_bar.State != InputBegan && != Input && != Release) return;`
     → `if (!_bar.HasFinger && _bar.State != NoteState.Last && _bar.State != NoteState.Release) return;`
   - `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:661`（中间点记忆门禁）
     → `if (bar != null && (bar.HasFinger || bar.State == NoteState.Last || bar.State == NoteState.Release))`

2. **测试的按住构造**（允许改，属"构造被测状态"）：
   `LiveLogicInputTests.cs:104` 与 `:246` 在写 `lastInputTime` 的同一处，一并写持有事实：
   `n.RecordHoldingInput(finger, time);`（手指编号沿用该测试循环里的 `finger`/实际注入的 id）。
   **不要**改这些测试里任何一条断言。

3. **验收顺序**：先只跑 `LiveLogicInputTests`，这 3 条绿了再跑全量；全量回到 `522 / 518 / 0 / 4` 才提交。

另外两条不要碰：`LongNote.cs` 里 `WasHoldingWhenTerminated` 必须能从 `HasFinger` 推出（或由 `RecordHoldingInput` 置位），不能变成恒 `false`；`FrictionLongNote` 等自身状态的断言原样保留。
