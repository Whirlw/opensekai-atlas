# 任务：把长条的「判定」从条上拆下来（部件 1）

> 这份文档是给**外部实现者**的任务书。读完这一份即可开工，不需要先读别的。
> 与之配套的背景文档是 `docs/operations/judgement-input-split.md`（记录了这一轮之前的所有尝试与结论），
> 遇到「为什么不能那样做」时去那里查。

- 仓库根：`E:\DeepseekHarness\OpenSekai`
- 引擎：Unity 6000.6.7f1（C#，目标 Android / IL2CPP）
- 基线提交：`f210572`（开始工作前先确认 `git log --oneline -1` 与之一致或更新）
- 基线测试：EditMode `510 总数 / 506 通过 / 0 失败 / 4 跳过`

---

## 0. 一句话目标

`LongNote` 现在同时承担**生命周期**和**判定**两件事。把判定那半拆到独立的对象上，
使 `LiveHoldCoordinator` 能每帧独立喂判定，从而让长条**离开按下通道**（候选集只收
「普通头 / 普通键 / 普通划键」）。

拆完之后要达到的规则（用户在 2026-11-10 定稿）：

1. 长条只保留生命周期：`Wait → Scrolling →（判定线）InputBegan/Release → …→ Last → Done`
2. 任何手指都能判中间点和尾键，**没有归属、没有竞争**
3. 普通头按 1.5 车道容差判（等价于普通键）
4. 只有普通头 / 普通键 / 普通划键走「每指每帧最多判一条」，其余音符完全不受这个流程影响

---

## 1. 现状（已核实的代码事实）

### 1.1 状态机枚举

`Assets/Game/Runtime/Sekai/Live/NoteState.cs`

```csharp
public enum NoteState { Wait = 0, Scrolling = 1, InputBegan = 2, Input = 3, Release = 4, Last = 5, Done = 6 }
```

与规格的对应关系（**易错点**，2026-11-10 已把 `Playing` 改名成 `Scrolling` 就是为了消除这个歧义）：

| 规格里的状态 | 代码里的枚举 |
| --- | --- |
| Wait（未出现） | `Wait` |
| Scrolling（下落中，未到线） | `Scrolling` |
| Playing（正被按住） | `InputBegan` / `Input` |
| Last（松手宽限 3 帧，灰） | `Last` |
| Release（未按住/已断，灰） | `Release` |
| End（结束） | `Done` |

### 1.2 `LongNote` 现在一帧做五件事

`Assets/Game/Runtime/Sekai/Live/LongNote.cs` 里 `Judgment(ref LiveTouch touch, float lane)`（第 563 行起）：

1. **头部结算**：`JudgeInfo.result == None` 时 `LiveConfig.CalculateLongStartNoteResult(this, touch.musicTime)` → 写 `JudgeInfo`，`State = InputBegan`
2. **按下记账**：`RecordHoldingInput(touch.fingerId, touch.musicTime)`、`_headTouched = true`、`MarkPressedThisFrame()`
3. **延迟清零**：`OffsetJudgeTime = 0f`
4. **内部点**：`ConnectionNoteJudgment(ref touch, lane)`（第 1397 行，遍历 `NoteList[1..Count-2]`）
5. **尾键**：`childNote.IsJudgment(...) && childNote.Judgment(...)` → 成功则 `State = Done`

其它相关成员（行号以 `f210572` 为准）：

| 成员 | 位置 | 作用 |
| --- | --- | --- |
| `HasFinger` | `LongNote.cs:518` | `_holdingTouchId >= 0`，唯一事实源 |
| `IsHeld(float)` | `LongNote.cs:519` | 现在直接 `return HasFinger` |
| `IsHeldByAnotherFinger(int,float)` | `LongNote.cs:536` | 头部抢占保护 |
| `KeepAlive(float)` | `LongNote.cs:639` | 续命 |
| `ShowsAsReleased` | `LongNote.cs:715` | `Release \|\| Last` → 视图变灰 |
| `RefreshHeldInput(ref touch, float)` | `LongNote.cs:1390` | 协调器用：记账 + `RestoreHeldState` + 内部点 |
| `RestoreHeldState()` | 约 1376 | `Release \|\| Last → InputBegan` |
| `ConnectionNoteJudgment(ref touch, float)` | `LongNote.cs:1397` | 内部点 + 尾键 |
| `IsJudgment(ref touch, float)` | `LongNote.cs:1443` | 车道 + 时间门 |
| `HoldGraceFrames` | `LongNote.cs:1359` | `const int = 3` |
| `ExpireTail(float)` | 见 `LiveHoldCoordinator.cs:270` 调用 | 兜底终结 |
| `AddHoldCombo(LongHoldCombo)` | `LongNote.cs:226` | 内部点挂到条上，并复制车道 |

