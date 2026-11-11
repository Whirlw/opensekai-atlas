# 长条第三轮交接（2026-11-03，接手先读此文件）

> **状态：已被后续重构取代（历史文档，不再更新）。** 现行交接看 `handoff-2026-11-11.md`。

## 1. 结论

**代码已修改并提交，第三版尚未打包、未做本版曲库门禁、未做真机确认。用户明确要求“不要再测试，改完赶紧写交接”，因此停止追加验证和出包。** 在此指令之前已启动的最终 EditMode 完成：282/282，通过，0 failed。

这次用用户的真实谱面复现了问题：两根手指保持左右位置，在宽窄长条交叉时换条，旧逻辑会把仍可由两根手指覆盖的两个长条挤到同一手指，导致一个长条本帧输入后仍是 Release（灰色），若碰到计分节点则 MISS。原持有者优先必须允许**成对换指**，不能逐条永久占住原手指。

用户最新实测：跟着原长条交叉换手位置不会断；保持左右位置换条会瞬灰/有时断连。不要再把此问题当成固定十六分小节错误，也不要宣称第二轮的 FrictionLong 修复解决了这份普通 LongNote 谱面。

本轮在候选分配前先保留有效原归属，再用增广匹配给未能续上的长条找到可用手指，必要时把宽条移到另一个有效手指，让窄条也有手指。原有一指每帧判一个候选、车道/时间阈值都不变。

## 2. 分支与提交

- 仓库 `E:\DeepseekHarness\OpenSekai`，Unity 6000.6.7f1。
- 当前分支 `codex/long-hold-pickup-fix`；最初任务基点 `94330df`，实际开始时另有任务书提交 `e4b217a`。
- 第一轮：`29bf597` 延迟拾取归属保护，`9905ebe` 报告。用户反馈立即断改善但完全重叠仍断。
- 第二轮：`90396a1` 轨迹长条记录持有者，`0092411` 包含关系用例，`785dc2c` 报告。用户反馈仍灰/断。
- **本轮 `4a4a739` — `fix(live): allow coordinated finger exchanges across held notes`**，包含生产修复、纯函数单测、连续输入测试、真实谱面最小几何夹具。
- 本交接另作 docs 提交；hash 用 `git log -1` 查看。

## 3. 改动面

`git show --stat 4a4a739`：

```text
 Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs   |  56 ++
 Assets/Game/Runtime/Sekai/Live/LongNote.cs         |   5 +
 .../Game/Runtime/Shared/Live/HoldInputMatching.cs  |  34 +
 .../Runtime/Shared/Live/HoldInputMatching.cs.meta  |   2 +
 .../Tests/EditMode/Live/HoldInputMatchingTests.cs  |  61 ++
 .../EditMode/Live/HoldInputMatchingTests.cs.meta   |   2 +
 Assets/Tests/EditMode/Live/LiveLogicInputTests.cs  | 156 +++++
 Tools/fixtures/hold-overlap-regression/README.md   |  14 +
 Tools/fixtures/hold-overlap-regression/score.json  | 735 +++++++++++++++++++++
 9 files changed, 1065 insertions(+)
```

- `LiveLogic.cs:919,991`：Allocate 开始时执行 AssignContinuingHoldInputs；先按现持有者匹配，再给未匹配的 InputBegan/Input/Release 长条增广匹配。使用构造时分配的 128 项数组和缓存 delegate，不新建每帧集合/委托。
- `LiveLogic.cs:934,1206`：已分给持续长条的输入不能再被其它候选占用；分配和延迟拾取都检查。
- `LongNote.cs:428`：internal AssignHoldingInput 只更改持有者，不刷新 lastInputFrame；续命仍由真正 Judgment 完成。
- `HoldInputMatching.cs`：internal 纯匹配算法，优先空闲有效输入，再递归移动已有占用；不合法的车道不会成为匹配边。
- `HoldInputMatchingTests.cs`：宽窄换指、保留原主、失败不挤掉已有持握、多级换指、无输入、无有效输入六个用例。
- `LiveLogicInputTests.cs`：真实 OnUpdate → FakeTouchSource → OnInput，包含六组连续曲线/宽度变化，以及真实谱面 11 组对照。模拟引擎帧间边界时显式将 lastInputFrame 设为 Time.frameCount-1；不是设备触摸录制回放。
- `Tools/fixtures/hold-overlap-regression`：来自用户谱面的七条链、48 个节点、BPM172，只保留几何，无音频、封面或完整曲包。测试直接读此稳定路径，无 TestResults 或用户绝对路径依赖。

