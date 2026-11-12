# 条尾三类：谁影响谁

> 任务书 v2（2026-11-11 修订，依据外部评审）。v1 的三处判断已被代码推翻，见 §0。
> 分支 `codex/long-note-head-split`，HEAD `8906a59`，工作区干净。
>
> **2026-11-12 复核**：文件路径已按 P3-1 之后的目录更新（`Runtime/Sekai/Live/...` → `Runtime/OpenSekai.Game/Live/Notes/...`），但**文中 `:行号` 仍是 11-11 的旧行号**——`LongNote` 现在是四个 partial，行号普遍漂移几十行，请按锚点（方法名/字段名）定位，不要按行号跳。§3、§4、§10、§11 的结论仍然有效；§11.3（解绑没全部落地）与 §11.4（`State = Done` 的 17 个赋值点待确认归属）截至 11-12 **仍未完成**，与交接页 §4 的"未完成"是同一批事。

## 0. 修订记录

| v1 的说法 | 实际情况 | 出处 |
| --- | --- | --- |
| 抬手候选「只看条自己的车道」 | 用的是 `LongNote.ContainsLane()`，它按**头部是否已判**在 `JudgeLane*` / `JudgedLane*` 之间切换，两套都带容差。删掉显式兜底后范围仍可能超出可见条身 | `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs:1410` |
| 类 1（隐藏 / friction / friction-flick）在代码里是 `SelfJudgedTail`，合规 | 只有 **friction-flick** 走 `SelfJudgedTail`；普通 friction 尾与隐藏尾都走 `ReleaseTail`。且 `DetachStandaloneTail()` 对未结算的尾**写 Miss**，属「条影响尾」 | `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/TailBehaviours.cs:13`、`LongNote.cs:904` |
| 类 3 已合规 | `TickSwipeTail()` 仍用 `HasFinger` + `Last`/`Release` 决定是否清掉待结算划动，条体状态参与尾的结算 | `LongNote.cs:1040` |

另：`ShowsAsReleased` 不只被绘制读，还被**条体状态推进**（`LongNote.cs:401`）和**判定入口**（`LongNote.cs:1400`）读，改它等于改判定路径。

## 1. 用户定义（权威，不改）

| 类 | 尾的种类 | 条 → 尾 | 尾 → 条 |
| --- | --- | --- | --- |
| 1 | 隐藏、friction、friction-flick | 完全不影响 | 完全不影响 |
| 2 | 普通尾（抬手尾） | 不影响 | 尾判定后结束条 |
| 3 | flick 尾 | 只传递「可以判定了」 | 只结束条 |
| — | 装饰条 | 条自己走完 | 尾不参与判定 |

推论（v2 收窄，只保留能直接验证的）：

- R1 抬手判定只看**抬手动作**：落在该尾键的车道内、且落在该尾键判定时间窗内 → 进候选；多个候选取**最早**。
- R2 条的外观（亮/灰）只由「此刻有没有手指在该条上」决定。
- R3 「条只被尾结束」**只适用于类 2 与类 3**；类 1 的条与尾互不结束（尾自己走完，条自己按自己的时间收场）。装饰条同款，且尾不判。

## 2. 「车道内」必须先定义（阻塞第 1 步）

现状三种边界并存：

| 用途 | 用的范围 | 含容差 |
| --- | --- | --- |
| `ContainsLane(lane)`（续命、抬手候选） | 头未判 `JudgeLaneStart/End`，已判 `JudgedLaneStart/End` | 是（±1.5 / ±1.25） |
| `IsJudgment`（点按判定） | `JudgedLane*` | 是 |
| 可视条身 | `LaneStartF/LaneEndF` | 否 |

需要定的两件事：

1. 抬手判定的「车道内」用**可视条身**（`LaneStartF/LaneEndF`）还是**尾键的判定范围**（尾键自己的 `JudgeLane*`，随尾键横移而变）？
2. 是否含容差？若含，容差取条体（1.5）还是尾键（按 `NoteCategory`）？

**建议**：用「尾键自己的 `JudgeLane*`」+ 该尾键类别自己的容差。理由：抬手判的是尾，尾可能横移到另一条车道（谱面允许），条身的车道此时已经不描述尾的位置。

必须先补两个用例再改代码：

- `ATailShiftedToAnotherLaneIsFinishedByLiftingThere` —— 尾键横移后，在尾键位置抬手能判，在条身原位置抬手不能。
- `ALiftInsideADecorationLaneDoesNotFinishTheNeighbour` —— 抬手落在相邻装饰条车道内、但落入目标条放宽范围时，不判目标条。

> **2026-11-12 补记**：两条已写进 `Assets/Tests/EditMode/Live/DecouplingAcceptanceTests.cs`，**写入时即通过**——`LiveHoldCoordinator.PickLiftTarget` 已经按尾键自己的 `JudgeLane*` 分配抬手（`LiveHoldCoordinator.cs:331-347`），正是本节建议的方案。所以这里不再是待改项，只剩回归保护。

## 3. 逐类核对（v2）