### 1.3 每帧谁在喂

`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs` 的 `Judgment()`（约 620–700 行）顺序固定：

```
_connectionNoteList.Clear()
JudgeDueComboPoints()            ← 内部点：每帧对每个活跃触摸，存在即判，不占名额
AllocateInputJudgableEachNotes() ← → LiveCandidatePipeline.Collect
OverlapJudgmentSorting()         ← 排序（rank → 时间 → 距离）
LeaveNotesFastGeneration()
PickUpNotBeginInputNotes()
RefreshSustainedHolds()          ← → LiveHoldCoordinator.Refresh
EvaluateJudgment()               ← 逐个候选调 note.Judgment
```

关键：**长条的每帧判定目前来自 `EvaluateJudgment()`**（候选通道）。`RefreshSustainedHolds`
只做「续命 + 内部点 + 尾键收尾」，**不做头部结算**。

### 1.4 协调器现在做什么

`Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs`

| 成员 | 位置 | 作用 |
| --- | --- | --- |
| `Refresh(...)` | 114 | 每帧入口 |
| `CoverHoldsWithAnyFinger(...)` | 170 | 覆盖检测、划动 delta 汇总、`KeepAlive`、`TailBehaviour.TryFinish`、`ExpireTail` |
| `TryRefreshFromOwner(...)` | 274 | 持有者在车道内 → `RefreshHeldInput` |
| `AdoptHoldFromAnyFinger(...)` | 310 | 换手 |
| `AssignContinuingHolds(...)` | 78 附近 | 每帧重算 `_holdInputNotes` 归属 |

### 1.5 候选通道现在收什么

`Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs` → `Collect(...)`（356 行）。
目前收：普通键、划键、长条、连接键、搓键、隐藏键、尾键、装饰条；
**已经排除**：`LongHoldCombo`（内部点，见提交 `dc3e107`）。

`NoteBase.ClosesSlotAfterPress`（`NoteBase.cs:612`，已改 `virtual`）：

```csharp
internal virtual bool ClosesSlotAfterPress => State == NoteState.Done || Result != NoteResult.None;
```

`LongHoldCombo` 覆写为 `false`（提交 `c42fb7e`）：中间点不占手指这一帧的名额。

---

## 2. 已经失败的做法（**不要重复**）

这两档在 2026-11-10 试过，代码已回退，数字如下：

