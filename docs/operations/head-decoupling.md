# 长条头与条体解耦（交给外部实现者的任务书）

状态：待实现 · 基线 `main = eac5b99` · EditMode `518 / 514 通过 / 0 失败 / 4 跳过`

## 1. 为什么要做

真机上有两个顽固问题，反复打补丁都没修好（每次只改一处、每次都没效果）：

1. **提前划的 flick 多爆一个 Miss**：在尾标准时间前 125 ms 内有效滑动，尾判已经出来了，同时又冒一个 Miss。
2. **friction 开头的长条，手指一按上去连击整批飞涨**。

试过并被否决的补丁（都在 git 历史里，别重复）：

| 补丁 | 结果 |
| --- | --- |
| `TickSwipeTail` 允许 `Last`/`Release` 继续计数（`aad9160`） | 「机制回来了」但 Miss 还在 |
| 尾键超时押后（`01b7954`）+ 恢复按住态不重发（`121fc41`） | 真机「都没改善」 |
| 中间点按自己的窗口判定（`9352bb7`） | friction 仍然一样 |
| 头部 Miss 只在未判定时发（`eac5b99`） | 同上 |

**现状更新（真机验证，2026-11-10）**：第 1 条（提前划多爆 Miss）已由 `eac5b99` 修好——`PublishHeadMiss` 现在只在头部未判时发布，且 `RestoreHeldState` 回到 `Input` 而不是 `InputBegan`。**这两处不要回退**，它们是本条的解。第 2 条（friction 连击飞涨）仍未解决，是本任务要收口的目标。

结论（用户判断，本任务的前提）：**这两个问题的共同根源是「长条头」的判定没有和条体解耦**——条体的视觉状态同时承担了「发判定/发连击/发分数」的职责，任何一次状态回溯（松手、重新按住、宽限期重启）都会重复上报或误报。

## 2. 现状：耦合点清单（都要处理）

文件：`Assets/Game/Runtime/Sekai/Live/LongNote.cs`
- `State` setter（约 97–125 行）：当 `Scrolling → InputBegan` 或 `Last → InputBegan` 时直接 `onJudgment / onUpdateCombo / onUpdateScore / onDamage` 四个回调。**条体状态在发判定**。
- `RestoreHeldState()`（约 1360 行）：`Release || Last → InputBegan`，于是每次 refresh（每帧）都可能穿一次上面那个入口。
- `Excute()` 中 `if (State == NoteState.Last) { State = NoteState.Release; Head.PublishHeadMiss(); }`：离开宽限期时无条件发头部 Miss。
- `NotifyHeadMiss()`（约 1226 行）：直接发 `onDamage/onUpdateCombo/onJudgment`，没有"只发一次"的保护。

文件：`Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs`
- `PublishHeadMiss()`（约 21 行）：只判 `PublishesHeadMiss`，不判「头部是否已经判过」，所以已判 Perfect 的头部会被改写成 Miss。
- `Judgment()`（约 44 行起）：头部判定写的是**条体的 State**（`SetHeadJudgmentState`），自身不发布会话结果，完全依赖 setter 的副作用。

## 3. 目标设计

1. **条体状态只表示外观**：`Wait / Scrolling / InputBegan / Input / Last / Release / Done` 只驱动颜色、宽限期、生命周期；进入任何状态都**不再发** `onJudgment / onUpdateCombo / onUpdateScore / onDamage`。
2. **头部是一次性音符**：头部判定（按下或超时）由 `LongNoteHead` 自己写结果、自己发布，且**只发一次**（用 `_headPublished` 之类的标志，池复位时清零）。头部结果一旦写出，任何条体状态变化都不得改写它。
3. **中间点**沿用已落地的窗口规则（`9352bb7`）：只在自己的 `[-125ms, +141.7ms]` 窗口内、由帧时钟判定，不补判积压。
4. **尾部**沿用现有 `ITailBehaviour` / `TickSwipeTail` 的延迟划尾语义，不改。
5. 发布顺序保持 `onDamage → onUpdateCombo → onJudgment`（现有顺序，别调）。

## 4. 必须新增的测试（先红后绿）

放 `Assets/Tests/EditMode/Live/`，建议新文件 `HeadDecouplingTests.cs`：

1. `AHeldBarPublishesItsHeadOnce`：普通头长条按住若干帧，`onJudgment` 回调计数必须恒为 1（现在的实现是每帧涨）。
2. `AJudgedHeadIsNotOverwrittenByTheGraceEnding`：头部判 JustPerfect 后让条进 `Last` 再出 `Last`，头部结果仍是 JustPerfect，且回调计数不增加。
3. `AnEarlySwipeAddsNoMiss`：尾部延迟划尾期间松手（条进 `Last`），断言没有额外 Miss 被发布、`childNote.Result` 只有尾判那一个。
4. `ARerefreshKeepsTheBarLitWithoutRepublishing`：`RefreshHeldInput` 连续调用 10 帧，`onUpdateCombo` 计数仍为头部那一次。

## 5. 红线（不许动）

- 不动 `LiveLogic.JudgeDueComboPoints()` 的 `_barWasInPlay` 语义。
- 不动中间点窗口判定（`LongNoteHead.ConnectionNoteJudgment` 里的 `EarlyGoodMs / LateGoodMs` 门禁）。
- 不动划键尾判：`TickSwipeTail` / `_swipeTailDeferred` / `_swipeTailCapped` / `FailTail` / `JudgeTail` 的判定与延迟语义。
- 不动 `Assets/Game/Runtime/OpenSekai/Live/Conversion/` 下的谱面转换。
- 不动三帧宽限常量 `HoldGraceFrames = 3` 与 `Last` 的进入/退出条件。
- 不改 `LiveHoldCoordinator` 的「谁持有哪条」归属逻辑（可以改它调用哪个方法，但归属规则不变）。

## 6. 工具链

```
C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe ^
  ...\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll ^
  @<Library\Bee\artifacts 下最新的 OpenSekai.Game.rsp / OpenSekai.Tests.EditMode.rsp> -nologo
```
改完 `unity_eval`（端口 7800，项目 `E:\DeepseekHarness\OpenSekai`）：
`AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`，
等 `Library\ScriptAssemblies\OpenSekai.Tests.EditMode.dll` 时间戳变新，再
`OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<名字>.xml")`，读根节点 `test-run`。

基线 `518 / 514 / 0 / 4`。交付要求 `failed = 0`，且含上述 4 条新测试。

## 7. 交付

1. 代码改动（预计 2–4 个文件）。
2. `docs/operations/head-decoupling-report.md`：耦合点逐条怎么解、4 条新测试的断言、最终数字、以及"哪些地方我判断可能影响手感但没敢改"。
3. 不要顺手做别的重构；一次只交这件事。

## 8. 验收（本仓库维护者）

- 全量 EditMode `failed = 0`，4 条新测试在内。
- 逐条核对：条体状态 setter 里不再有四个回调；头部发布有且仅有一次的保护；`PublishHeadMiss` 不会覆盖已判头部。
- 真机：①提前划只出 1 个判定；②friction 条按上去连击逐点 +1；③普通长条按住/换手/尾判无回归。