### 类 1：结算责任**已定案**（2026-11-12，用户口述）

现状分派（`TailBehaviours.For`）：

```csharp
if (tail is FlickNote) return SwipeTail.Instance;                // 划键尾：判定即结束条
if (tail is FrictionFlickNote) return SelfJudgedTail.Instance;   // 独立尾（摩擦划）
return ReleaseTail.Instance;                                     // 普通尾（含普通 friction 尾、隐藏尾）
```

规则（用户原话整理成四条，实现以此为准）：

| # | 规则 | 落点 |
| --- | --- | --- |
| **R1** | **超时 Miss 只有中间点由条写；头和尾各写各的。** 条不给尾写任何结果。 | 头：`LongNoteHead.PublishHeadMiss`；尾：`LongNoteTail.PublishTail`（划键/普通尾由条代判，也是"尾自己的出口"）；中间点：`LongNote` 的 Done 转移 + `NoteBase.Excute` 的自超时 |
| **R2** | **划键尾与普通尾判定时让条立刻退场；其他尾不影响条。** 时序：条把帧交给尾 → 尾**先写并发布**自己的结果 → 这条结果**才**结束条（尾的 `State = Done` 里 `OnUnSpawnNote()` 必须在 `NotifyParentFinished()` 之前，否则条会先回收视图）。 | `ForceTerminate` 忽略 `JudgesItself` 的尾的 Done；`DetachStandaloneTail` 只读尾的结果；`FlickNote` / `FrictionFlickNote` / `FrictionNote` / `NormalNote` 四个 Done 分支的调用顺序 |
| **R3** | **条只和中间点共进退**：条结束后没判的中间点**立刻判 Perfect**，已判的保留原判。 | `LongNote.State` 的 Done 分支里 `SettleLeftoverAsPerfect()` |
| **R4** | **条只有两种结束方式**：被划键/普通尾结束，或**到达尾的标准时间**。 | 前者 `ForceTerminate`；后者 `IsPastTailWindow`（尾时间 + 它自己的判定窗，否则正常的 +125 ms 迟抬手会被吃掉） |

原「待确认的三个问题」的答案：
1. **谁写超时 Miss** → 独立尾自己写（R1）。条只负责驱动它、读它的结果、然后退场；`DetachStandaloneTail` 里给未结算尾写 Miss 的那段已删除。
2. **尾先判时条何时退场** → 不因尾判定而退场（R2）。条走到自己的时间才走（R4）。
3. **条先结束时尾还能不能判** → **不判**（用户 2026-11-12 明确）：尾的判定到达时条已经 `Done`，就**不要**再判一次，也不许借这次判定把条唤醒或改写任何结果。代码现状正是如此（`JudgeTailAsRelease` / `TrySwipeTail` / `ExpireTail` / `FailTail` 都以 `_bar.State == Done` 早退，`PublishTail` 再把它挡一层），用例 `AVerdictThatArrivesAfterTheBarIsDoneIsNotAskedAgain` 把它钉住。

实施后的登记：`DetachStandaloneTail` 现在要求「已过自己的时间」**且**「尾已有自己的判定」才退场；未结算就等一两帧（条还活着，子音符仍被它的子循环驱动，`NoteBase.Excute` 会把过窗的音符判成自己的 Miss），所以不会卡住。用例：`AnUntouchedFrictionFlickTailIsGradedMiss`、`AnUnsettledFrictionFlickTailEndsTheBarOnlyWhenItsWindowCloses`、`AFrictionFlickTailIsJudgedByItselfAndTheBarJustDetaches`、`AMissedStandaloneTailShowsItsCharacterEvenThoughTheBarDidNotGradeIt`、`AComboPointTheBarsEndNeverReachedIsSettledPerfect`、`AVerdictIsPublishedBeforeItsBarIsAllowedToLeave`（均在 `Live/FlickEndBehaviourTests`）。

> **时序那一处是修出来的，不是审出来的**：`AVerdictIsPublishedBeforeItsBarIsAllowedToLeave` 先红——尾发布自己的判定那一刻，条已经是 `Done`（四个音符类型都在 `OnUnSpawnNote()` 之前调 `NotifyParentFinished()`）。把两者对调后条才「被这条判定结束」，而不是「先结束了再收到判定」。

### 类 2：**违规一处（已定位，可立即改）**

`Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/ReleaseTail.cs:19`

```csharp
if (!frame.ReleasedInside || !frame.IsBeingHeld) { return false; }
```

`frame.IsBeingHeld` 是条自己的 `IsHeld(time)`（`LiveHoldCoordinator.cs:262`）；抬手那一帧手指已经没了 → 恒 false → 按住到尾再抬手永远判不到尾。窗口判断本来就在 `LongNote.JudgeTailAsRelease`（`LongNote.cs:816`），删掉它不丢保护。

### 类 3：**部分违规**

`TickSwipeTail()`（`LongNote.cs:1040`）在延迟结算期间用 `!HasFinger && State != Last && State != Release` 判断「是不是池残留」。按定义，条只该传「手指离开了」这一件事；`HasFinger` 属于手指信息（可以），`State` 属于条状态（不该参与）。同时要明确延迟期作废的条件：哪些情况下这次划动作废。

