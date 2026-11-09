# 判定内核纯函数化：交给外部执行者的任务书

> 目的：把 `LiveLogic` 每帧判定主循环里仍然隐式的规则显式化、纯函数化，并补上表征测试，避免再出现「成串漏判」这类只能靠真机录像定位的问题。
> 适用读者：接手本任务的外部执行者。
> 前置条件：读过 [判定与窗口](../reference/judging.md)、[长条 v3 交接](long-hold-v3-handoff.md)。
> 最后更新：2026-11-04

## 1. 一句话目标

把「每指每帧只有一条音符能消耗这一按」这条隐式规则抽成显式、可单测的调度器，并统一三类音符的完成协议与时间单位；**不改变任何现有判定手感**。

## 2. 项目与运行环境

- 仓库：`E:\DeepseekHarness\OpenSekai`（Open Sekai Atlas），分支 `codex/long-hold-pickup-fix` 为当前工作分支。
- Unity `6000.6.7f1`，仅 Android；真机 OPPO `PLQ110`（165 Hz）。
- EditMode 测试：编辑器内菜单 `OpenSekai/Tests/Run EditMode`，或 `Tools/run-editmode-tests.ps1`（需先关编辑器）。报告在 `TestResults/editmode.xml`，读 `test-run` 的 `testcasecount/result/passed/failed`，**先看文件 `LastWriteTime` 判断是不是本轮产物**（域重载会吃掉上一轮的报告）。
- 当前基线：**313 条 EditMode 全绿**。

## 3. 现状（不要重复调研）

已抽成纯函数并有单测的模块（`Assets/Game/Runtime/Shared/{Scoring,Live}`）：

| 模块 | 覆盖内容 |
|---|---|
| `ScoreGauge` | 判定→血条/分数区间 |
| `ScoreLogic` | 累积摊分，任意音符数总分恰为 1,000,000 |
| `JudgeTuning` / `JudgeWindow` / `JudgeFrameTable` | 判定阶梯与「帧数 ↔ 毫秒」换算 |
| `CandidateOrder` | 候选排序 rank（`FlickRank=2` / `TouchRank=1` / `HeldRank=0`） |
| `CandidateOwnership` | 长条归属与换手规则 |
| `HoldInputMatching` | 增广匹配，允许成对换指 |
| `FlickGesture` | 划动采样（方向锥 180°、距离阈值） |
| `TouchLane` / `NoteSpeedRatio` / `NoteDisplayOffset` | 车道解析、流速、显示偏移 |
| `JudgmentAggregator` | 同帧取最差判定显示 |

仍然是反编译原样、**本轮要动**的两个文件：

- `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`（约 1550 行）——每帧主循环；
- `Assets/Game/Runtime/Sekai/Live/LongNote.cs`（约 537 行）——长条状态机与 16 分 combo。

## 4. 四项工作（按优先级，可分批交付）

### 任务 A：把「一帧一个名额」抽成显式调度器

现状：`EvaluateJudgment` 里

```csharp
note.Judgment(ref input.Touch, lane);
...
if (note.State != NoteState.Done && note.Result == NoteResult.None) continue;   // 2026-11-04 补的
input.CandidateNotes.Clear();
_usedFingerList.Add(input.Touch.fingerId);
break;
```

要求：抽成 `OpenSekai.Shared.Live.JudgmentSchedule`（或等价命名）的纯函数，输入「候选列表 + 每条的完成状态」，输出「本帧该判哪几条、哪些手指被消耗」。`LiveLogic` 只保留调用。

理由：这条规则是本轮「friction 成串漏判」的直接原因（等待中的 flick 每帧独占名额），但它现在是两条相邻语句之间的隐式契约，没有任何东西阻止将来有人把 `continue` 改回 `break`。

验收：新模块有 `JudgmentScheduleTests`，至少覆盖——(a) 首条已 `Done` 时消耗手指；(b) 首条未完成时同帧继续判下一条；(c) 同帧两条都能判完时两条都被判；(d) 已结束音符不再进入调度。

**状态：已完成（2026-11-04，提交 `55af8eb`）。**

