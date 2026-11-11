# 判定通道与解耦收尾：任务 B 的执行计划

> **状态：已完成并合入（历史文档，不再更新）。** 现行规则见 `reference/judging.md`，当前待办见 `tail-classes.md`。

> 目的：把长条那一轮（20 个缺陷）暴露出的四类系统性欠账一次处理掉——**判定通道没有显式表达**、**视图与判定共用状态**、**对象池跨局污染**、**单位与时间多真源**。
> 适用读者：接手本任务的外部执行者，以及后续维护判定内核的人。
> 前置条件：读过 [判定与窗口](../reference/judging.md)、[长条 v3 交接](long-hold-v3-handoff.md)（尤其 §9–§11）、[判定内核任务书](judgment-core-brief.md)（A/C/D 已完成）。
> 最后更新：2026-11-07

## 1. 一句话目标

用「一个通道枚举 + 每个通道一个实现」取代 `UsesReleaseTail` / `UsesSwipeTail` / `UsesStandaloneTail` 这类布尔开关，让「这条音符怎么结束、谁来结束、什么时候从画面上消失」在类型层面就唯一确定；**不改变任何现有判定手感**。

## 2. 项目与运行环境

- 仓库：`E:\DeepseekHarness\OpenSekai`（Open Sekai Atlas），工作分支 `codex/long-hold-pickup-fix`。
- Unity `6000.6.7f1`，仅 Android；真机 OPPO `PLQ110`（165 Hz）。
- EditMode：`OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", path)`（编辑器内），或 `Tools/run-editmode-tests.ps1`（需先关编辑器）。读报告先看 `LastWriteTime`。
- **当前基线：430 条 EditMode 全绿 + 4 条停放**（HEAD `0203afa`）。
- 出包：`AssetDatabase.Refresh(ForceUpdate)` → `OpenSekaiAssetBundleBuildPipeline.BuildForTarget(Android, true)` → `BuildPipeline.BuildPlayer`；`AssetBundlePackagingVerifier` 应报 14 条 bundle。

## 3. 现状（不要重复调研）

| 位置 | 问题 |
|---|---|
| `Assets/Game/Runtime/Sekai/Live/LongNote.cs` | `UsesReleaseTail`（virtual）/ `UsesSwipeTail` / `UsesStandaloneTail` 三个布尔决定尾的结束通道；`IsTailJudgeableAt`、`IsTailWindowOpenAt`、`JudgeTailAsRelease`、`NotifyLaneSwipe`、`TrySwipeTail`、`DetachStandaloneTail`、`FailTail`、`ExpireTail` 分散实现 |
| `Assets/Game/Runtime/Sekai/Live/NoteBase.cs` | `KeepsViewUntilParentEnds` / `ShouldDeferViewRemoval` / `onRetireView` / `SupressView` / `IsViewConsumed` / `MarkViewConsumed` 六个与视图相关的开关；`ClosesSlotAfterPress` 由结果推断 |
| 六个音符类 | `NormalNote` / `FlickNote` / `FrictionNote` / `FrictionFlickNote` / `GuideNote` / `FrictionHideLongNote` 各自 `override` `State`，`Done` 分支里各自决定「要不要现在 `OnUnSpawnNote`」 |
| `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs` | `CoverHoldsWithAnyFinger` 里同时处理：覆盖、抬手、早划、划键、自判尾、超时、尾键回收；`PlayableTailOf` 决定装饰条是否进判定列表 |
| `Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs` | `flickDistance` 的单位换算（厘米 → 像素）只在这一处，`FlickNote` 构造函数仍接受裸 float |

这三处共同造成了「改一处坏两处」：尾的结束条件、尾键的显示时机、长条的收尾责任写在三个地方，任一处漏改都要靠真机录像才能发现。

## 4. 工作包

按交付批次排序，**每包独立可回滚**。

### WP0 判定通道表（只写文档与枚举，不改行为）