### 外观（不属于三类，独立任务）

`ShowsAsReleased`（`LongNote.cs:637`）被四处读：

- `NoteLineView.cs:309` 绘制颜色
- `LongTapEffectView.cs:185` 特效
- `LongNote.cs:401` 条体状态推进
- `LongNote.cs:1400` **判定入口**

所以它不是纯视图开关。改之前先写亮灰验收表（过线前 / 松手 / 宽限期内 / 宽限期外重新覆盖），并确认 `:401`、`:1400` 的语义不被带偏。

## 4. 执行顺序（v2）

每步单独提交、单独门禁；红就回退。

### 第 0 步（前置）：定义边界 + 两个用例

答完 §2 的两个问题，写 `ATailShiftedToAnotherLaneIsFinishedByLiftingThere`、`ALiftInsideADecorationLaneDoesNotFinishTheNeighbour`，先红后有实现。

**状态（2026-11-12）**：已完成。两个用例在 `Live/DecouplingAcceptanceTests`，且没有变红——实现早就是 §2 建议的方案（抬手按尾键自己的判定范围归属）。§2 的两个问题因此由代码给出了答案：**用尾键的范围，容差取尾键类别自己的**。

### 第 1 步：`ReleaseTail` 去掉 `IsBeingHeld`

- 改 `ReleaseTail.cs:19`，只留 `frame.ReleasedInside`。
- 新测试：按住到尾、车道内抬手 → 尾有结果且条 `Done`；提前抬手（窗口外）→ 不判。
- 门禁：`LongHoldPipelineTests`、`TailReleaseLaneTests` → 全量。

### 第 2 步：抬手归属（含同帧冲突）

现状：`_liftTargets` 在**任何结算之前**给每根抬手手指分配目标（`LiveHoldCoordinator.cs:155` 循环），两根手指可能选中同一条最早的尾；同帧第二个抬手拿不到东西。

验收三条：

1. 一次抬手最多结一条尾；另一条在窗口内**仍然可判**（下一次抬手能结它）。
2. 两根手指**同帧**抬手、各自能命中不同条 → 两条各结算一次。
3. 两根手指同帧都能命中**同一条**时：只有一条被结，且不产生第二次判定（不能双判）。若要求第二个抬手转给下一个候选，必须在**分配时**排除已被分配的尾，或按结算成功顺序继续分配。

### 第 3 步：类 1 的结算责任落地

按 §3 三个问题的答案改 `DetachStandaloneTail` 与分派：普通 friction 尾、隐藏尾是否改成 `SelfJudgedTail`；超时 Miss 由谁写；条何时退场。会动既有断言，逐条看保护的行为再决定改还是留。

### 第 4 步：类 3 的输入去状态化

`TickSwipeTail` 去掉 `State` 依赖，只读「手指是否还在条上 / 是否已离开」，并明确延迟期作废条件。

### 第 5 步：外观解耦

先写亮灰验收表，再改 `ShowsAsReleased` 与 `RestoreHeldState`，单独核对 `LongNote.cs:401`、`:1400`。

## 5. 已踩过的坑

| 尝试 | 结果 | 原因 |
| --- | --- | --- |
| 同时改 `ReleaseTail` + `RestoreHeldState` + `ShowsAsReleased` | `513/487/22 失败` | 三类问题混在一个 diff，红分不清；删掉 `IsBeingHeld` 后「谁在按」在换手用例里丢失 |
| `RestoreHeldState` 对普通条无条件恢复 `Scrolling` | 26 帧 `Scrolling→Last` 循环，宽限永远归零，尾窗关闭吃 `tailMiss`（`live-20261111-045825.log`） | `Scrolling` 语义是「从没被按过」 |
| 抬手归属用 `JudgeLaneStart/End`（±1.5 放宽）+ 最近中心 | 装饰条车道内抬手判到旁边长条的尾 | 放宽范围 + 按中心挑，不按「手指站在谁的车道上」挑 |
| 用 `HasFinger` 当「上一帧是否覆盖」 | `ALiftInAnotherLaneDoesNotGradeTheTail` 等 3 条红 | `HasFinger` 由任意 `Judgment` 置位，不代表覆盖；要单独记 `_wasCovered` |

## 6. 工具链

编译自检（不动 Unity）：

```powershell
$dotnet='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe'
$csc='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$rsp=Get-ChildItem 'Library\Bee\artifacts' -Recurse -Filter 'OpenSekai.Game.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
& $dotnet $csc "@$($rsp.FullName)" -nologo 2>&1 | Select-String 'error CS'
```

`csc` 干净 ≠ Unity 用了新代码，必须真重编：

1. `unity_command editor focus`
2. `unity_eval`：`AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`
3. 等 40–60 s，确认 `Library\ScriptAssemblies\OpenSekai.Game.dll` 时间戳变新
4. `unity_eval`：`OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<name>.xml")`