未改 public API、未重命名原成员、未改 Graphics/AssetBundles/构建脚本/ProjectSettings；未提交 Builds、TestResults、Library。

## 4. 证据

### 用户资料及定位

- 用户提供 `E:\Download\chart (1).zip`：Breach of Faith / BOF，Supire / Eili，MASTER 34，谱师 yorutsuki，BPM172，fillerSec=9。
- 原包含 audio.ogg、jacket.jpg、manifest.json、score.json。仅诊断提取的 JSON 在 `TestResults/user-chart/`，未提交原包。
- 截图 `C:\Users\31089\AppData\Local\Temp\codex-clipboard-7130722f-340d-4840-a930-cb0cbc987789.jpg`：一黄一绿、宽窄不同的曲线长条交叉；COMBO71、score79268，约165fps。

### 哪一帧、哪个列表/状态、谁改坏

真实谱面起点 ticks9600（源 id978 的绿色普通长条）与 ticks13440（源 id1002 的黄色普通长条）组合：

- 60Hz 模拟第359帧，谱面时间 **12.96008s**；165Hz 第988帧，**12.96462s**，绿色长条在 OnInput 后仍为 **Release**。绿色实际浮点车道分别为 [1.138093,4.517457]、[1.122099,4.496132]，旧 holder=2。
- ticks9600/ticks21120 另一组合，165Hz 第1448帧，**15.7525s** 同样 Release。
- 输入轨迹：两根手指保持左右顺序、分别跟随屏幕上的两个中心；跟随各自原长条而让双手位置交叉的对照没有失败，用户也确认这种操作不再断。
- 机制：旧 IsReservedForAnotherInput 只逐条保留仍在宽条内的原持有者。当另一条需要同一手指接管时，宽条不肯移到空闲且有效的另一指；候选经过 Allocate / PickUp 后不能让两个输入各续一条。EvaluateJudgment 的 Clear + break 使多余候选没收到 Judgment，lastInputFrame 未刷新；本帧状态留在 Release。NoteLineView.cs:273 使用 ReleaseColor，所以实际显示灰；LongHoldCombo 若在此时 Last，下一次 Excute 可将其变 MISS。
- 新逻辑在候选被删之前整体安排持握归属，不再用放宽阈值遮盖缺帧。

### 测试与门禁

- 修复前：`TestResults/long-hold-user-chart-before-matching.xml`，**276 total / 273 passed / 3 failed**，失败恰为以上三组不交叉手指换条用例。
- 初版匹配修复后：`TestResults/long-hold-matching.xml`，276/276/0。
- 最终稳定夹具、纯函数和中间计分检查：`TestResults/long-hold-v3-final.xml`，**282 total / 282 passed / 0 failed**。此运行在用户叫停前启动，叫停后只读取已完成报告，没有再跑测试。
- 本轮曲库门禁：**未运行**。上一版截图 `TestResults/long-hold-tick-gate.png` 是 1,000,000/S/221，只能证明第二版，不得标记为第三版结果。
- 本轮真机四项：**均未验证**（双长条重叠、双长条交叉、长条叠普通音符、正常结束与 MISS）。用户没有拿到第三版 APK。

## 5. 风险与未覆盖