| 尝试 | 改动 | 结果 |
| --- | --- | --- |
| 并存 | `TryRefreshFromOwner` 把 `held.RefreshHeldInput(...)` 换成 `held.Judgment(...)` | 510 /**503** / **3 失败**：`OverlappingTapDoesNotConsumeTheHeldNotesDeferredCandidate`×2、`SustainedHoldOnlyRefreshesWhileTheHolderStaysInLane(4.5,True)` |
| 一并摘除 | 上一步 + `Collect` 跳过 `LongNote` | 510 /**468** / **38 失败**（`AuthoredCrossingAllowsFollowingOrExchangingFingers` 全族、`LiveLogicCharacterizationTests` 一条等） |

原因（这就是本任务存在的理由）：

- `LongNote.Judgment` 同时做头部结算、记账、延迟清零、内部点、尾键。放进协调器 = 把按下通道的语义
  复制一份；两条路并存就**同帧双判**（延迟记账被打乱 → 3 红）。
- 只留协调器 → 没被任何手指覆盖的长条整帧拿不到 `Judgment`，头部结算停摆（→ 38 红）。

因此：**必须先拆对象，再换入口。** 换入口本身不是任务，拆对象才是。

---

## 3. 要做什么

### 3.1 拆分目标

新增一个「头部」对象（建议名 `LongNoteHead`，放在 `Assets/Game/Runtime/Sekai/Live/`），
承载从 `LongNote` 搬出的判定职责：

| 搬走的东西 | 搬到哪 |
| --- | --- |
| 头部结算（`CalculateLongStartNoteResult` → `JudgeInfo`、`PublishesHeadMiss` 的 Miss） | `LongNoteHead` |
| `RecordHoldingInput` / `_headTouched` / `MarkPressedThisFrame` / `OffsetJudgeTime = 0` | `LongNoteHead`（但写回条的生命周期字段时仍走条的接口） |
| 内部点遍历（`ConnectionNoteJudgment` 的循环） | `LongNoteHead`（调用 `LongHoldCombo` 已有的判定） |
| 尾键判定入口 | `LongNoteHead`（内部仍委托 `TailBehaviour`） |

留在 `LongNote`：

- `Wait/Scrolling/InputBegan/Input/Last/Release/Done` 状态机
- `KeepAlive`、宽限期 `HoldGraceFrames`、`ShowsAsReleased`、`ExpireTail`
- `HasFinger` / `HoldingTouchId` / `AssignHoldingInput`
- 视图通知（`OnSpawnNote`/`OnUnSpawnNote` 已有桥接，不要动）

**约束：外部可见行为必须逐帧一致。** 这是纯结构重构，不是重写语义。

### 3.2 拆分之后才允许的第二步

只有第 1 步全绿之后，才做：

1. `LiveHoldCoordinator` 每帧对「已被覆盖」的长条调 `LongNoteHead.Judgment(...)`
2. `LiveCandidatePipeline.Collect` 跳过 `LongNote`
3. 再跑全量

第 2 步若仍红，**回退第 2 步**（保留第 1 步的结构拆分，它本身应该是零行为变化的）。

### 3.3 不建议一次做掉的东西

- 搓键（`FrictionNote`）/ 隐藏键 / 连接键的「自由通道」——它们比长条简单，但排在长条之后
- `LiveLogic` 里 `JudgeDueComboPoints` 的规则**不要改**（已定稿：判定区间内，所归属的长条状态出现过
  `InputBegan`/`Input`/`Last` 即视为「在玩」；存在即判；重叠各判各）

---

## 4. 工具链（在这台机器上怎么跑）

### 4.1 编译校验（不启动 Unity，秒级）

```powershell
$dotnet='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe'
$csc='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$rsp=Get-ChildItem 'Library\Bee\artifacts' -Recurse -Filter 'OpenSekai.Game.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
& $dotnet $csc "@$($rsp.FullName)" -nologo 2>&1 | Select-String 'error CS'
```

换成 `OpenSekai.Tests.EditMode.rsp` 即可校验测试程序集。

### 4.2 让 Unity 真正重编

编辑器有时吞掉重编请求。稳妥顺序：

1. `unity_command {"command":"editor","args":["focus"]}`
2. `unity_eval`：
   `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` →
   `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`
3. 轮询 `Library\ScriptAssemblies\OpenSekai.Game.dll` / `OpenSekai.Tests.EditMode.dll` 的
   `LastWriteTime`，**变新了才算编译完成**（曾出现测试程序集滞后一小时的情况）

### 4.3 跑测试

`unity_eval`：`return OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<名字>.xml");`

然后读 `TestResults\<名字>.xml`，正则取
`testcasecount / total / passed / failed / skipped`。
失败用例名从 `<test-case ... fullname="..." result="Failed">` 取。

---

## 5. 验收标准

### 5.1 硬门禁（必须全满足）

| 项 | 标准 |
| --- | --- |
| EditMode 全量 | `total >= 510`、`failed == 0`、`passed == total - skipped`、`skipped == 4` |
| 编译 | `OpenSekai.Game` 与 `OpenSekai.Tests.EditMode` 均无 `error CS` |
| 行为 | 不新增/删除测试断言来「让红的变绿」；只允许**新增**测试 |
| 提交粒度 | 结构拆分（3.1）一个提交；换入口（3.2）另一个提交，且可独立回退 |

### 5.2 必读的红线测试（改坏就会被它们抓住）

`Assets/Tests/EditMode/Live/` 下：

- `LongHoldPipelineTests` — 长条流水线（持有、换手、宽限、重叠、尾判）
- `LongHoldOwnershipTests` — 归属保留语义
- `LongNoteGraceTests` — 丢手 3 帧宽限（`Last` 灰 → `Release`）
- `LiveLogicInputTests` — 按下的整体行为（含 `AuthoredCrossingAllowsFollowingOrExchangingFingers`）
- `LiveLogicCharacterizationTests` — 行为快照
- `NotePressDispositionTests` — 谁占手指这一帧
- `FlickEndBehaviourTests` / `TailReleaseLaneTests` / `DecorationTailJudgmentTests` — 尾键三种形态

### 5.3 真机验收（结构拆分通过后、换入口通过后各一次）

三张谱：普通 4/4、长条密集（BOF）、变速谱。逐项看：

1. 长条按住不断、换手不断
2. 松手 → 条立刻变灰 → 3 帧后消失（不是同帧消失）
3. 宽限期内手指回来 → 条恢复亮色并继续
4. 两条长条重叠时，各自内部点都在涨（不互相打断）
5. 划键反划封顶 Great，尾判在松手后上屏

---

## 6. 交付物

1. 代码：`Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs`（新增）+ `LongNote.cs` / `LiveHoldCoordinator.cs`
   / `LiveCandidatePipeline.cs` 的必要改动
2. 新增测试：至少覆盖「头部结算仍在同一帧生效」「内部点归属不变」「尾键入口不变」
3. 一份 `docs/operations/long-note-head-split-report.md`，写清：
   - 每个搬走了什么、留在原处的是什么（表格）
   - 全量测试前后数字
   - 有哪几条测试的断言被改动过（如果有，逐条说明为什么不是放宽标准）
   - 真机三张谱的结果