**测试入口的限制**：`TestRunnerBridge.Run(mode, reportPath)` 只按 `EditMode`/`PlayMode` 过滤（`Assets/Game/Editor/TestRunnerBridge.cs:32-70`，`new ExecutionSettings(new Filter { testMode = testMode })`），**没有按 fixture/class 过滤的参数**。单跑某一类只能全量跑再从 xml 过滤：

```powershell
$x='TestResults/all.xml'; $s=Get-Content $x -Raw
[regex]::Matches($s,'<test-case\b(?<a>[^>]*)>') |
  Where-Object { $_.Groups['a'].Value -match 'result="Failed"' } |
  ForEach-Object { if($_.Groups['a'].Value -match 'name="([^"]+)"'){ $Matches[1] } }
```

装机与取证：

```powershell
$adb='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
unity command build run --target Android --outputPath "Builds/<name>/OpenSekaiAtlas.apk" --confirm --result-only
& $adb -s 3B164A00Y5600000 install -r "<apk>"
& $adb -s 3B164A00Y5600000 shell rm -rf /sdcard/Android/data/io.github.opensekai/files/logs
& $adb -s 3B164A00Y5600000 shell monkey -p io.github.opensekai -c android.intent.category.LAUNCHER 1
adb pull /sdcard/Android/data/io.github.opensekai/files/logs/live-<stamp>.log
```

`[Tail]` 分类下关键行：`liftPick`、`tailFrame`、`release`、`tailMiss`、`expireCheck`、`winClosed`。

## 7. 红线

- 不改 `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNoteHead.cs`（头部判定已收口）。
- 不把 `LongHoldCombo`（内部点）放回候选集。
- 不改判定窗口常量（`LongEndJudgement`、`JudgeTuning`）。
- 不在 `ShowsAsReleased` 里读 `MusicScore.CurrentFrameInfo`（视图每帧读，静态读会绑死帧序）。
- 动 `ReleaseTail` 时不同批改外观（`ShowsAsReleased` / `RestoreHeldState`）。

## 8. 基线

- 分支 `codex/long-note-head-split`，`HEAD = 8906a59`，工作区干净（`git status --porcelain` 0 行）。
- 最近提交：`8906a59`（抬手归属 + 本任务书）、`e2d6c12`（条不再被打回「从没被按过」）、`15bf2b2`（删 owner 机械）。
- **历史结果**：`513 / 509 通过 / 0 失败 / 4 跳过`，报告 `TestResults/liftownlane-*.xml`。`TestResults/liftpure2.xml` 的 22 红属被回退的实验，不要当基线。
- 装机包：`Builds/liftownlane-2026-11-11/OpenSekaiAtlas.apk`（79.59 MB，2026-11-11 06:02:54）。
- 每次改动后重跑全量并记录：总数 / 通过 / 失败 / 跳过 + 报告路径。

## 9. 真机验收（可重放）

通用清单见 `docs/operations/device-acceptance-checklist.md`。

**措辞对齐**：该清单 A 项写「装饰条只有尾键可判」，本任务书写「装饰条的尾不参与判定」。两者指的不是同一种条，需要合并成一句定义：

- 装饰长条（`IsDecoration`，`category 9 / noteBaseType 10`）：条没有可判的头，尾按尾键自身规则判定；
- 装饰条的**中间点**不参与判定。

**Test16 重放记录模板**（每次复验填一份）：

| 项 | 值 |
| --- | --- |
| 谱面包 | 名称 / 版本 / 导入日期 |
| 长条 1 | 头部时间 / 尾时间 / 占用车道 / 尾类型（普通·flick·friction·隐藏） |
| 长条 2 | 同上 |
| 装饰条 | 时间 / 车道 |
| 操作 | 抬手发生在哪条车道、是否同帧、是否换手、是否滑出再回来 |
| 预期 | 哪条尾应被结、应得什么等级、另一条应保持可判 |
| 实际 | 判定结果 / 日志片段（`liftPick` / `tailFrame` / `release`） |

三谱各有侧重：

- 普通谱：类 2 正例（按住到尾 → 车道内抬手 → 出判定）与反例（提前抬手不判）。
- 长条密集谱（BOF）：重叠条同帧、换手、同帧双抬手。
- 变速谱：尾判时机不随流速漂移。

## 10. 结算兜底：只补「仍然绑在条上、还没判定」的部件（2026-11-11）

### 10.1 触发设计的问题

Test16 双押右条 `id=44`：`endsNoJudge=True`（尾是一条 `FrictionHideNote@0.001`），
`ExpireTail → CanFinishSilently → FinishSilently` 只把 `State` 置 `Done`，
`JudgeInfo` 仍是 `None`（`Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs:654`），
于是这一条的部件被「删除」而不是被结算，全 P 只有 6/7 连击。

同一类「静默删除」在代码里还有三处，但它们的语义不同：