- **第三版没有 APK**。现有 `Builds/long-hold-overlap-v2.apk` 不含 4a4a739，不要让用户重复测试它来判断第三版。
- 换指匹配使用每输入一个持续长条的容量；超过输入数的持握、相同 fingerId 多条样本、Ended/Canceled 与尾部同帧的复杂交互还需审查/设备确认。
- 归属在分配阶段更改，但 lastInputFrame 只在 Judgment 中刷新；若后续候选阶段仍有其它拒绝路径，需要查看已匹配的候选是否确实进 Evaluate。
- 另发现独立起手缺口：两条轨迹长条从起点就相互包含时，可只有一条起手。最初诊断失败保存在 `TestResults/long-hold-nested-start-failure.xml`（265/263/2）；后续连续测试改为分离起手再进入包含区。这项不是本次截图的已持握换指缺陷，**未修复**。
- 曲线判定和渲染共用 LiveConfig.GetNoteLineCenterProgress，未发现简单的“渲染曲线、判定直线”差异。
- 原任务书“判定列表不是每帧重建”不准确：LiveLogic.InitializeUpdate 每帧清空 `_judgmentNoteList`。不要再依据该描述推导跨帧永久丢列表。

## 6. 遗留及接手顺序

1. 用户当前要求立即交接、不要再测试；尊重这一停止点，不自动继续出包/门禁。
2. 用户下次要求继续时，以 4a4a739 为生产代码起点，先读本文件和真实谱面失败报告，避免重做前两轮的静态候选推断。
3. 审查全局匹配对释放/尾部/触摸取消的边界；按用户新的授权运行剩余门禁、生成明确标记 v3 的 APK。
4. 用户用同一 Breach of Faith 交叉段、保持左右手位置复测，分别确认不瞬灰、不 MISS；再补四项人工门禁。
5. 直到本版曲库与真机确认齐全，不得标记整个任务验收完成。

## 7. 第四轮：保持刷新与归属解耦（2026-11-04，提交 `cc8fff2`）

### 7.1 重新定位的机制

- `LiveLogic.EvaluateJudgment`（`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:1251` 起）每指每帧只判一条候选，判完把手指加入 `_usedFingerList` 并 `break`。
- `lastInputFrame` 只在 `LongNote.Judgment` → `RecordHoldingInput` 里刷新（`Assets/Game/Runtime/Sekai/Live/LongNote.cs:407,422`），释放条件 `lastInputFrame + MissMesh < Time.frameCount`（`:328`，`MissMesh = 0`）。
- 因此「保持」过去被绑在候选归属上：这一帧没抢到名额的长条必然掉帧转 `Release`。第三轮的换指匹配是在这个约束下给每条长条找到手指，属于把不变量做对之前的权宜。
- 与 combo 的关系：`LongHoldCombo.Excute`（`Assets/Game/Runtime/Sekai/Live/LongHoldCombo.cs:45-62`）在自己的十六分音符时刻判断车道上有没有手指，所以 **combo 只在十六分音处结算**，但长条存活是每帧的 `lastInputFrame`，两条链路不同。

### 7.2 改动

- 新增 `LiveLogic.RefreshSustainedHolds()`，在 `Judgment()` 里 `PickUpNotBeginInputNotes()` 之后、`EvaluateJudgment()` 之前调用：对每条 `HoldingTouchId >= 0` 且处于 `InputBegan/Input/Release` 的长条，若其持有者手指仍在车道内，直接 `RecordHoldingInput`，**不占候选名额、不改归属**。
- 新增 `LongNote.ContainsLane(float lane)`（internal）：只做车道包含判断，不含 `IsJudgment` 的判定时间窗——两个十六分音之间的时间窗是关闭的，用 `IsJudgment` 会把续命挡掉。

### 7.3 验证

- `TestResults/editmode.xml`：**287 total / 287 passed / 0 failed**（在本轮新增 3 个用例之后）。
- 新增用例（`Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs`）：
  - `SingleFingerSustainsBothOverlappingHolds`：一指覆盖两条重叠长条，两条都必须续命（改动前失败：`Expected: 1 But was: 0`）。
  - `SecondFingerDoesNotStaleTheFirstHold`：第二根手指进入车道（Began / Stationary 两种）不得让原持有者掉帧。
  - `SustainedHoldOnlyRefreshesWhileTheHolderStaysInLane`：手指在车道内 → 刷新；移到车道外 → 不刷新。

### 7.4 关于“删除换指匹配”：尝试过，但被测试否决