- 新增 `Assets/Game/Runtime/Shared/Live/NoteChannel.cs`：`Press` / `Slide` / `Friction` / `Swipe` / `GuideMarker` / `LongHold` 六个值，注释说明每个值对应「按下怎么记账、什么时候完成、由谁完成」。
- 在 `NoteBase` 上给每个音符类型声明 `internal virtual NoteChannel Channel { get; }`，先只声明、不消费。
- 完成判据：`assets/Tests/EditMode/Live/NoteChannelTests.cs` 断言每个音符类型的 `Channel` 与文档表格一致（表格同时写进本文档 §5）。

### WP1 尾的结束通道（任务 B 的主体）✅ 2026-11-08 完成

- 三个实现：`Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs`、`SwipeTail.cs`、`SelfJudgedTail.cs`，外加 `SilentTail.cs`（装饰条 / 显式跳过尾判的条：三个能力全否，`TryFinish` 恒 false，由条自己的超时静默结束）。
- 实际接口（比原计划窄）：`bool TryFinish(LongNote bar, in TailFrame frame)` + 三个能力属性 `AcceptsRelease` / `AcceptsSwipe` / `JudgesItself`。`TailFrame`（`Tails/TailFrame.cs`）只带一帧的事实：`Now` / `DeltaTime` / `SwipeDelta` / `ReleasedInside` / `Covered` / `IsBeingHeld`。
- `LongNote.TailBehaviour` 按 `childNote` 选择，**不缓存**（池会 `ResetNote`，缓存会被清）；`UsesReleaseTail` / `UsesSwipeTail` / `UsesStandaloneTail` 三个布尔与其 6 处内部分支已删除，改读 `TailBehaviour.Accepts*`。
- `LiveLogic.CoverHoldsWithAnyFinger` 里原来的 `if (UsesSwipeTail) … else if (UsesStandaloneTail) … else if (releasedInside …)` 三岔口收敛为一次 `held.TailBehaviour.TryFinish(held, new TailFrame(...))`；超时仍由 `held.ExpireTail(...)` 收尾。
- 完成判据：
  - 用例数不减少：482 → **485 条 / 481 passed / 0 failed / 4 skipped**（`TestResults/wp1-tail-contract2.xml`）；
  - `CoverHoldsWithAnyFinger` 里不再出现 `if (UsesSwipeTail)` 这类开关：已 grep 归零，并有源码扫描用例 `TailBehaviourContractTests.NoBarAdapterBooleansComeBack` 守住；
  - 真机四段复验（lift / swipe / self judged / silent）。

### WP2 完成协议（`PressDisposition` 的正确版本）

- 每个音符显式声明「这一按是否消耗每指每帧唯一名额」：`internal virtual PressSlot Slot => PressSlot.Consumes`（`Consumes` / `KeepsOpen` / `Ignored`）。
- `JudgmentSchedule.Outcome` 由音符自己给出，不再用 `State == Done || Result != None` 间接推断。
- **不要**再做一次三态 `MarkPress` 记账（上一轮做过、被变异实验证明与结果等价、已删除）：这里要的是「类型级常量」，不是「每次按下记录一次」。
- 完成判据：`NotePressDispositionTests` 六条改为断言 `Slot` 常量；新增变异实验：把 `KeepsOpen` 改成 `Consumes` 必须让 `ARubNoteKeepsTheSlotOpenWhileItArmsItself` 变红。

### WP3 视图离开 `State`

- 新增 `Assets/Game/Runtime/Sekai/View/TailKeyLifetime.cs`：只根据「尾键时间」「判定结果」「条是否结束」三个输入决定「显示 / 跟随 / 下移 / 回收」，不读 `Note.State`。
- 删除 `NoteBase` 上的 `KeepsViewUntilParentEnds` / `ShouldDeferViewRemoval` / `onRetireView` / `SuppressView` / `IsViewConsumed` / `MarkViewConsumed`，六个音符类的 `State` override 里恢复统一行为。
- 完成判据：`LongNoteViewLifetimeTests` 5 条改为直接驱动 `TailKeyLifetime`；新增「断连不回弹」「到线未判继续下移」两条；六个 `State` override 全部消失（可用 grep 断言）。