| 位置 | 现状 | 是否本次范围 |
| --- | --- | --- |
| `LongNote.cs:654 FinishSilently` | 只置 `Done`，结果留 `None` | **是**：条自然走完、玩家没来得及判的部分 |
| `LongNote.cs:423 ForceTerminate` 失败分支 | 写 `Miss` | 否：外部终结，有明确失败语义 |
| `LongNote.cs:1090 FailTail` | 尾写 `Miss` + 子音符 `Miss` | 否：超时失败 |
| `LongNote.cs:911 DetachStandaloneTail` | 未结算就写 `Miss` 并解绑 | 否：独立尾自判，已解绑 |

### 10.2 规则（范围刻意保持最小）

条**自然结束**时（只限 `FinishSilently` 这一条路径），把**仍然绑在这条上**、且
`JudgeInfo.result == NoteResult.None` 的部件结算为 `(NoteResult.Perfect, NoteResultDescription.None)`，
走正常发布（判定回调 / 连击 / 分数 / 特效）。

判定「仍然绑在条上」的三个条件，缺一不可：

1. 在 `NoteList` 里并且 `ParentNote == this`（已 `DetachStandaloneTail` 的不算）；
2. `!IsSkip`（自动铺的中间点标记不算独立判定单位的那部分不重复结算）；
3. `Result == NoteResult.None`（已经判过的不覆盖）。

### 10.3 不在范围内的三件事（明确不做）

- **头**：`NoteList[0]` 是条自己，头不在 `NoteList` 里；头仍走 `PublishHeadMiss`。因此
  「一个键都不按」不会变成满分，`PublishHeadMiss` 与 `CanFinishSilently` 的头门禁都保留。
- **已经被解绑的独立尾**：`DetachStandaloneTail` 之后它自己负责结果，条不再改写它。
- **显式失败**：`FailTail`、`ForceTerminate` 的 `Miss` 语义不动。

### 10.4 实现位置

- 新增 `private void SettleLeftoverAsPerfect()`（`Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs` 尾部，
  `FinishSilently` 之上），按 10.2 的三个条件遍历并 `FinishWith(NoteResult.Perfect)`。
- 只在 `FinishSilently()` 里调用一次，之后再置 `State = NoteState.Done`。
- `CanFinishSilently` 的前置门禁（`PublishesHeadMiss && !Head.IsSettled ⇒ false`）保留：
  有条能按的头时不允许静默收尾，否则头永远没有机会。

### 10.5 影响与验收

- 分数、连击、HP、`PlayResult` 的判定统计都会因为「不再有 `Done/None`」而变化；
  `RulesVersion` 是否需要 +1 待定（若只影响隐藏尾谱面，可不升）。
- 必须先改的既有契约：`Assets/Tests/EditMode/Live/TailBehaviourTests.cs` 里
  `AHiddenEndedBarStopsWithoutAnyResult` 断言「隐藏尾结束且无结果」，按新规则要改成
  「隐藏尾结束、部件全部 Perfect、头未按时仍 Miss」。
- 新增用例：
  1. `ABarThatEndsWithAHiddenTailSettlesEveryBoundPoint`（Test16 形状，按头后中间点全 Perfect）；
  2. `ASilentlyFinishedBarLeavesNoNoneResult`（结束后 `NoteList` 无 `None`）；
  3. `ADetachedTailKeepsItsOwnResult`（解绑后不被改写）；
  4. `AnUntouchedBarStillMissesItsHead`（头仍 Miss，防止满分通胀）。
- 真机：Test16 双押 7/7；普通谱 / 长条密集谱 / 变速谱三项照 `docs/operations/device-acceptance-checklist.md`。

### 10.6 执行顺序

1. 落 `SettleLeftoverAsPerfect()` + 只在 `FinishSilently` 接入（最小改动，直接修 Test16）；
2. 改 `AHiddenEndedBarStopsWithoutAnyResult` 并补 4 条新用例，跑全量（基线 514 条）；
3. 装机复验 Test16 与三谱；
4. 观察是否还有「`Done/None`」的条出现在日志里（`LiveTrace` 已有 `unregDone`/`headSkip` 打点）。
### 10.7 对齐判据（2026-11-11 补充，最重要）

一条铁律，代替 10.2 里「`Result == None`」这种结果导向的描述：

- **能被条结束的部件** → 条结束时一定判 `Perfect`（`SettleLeftoverAsPerfect` 负责，只在 `FinishSilently` 调用）。
- **不能由条结束的部件** → 必须从一开始就**不在 `NoteList` 里**，否则条会替它做决定（现在的 `DetachStandaloneTail` 就是留在了列表里再写 `Miss`）。

按这条判据重排三类尾：

| 类 | 尾能不能被判结束 | 结尾时谁结算 | `NoteList` 里是否保留 |
| --- | --- | --- | --- |
| 1 隐藏 / friction / friction-flick | 不能（自己判自己） | 尾自己 | **不保留**（绑定时就不加入，或加入后立即移出） |
| 2 普通尾 | 能（条结束它，它也结束条） | 条结束时判 Perfect / 提前松手判 Release | 保留 |
| 3 flick 尾 | 不能（尾自己结束条，条不结束尾） | 尾自己 | **不保留** |

因此还要动两处结构：