- 真机通过（用户确认交叉段不瞬灰、不断连）之后，曾提交 `45431b8` 删除 `HoldInputMatching` 与分配期增广匹配，理由是续命已由 `RefreshSustainedHolds` 接管。
- **这个删除是错的，已 revert**。`AuthoredCrossingAllowsFollowingOrExchangingFingers` 的 `keepSides=true` 三组用例在删除后从绿变红（`note=13440`、`note=21120` 停在 `Release`）：那类谱面里长条的持有者手指会整段停在另一侧，需要**把归属交给仍在车道内的另一根手指**，而续命刷新只会沿用旧持有者，无法换手。
- 结论：`RefreshSustainedHolds`（同一持有者跨帧续命）与 `HoldInputMatching`（跨手指换手）解决的是两件不同的事，两者都需要。`IsReservedForAnotherInput` / `ShouldReserveHold` 同理保留。

### 7.5 开头 MISS 后仍可中途接上（用户反馈的原版行为）

- 机制：`LongNote.Excute` 在头部超过判定窗后走 `State = Last → Miss + Release`；过去 `AddCheckLongInputedNormalNoteTouch` 只在头部 `Result == None` 时才允许补候选，于是头部 MISS 的长条再也起不了手。
- 现在 `LiveLogic.CatchUpMissedHeads()` 在 `RefreshSustainedHolds()` 末尾单独处理：对「`State == Release` 且 `Result == Miss` 且 `OffsetJudgeTime <= 0`（身体还没过完）」的长条，只要有空闲手指落在其车道内，就直接 `Judgment()` 把保持接上并记下归属。它**不经过候选队列**，因此不会占用每指每帧唯一的判定名额，也不改变交叉谱面的既有行为。
- 配套：`LongNote.RestoreHeldState()` 把仍被覆盖的 `Release` 拉回 `InputBegan`，避免“手指还按着却一直显示灰色”。
- 用例：`MissedHeadDoesNotBlockCatchingTheHoldUpMidBody`（头部 MISS 后中途按下 → 进入 `Input`/`InputBegan` 且 `HoldingTouchId` 正确）。
- 全量：**288 total / 288 passed / 0 failed**（`TestResults/editmode.xml`）。
- 仍未验证：本版未出包、未做曲库门禁、未真机复测；`Ended/Canceled` 相位与多条长条共享同一 fingerId 的极端样本未覆盖。
### 7.6 同帧多次判定的显示取最差

一帧里可能同时结算好几个音符（和弦、长条尾巴和叠在它上面的键）。`JudgmentView.Excute` 原来每调用一次就改写精灵图，于是最后一个被判的音符赢——一次 Miss 会被随后的一发 Perfect 盖掉，玩家只看到 PERFECT。

现在按帧聚合：`OpenSekai.Shared.Live.JudgmentAggregator.Worse(current, candidate)` 取 `NoteResult` 里更差的那个（枚举从 `Miss=1` 递增到 `JustPerfect=8`），并排除 `None` 与 `Auto` 这两个不在阶梯上的值。`JudgmentView.Excute` 返回「我是否赢得了这一帧的弹窗」，`LiveFrontView` 只在赢得时驱动 `JudgmentDescriptionView`，这样 Fast/Late 注记也跟着同一条音符走。

用例：`FramePopupShowsTheWorstJudgement`（6 组参数）。

## 8. 摩擦条成串漏判（2026-11-04，提交 `29b601e`）

现象：flick 行之后一排 `Friction` / `FrictionCritical` 在几帧内全部 Miss，LIFE 一次掉几百。

原因：`EvaluateJudgment` 每指每帧只判一条，而且**候选没有真的被吃掉也 `break`**。`FrictionFlickNote` 一被碰到就进 `Input`，要等过线那一帧仍有有效划动才完成；等待期间它每帧独占该手指的唯一名额，排在它后面的摩擦条一次 `Judgment` 都拿不到，`isTouched` 永远是 false，到点 `Last → Miss`。

改法：只有候选进入 `Done` 或已有判定结果时才 `input.CandidateNotes.Clear(); break;`，否则 `continue` 判下一个候选。用例 `ANoteThatDoesNotConsumeThePressStillLetsTheNextOneBeJudged`。