### WP4 对象池卫生强制

- 新增 `Assets/Tests/EditMode/Live/PoolHygieneTests.cs`：反射遍历 `NoteBase` 及其子类的所有实例字段，断言 `ResetNote()` 之后每个字段等于 `new` 出来的默认值（`SuppressView`、`_swipeTailDeferred`、`_pressedThisFrame` 这类漏清会立刻被抓）。
- `OnSpawnNote` 的断言保留（`Result == None && OffsetJudgeTime ≈ 0`），条件必须用真能观察到的量。
- 完成判据：给任意一个字段加进 `ResetNote` 遗漏清单，测试必须变红（变异实验）。

### WP5 单一时间源收尾

- `OffsetJudgeTime` 降级为「缓存值」，XML 注释写明**禁止用于判定**；判定路径内 grep 断言不再出现 `OffsetJudgeTime`（`Time.frameCount` 只允许出现在 UI 去重）。
- 新增 `Assets/Tests/EditMode/Live/TimeSourceTests.cs`：反射扫描 `Assets/Game/Runtime/Sekai/Live` 与 `Core/Live` 下的源码字符串，出现 `Time.frameCount` 即失败（白名单为 `JudgmentView`、`TapEffectView`）。
- 完成判据：该测试对当前代码通过；手工插入一行 `Time.frameCount` 后失败。

### WP6 单位只在入口换算

- `FlickNote` / `FrictionFlickNote` 构造参数改名 `flickDistancePixels`，注释说明单位；转换只在 `RuntimeChartConverter` 与 `SusRuntimeConverter` 里做一次。
- 完成判据：新增用例 `AFlickTailUsesPixelDistance` 断言 0.0833 cm 在 400 dpi 下换算后 ≈ 13 px，且 `LongNote.TrySwipeTail` 的门槛与它一致。

### WP7 流程项

- `LiveTrace` 默认掩码改为 `All`（真机复现时不再出现「某类日志一条都没有」的误读）；设置页仍可全关。
- 新增 `Assets/Tests/EditMode/Live/LiveHarness.cs`：封装「造 `LiveLogic` + 塞判定列表 + 给输入 + 调 `CoverHoldsWithAnyFinger`」，并在注释里禁止直接 `new LongNote(...)` 后调内部方法写用例。

## 5. 判定通道表（WP0 的产物，后续所有改动以此为准）

| 音符 | `Channel` | 按下记账 | 完成条件 | 收尾者 |
|---|---|---|---|---|
| `NormalNote` | `Press` | 消耗 | 当帧判定出结果 | 自己 |
| `ConnectionNote` / `HiddenConnectionNote` | `Slide` | 忽略 | 到达 `Last` 后一帧内被按住 | 自己 |
| `FrictionNote` | `Friction` | 保持开放（首碰只置 `isTouched`） | 进度到线且被按住 | 自己 |
| `FrictionLongNote` | `LongHold` + `Friction` | 保持开放 | 进度到线且被按住；或内部摩擦音符判完 | 自己（**有判定，有声音**） |
| `FrictionHideNote` / `FrictionHideLongNote` | `GuideMarker` | 忽略 | 不判（`HasJudgment == false`） | 无（若其末端带可判音符，由那个音符自己结算；**全程静音**） |
| `FlickNote` | `Swipe` | 忽略 | 一帧内净位移达 `flickDistancePixels` 且方向正确 | 自己；作为尾时由长条驱动 |
| `FrictionFlickNote` | `Swipe` | 忽略 | 同上（自判，长条只跟随） | 自己 |
| `LongNote` | `LongHold` | 保持开放 | 尾部通道结算（本文件 WP1） | 尾 |
| `GuideNote` | `GuideMarker` | 忽略 | 不判；可带一条可判尾 | 尾（尾自己在列表里） |
| `LongNote`（装饰标志） | `GuideMarker` | 忽略 | 不判；可带一条可判尾 | 尾（尾自己在列表里） |
| `LongHoldCombo` / 隐藏点 | `GuideMarker` | 忽略 | 到期按「是否按住」给 PERFECT / MISS | 长条 |