1. `DetachStandaloneTail`（`LongNote.cs:911` 一带）不再「留在 `NoteList` 里写 `Miss`」，改为从 `NoteList` 移除并把 `ParentNote` 置空；
2. 类 1 的隐藏尾在绑定期（`AddNoteListAndSetupChildNote` / `AddHoldCombo`）就不进入 `NoteList` 的「可判列表」，`IsTailCandidate` 的排除名单随之简化。

改完之后 `EndsWithoutJudgement`、`CanFinishSilently` 的头门禁都变成同一件事的两种说法，可以后续合并。

## 11. 静默收尾：只看生命周期，不看尾类型（2026-11-11 追加）

### 11.1 规则

`FinishSilently` 的唯一触发条件是**条自己走到尽头、且没有被提前结束**，与它是哪一类尾无关。

- 走到尽头 = 最后一个绑定的音符（`_lastBoundNote`）的时间已经过去。
- 没有被提前结束 = 没有走 `ForceTerminate` / `FailTail` / 独立尾自判这些出口。
- 由于尾在条结束前**已经解绑**（`DetachStandaloneTail` 先 `NoteList.Remove(childNote)`，再让尾自己吃超时 Miss），条结束本身不会影响尾键的判定，因此这条路径不需要按尾类型分支。

### 11.2 已实现（`Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs`）

```csharp
internal bool CanFinishSilently(float currentTime)
{
    if (PublishesHeadMiss && !Head.IsSettled)
    {
        return false;
    }

    return _lastBoundNote != null && currentTime > _lastBoundNote.MusicScoreInfo.time;
}
```

- 去掉了原来的 `EndsWithoutJudgement` 条件（它按尾类型判断，与 11.1 冲突）。
- 头门禁保留：还能被按的条必须先经过 `Last → Release`，让 `Head.PublishHeadMiss()` 有出口，否则 Miss 会被静默吞掉（这就是 Test16 双押丢 combo 的成因）。
- `FinishSilently()` 在置 `Done` 前调用 `SettleLeftoverAsPerfect()`，只结算 `LongHoldCombo`（中间点），不动尾。

### 11.3 未完成：解绑还没有全部落地

`NoteList` 目前同时承担「绘制/生命周期」和「结算」两种用途，因此尾的解绑只做了独立尾这一条路径：

| 尾类型 | 是否已移出 `NoteList` | 备注 |
| --- | --- | --- |
| 独立尾（flick / friction-flick，`UsesStandaloneTail`） | 已解绑 | `DetachStandaloneTail` 里 `NoteList.Remove(childNote)` 后自判 |
| 普通尾 / 隐藏尾 / friction 尾 | **尚未解绑** | 仍在 `NoteList`；一次过宽的改动（不再 `NoteList.Add(note)`）触发 61 条红，因为视图生命周期与划尾都靠 `NoteList` 遍历 |

所以把普通尾也解绑之前，11.1 的「条结束不影响尾」对普通尾**还不成立**：`CanFinishSilently` 一旦对普通尾放开，会先于 `FailTail` 命中，把尾的 Miss 吞掉。

结论：先把尾从 `NoteList` 里挪出去（结算走单独的列表，绘制仍走 `NoteList`），再放开 11.1。

### 11.4 `State = NoteState.Done` 的 17 个赋值点（待逐个确认归属）

| 文件 | 行 | 语义 |
| --- | --- | --- |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs` | 466 | `ForceTerminate` 写结果后收尾 |
| 同上 | 541 | 尾部结算（`JudgeTail` 路径） |
| 同上 | 698 | `FinishSilently` |
| 同上 | 886 | 划尾延迟结算收尾 |
| 同上 | 938 | 独立尾解绑后收尾 |
| 同上 | 961 | `DetachStandaloneTail` 里未结算的独立尾 |
| 同上 | 1153 | 超时终结 |
| 同上 | 1484 | 池回收 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/NoteBase.cs` | 459 | 通用音符判定后收尾 |
| 同上 | 565 | `State >= Last` 超时 Miss |
| 同上 | 696 | 池回收 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/FrictionLongNote.cs` | 82 | 摩擦条自判完成 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongHoldCombo.cs` | 44 / 71 / 106 / 125 / 157 | 中间点判定、终结、发布 |

检查要点：凡是「条收尾」相关的赋值，都应该经过 `FinishSilently` 或 `ForceTerminate` 二者之一；直接置 `Done` 且不写 `JudgeInfo` 的地方，就是会吞判定的地方。

## 十二、头的判定还没解耦干净（2026-11-11，Test16 双押丢 combo 收尾）

### 12.1 现象

Test16 开头双押（左右各一根长条，两侧同一时刻按下），全 P 只有 6/7 连击：右侧那根条的**头**从头到尾既没有判定、也没有 Miss，条却自己 `Done/Miss` 消失了。

设备日志（`live-20261111-091838.log`，带 lane 打点）关键行：