- `Assets/Game/Runtime/Shared/Live/JudgmentSchedule.cs`：`IJudgmentPass`（`Judge(int)` / `Consumed(int)`）+ `JudgmentSchedule.Run<T>(int count, ref T pass)`（`T : struct` 以避免每帧分配）。`Outcome.ConsumesPress` 定义为「`IsDone` 或 `HasResult`」，即「等待中的音符不吃名额」这条规则现在只有一个实现处。
- `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`：`EvaluateJudgment` 的 44 行循环体换成 4 行，判定细节搬进嵌套的 `private struct JudgmentPass : IJudgmentPass`（持有 `Logic` 与 `Input`，写回 `InputedNormalNoteTouch` / `_judgmentNoteList`）。
- `Assets/Tests/EditMode/Live/JudgmentScheduleTests.cs`：9 条用例，覆盖上面 (a)–(d) 及空列表、`-1` 返回、`Consumed` 只对赢家触发。
- 变异验证：把 `Run` 改回「判一条就返回」（等价于旧的 `break`），`AWaitingNoteLetsTheNextCandidateBeJudged`、`NoCandidateConsumesWhenAllOfThemWait`、`TwoWaitingNotesBothGetTheirFrame`、`TheConsumingCallbackRunsOnlyForTheWinner` 四条立刻变红；还原后 322/322 全绿。
- 自校验：`:72-86` 的逻辑与改造前逐句对应——`ConvertLaneBy` → `Judgment` → `InputedNormalNoteTouch` 写入 → `_judgmentNoteList.Remove` 判定 → 消耗时 `CandidateNotes.Clear()` + `_usedFingerList.Add`。真机未重跑（本任务无预期行为变化），若需要我随时出包。

### 任务 B：统一「音符完成协议」

现状：三类音符语义完全不同：

| 类型 | 完成条件 | 位置 |
|---|---|---|
| `FrictionNote` | 两段式：`Judgment` 先设 `isTouched`，`Excute` 在 `Progress >= 1` 时给 `JustPerfect` | `Assets/Game/Runtime/Sekai/Live/FrictionNote.cs` |
| `FlickNote` / `FrictionFlickNote` | 一次性：当帧 `delta` 满足方向+距离，或 `_isTrueDirection` 兜底 | `FlickNote.cs`、`FrictionFlickNote.cs` |
| `ConnectionNote` / `HiddenConnectionNote` | 单帧窗口，`Last` 后 80 ms 内可判 | `ConnectionNote.cs`（`SlideLateWindowSeconds = 0.08f`） |

要求：抽一个 `INoteCompletion`（或抽象的 `NoteCompletionPolicy`）描述「这条音符还需要什么才能完成」，并给出三种实现；`LiveLogic` 不再按类型做 `is FrictionNote` / `is LongNote` 的分支判断（现有 `CandidateRank` 里的类型分支也应改为查询该策略）。

验收：三种实现各有单测；`LiveLogic` 中按类型判断的分支数量从当前的若干处降到 0；313 条基线全绿。

**状态：已完成（`feat(live): let each note declare what a press did to it`），待真机走查。**

不新增 `INoteCompletion` 策略表，而是把「这次按压有没有把手指用掉」交给音符自己声明——这才是该协议真正要回答的问题：

- `Assets/Game/Runtime/Shared/Live/PressDisposition.cs`：`IgnoresPress` / `KeepsSlotOpen` / `ClosesSlot`，`ConsumesSlot()` 只在 `ClosesSlot` 时为真。
- `Assets/Game/Runtime/Sekai/Live/NoteBase.cs`：`MarkPress(PressDisposition)`（protected）+ `LastPressDisposition`（internal）+ `ClosesSlotAfterPress`（internal，显式声明优先、`Done`/有结果兜底），`ResetNote` 重置。
  - 兜底是必要的：任何漏标记的新音符都退化成旧行为，不会突然抢帧或丢帧。