### 5.1 谁进 `_judgmentNoteList`（2026-11-07 定稿，唯一判据）

**判据是「能力」，不是标志位。** 一根条能不能接输入，只看它能不能自己判：

```csharp
bool barAnswersInput = (note.HasJudgment && !note.IsDecoration) || note is FrictionHideLongNote;
if (barAnswersInput) _judgmentNoteList.Add(note);
if (!barAnswersInput && tailNote != null) _judgmentNoteList.Add(tailNote);   // 尾代替条
```

- `FrictionHideLongNote` 例外保留：它自己不判，但必须收持握输入来驱动内部隐藏点。
- 不能自判的条（`GuideNote`、带装饰标志的条）**不进列表**，它末端的可判音符**以独立候选身份进列表**，用自己的通道结算。
- 反例（曾经的真实故障）：把判据写成 `note.IsDecoration` 时，谱面里的装饰条**一个都不匹配**——见下一条。

**谱面里根本没有「装饰条」这个标志。** 装饰长条在 `score.json` 里是 `category 9 / noteBaseType 10`，转换后是 `GuideNote`：`HasJudgment == false`、`IsDecoration == false`。任何按 `IsDecoration` 写的分支对它们都是死代码。判断「谁不判」请用 `HasJudgment` / 类型，不要用 `IsDecoration`。

**长度只由父条驱动这一点仍然是事实。** 子音符不会自己进 `_judgmentNoteList`（只有 `_noteArray` 里的根音符会）。所以「尾怎么被判」这件事，必须在注册阶段就决定：要么条代判（`ReleaseTail`），要么尾进列表（本节的写法）。指望「尾自己会进列表」是错的。

**断这一层要用真实谱面驱动。** 手工 `new LongNote(...)` + 反射调内部方法写出来的用例，会在「候选分配」这一步全军覆没（连普通尾都进不了候选），得不出任何结论——见 §11。

### 5.2 三条通道不要混用（2026-11-07 追加，血泪版）

判定相关的东西横跨三条**互不相同**的通道。混一条，就会得到「修好一个、坏掉两个」：

| 通道 | 载体 | 只影响 | 千万不要用它做 |
|---|---|---|---|
| 绘制/路径 | `LongNote.ViewNoteList`、`LineNoteList` | 视图生成、头部插值、尾键是否出现 | 表达「这个音符该不该判」 |
| 判定候选 | `_judgmentNoteList`、`_connectionNoteList` | 每帧谁可以被手指选中 | 表达「有没有声音」 |
| 结算发布 | `NoteBase.PublishResult()` → `onJudgment` / `onUpdateCombo` / `onUpdateScore` / `onDamage` | **分数、连击、掉血、命中统计、特效与音效** | 当作「显示层通知」使用 |

- `PublishResult()` 不是显示通道。给一根**不计分的载体条**发布一个 `Pass` 去「让视图播个声音」，代价是：
  `ScoreLogic.CalculateAddScore` 的 `TakeNextNoteBaseScore()` 按 `_scoredNoteCount` 累积摊分，凭空多算一个音符 → **结算总分超过 1,000,000**；同时 `UpdateCombo` 把 `Pass`（`< Great`）当失误 → 连击被重置；而这条路径上的 `Miss` 还会再打一次到屏幕上。
- 想「只出声音/特效」，就不要经过结果发布：如长条按住音走 `LongTapEffectView`（自己在 `State == Input/InputBegan` 时起循环音），而不是给条补一个假结果。
- 反过来，**判了就必须发布**：一根有判定的条如果重写了 `OnUnSpawnNote()` 却忘了 `PublishResult()`，它就是「判了但什么也没发生」（无声、无特效、无统计）。`FrictionHideLongNote` 就是这种「刻意不发布」的载体，反过来也说明：**要声音的音符不能做成载体**。