```
spawn id=39 LongNote t=0.000 lane=-1.50..4.50      ← 左条
spawn id=44 LongNote t=0.000 lane=7.50..12.50      ← 右条（丢的那条）
spawn id=43 NormalNote t=1.000 lane=6.50..12.50 cat=Long   ← 右条真正的尾
[TL][Tail] expireCheck barT=0.000 now=0.002 state=Scrolling res=None child=FrictionHideNote@0.001 past=True
[TL][Hold] forceTermIgnored by=LongNote.ForceTerminate … barState=Scrolling
[TL][Pool] remove barT=0.000 state=Done result=Miss
```

整份日志里 `id=44` **没有任何 `judge` / `combo` 行**。

### 12.2 已排除

| 假设 | 结论 |
| --- | --- |
| 池子发出脏 `speedRatio`，`ScanNote` 静默跳过 | 已改为 `IsHighSpeedNote` 绑定标志（`73eaf25`），仍复现 |
| 尾绑定错（把 t=0.001 的 `FrictionHideNote` 当 child） | 属实但不是丢 combo 的原因，尾不发表判定 |
| `FinishSilently` 吞头 | 已加 `CanFinishSilently` 头门禁，仍复现 |
| `ForceTerminate` 直接写 Miss 绕过头 | 已加「写结果前先 `Head.PublishHeadMiss()`」，仍复现（该条根本没走 `forceTerm`，只有 `forceTermIgnored`） |

### 12.3 目前的判断

头的**唯一**发布出口是 `LongNote.Excute` 里的这一段：

```csharp
if (State == NoteState.Last)
{
    State = NoteState.Release;
    if (!Head.IsSettled) Head.PublishHeadMiss();
}
```

只要条没有经过 `Last` 就离开游玩态，头就永远不会发布。而 `LongNote.cs:369-385` 的过线分支在 `_headTouched == false` 时**直接** `State = Release`（不经过 `Last`），随后基类 `NoteBase.Excute` 的 `if (State >= NoteState.Last) { JudgeInfo = Miss; State = Done; }`（`NoteBase.cs:565`）把条写成 `Done/Miss` —— 条结束了，头却从未结算。这与日志完全吻合（无 `judge`、`forceTerm*` 只有 ignored）。

### 12.4 需要彻查的点（本任务）

1. **所有让条离开游玩态的分支**都必须保证：`PublishesHeadMiss && !Head.IsSettled` ⇒ 先 `Head.PublishHeadMiss()`，再改状态。清单：`LongNote.cs:369-385`（过线 → Release）、`:387-398`（Last 宽限 → Release）、`:404-418`（丢手 → Last）、`:426-476`（ForceTerminate）、`:641-658`（`CanFinishSilently`/`FinishSilently`）、`:1153`（超时终结）、`NoteBase.cs:459`（通用判定收尾）、`NoteBase.cs:565`（`State >= Last` 超时 Miss）、`NoteBase.cs:696`（池回收）。
2. **头不该依赖条的生命周期**：目标是「头在按下那一帧就判」+「过线未按就 Miss」，两条路径都不经过条的状态机。目前 `LongNoteHead.TryJudge` 已经「按下即 `Settle`」（`LongNoteHead.cs:67-87`），这部分是对的；缺的是「未按时由谁发布 Miss」，现在挂在 `Last → Release` 这一个分支上。
3. **谁来写头的 Miss** 应只有一个出口（建议：`LongNote.Excute` 开头统一调用 `Head.SettleExpiredHead(currentFrameInfo.time)` 已经存在，但它只在 `IsSettled == false && 超过 JudgeTime` 时发布——需确认条被提前 `Done` 时这条路有没有机会跑）。
4. **条结束后头还能不能判**：条 `Done` 之后 `LongNoteHead.TryJudge` 会被 `if (_bar.State == NoteState.Done) return false;` 挡掉（`LongNoteHead.cs:69`）。这条是否合理要单独定：按当前语义「头只能在自己的窗口内判」，条提前结束不应让头失去判定机会，因此这条早退很可能是多余的。

> **2026-11-12 结案**：第 3 条已做——`LongNote.LeavePlayState()` 是唯一的门，`Head.PublishHeadMiss()` 只从这一个方法里出；四个调用点是过线（`Excute` 的 `Last → Release`）、超时（`SettleExpiredHead`）、`ForceTerminate`、`FinishSilently`。§12.4-1 清单里的 `NoteBase` 三个入口与「池回收」**刻意不纳入**：`NoteBase` 那三处要么在条自己被 `Done` 之后运行（那时头已在出口发布），要么是复用时的 `ResetNote()`，在那里发布会把 Miss 写进下一局。
>
> 第 4 条**定为保留**：输入路径已由 `LongNote.IsJudgment` 挡掉 `Done`，而条离场时头必然已发布，所以这行既不会误伤也不会被依赖，只是第二把锁；删掉它反而让直接调用者能驱动已结束条的子音符。理由写在 `LongNoteHead.cs` 该行上方。

### 12.5 验收

- 新增用例（红→绿）：`ABarThatCrossesTheLineUnpressedStillPublishesItsHeadMiss`（过线不按，断言头的 `OnJudgment` 恰好发一次 Miss、连击 -1）、`ABarFinishedByItsTailStillPublishesTheHead`（尾先结束条，头仍发布）。
- 真机：Test16 全 P 必须是 7/7；不按右侧时右侧头计 Miss（连击断）。
- 全量 EditMode 不回归（当前基线 `583 / 579 / 0 / 4`）。