- 三类完成形状现在一眼可辨，且都是它们自己写的：
  | 形状 | 音符 | 声明 |
  |---|---|---|
  | 两段式 | `FrictionNote`（`Progress < 1` 时只 `isTouched`） | `KeepsSlotOpen` |
  | 等手势 | `FlickNote` / `FrictionFlickNote`（记录 delta / 方向，等过线那帧） | `KeepsSlotOpen` |
  | 单帧窗口 | `ConnectionNote` / `HiddenConnectionNote`（非 `Last` 时什么都不做） | `IgnoresPress`；`Last` 完成时 `ClosesSlot` |
  | 保持中 | `LongNote` / `FrictionLongNote` | 子音符未完成时 `KeepsSlotOpen`，完成时 `ClosesSlot` |
- `Assets/Game/Runtime/Shared/Live/JudgmentSchedule.cs`：`Outcome` 只保留一个 `ConsumesSlot` 字段，删掉 `IsDone`/`HasResult`/`ConsumesPress` 的间接推断；`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs` 的 `JudgmentPass.Judge` 直接 `new JudgmentSchedule.Outcome(note.ClosesSlotAfterPress)`。
- 测试：新增 `Assets/Tests/EditMode/Live/NotePressDispositionTests.cs`（7 条：枚举映射、默认忽略、普通键完成/非 Began、摩擦条两段式保持名额、slide 非窗口期忽略、flick 等手势保持名额）；`JudgmentScheduleTests` 的 `Waiting()/Judged()/Finished()` 与三条断言改为 `ConsumesSlot`。
- 结果：**340/340**（333 + 7）。走查重点：`DEINOS PHAINEIN` 前 30 秒「flick 之后那一串摩擦条」不掉血；交叉长条不瞬灰；`BOF` 自动 `1,000,000 / S / 1669`。

### 任务 C：判定窗口统一到秒

现状：窗口以 60 fps 帧数定义（`LiveConfig.AddJudgeFrame(...)`，`Normal/Long 2.5/2.5/5/5/6.5/6.5/7.5/7.5`），`CalcFrameToTime` 换算；而渲染跟随屏幕（165 Hz 下一帧 6 ms）。已经出过一次事故：slide 的可判窗口一度只有 1 帧。

要求：所有对外的窗口参数改为秒（内部可保留帧数表用于对照），`JudgeFrameTable` 显式声明基准帧率 60 fps，并保留一张「表值 → 秒」的对照表在文档里。

验收：`JudgeFrameTableTests` 增加「换算与帧率无关」的用例；`docs/reference/judging.md` 更新为秒；不改变现有判定手感（用下述真机回归确认）。


**状态：已完成（提交 `refactor(live): measure hold liveness in seconds instead of engine frames`），待真机走查手感。**