### 5.3 类别在转换层被改写（2026-11-07，本次真因）

「摩擦长条没有声音、没有尾键」查了三轮，前两轮都在视图层和计分层打补丁，全错。真因在 `RuntimeChartConverter`：

```csharp
// 已删除：ConvertLongNoteTraceToNoJudgment
if (nextConn != -1 && category == FrictionLong)  category = FrictionHideLong;   // 摩擦头 → 隐藏头
if (nextConn == -1 && category == Friction)      category = FrictionHide;       // 摩擦尾 → 隐藏尾
+ 再塞一个 isSkip = true 的假摩擦音符
```

后果：谱面里「摩擦」和「隐藏」这两个**本来不同的类别**，转换后变成同一个「无判定载体 + 被跳过的假音符」。于是摩擦既没有判定（没有结果可发布）、也没有声音、也没有尾键。只要转换层把区别抹掉，后面每一层都不可能再区分——**判定问题先往上追一层，看谱面到运行时音符的映射是否保真**。

判断依据也给出来了：谱面里尾巴是 `Friction` 就是「摩擦」（要判定、要有声），是 `FrictionHide` 就是「隐藏」（静音）；头是 `FrictionLong` 就是可擦的条，`FrictionHideLong` 才是载体。转换器只允许做**无损**的 `noteBaseType` 归一化（如链尾 `Long → Normal`），不允许把可判类别降级成载体。

### 5.4 抬手归属：只有持握者或落点在车道内才算松手（2026-11-07）

`CoverHoldsWithAnyFinger` 里对 `Ended/Canceled` 样本的旧写法是先置 `releasedInside = true` 再做车道判断，等于**任意手指、任意位置抬手都会结束任意一根正在按着的条**（普通尾尤其明显：任意位置抬手即判定）。

正确判据（`JudgeLane*` 已经是 `lane ± LongLaneOffset`，即左右各外扩 **1.5 轨**，这就是「边缘以外 1.5 根轨道以内」的出处）：

```csharp
bool isHolder = input.Touch.touchId == held.HoldingTouchId;
if (isHolder || HoldSweep.TouchesLane(input.Lane, held.JudgeLaneStart, held.JudgeLaneEnd))
    releasedInside = true;
```

两条都要：**持握者 id** 覆盖「快速抬手、样本已在判定线上方」的划键样本；**车道判定**覆盖「另一根手指恰好在条内按下又抬起」的正常接管。不要再额外加容差，`JudgeLane*` 里已经有 1.5 轨。

## 6. 硬性约束

1. **不改手感**：判定窗口、尾判毫秒表、3 帧宽限期、松手后第 3 帧这些数值一律不动；本任务只改「谁在哪判断」。
2. **一次只切一个音符或一个通道**，切完立刻跑全量 + 真机一小段，不允许一个提交里同时切换两个通道。
3. **绞杀者模式**：新通道与新枚举先并行存在，逐个切换，最后删旧分支；禁止先删后加。
4. **每个工作包一个提交**，提交信息写清「行为不变」或「行为变化点」。
5. 不在本任务里做 UI 翻新、不引入新依赖。

## 7. 验收

每个工作包完成后：

1. headless 全量 EditMode 全绿（数字不少于 414，且报告 `LastWriteTime` 是本轮）；
2. 至少一条变异实验（把新加的判据改错，确认对应用例变红，再恢复）；
3. 真机一小段：普通长条、划键长条、friction 长条、装饰长条各一条，确认无回退；
   摩擦条还要听一耳朵：**普通摩擦条起手与尾巴有擦音、隐藏摩擦条全程静音**（§5.3 的类别保真）；
   普通尾另加一条：**另一只手在别处点一下就抬，尾不许判**（§5.4 的抬手归属）；