同一轮另修：判定列表登记原本用「每音符自己的显示提前量」（随 `speedRatio` 变化），快速音符只提前约 130 ms 进入列表，已贴到判定窗口边缘；`IsJudgeTimingTimeOffsetNote` 改为 `Mathf.Max(timeOffset, 0.35f)`，保证至少提前 350 ms 登记。用例 `AFastNoteEntersTheJudgeListBeforeItsOwnDisplayLead`。

真机复验：DEINOS PHAINEIN 的 flick 行不再掉 LIFE；出包 `Builds/hold-final-2026-11-04/OpenSekaiAtlas.apk`（113.4 MB，无诊断日志）。EditMode 313/313。

## 9. 尾判与划键长条结束点（2026-11-04，提交 `a87a24b` / `7992dd4` / `609c27a`）

长条尾判分两种触发，规则来自原版实测：

| 尾部音符 | 触发 | 评级 |
|---|---|---|
| 普通尾 | 条内手指**离开屏幕**后的那一帧 | `LongEndJudgement` 毫秒表，有早晚档、无 bad |
| 划键尾（`FlickNote` / `FrictionFlickNote`） | 条内一次**滑动** | 同上，但提前滑会推迟到松手后第 3 帧 |

要点：

- 只认**时间最晚**的子音符为尾（隐藏点与预设点按谱面顺序追加，可能排在尾之后）。
- 尾的时间必须取自时钟：音符的 `OffsetJudgeTime` 只在 `Excute` 时刷新，条身走到尾之前尾音符根本没被 `Excute`，读它永远得到 0，任何位置的松手都会被判成完美尾判（`AReleaseInTheMiddleOfTheBarIsNotTheEndPoint` 守着这条）。
- 划键尾**不接受抬手**当结束（`AFlickEndedTailIgnoresAPlainLift`），否则在条内滑动调整位置会被当成结束。
- 提前滑动（尾时间之前、且落在尾窗口内）只记录，松手后第 3 帧结算；推迟本身让结算时刻更靠近尾时间，所以早划更容易拿到好档（`ASwipeBeforeTheTailStandardTimeJudgesOnTheThirdFrameAfterRelease`）。
- `IsTailJudgeableAt` 与 `IsTailWindowOpenAt` 分开：前者的 `NoteCategory.Long` 闸门是为抬手路径准备的，划键尾的音符类别是 flick，必须走后者。

## 10. 装饰长条的划键尾（2026-11-06，提交 `6b2ffc9`）

现象：以装饰音符开头的长条，条尾的划键**完全划不动**；判定特效、音效、判定列表都没有它。前面几轮用 `IsDecoration` 判断，改了 `SetDecoration`、改了 `FlickNote` 的评级表、又改了 `UpdateJudgmentNoteArray` 的调用点，全部无效。

原因：装饰长条不是「`IsDecoration == true` 的长条」，而是 **`GuideNote : LongNote`**（`Assets/Game/Runtime/Sekai/Live/GuideNote.cs`，`HasJudgment => false`）。注册进 `_judgmentNoteList` 的闸门要求 `note.HasJudgment`，装饰条永远过不去 ⇒ 它压根不在判定列表里，尾部的划键一次都没被采样。

改法（`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`）：

- `PlayableTailOf(note)` 的判据由「尾不是装饰」改成「尾有判定」（`tail.HasJudgment`）；
- 注册闸门放行「装饰条但尾部可判」：`(!note.IsDecoration || playableTail) && (note.HasJudgment || note is FrictionHideLongNote || playableTail)`。

用例 `ARealSwipeOnAGuideBarTailIsJudged`（`Assets/Tests/EditMode/Live/FlickEndBehaviourTests.cs:311`）：`GuideNote` + 左向 `FlickNote` 尾，塞进判定列表、置 `InputBegan`、给一次 `delta=(-60, 0)` 的真实滑动，走 `CoverHoldsWithAnyFinger` 后为 `Done` + `JustPerfect`。

