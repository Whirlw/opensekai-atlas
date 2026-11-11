# 条尾三类：谁影响谁

> 任务书 v2（2026-11-11 修订，依据外部评审）。v1 的三处判断已被代码推翻，见 §0。
> 分支 `codex/long-note-head-split`，HEAD `8906a59`，工作区干净。

## 0. 修订记录

| v1 的说法 | 实际情况 | 出处 |
| --- | --- | --- |
| 抬手候选「只看条自己的车道」 | 用的是 `LongNote.ContainsLane()`，它按**头部是否已判**在 `JudgeLane*` / `JudgedLane*` 之间切换，两套都带容差。删掉显式兜底后范围仍可能超出可见条身 | `Assets/Game/Runtime/Sekai/Live/LongNote.cs:1410` |
| 类 1（隐藏 / friction / friction-flick）在代码里是 `SelfJudgedTail`，合规 | 只有 **friction-flick** 走 `SelfJudgedTail`；普通 friction 尾与隐藏尾都走 `ReleaseTail`。且 `DetachStandaloneTail()` 对未结算的尾**写 Miss**，属「条影响尾」 | `Assets/Game/Runtime/Sekai/Live/Tails/TailBehaviours.cs:13`、`LongNote.cs:904` |
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

## 3. 逐类核对（v2）

### 类 1：**部分违规**，需先确定结算责任

现状分派（`TailBehaviours.For`）：

```csharp
if (tail is FlickNote) return SwipeTail.Instance;                // 类 3
if (tail is FrictionFlickNote) return SelfJudgedTail.Instance;   // 类 1（仅此一种）
return ReleaseTail.Instance;                                     // 类 2（普通 friction 尾、隐藏尾也落在这里）
```

也就是说「friction 尾」「隐藏尾」目前按类 2 处理，与定义冲突。

`DetachStandaloneTail()`（`LongNote.cs:904`）对独立尾做三件事：读尾结果 → 把结果抄进条 → **未结算就写 Miss 给尾**（`:917-928`）。第三条是「条影响尾」。

待确认的三个问题（写进实现前必须先答）：

1. **谁写超时 Miss**：独立尾自己超时 Miss，还是由条在窗口关闭时代写？若由尾自己写，条只负责「解绑并收场」。
2. **尾先判时条何时退场**：尾在条还亮着时判完了，条立刻 `Done`，还是等自己的可视长度走完？
3. **条先结束时尾还能不能判**：条因超时收场、尾还在窗口内 —— 允许吗？

### 类 2：**违规一处（已定位，可立即改）**

`Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs:19`

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

- 不改 `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs`（头部判定已收口）。
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