4. 动到「尾进不进列表」时，必须同时跑 `Live/DecorationTailFromChartTests`（真实谱面驱动，见 §10）；
   动到「尾怎么被松开」时，必须同时跑 `Live/TailReleaseLaneTests`（另一根手指的抬手不许判定，持握者的抬手必须判定，见 §5.4）；
   动到「长条怎么掉下来 / 谁停在判定线上」时，必须同时跑 `Live/NoteLineProgressTests`（身体恒止于线、条头键可流下，数值断言，见 §11）；
   真机要顺带看一眼**尾键前面有没有断一截**——那是网格缓冲被进度范围撑爆的症状；
5. 曲库门禁（BOF 自动）仍为 `1,000,000 / S / 1669`。

## 8. 风险与回滚

| 风险 | 缓解 |
|---|---|
| WP1 动到所有尾判路径，回归面大 | 先做 WP0 的表；每切一个通道跑一次全量 + 真机 |
| WP3 删掉视图开关后尾键行为变化 | 保留 `TailKeyLifetime` 的 5 条表征测试，先让它们在新实现下全绿再删旧代码 |
| 池卫生断言误报（历史上有过 `State == Wait` 永不成立） | 断言只用「可观察且与实现无关」的量 |
| 时间源扫描测试误伤 | 白名单写进文件头注释，逐条登记理由 |

## 9. 报告格式

每个工作包交付时写：

- 改了什么（文件 + 行数）；
- 行为不变 / 行为变化点；
- 用例增删（数字与名字）；
- headless 结果与报告时间戳；
- 真机复验的四个片段各一句结论。

## 10. 一条测试的教训（2026-11-07）

装饰条的尾巴修了四轮，前三轮全错，错在同一件事：**用手搓的音符验证「谁进列表」**。

`Assets/Tests/EditMode/Live/DecorationTailJudgmentTests.cs` 手工 `new` 条与尾、反射调
`UpdateJudgmentNoteArray` / `AllocateInputJudgableEachNotes` / `EvaluateJudgment`，结果**连普通尾都进不了候选**
——因为真实链路里子音符的进度、视图与候选是在 `OnUpdate` 的整条扫描里推进的，缺了那一段，
`IsJudgment` 永远为假。这组用例已用 `[Ignore]` 停放并写明原因，不要再拿它下结论。

可用的做法（`Live/DecorationTailFromChartTests.cs`）：

1. `ChartJson.Read` 读真实谱面（`Tools/fixtures/deco-tail-test10/score.json`，取自报告者的 `Test10.zip`）；
2. `RuntimeChartConverter.Convert` 得到与真机完全一致的音符图；
3. 照 `LiveLogicInputTests` 的方式驱动：`SetField("_musicScore"/"_noteArray"/"_noteDisplayTimeOffset"/"_liveViews")`
   → `logic.OnUpdate(t, …)` 逐帧推进 → `source.Add(Touch(...))` + `logic.OnInput()`；
4. 断言每种尾的 `Result != None`，失败时把逐条 `Describe(note)` 打进断言消息。

另外两个必须设置的开关，否则秒级窗口全部判为非法、所有尾都「不判」：

- `LiveConfig.LoadMasterConfig()`；
- `LiveConfig.UseLegacyJudgeTiming = false`（遗留帧计时需要真实帧）。

**脚手架时钟只能前进，不能倒退。** 2026-11-07 写 `TailReleaseLaneTests` 时踩过：先按 `tailTime` 推完所有帧（最后一帧落在 `tailTime + 1/60`），再回头 `Step(tailTime)` 去「在尾时刻松手」。时间倒退会让长条的 `lastInputTime` 落在未来，持握判定失效，测试表现为「持握者抬手也判不了尾」——看起来像产品 bug，其实是用例自己的问题。正确写法是**停在事件前一帧、把事件本身作为下一步**：

```csharp
const float frame = 1f / 60f;
for (float now = barTime + frame; now < tailTime - frame * 0.5f; now += frame)
    Step(now, Touch(holder, lane, Stationary));
Step(tailTime, Touch(holder, lane, Ended));   // 事件就是这一步
```