定位方法（值得复用）：在 `NoteBase.State` 的 setter 里输出调用栈（`System.Diagnostics.StackTrace` 取 1..3 帧）。这次的 `tailDone … caller=NoteBase.Excute<FlickNote.Excute<GuideNote.Excute` 直接暴露了真实类型，比继续猜 `IsDecoration` 快得多。

真机复验：曲库 10 首、165.4 fps，装饰条尾划键可判且不再黏键；`Builds/guide-tail-clean-2026-11-06/OpenSekaiAtlas.apk`（117,073,754 B）。EditMode 384/384。

## 11. 尾键、条头 MISS 与划键判据（2026-11-06，提交 `3937f08` / `1ba54a6` / `ac0d659` / `ccb162f`）

这一轮把「尾巴在画面上怎么活」和「尾判怎么算」彻底分开，并把真机上报的三个问题一次收掉。

### 11.1 尾键的显示与消失

规则（用户口径）：

- 只有**普通开头**的长条会画出尾部那个键；`GuideNote`、`FrictionLongNote`、`FrictionHideLongNote` 从头到尾不显示（`LongNote.ShowsTailKey`）。
- 键一旦出现就跟着条走：中途断连、头部 MISS 都只是条变灰，键原样跟随（`NoteBase.ShouldDeferViewRemoval()` + `KeepsViewUntilParentEnds`，`Assets/Tests/EditMode/Live/LongNoteViewLifetimeTests.cs` 5 条守着）。
- 键消失只有两个时机：走到条尾，或尾部判定完成。
- 键到了线仍未判定时**继续往下走**（不消失、不黏线）：`NoteBase.onRetireView` → `LiveLogic.RetireNoteView` / `TickRetiringViews`，`Progress >= 1.35` 才回收。

为什么必须重写：`FlickNote` / `FrictionNote` / `NormalNote` / `GuideNote` / `FrictionFlickNote` / `FrictionHideLongNote` 都各自 `override` 了 `State`，只在 `NoteBase` 的 setter 上做手脚不生效；六个 `override` 的 `Done` 分支统一改成 `if (!ShouldDeferViewRemoval()) OnUnSpawnNote();`。

### 11.2 条头 MISS

「手指提前压在轨道上等条头」不再是免费通过：`LiveLogic.CoverHoldsWithAnyFinger` 里加闸门

```csharp
bool headSettled = held.Result != NoteResult.None
    || held.State == NoteState.InputBegan
    || held.State == NoteState.Input;
if (covered && headSettled) held.KeepAlive(currentFrameInfo.time);
```

头部没被按到时条不进入按住，`LongNote.Excute` 正常走 `Wait → Playing →（过 JudgeTime）Last → Miss`；头部 MISS 之后 `Result != None`，闸门放行，**「开头 MISS 中途接上」仍然成立**（`AFingerOverAHeadThatWasNeverPressedDoesNotAdoptTheBar`）。

### 11.3 尾判的三条通道

| 尾部音符 | 通道 | 结束条件 |
|---|---|---|
| `FlickNote` | `LongNote.TrySwipeTail`（`UsesSwipeTail`） | 条内滑动达速；提前滑推迟到松手后第 3 帧 |
| `FrictionFlickNote` | 它自判（`UsesStandaloneTail`），长条只 `DetachStandaloneTail` | 自己出结果就跟随；窗口关闭仍无结果 → 尾巴补 Miss |
| 其它（普通/`FrictionNote`/隐藏） | 抬手路径 `JudgeTailAsRelease` | 条内手指离屏，下一帧评级 |

三条通道的判据都必须自带窗口：`IsPastTailWindow` / `ExpireTail` 每帧兜底，谁都不会停在判定线上。

### 11.4 划键判据：60 fps 净位移 + 正确的像素单位