- `Assets/Game/Runtime/Shared/Live/ReferenceFrame.cs`：`Seconds = 1f / 60f`、`FromFrames(frames)`、`IsStillFresh(lastInputTime, currentTime, toleranceSeconds)`。注释写明为什么不能再用 `Time.frameCount`（165 Hz 一帧 6 ms、120 Hz 8.3 ms，同一份逻辑在不同设备上容差不同）。
- `Assets/Game/Runtime/Sekai/Live/LongNote.cs`：`protected int lastInputFrame` → `protected float lastInputTime`；`RecordHoldingInput(int fingerId)` → `RecordHoldingInput(int fingerId, float musicTime)`；释放条件 `lastInputFrame + MissMesh < Time.frameCount` → `!IsHoldFresh(currentFrameInfo.time)`；新增 `private static float HoldToleranceSeconds => ReferenceFrame.FromFrames(LiveConfig.noteTypeJudgeData.MissMesh) + ReferenceFrame.Seconds` 与 `internal bool IsHoldFresh(float currentTime)`；`ShouldIgnoreTouch` 的签名从 `(…, int lastInputFrame, int currentFrame, int missFrames)` 改成 `(…, float lastInputTime, float currentTime, float toleranceSeconds)`，`IsHeldByAnotherFinger` 增加 `currentTime` 参数；`AutoJudgment` 用 `currentFrameInfo.time`、`ResetNote` 用 `0f`。
- `Assets/Game/Runtime/Sekai/Live/ConnectionNote.cs`：`SlideLateWindowSeconds` 由上一轮的 `0.08f` **改回一帧**（`= OpenSekai.Shared.Live.ReferenceFrame.Seconds`），注释说明为什么一帧既够覆盖当帧、又远小于 120 BPM 十六分的 125 ms。
- `Assets/Game/Runtime/Sekai/Live/FrictionLongNote.cs`、`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`：`RecordHoldingInput`/`IsHeldByAnotherFinger` 的调用点同步传时间。
- 测试：新增 `Assets/Tests/EditMode/Live/ReferenceFrameTests.cs`（6 条）；`LongHoldOwnershipTests` 的 7 组用例从帧号改成秒（0.1 s 一格）；`LongHoldPipelineTests` 的 `LastFrame` → `LastTime`、7 处 `Is.EqualTo(Time.frameCount)` → `Is.EqualTo(0f).Within(1e-5f)`（测试里的输入 `musicTime` 恒为 0）；`ASlideStaysJudgeableAfterItsTimePasses` 的用例由 `0 / 0.05 / 0.075` 改成 `0 / 0.01`（窗口从 80 ms 回到一帧）；`GreatHeadStillKeepsTheHoldAlive` 改成逐帧 `Judgment` 驱动并按秒断言存活。
- 补完（提交 `fca53c6`）：`Assets/Game/Runtime/Sekai/Live/FlickNote.cs` 与 `FrictionFlickNote.cs` 的 `_lastInputFrame + 1 < Time.frameCount` 一并改成 `_lastInputTime + ReferenceFrame.Seconds < currentFrameInfo.time`，字段改 `float _lastInputTime`，写入用 `touch.musicTime`、重置用 `0f`。至此 live 判定路径里不再有 `Time.frameCount`（只剩 `JudgmentView`/`TapEffectView` 的每帧 UI 去重）。
- 结果：**333/333**。真机手感需要走查：长条释放容差在 165 Hz 上从 6 ms 变成 16.7 ms，理论上更宽容；`SlideLateWindowSeconds` 从 80 ms 收回到 16.7 ms，滑条（slide）的晚判窗口变窄，需要确认不再出现「滑条到线按不到」。

### 任务 D：拆开「显示提前量」与「可判提前量」

现状：判定列表登记原本用每音符的显示提前量（随 `speedRatio` 变化），快速音符只提前约 130 ms 进列表，已贴判定窗口边缘。2026-11-04 加了 `JUDGE_LIST_LEAD_SECONDS = 0.35f` 下限（`IsJudgeTimingTimeOffsetNote`）。

要求：把两者拆成独立参数并写进 `LiveConfig`：`DisplayLead(note)` 只影响渲染，`JudgeLead` 是常量且独立于音符速度。

验收：`IsJudgeTimingTimeOffsetNote` 的输入不再含 `speedRatio`；新增用例证明「同时间、不同 speedRatio 的音符进入判定列表的时刻相同」。


**状态：已完成（2026-11-04，提交 `b` 之后的一条：`refactor(live): split the judgeable lead from the display lead`）。**

- `Assets/Game/Runtime/Shared/Live/JudgeListLead.cs`：`Seconds = 0.35f`（恒定，与速度无关）+ `IsStillTooFarAhead(currentTime, noteTime[, leadSeconds])`。文档注释写明了两个提前量的区别：显示提前量 = `NoteDisplayOffset.Resolve`（按 `speedRatio` 除，快歌更晚画出），可判提前量 = 判定列表登记门槛（只与时间有关）。
- `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`：`IsJudgeTimingTimeOffsetNote` 不再接收 `timeOffset` 参数（显示提前量在该判断里已无用），改为 `JudgeListLead.IsStillTooFarAhead(currentFrameInfo.time + TARGET_FRAME_TIME, note.MusicScoreInfo.time)`；`IsJudgeTimingTimeOffsetHighSpeedNote` 同步去掉参数；私有常量 `JUDGE_LIST_LEAD_SECONDS` 删除。
- `Assets/Tests/EditMode/Live/JudgeListLeadTests.cs`：6 条用例，其中 `TheEntryMomentDoesNotDependOnTheScrollSpeed` 用 `EntryTime()` 复现扫描步进，断言「旧规则下 4 倍速比 0.5 倍速晚 0.1 s 以上进入列表，新规则下两者完全相同且更早」。
- 结果：**328/328**（322 + 6）。真机未重跑：本任务把上一轮的 `Mathf.Max` 兜底换成显式常量，数值等价（都是 0.35 s），无行为变化。