> **2026-11-12 补记**：两条用例已写进 `Live/DecouplingAcceptanceTests`，**写入时即通过**——「过线未按」由 `SettleExpiredHead` 兜底、「被尾结束」时头早在按下那一帧就发过。「连击 −1」这一半没断言：`LiveHarness` 不接 `ScoreLogic`，要断它得另开一条带计分的用例。真机那条仍未做。
>
> **出口那一轮之后**：`ForceTerminate` 与 `FinishSilently` 已并入 `LeavePlayState()`，红→绿的用例是 `Live/HeadDecouplingTests.ABarEndedBeforeItsHeadIsJudgedStillPublishesTheMiss`（条被 `ForceTerminate(tail)` 结束、头补 Miss、只发布一次），另加 `AnEndingAskedTwiceStillPublishesTheHeadOnce` 与 `APressOnAFinishedBarChangesNothing`。

## 十三、flick 尾解耦后的判定范围与重复判定（2026-11-11，用户补充）

### 13.1 规则

解耦完成后，**flick 尾自己只探测「标准时间之后」的滑动**：

- 尾的标准时间之前发生的滑动，不再由尾自己消费——那属于「按下/预备」阶段，由条的那一侧处理（若仍需要「提前划也有效」的玩法，必须由条记录意图并在尾的时间点之后转达，而不是让尾去读更早的样本）。
- 因此 flick 尾的判定入口（`Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/SwipeTail.cs` 的 `TryFinish`、以及 `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs:1040` 的 `TickSwipeTail` 里那段「待结算划动」逻辑）需要按「事件时间 ≥ 尾标准时间」过滤。

### 13.2 必须防的重复判定

条与尾之间存在一个 **3 帧（≈50 ms，`HoldGraceFrames` / `ReferenceFrame.FromFrames`）** 的时间差：条在最后一帧仍可能把尾再判一次。典型风险序列：

1. 尾在 `t = 尾标准时间` 判出结果（`JustPerfect`，连击 +1）；
2. 条的收尾/宽限还在跑，3 帧内再次调用尾的 `Judgment`；
3. 尾第二次发布结果 → 连击 +2、打击音双响、判定字叠两层。

因此解耦的验收必须包含「**同一根尾只发布一次**」这一条，且不能依赖「`State == Done` 早退」这种间接保证——要显式保证（例如尾自己的 `_published` 标志 + 用例直断发布次数）。

> **2026-11-12 落地**：`LongNoteTail` 有了 `_tailPublished` 与单一发布出口 `PublishTail(result)`（写条的结果 + `CompleteTail`）。三条结算路径 `JudgeTailCore`（抬手/划动判定）、`FailTail`（窗口过期）、`DetachStandaloneTail`（自判尾解绑）全部走它；`ResetDecision()` 不清这个标志（它跑在正要发布的路径上），只有 `Reset()`（池复用）清。`DetachStandaloneTail` 里「把尾从 `NoteList` 摘掉」被挪到发布之后——否则一次被拒的重复结算已经动了列表。
>
> 用例（`Live/TailBehaviourContractTests`）：`ASecondVerdictOnTheSameTailChangesNothing` **先红**（`FailTail` 返回 true 并把条的结果从 `JustPerfect` 改写成 `Miss`），把条硬塞回 `Scrolling` 来证明这个保证不靠 `State == Done`；`AReusedBarCanPublishItsTailAgain` 盯「复位后能再发布一次」，防止标志在池里粘住。

### 13.3 需要一起看的代码

| 位置 | 关注点 |
| --- | --- |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs:1040` `TickSwipeTail` | 待结算划动的清空条件与计数起点 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs` `_swipeTailDeferred` / `_swipeTailCapped` | 谁置位、谁清、能否在解耦后整体删掉 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/SwipeTail.cs` | `TryFinish` 是否读「标准时间之前」的样本 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/SelfJudgedTail.cs` | 摩擦划尾的自判是否同样只看向后的样本 |
| `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/ReleaseTail.cs:15` | 抬手判定的入口（已在 §4 第 1 步处理 `IsBeingHeld`） |

### 13.4 验收用例

- `AFlickTailIgnoresSamplesFromBeforeItsOwnTime`：标准时间之前给一串滑动样本，尾不得判定；到点后给同一方向的样本才判定。
- `AFlickTailPublishesOnceEvenIfTheBarKeepsTicking`：尾判定之后继续驱动 5 帧（覆盖 3 帧宽限），断言发布次数恰好 1、连击只 +1。
- 真机：Test16 + 含 flick 尾的谱面，验证「提前划」与「正点划」都不出双判定、也不吞判定。

**状态（2026-11-12）**：前两条已写进 `Live/DecouplingAcceptanceTests`（分别按"400 ms 前"和"连续 30 帧"驱动，比这里的口径更宽），写入时通过。真机那条仍未做。