**断言消息里放状态，不要放形容词。** 同一个用例第一次只写了「持握者的抬手应该判尾」，失败后一无所获；把 `state / holder / IsTailJudgeableAt / 尾时间 / 条时间` 拼进断言消息后，一次运行就看出是时钟倒退。断言失败时要能**直接读出**是哪个条件不成立。

**断言「消费点看到的数」，不要断言「你的意图」（2026-11-08）。** 「摩擦/Guide 开头的长条不该黏在判定线上」那一轮，我写的是三条断言 `IsOrdinaryHead` / `KeepsHeadOnTheLine` 的用例——**谓词对了，消费点没改，用例照样全绿**，而真机一点变化都没有。谓词是「我想让它怎么判断」，数值才是「视图真的拿到什么」。可靠的写法是把规则抽成纯函数、断言它的数：

```csharp
Assert.That(NoteLineProgress.Body(offset: 1f, now: noteTime + 0.2f, noteTime), Is.EqualTo(1f));   // 旧代码若放行会是 1.2
Assert.That(NoteLineProgress.BarKey(1.2f, pinsAtLine: false), Is.EqualTo(1.2f));
```

判断标准很朴素：**把生产代码改回旧写法，这条用例必须变红**。如果改回去还是绿的，它测的是注释，不是代码。

**改动之前先证明那条语句是活的（2026-11-08）。** 同一轮的第一版修在了 `viewProgress` 上，而 `rawProgress` 在更上面已经被 `Mathf.Clamp01` 夹成 1，我加的条件**永远不成立**——写了一行死代码，然后花了一轮真机时间才发现「完全没变化」。改判定/视图的夹取逻辑前，二选一：

- 先写一条会红的数值用例，红的那一下就是「这条路径活着」的证明；
- 或者在看得到的 trace 里先打出这个量（`[TL][View] view barT=… raw=… view=…`），真机日志确认它在动，再动手。

反过来说：**真机「完全没变化」时不要再猜第二版**，先取一次证据（trace / 日志 / 数值断言），否则很容易在同一个错层上再错一次。

## 11. 视图层也有硬边界（2026-11-08）

视图不是「想画多少画多少」：条的身体网格按进度范围切分，缓冲固定。

```csharp
SplitMeshCount = 100;
_meshSettingBuffer = new Setting[SplitMeshCount + 10];          // 110 格
int splitCount = Mathf.CeilToInt(progressRange * SplitMeshCount);
for (int i = 0; i < sampleCount && i < _meshSettingBuffer.Length; i++)   // 用完就停
```

所以「让某个 progress 超过 1」不是纯数学改动：`progressRange` 一旦超过 1.1，`splitCount` 就超过缓冲，循环在中途停住，**尾侧最后十几个采样点根本不进网格**——表现为「中后段到尾键之间缺一截」，而且缺的位置随流速变化，看着像随机断线。

同一条规则不能同时套在「身体」和「头部键」上，它们的语义不同：

| 对象 | 规则 | 消费点 |
|---|---|---|
| 条的身体 | **永远**止于判定线（`NoteLineProgress.Body`，无条件夹） | `NoteLineView` 网格 |
| 条头那颗键 | 没人碰过且不是普通头时，**流下**判定线（`NoteLineProgress.BarKey`） | `LongNoteView` |

改视图范围类逻辑时，先问三句：这个量被夹在几处（`rawProgress` / `viewProgress` / sprite / 网格各一层）？它的消费者有没有容量或取值假设？改了之后哪条数值用例会红？

## 12. 相关文档

- [判定与窗口](../reference/judging.md)
- [长条 v3 交接](long-hold-v3-handoff.md)（§9–§11 是本次任务的直接前因）
- [判定内核任务书](judgment-core-brief.md)（A/C/D 已完成，B 由本文件取代）
- [测试与走查](testing.md)