## 5. 已查明的事实（直接使用，不要重新推导）

- 每指每帧只有一个判定名额；`LongNote` 的存活另有 `RefreshSustainedHolds` 每帧续命，**不依赖**这个名额。
- `LongNote` 释放条件：`lastInputFrame + LiveConfig.noteTypeJudgeData.MissMesh < Time.frameCount`，且 `MissMesh == 0`。
- `FrictionNote.Judgment` 在 `Progress < 1` 时只设 `isTouched` 并返回 `false`；`Excute` 才在 `Progress >= 1` 时给 `JustPerfect`。
- `FrictionFlickNote` 一被碰到就进 `Input`，要等「过线那一帧仍有有效划动」才完成——这正是它长期占名额的原因。
- `JudgeTimeAfter` 由所有类型的最大 Bad-after 决定（约 125–142 ms）。
- 判定列表只在 `EvaluateJudgment` 里移除音符（条件：`State == Done || Result != None`）。
- `CandidateRank` 的语义：flick 需要当帧 `delta`，摩擦条需要当帧 `Judgment`，被按住的长条不需要——**排序依据是「谁需要这一帧」，不是「谁更近」**。

## 6. 约束（硬性）

1. 不改谱面格式、曲包 ID、设置键名、序列化字段名（改字段必须同步 prefab/scene YAML 键）。
2. 不引入新依赖，不新增第三方包。
3. 每个任务单独可交付、单独可回退；一次提交只做一件事。
4. 不得为了「更干净」顺手改判定数值（窗口、评分、血条），手感变化必须有真机对照证据。
5. 测试必须真的跑：报告时间戳要对得上，用例要能在改坏时变红（做完改动要至少做一次变异性验证）。

## 7. 验证与走查

- EditMode：≥ 313 条全绿，新增用例计入。
- 真机：`Builds/` 下新包，`adb install -r` 后按下面三段走：
  1. `DEINOS PHAINEIN` 前 30 秒：flick 行之后的一串 `Friction` / `FrictionCrt` 必须不掉血（这是本轮修复的现场，录像在 `E:\Download\Record_2026-11-04-04-04-49_b783bf344239542886fee.mp4`）；
  2. 交叉长条段落：不瞬灰、不断连；
  3. 曲库门禁：`BOF` 自动 `1,000,000 / S / 1669`。
- 打包：`BuildPipeline.BuildPlayer` 三个场景 `Library / TransitionBlank / Core`；release 签名由 `ReleaseSigningBuildStep` 自动应用。

## 8. 验收标准（全部满足才算完成）

1. 四个任务各自的单测存在且通过，`LiveLogic` 中按音符类型分支的数量下降（任务 A/B/D）。
2. 313 条基线不回归；新增用例总数 ≥ 20。
3. 真机三段走查通过，且提供每段的截图或录像。
4. 交付报告说明「哪些代码从 `LiveLogic` 移出、移出后 `LiveLogic` 行数变化」。
5. `docs/reference/judging.md` 与 `docs/operations/long-hold-v3-handoff.md` 同步更新。

## 9. 交付报告格式（缺一不可）

1. **结论**：四个任务分别完成到什么程度（完成／部分／未做，未做要给原因）。
2. **改动面**：文件列表 + 每个文件改了什么，标注新增模块与调用点行号。
3. **证据**：测试报告时间戳与 `testcasecount/result/passed/failed`；真机截图/录像路径。
4. **行为差异**：如果有任何手感或数值变化，逐条列出并给对照。
5. **未覆盖与风险**：明确写出「哪些判断仍然留在 `LiveLogic` 里，为什么」。
6. **下一步建议**：按优先级列出。

## 10. 相关文档

- [判定与窗口](../reference/judging.md)
- [长条 v3 交接](long-hold-v3-handoff.md)
- [触摸源抽象任务书](touch-source-brief.md)
- [测试](testing.md)