- 判据按用户口径：**一帧（1/60 s）内的平均速度 ≥ 5 cm/s**，也就是 16.7 ms 内净位移 ≥ `flickDistance`。实现见 `LongNote.TrySwipeTail` 的 `_swipeAccum` / `_swipeWindowStart`：向量累加抵消抖动，窗口满或净位移已达标就判，真划不必等满一帧。
- **单位 bug（本轮最关键）**：`Assets/Graphics/Resources/live/config/LiveBundleBuildData.asset` 的 `_flickDistance: 0.083333` 是**厘米**，必须经 `LiveUtility.ScreenDpiToInch` 换成像素才能和 `touch.delta` 比较。`RuntimeChartConverter.cs:535` 原来把它原样传给 `FlickNote`（阈值变成 0.0833 **px**，小 155 倍），而 `:538` 的 `FrictionFlickNote`、以及 `SusRuntimeConverter.cs:354/422` 两条都做了换算——这就是「普通 flick 尾总是自动判、friction flick 从来没误判」的原因。
- 症状特征：日志里 `tailSwipe … speed=6 min=5 mag=0.0`——位移显示不到 0.05，速度却过门。

### 11.5 MISS 字符

`LiveLogic.OnJudgmentNote` 曾经因为 `SuppressJudgmentEffect` 提前 `return`，把 `LiveViewExt.JudgmentNote` 一起挡掉；而 `JudgmentNote` 同时负责**弹判定字符**和**触发打击特效**，于是「长条判死的尾/检查点」只扣分扣血、不显示 MISS。

现在这个手写标志已删除：只对 `note.IsSkip`（长条内半拍刻度、不可见隐藏点）提前返回，其余一律进 `JudgmentNote`；「没打中不响」由 `TapEffectView.Excute` 按结果类型（`None || Miss || Pass || Bad`）自行判断，不再需要 note 级开关。

### 11.6 定位方法（复用价值高）

- **先写管线级用例**：直接 new `LongNote` 调 `NotifyLaneSwipe` 这类捷径会绕过真实输入循环，测不出「delta 被车道门丢弃」「状态被覆盖」这类 bug（这轮踩过两次）。用例要走 `CoverHoldsWithAnyFinger`（反射调用即可）。
- **日志落盘到设备文件**，`adb pull /storage/emulated/0/Android/data/io.github.opensekai/files/logs/live-*.log` 直接拿整局；`adb logcat` 环形缓冲几秒就冲掉，`Start-Process` 重定向会块缓冲。
- `LiveTrace` 的默认掩码一定要覆盖要看的分类，否则「日志里一条都没有」会被误读成「代码没执行」（本轮 `Swipe` 类就一度没开）。
- 拒绝在真机上「猜链路」：`tailJudge`（deferred 与否）/ `tailSwipe`（speed、min）/ `flickFinished`（是否自判）三列足以区分三条通道。

### 11.7 复验

- EditMode **413 / 413**；本轮新增/改写的用例：`AFingerOverAHeadThatWasNeverPressedDoesNotAdoptTheBar`、`AnUntouchedFrictionFlickTailIsGradedMiss`、`AMissedTailShowsItsCharacterEvenWhenTheBarIsTheOneThatFailedIt`、`AFailedTailIsGradedAndStillReportsItsJudgement`。
- 装机包 `Builds/miss-popup-2026-11-06/OpenSekaiAtlas.apk`（117,077,554 B），真机 165.3 fps，用户确认三处 MISS 字符与「按住不再自动判」均正常。

### 11.8 自动模式

`GuideNote.AutoJudgment` 原来直接 `return false`（装饰音符不参与判定），于是「装饰开头 + 有判定的尾」这条长条在自动模式下**永远拿不到尾键**——自动模式没有手指去划，尾只能由长条驱动。现在它把可判的尾交给 `childNote.AutoJudgment`，尾结算时通过生命周期契约（`ParentRole.Tail`）把长条收尾；纯装饰（尾也没有判定）仍然 `return false`。

用例 `AGuideBarTailIsAutoJudged`：尾时间之前返回 false 且尾仍未判；过线后返回 true、尾为 `Auto`、长条 `Done`。

### 11.9 遗留

- 任务 B（把 `FrictionNote` 两段式、`FlickNote` 一次性 delta、`ConnectionNote` 单帧窗口统一成显式完成协议）仍未开始。
- 曲库门禁（BOF 自动 `1,000,000 / S / 1669`）在本轮改动后尚未复跑。