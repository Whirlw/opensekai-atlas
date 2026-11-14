# 判定规则设计

> 目的：把「一个音符凭什么被判定、由谁判、什么时候判、判错会怎样」写成一份可以对照代码验收的完整规则。
> 适用读者：改判定、改音符视觉、排查「明明按了却没判」的人。
> 前置条件：读过[打歌运行时](live-runtime.md)、[判定窗口](../reference/judging.md)。
> 最后更新：2026-11-14

本文以当前工作区代码为准，描述手动判定、自动播放及排查入口；它不是上游版本的规则说明。判定窗口由 `LiveConfig` 和长条尾实际使用的 `LongEndJudgement` 定义，轨道容差与划键距离来自 `LiveBundleBuildData.asset`。文中的时间单位为毫秒时，数值是便于阅读的近似值；边界是否包含以对应代码为准。`Last` 是状态名，在长条、内部点和普通音符上有不同含义，不能一概解释为已经 Miss。

---

## 一、总原则

1. **每个可计分判定点只发布一次。** 长条的头、尾及内部 combo 点分别结算。条上的 `JudgeInfo` 会镜像头、尾的结果。
2. **判定由三条通道分工完成。** 按下（press）、抬起（release）、常驻（resident）各自只回答一类问题，互不代劳：
    - **按下通道**：这次按下判不判得了这个键。按下键只有单键、独立划键、普通长条的头三种，且要在这根手指的车道和时间窗口内（7.1）。一次按下最多判一个键；划键被判时只推进到接收滑动阶段，滑动由常驻通道评分。
    - **抬起通道**：抬起键只有长条的普通尾（`ReleaseTail`）。一指一帧最多结算一条，按尾时间和车道距离选择。
    - **常驻通道**：处理条身覆盖、内部 combo 点、自判尾的输入、长条到期和划键手势。普通头的超时在按下阶段收尾，自判尾的时钟在更新阶段推进。
    同一触摸样本可以参与多个阶段，例如按下既可判头，也可建立条身覆盖。
3. **判定的输入是「触摸样本」+「结果表」。** 位置决定这个音符**收不收得到**输入，时间决定**判成什么**，两者不互相替代。
4. **视觉与判定解耦。** 头/尾在屏幕上停在哪、何时消失，都由表现层决定（`LongNoteView`、`LivePresentationBridge.TickTailKeys`），判定不看这些状态；反过来，判定也不改变几何。
5. **没有判定权的音符不发布可计分结果。** 隐藏点可以写入内部 `Pass`，用于推进状态。

---

## 二、帧内时序

打歌进行中，控制器依次调用 **`LiveLogic.OnUpdate` 更新状态 → `LiveLogic.OnInput` 处理触摸 → `LiveLogic.OnView` 推进尾键与退场动画**。`LiveNoteFrame` 负责状态和表现，`LiveInputRouter` 与 `JudgmentScheduler` 负责触摸处理。

```
① 更新状态
LiveLogic.OnUpdate(scoreInfoTime, gameTime)
  clock.Sample ........................ 得到本帧 MusicScoreInfo
  NoteFrame.UpdateNotes()
      register 未结算的头候选 ......... 头的候选只要头还开着就一直注册（不看条是否已结束）
      WalkNotes(ScanNote) ............. 走音符：生成 / 推进 / 生成视图
      WalkHighSpeedNotes(...)
      Notes.TickTails(...) ........... 推进自判尾和条身已结束的普通尾、划键尾

② 处理触摸
LiveLogic.OnInput()
  Input.CollectTouches() .............. 原始触摸 → InputTmp（含车道解析，y 只有这一道门）
  Scheduler.Run()
      1 按下通道 ....................... CollectCandidates → SortPressCandidates → EvaluateJudgment
                                         只收 Began；一根手指一帧只花一次按下
                                         然后按本帧音乐时间结算已过晚界的单键与普通头
      2 抬起通道 ....................... JudgeLifts：只收 Ended / Canceled
                                         只判 ReleaseTail，一帧一指只了结一条
      3 常驻通道 ....................... JudgeDueComboPoints → DriveIndependentNotes → RefreshSustainedHolds
                                         条内点、条身、划键尾、自判尾的输入、长条超时
      4 EndFrame() ..................... 忘掉结束的触摸，清空本帧输入

③ 生成视图
LiveLogic.OnView() → NoteFrame.TickViews()
  TickTailKeys(now) ................... 推进装饰条尾键的兼容退场路径
  TickRetiring(frame) ................. 正在滑出的键继续往外走，到 1.35 回收
```

三个通道按顺序读取本帧样本。抬起阶段直接选择并结算普通尾。常驻阶段独立处理条身覆盖和划键尾手势。

自动播放在 ① 扫描音符前先结算到时的非装饰音符，以免掉帧时先被超时逻辑判为 Miss。控制器随后调用 `OnAutoInput` 补跑自动判定，③ 处理尾键和退场动画。① 的扫描也会推进音符表现。

**三个时机补偿与两条时钟**（`TimingCompensation`，设置里各一个毫秒输入框）

| 设置 | 含义 | 移动什么 |
| --- | --- | --- |
| 音频延迟 L | 音乐晚多少毫秒到耳朵 | 判定和画面一起往后移 |
| 画面延迟 V | 画面晚多少毫秒到眼睛 | 只移画面（提前 V 绘制） |
| 判定偏移 J | 玩家习惯，正 = 打得晚 | 只移判定 |

所有判定点使用逻辑时钟，按下和独立划键还会在其基础上映射触摸样本时间。未触及零点时，`逻辑 = 音频 − (L + J)`，`画面 = 音频 − L + V`；实际 `LogicTime` 和 `ViewTime` 分别把结果下限限制为 0，接近开头时不能直接套用未截断的等式。控制器把逻辑时钟交给 `LiveLogic.OnUpdate`，把画面时钟交给各视图；键的精灵由 `LiveConfig.ViewProgressOf` 在逻辑进度上加 `ViewLeadSeconds`，还没到线的键提前、最多到线为止，已在线上或越过线的键按逻辑位置画。条身直接用画面时钟。自动播放不应用 J。打击音不补偿：它只能在知道按下之后才开始。旧的 `TimingAdjustData`（单位 1/60 s）读取时迁移为 `AudioLatencyMs = −帧数 × 16.67`，并受音频延迟范围限制。

设置值范围分别为：音频延迟 −200..500 ms、画面延迟 −200..300 ms、判定偏移 −200..200 ms；无效的 NaN 按 0 处理。默认「触摸时刻」把事件与当前帧的时间差夹在 −250..0 ms；「下一帧（旧式）」将事件与帧的秒差乘以 1/60 后叠到帧时间，不作同样的夹取。两种模式只影响使用样本时间的判定，普通尾抬手和条内托管尾按当前帧时间评分。

**三条通道与②内三步的对应关系**

| 通道 | 落在②的第几步 | 回答的问题 | 典型音符 |
| --- | --- | --- | --- |
| 按下 press | 1 | 这次按下能不能判掉一个瞬间键 | 单键、普通长条的头 |
| 抬起 release | 2 | 只判长条的**普通尾**（末端是普通音符） | `ReleaseTail` |
| 常驻 resident | 3 | 持续输入与长条到期 | 条身、内部点、划键尾、自判尾的输入 |

---

## 三、输入层

### 3.1 触摸 → 车道

| 步骤 | 实现 | 规则 |
| --- | --- | --- |
| 归一化 | `TouchDeduplication` / `LiveInputCollector` | 丢弃倒退时间戳。重复时间戳上的 `Began` / `Stationary` 转成 `Moved`，重复的结束相位丢弃。记录为 `Ended` 后同 ID 不再处理 |
| 坐标系 | `InputSystemTouchSource` | `Camera.ScreenToWorldPoint` 得到世界坐标 |
| 车道解析 | `TouchLane.Resolve` | 返回**小数**车道（2.5 = 2、3 之间）；超出外侧容差返回 `-1`（Outside）。只看 x，不看 y |
| y 轴门（唯一一道） | `TouchLane.IsAboveTheLine` | 只在触摸**出生**（`Began` 样本）时问一次：高出判定线超过 `_judgmentOffsetY` 的触摸被记进 `_touchesAboveTheLine`，此后它所有样本都算 Outside。落在线上的手指后来划到线以上，仍然是有效输入 |
| 判定时机 | `LiveLogic.ResolveInputMusicTime` | 默认把样本时间转成音乐时间，偏移限制在 `[-250 ms, 0]`。旧式模式为逐帧近似。普通尾的抬起评分使用当前帧音乐时间 |

`-1` 表示当前样本在判定范围外。划键尾可用同一触摸上一采样帧的车道识别从条内划出的最后一段位移。启用「原始触摸」追踪后，可用 `rawTouch` 核对输入采集和初步过滤；这与「判定结果」追踪是两个独立选项。每帧输入缓冲上限为 128 个样本，超出容量的样本不加入活动输入列表。

**y 轴只在输入口检查。** 各通道读取已解析的 `InputTmp.Lane`。划键尾还能读取同一触摸已解析的上一采样帧车道。

### 3.2 轨道容差

`JudgeLaneStart/End = laneStart − offset .. laneEnd + offset`，`offset` 来自配置资产，按类别选：

| 类别 | 容差 |
| --- | --- |
| 单键（`Normal`） | 1.25 轨 |
| 长条（`Long` / `FrictionLong` / `FrictionHideLong`） | 1.5 轨 |
| 划键（`Flick` / `FrictionFlick`） | 1.5 轨 |

**长条的「头」沿用长条自身的 1.5**（这是刻意的：头与条同一条轨道）。这条容差曾被误改成单键的 1.25，导致短条头部抢不到/抢多了邻近车道的按下，已回退。

---

## 四、音符类型与判定权

| 运行时类型 | `Category` 语义 | 有判定 | 由谁判 | 输入 |
| --- | --- | --- | --- | --- |
| `NormalNote` | 单键 | 是 | 按下通道 | 按下（`Began`） |
| `FlickNote` | 划键 | 是 | 按下通道推进，常驻通道评分 | 独立划键被一次按下（`Began`）判中时立刻进入接收滑动阶段（`Input`），之后任何手指在它车道内、判定窗口内超过 5 cm/s 的滑动都能完成它 |
| `FrictionNote` | 摩擦 | 是 | 常驻通道 | 按住并在轨道上蹭 |
| `FrictionFlickNote` | 摩擦划键 | 是 | 常驻通道 | 按住 + 位移 + 方向 |
| `LongNote`（普通条） | 长条 | 头/尾各一次 | 头：按下通道；尾：抬手或划键 | 见第六节 |
| `FrictionLongNote` | 摩擦长条 | 头/尾 | 头：常驻；尾：自判 | 按住 |
| `FrictionHideLongNote` | 隐藏摩擦条载体 | 头无计分判定；内部点与末端看各自类型 | 持续输入驱动载体；末端另行判定或推进内部状态 | 持续接触 |
| `LongHoldCombo` | 条内 combo 点 | 是 | 长条驱动；已注册时也可由常驻通道处理 | 到点后结合父条状态和车道检查，见 6.4 |
| `ConnectionNote` | 连接点 | 取决于具体实例的 `HasJudgment` / `IsSkip` | 有判定权时由所属路径处理 | 按实例类型 |
| `HiddenConnectionNote` / `FrictionHideNote` | 隐藏点 | 否 | — | — |
| `GuideNote` | 装饰条 | 否 | — | — |
| `GuideEndNote` | 继承 `FrictionHideNote` 的隐藏末端 | 否 | 可写入内部 `Pass` | — |

要点：

- 装饰条本体不计分。末端如果绑定了可判音符，会按末端自己的类型参与判定。
- 隐藏点提供几何与计时锚点，也可以用内部 `Pass` 推进状态。

### 4.1 判定与可见是两个独立标志

每个音符只回答两个问题：`NoteBase.HasJudgment`（判不判定）和 `NoteBase.IsVisible`（画不画）。

| 组合 | 音符 |
| --- | --- |
| 判定、可见 | 普通单键、划键、摩擦键、普通长条及其可判末端 |
| 不判定、可见 | `GuideNote`、`FrictionHideLongNote`（载体条身可见），以及谱面标了 `isDecoration` 的任何音符 |
| 不判定、不可见 | `HiddenConnectionNote`、`GuideHiddenConnectionNote`、`FrictionHideNote`、`GuideEndNote` |

- 类型可以关掉判定；谱面的装饰标志也会关掉判定（`HasJudgment` 的基类实现为 `!IsDecoration`）。装饰音符与 `GuideNote` 完全一样：不进按下候选、不 Miss、不发布结果，没有特效、打击音、判定文字、分数和连击。到点时只写内部 `Pass` 并退场。
- 「不发布」只有一道门：`ResultPublication.Publish` 遇到 `!HasJudgment` 时占住发布槽并直接返回，任何回调都不执行。
- 「画不画」只看 `IsVisible`：不可见的末端没有尾键阶段（`TailKeyPhase.Hidden`），也没有视图可回收。
- 装饰标志会传给条的连接点和 combo 点，但不传给末端：装饰条末端的真实音符照常按自己的类型判定。
- `FrictionHideLongNote` 不判定，但仍接受持续接触（候选管线对它单独放行），因为手指要能按住它。
- 装饰条（`GuideNote` 或谱面标了装饰的条，`LongNote.IsDecorativeBar`）的末端是兜底键，一般不应放可判定的键。若放了，它按**独立单键**判定：准入、评级和超时都用 `Normal` / `Flick` 等单键表，而不是 `Long_End` / `Long_End_Flick`。判据是 `NoteBase.GradesAsBarPart`，`LiveUtility.GetINoteToJudgeFrameType`、`NormalNote` 和 `FlickNote` 共用它。其他长条（普通条、摩擦条、隐藏摩擦载体）的末端仍用长条尾表。

---

## 五、判定窗口

判定表按**帧**配置，`CalcFrameToTime(frame) = frame / 60f` 固定 60 fps 换算，与实际帧率无关。八个数是 Perfect/Great/Good/Bad 的早侧与晚侧。

| 音符 | Perfect（早/晚） | Great | Good | Bad | 早界 | 晚界 |
| --- | --- | --- | --- | --- | --- | --- |
| `Normal` | 2.5 / 2.5 | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Normal_Critical` | 3.3 / 3.3 | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Flick` | 2.5 / 2.5 | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Flick_Critical` | 3.5 / 3.5 | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Long`（头） | 2.5 / 2.5 | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Long_Critical`（头） | 3.3 / 3.3 | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 | 125 ms | 125 ms |
| `Long_End` / `Long_End_Flick`（含 Critical） | 3.5 / 4 | 6.5 / 8 | 7.5 / 8.5 | 7.5 / 8.5 | 125 ms | 141.7 ms |
| `Friction` / `Friction_Flick` | 复制对应的 `Flick` 表，含 Critical 版本 | — | — | — | — | — |
| `Friction_Long` / `Friction_Long_End` | 分别复制对应的 `Long` / `Long_End` 表，含 Critical 版本 | — | — | — | — | — |

按下候选先以 `touch.time − note.time` 检查 `PressCandidateWindow`；其常量边界含等号并有 0.001 ms 的浮点容差。随后还会经过音符自己的 `IsJudgmentTiming`：该检查使用 `note.time − touch.time`（正值表示早），外边界是开区间。因此不能只凭候选窗口含等号断言恰在边界的按下一定会被接收。普通尾和划键尾的条内结算走 `LongEndJudgement`，使用 `frame.time − tail.time`（负值表示早）。该表的 Perfect、Great、Good 早侧为 58.3 / 108.3 / 125 ms，晚侧为 66.7 / 133.3 / 141.7 ms，边界包含等号；它没有 Bad 档，Perfect 区间返回 `JustPerfect`。

通用结果表在方向有效时，正中 ±1/120 秒（约 ±8.33 ms）返回 `JustPerfect`，其余表内 Perfect 区间返回 `Perfect`；显示统计中 `perfectCount` 已包含 `JustPerfect`。有方向划键若方向不符，在 Perfect 区间返回 Great / `FlickMiss`，更宽的时间区间仍按 Great、Good、Bad 表评级。无方向的 `Default` 划键接受任意方向。`onUpdateResult` 还可通过技能逻辑调整部分音符的结果，因此最终显示以实际发布结果为准。

单键与普通头的准入、评级和超时从同一类型表取窗口。跨窗口掉帧时，本帧已收到的按下先按样本时间判定。摩擦和摩擦划键分别使用 `Friction` 和 `Friction_Flick` 表。独立键与长条尾键按同一规则选择普通或 Critical 窗口。

> 头部的偏移曾经写成 `touch.time − note.time`（符号反了），于是「早一点」被当成「晚一点」、「晚一点」又落在早侧边界外，表现为早/晚都判不上。现在头部与 `NormalNote` 用同一个符号、同一个门。

**Miss 的时机**：

| 情形 | 何时判 Miss |
| --- | --- |
| 单键 / 普通长条头 | 本帧按下处理完成后，当前音乐时间严格晚于该类型的晚界时结算 Miss |
| 划键 / 摩擦 | 各自的更新路径在越过对应类型的晚界后进入 `Last`，下一次执行时结算 Miss |
| 摩擦长条头 | 更新阶段按头部类型的晚界检查到期 |
| 长条的**尾**（抬手/划键） | `LongNoteTail.ExpireTail` 判 `IsPastTailWindow` 之后 `FailTail()` |
| **自己判定的尾** | 它自己的时钟：会话 `TickTails` 每帧 `Excute`，超时由 `NoteBase` 判 Miss |
| 条内 combo 点 | 到点进入 `Last` 后仍未判中时，下一次执行结算 Miss。条提前结束可补判尚未结算的内部点 |

---

## 六、长条的四段结构

长条不是「一个音符」，是四段各有归属的东西：

| 段 | 实体 | 有判定 | 谁判 | 判定窗口 |
| --- | --- | --- | --- | --- |
| **头** | `LongNoteHead` + 候选 `LongNoteHeadNote` | 普通头与可计分摩擦头 | 普通头走按下，摩擦头走持续接触 | 按对应类型选表 |
| **身** | `LongNote.Body`（`HoldBody`） | 无 | — | 负责覆盖、刷新和丢手宽限 |
| **内部点** | `LongHoldCombo` 及有判定权的连接点 | 视类型而定 | 长条驱动；已注册的 combo 点也可由常驻通道处理 | 见 6.4，不能简单等同于完整的早晚窗口 |
| **尾** | `LongNoteTail` + 末端音符 | 取决于末端音符 | 见下表 | `Long_End` / 自判 |

### 6.1 头的规则（最重要的一条）

- 普通头是独立的瞬间判定键，在窗口内按下后按表评级。
- 头的判定窗口独立于条身状态。条已结束后，尚未结算的头仍可在自己的轨道与窗口内接受按下。
- 头的候选**从头开着就一直注册**（`LiveNoteFrame` 用 `Notes.PendingHeads` 注册，而不是靠音符遍历）。遍历只走「还在播放的条」，而超短条（1 tick）在头之后几毫秒就结束——靠遍历注册会让头在剩余 125 ms 窗口里彻底失去候选，之后按下的那一下必然 Miss。
- 单纯按住不算按下：头只认 `Began`。手指提前按在轨道上、一直不放，不会判头，也不会扣血；头到期由自己发布 Miss。
- 头在自己的槽位记录结论，用独立 identity 触发计分回调，同时把评级镜像到条的 `JudgeInfo`。

### 6.2 尾的行为选择

`LongNote.TailBehaviour` 根据绑定的尾键和是否跳过条内尾判选择行为。

| 末端音符 | 行为 | 判定方式 | 条要不要等它 |
| --- | --- | --- | --- |
| `NormalNote`（普通尾） | `ReleaseTail` | 窗口内抬手按 `LongEndJudgement` 评分，允许条身结束后在晚窗内抬手 | 条身在尾键结算或抵达几何终点时结束 |
| `FlickNote`（划键尾） | `SwipeTail` | 每段位移归给一个可接收的条，按触摸 ID 累计净位移。早划在该触摸离轨、结束或到尾时间后第 3 个游戏帧评分，倒计时可跨越条身终点。尾时间及之后的有效划动可立即评分。方向错误最高 Great | 条身在尾键结算或抵达几何终点时结束 |
| `FrictionNote` / `FrictionFlickNote` | `SelfJudgedTail` | 复用独立音符的类型、判定窗口和视图生命周期。显示使用长条的谱面流速倍率 | 条身按几何终点结束，尾键继续到自身结算 |
| `HiddenConnectionNote` / `FrictionHideNote` | `SelfJudgedTail` | 推进隐藏端点的内部状态 | 条按自己的生命周期退出 |
| 跳过条内尾判的条 | `SilentTail` | 条身退出，绑定的可判末端走自身的判定路径 | 条按自己的生命周期退出 |

尾的三条不变量：

1. **条不替自判定的尾写结果**；条只 `DetachStandaloneTail` 把自己结束掉。
2. **自判定的尾有自己的时钟**（`LiveNoteSession.TickTails`），条先结束也不会孤儿化它。
3. **无判定的末端不产生 Miss**（隐藏点）：条的结束不能借它的名义记一次断连击。

### 6.3 条的结束

有效抬手或划键可以提前结束条。条身抵达几何终点时也会结束，会话继续保留未结算的普通尾、划键尾和自判尾。普通尾和划键尾由原规则判到窗口关闭，到期未命中才结算 Miss。条结束后，普通头仍由会话独立登记。条进入 `Done` 时会给未结算的内部 combo 点补 `Perfect`。

### 6.4 条内点与条身状态

`LongNoteHead.ConnectionNoteJudgment` 对每个实际可判的内部点，以**当前帧音乐时间**检查相对该点 −125..+141.7 ms，防止使用一直按住的手指最初按下时间反复补判。这个检查只是长条驱动点的外层门禁，不代表 `LongHoldCombo` 在整段时间内都可命中：

1. 普通 combo 点到自身时间前不会进入待判 `Last`，此时调用其 `Judgment` 不会结算。
2. 到点后，若尾已结算或处于相关输入状态，点可直接得到 `JustPerfect`；否则进入 `Last`。
3. 在待判阶段，长条驱动或已注册点的常驻处理可以让符合条件的接触结算为 `JustPerfect`。常驻路径要求父条曾被记录为有手指、`Last` 或 `Release`，并检查该点的车道范围；其状态记录是在处理活动输入时建立的。
4. 如果到下一次执行仍处于 `Last`，该点成为 Miss。窗口是否有机会被命中取决于更新／输入时序和帧率，并不是承诺能在 +141.7 ms 内任意补按。
5. 普通长条进入 `Done` 时，仅对属于该条且结果仍为 `None` 的 `LongHoldCombo` 补 `Perfect`；已有结果保留。这包括提前结束时尚未到点的点。其他类型的连接点另按各自的终止规则处理，不能把此补判推广到所有内部对象。

`JudgedPoints` 收集 combo 点和 `HasJudgment && !IsSkip` 的点，按谱面时间四舍五入到毫秒后在**同一条**内去重；两条的点仍各自处理。隐藏点可以参与条形几何，但不因此获得可计分判定。普通长条与隐藏摩擦载体的结束路径有区别，排查时应核对实例类型，不能只看 `LongNote` 基类。

条身丢手后，已被接触过的条进入 `Last`，宽限为 `3/60 s`，即约 50 ms 的音乐时间；宽限内有手指被记录在条上（任何一根手指，包括换手接上的那根）即回到按住状态（普通条 `Scrolling`、摩擦条 `Input`），不再停留在 `Last`；严格超过宽限后进入 `Release`，此后普通条只在头部窗口内可恢复。仅被保活而没有记录手指，不会让条离开 `Last`，否则更新阶段会每帧重新进入宽限。从未接触过的条越过头部时间后走不同的释放分支。多手指覆盖是每帧重新计算的：一条只要有合格手指覆盖即可维持，条身覆盖与按下额度分开。50 ms 的条身宽限使用音乐时间；划尾的三帧倒计时使用实际游戏帧，不能把二者都写成固定 50 ms。

---

## 七、按下通道

### 7.1 谁能进候选

候选是硬标准：`LiveCandidatePipeline.AdmitsPress` 同时检查种类、车道和时间，三项都满足才进入这根手指的列表（之后仍问一次音符自己的 `IsJudgment`，保证不会把按下交给会拒绝它的键）。

**种类**（`IsPressStep`）只有三种，且键一经判定立刻离开：

1. 单键：不挂在条上的 `NormalNote`，`Category == Normal`，未 `Done`；
2. 划键：不挂在条上的 `FlickNote`，且仍在 `Scrolling`（尚未被按下推进）；
3. 长条普通头：`LongNoteHeadNote` 且 `IsAnswerable`（普通头、可计分、**未结算**）。

**车道**：触摸车道落在键的 `JudgeLaneStart..JudgeLaneEnd` 内；头用所属条的扩展车道。

**时间**（`PressCandidateWindow`，偏移 = 触摸 − 音符，负为早）：单键与普通头 −125 ms 到 +125 ms；划键 −125 ms 到 +141.7 ms。

候选还必须通过音符自己的 `IsJudgment`，其开区间时间门可能在候选常量的边缘拒绝输入；候选表记录的范围不单独构成最终准入保证。可判列表提前量为 0.35 秒，注册调用使用本帧时间加一个 1/60 秒参考帧；显示提前量不决定输入能否进入列表。

**划键的按下**：划键被选中时，这次按下把它推进到接收滑动阶段（`Scrolling → Input`，记下这个触摸），并按消费处理——这根手指本帧的按下额度用在它身上。推进后它不再是按下候选。滑动阶段不限手指：常驻通道把本帧每根手指的样本都交给它，直到它完成（`DriveIndependentNotes`）。样本须在划键自己的车道内、在它的判定窗口内（`FlickNote.IsJudgment`），速度超过划键距离（5 cm/s，`FlickGesture`）。评分仍由滑动完成。挂在条上的划键是尾，不走这里。

**条身、内部点、隐藏点、摩擦键、装饰键和所有尾键永远不进入按下候选。** 曾经的 `PickUp` / `TryAddConnectionCandidate` 两条路径会把整条 `LongNote` 塞进候选，已删除——它们会让一次按下被「一条已经判完的条」吃掉。

### 7.2 一个音符的按下准入

| 检查 | 规则 |
| --- | --- |
| 相位 | 必须是 `Began`（按住、滑动都不算「按下」） |
| 车道 | 触摸车道落在该音符的 `JudgeLaneStart..JudgeLaneEnd`（头用条的扩展车道） |
| 时间 | `PressCandidateWindow`：单键与普通头 ±125 ms，划键早 125 ms / 晚 141.7 ms |
| 单槽 | 一根手指一帧只有一个候选（见 7.3） |
| 触摸记录 | 只读。`_judgedTouchTimes` 只记已被音符消费的触摸。已有记录、预测评级为 Great 或更差、且距消费不超过 1/6 s 时，跳过该候选 |

收集候选不消耗按下。尚未命中的候选不会挡住同一触摸的后续候选。

### 7.3 单槽与归属

每根手指先有一张自己能判定的按键列表。`SortPressCandidates` 分三步去重，再让每根手指从剩下的列表里取一个键。列表去重后为空的手指本帧不判定。

1. **唯一选择优先**（`KeepSoleOptions`）：某个键出现在多根手指的列表里时，如果其中一根只剩这一个键，就把它从其余还有别的键可打的手指列表里删掉。删除后可能产生新的“只剩一个”，所以重复到没有变化为止。共享这个键的手指都只剩它时，这一步不动它们，也不会清空任何一根。
2. **两指两键配对**（`AssignMatchedPairs`）：恰好两根手指的列表都恰好是同样两个键时，按谱面顺序分配：较早的键给离它更近的那根，较晚的键给另一根。只处理两指两键；三根及以上手指、或键数不等的情况留给第 3 步。配对后这两个键即分配完毕，从其他所有手指的列表里剔除，即使剔除后列表为空。
3. **距离归属**（`ClaimsSharedNote`）：仍然共享的键给离音符车道中心更近的手指，距离相同取较小的 `fingerId`。同一手指的多个样本之间不互相争夺。
4. **单指取键**（`PickForOneFinger`）：每根手指剩下的键按谱面时间排序，同时间按谱面顺序 `ChartOrder` 排序（`LiveNoteSession.Bind` 按谱面数组下标写入，头沿用所属条的值；排序稳定，与本帧注册顺序无关）。取最早的按下步骤；如果它按这次触摸的时间只能拿 Good 或更低，而后面某个键能拿 Great 及以上，就取其中最早的那个。头的评级用所属条的起点窗口（`LongNoteHeadNote.CalcNoteResult`）。

执行前每个输入的候选列表已裁到最多一项。`JudgmentSchedule.Run` 把产生结果、进入 `Done`、或把划键推进到接收滑动阶段记作消费。

按下只在消费处记账（`JudgmentPass.Consumed`）：写入 `_judgedTouchTimes`，并把该手指记为本帧已用。一根手指一帧只有一个按下额度，同一手指同帧的多个 `Began` 样本共用它，第一个被消费后其余样本不再判定。

例：手指 A 同时够到较早的 X 和共享的 Y，手指 B 只够到 Y 且离 Y 更远。第 1 步把 Y 从 A 的列表删掉，A 打 X，B 打 Y，与注册顺序无关。各步骤由 `PressOwnershipTests` 固定。

### 7.4 抬起的归属

抬起通道结算由 `ReleaseTail` 负责的普通尾。划键尾在常驻阶段使用触摸结束或离轨来推进已识别的手势。摩擦和摩擦划键末端由各自的音符逻辑结算。

它是 ② 里的**独立一趟**（`LiveHoldCoordinator.JudgeLifts`），跑在按下通道结算之后、常驻通道刷新之前：

- 只处理 `Ended` / `Canceled` 的触摸，一指一帧只了结一条；
- 抬起候选只有一种：长条的普通尾（`LiveHoldCoordinator.IsReleaseKey`：末端是 `NormalNote`、非装饰、未结算、条的尾行为是 `ReleaseTail`）。划键尾、摩擦尾、隐藏载体永远不是抬起候选。
- `PickLiftTarget` 再按尾键扩展判定车道和当前帧时间（`LongEndJudgement`，早 125 ms / 晚 141.7 ms）筛选。尾时间最早者优先，同时间取离尾键中心最近者。
- **拥挤的尾**：抬手本身不说明是谁按着这条长条。尾键判定范围内还有其他仍在屏幕上的触摸时，这次抬手只有落在 Perfect 区间（早 58.3 ms / 晚 66.7 ms 以内，`LongEndJudgement.IsPerfect`）才能判这个尾；否则跳过这条，留给仍按着的手指。抬手的触摸是范围内唯一一个时，任何评级都能判。同帧也在抬起的触摸不算“其他触摸”，同一触摸的多个样本算一个。
- `JudgeTailAsRelease` 用当前帧音乐时间走 `LongEndJudgement`。窗口外的抬手交给条身覆盖和丢手宽限处理。
- 普通尾在该阶段直接结算。常驻阶段单独给划键尾分配手势，使用长条对象引用保存本阶段的目标。

---

## 八、常驻通道

常驻通道每帧驱动**所有**条与自判定尾，与按下无关。它负责：

| 职责 | 实现 |
| --- | --- |
| 条身是否被按住 | `HoldBody` + `RecordHoldingInput` |
| 条内 combo 点结算 | `JudgeDueComboPoints`：已注册的点进入 `Last` 后，在有活动输入时记录父条 `HasFinger`、`Last` 或 `Release`，再检查点的车道。两条重叠的点各自结算；长条直接驱动点另有 6.4 的时间门 |
| 长条结束（超时） | `LongNoteTail.ExpireTail` |
| 自判定尾的输入 | `DriveIndependentNotes`。其时钟由更新阶段的 `LiveNoteSession.TickTails` 推进 |
| 划键尾 | `LiveHoldCoordinator` 分配位移，`SwipeTail` 推进手势 |

条身覆盖和刷新检查当前样本车道。划键尾按位移起点车道选择最近的可接收长条，同一段位移只送给该条，累计窗口按触摸 ID 隔离。早划进入等待后，释放时机跟随触发划动的手指，即使另一根手指仍按着条身。

延迟结算使用同一触摸最近一次通过划动门槛的方向判断。默认速度门槛为 5 cm/s，低于门槛的后续位移保留上一次的方向判断。

### 8.1 独立划键、摩擦键与划尾的区别

| 类型 | 接触与评分 |
| --- | --- |
| 独立 `FlickNote` | 先由按下推进到 `Input`；之后活动输入中的任意手指可在自身车道和时间门内提供有效位移。有效位移达到速度门槛即可结算，方向错误按通用表降级。 |
| `FrictionNote` | 常驻接触可在到点前进入 `Input`；到点执行时给 `JustPerfect`。到点后符合条件的接触也可完成；没有接触则超时 Miss。 |
| `FrictionFlickNote` | 由常驻输入记录有效位移；已记录手势可在到点时结算，满足条件的正确方向晚划也可即时完成。其评级使用 `GetINoteToJudgeFrameType` 所选表，不把摩擦尾一律当成普通尾。 |
| 普通条的 `FlickNote` 尾 | 由条的 `SwipeTail` 而非普通尾抬手通道完成。车道归属优先使用同一触摸的上一采样车道；一段位移只给一个目标，已进入延迟等待的目标优先保留给原触摸。 |

一般划键将每帧位移按 `deltaTime` 换算成 1/60 秒参考帧，长度必须**严格大于**阈值；方向键以目标向量的点积 ≥ 0 判断方向，左／右分别以左上／右上为目标，`Default` 接受任意方向。默认配置对应 5 cm/s，实际像素门槛受设备 DPI 和配置影响。

划尾额外按触摸 ID 汇总净位移：等满一个参考帧，或提前累计到一个划动距离后尝试；同时检查净位移门槛及实际平均速度。只有条已被覆盖、尾窗开放且尾行为接受划动时才可推进。提前有效划动进入等待：触发手指抬起、离开条道或到达尾时刻开始计数，在其后的第三个游戏帧按**结算时刻**评分；尾时刻及以后有效划动立即评分。延迟期间若尾窗关闭，不能承诺一定得到原先手势的等级，最终由实际超时／结算路径决定。反方向只对 Perfect 档施加最高 Great 的限制；不把时间本来只能得到 Good 的手势提升到 Great。

---

## 九、结果发布与计分

所有判定点都从 `ResultPublication.Publish(point, callbacks, visual, shape)` 发布结果。

| 参数 | 含义 |
| --- | --- |
| `point` | 判定点身份，连击、分数、伤害都记在它上面。普通键、尾和内部 combo 点用各自的音符实例，头用 `LongNoteHead.Identity` |
| `callbacks` | 判定点所属音符的四个回调。头使用所属条的回调 |
| `visual` | 判定效果的表现对象。普通头借用条，其余判定点用自身 |
| `shape` | 回调集合与顺序，见下表 |

| `shape` | 适用 | 回调顺序 |
| --- | --- | --- |
| `Key` | 单键、划键、摩擦键、尾 | 判定 → 连击 → 分数 → 伤害 |
| `InteriorPoint` | 内部 combo 点（结果非 `None`） | 分数 → 连击 → 伤害 → 判定 |
| `Head` | 被按下的头 | 伤害 → 连击 → 判定 → 分数 |
| `ExpiredHead` | 超时 Miss 的头 | 伤害 → 连击 → 判定，不调用分数 |

| 约定 | 说明 |
| --- | --- |
| 只发布一次 | 守卫在判定点对象上（`NoteBase.HasPublishedResult`），在任何回调之前置位。回调里再次结算同一判定点不会再发布。`ResetNote` 清空守卫，重开和对象池复用都经过它 |
| 去重不用 `Id` | 生成的内部点共用同一个 `Id`，所以守卫按对象，不按谱面 ID |
| 头 | `LongNoteHead.Settle` 发布，`IsSettled` 与 `Identity` 的守卫共同保证一次。无判定权的载体头只置守卫、不发布 |
| 尾 | 尾音符自己的 `Done` 转换走 `Key` 发布。条侧的 `LongNoteTail.PublishTail` 只写条的镜像结果并结束条，不触发回调，它有自己的 `_tailPublished` 守卫 |
| 不发布 | `HasJudgment` 为假的判定点（隐藏点、`GuideNote`、载体、装饰音符）在 `Publish` 入口被拦下，不执行任何回调。`IsSkip` 的 `ConnectionNote` 不发布。结果为 `None` 的内部点不发布 |
| 基分 | 累计计分每次分数回调推进一个基分序号（`ScoreLogic.TakeNextNoteBaseScore`）。超时头不调用分数回调，因此不消耗基分 |
| 条身结束 | 普通 `LongNote` 进入 `Done` 时给其结果仍为 `None` 的 `LongHoldCombo` 补 `Perfect`；在由尾结果触发结束的路径上，尾先发布，然后补内部点。特殊载体重写了状态处理，不能一概套用 |
| Miss 也发 combo | 断连击来自「结果发布」，不是来自视觉效果；谁发布结果谁断连击 |

回调次数、顺序、基分序号、总分、连击和伤害由 `ResultPublicationTests` 固定，手动和自动播放各有用例。

`ScoreLogic` 将 `JustPerfect`、`Perfect`、`Auto` 按基分的 100% 计分，Great 为 70%、Good 为 50%，Bad / Miss 等为 0；之后再应用技能倍率。基分通过累计差分把 1,000,000 分摊到谱面总连击点数，避免每点固定取整造成的尾差。Great 及以上维持连击，Good、Bad、Miss 重置连击；不判定的音符（含装饰）和 `None` 不参与普通连击统计。这里的分摊依赖实际分数回调次数，不能把“满分 1,000,000”推广成所有带技能、漏判、特殊回调或异常谱面都必然得到同一个结果。更完整的计分口径见 [累计计分决策](../decisions/0004-cumulative-scoring.md)。

---

## 十、表现层的不变量（判定不看它，但它们不能反噬判定）

长条以谱面的头和尾为几何锚点，读取各自的时间、原始左右轨道边界和谱面流速。每段曲线由起点的 `LineType` 定义，首段因此读取头的 `LineType`。头尾的判定结果和键面退场独立于几何连接，尾端之后的标记也不延长条身。

| 不变量 | 位置 |
| --- | --- |
| 头的 key 停不停在判定线，只由 `KeepsHeadOnTheLine` 决定（普通头停、摩擦头与 guide 走） | `LongNoteView` |
| 摩擦头的 key 与独立 friction 音符同生命周期：判定（搓到、自动或 Miss）的那一帧即消失，判定特效按头的 `FrictionLong` 身份播放，条身继续；判据 `LongNote.HeadKeyLeftWithItsVerdict` | `LongNoteView` |
| 条的 key 在「条结束」或「末端标准时间到」时立即消失 | `LongNoteView.Move` |
| 普通尾、划键尾、摩擦尾和摩擦划键尾持续移动，在自身命中或 Miss 时释放视图 | `LiveNoteSession.TickTails`、`NoteBase.OnUnSpawnNote` |
| 隐藏尾为无可见键的几何锚点 | `LongNote.LineNoteList` |
| 重开一局必须清空 tick 列表（头/尾）与遍历游标 | `LiveNoteSession.Reset` |

---

## 十一、自动播放

`IsAuto` 时判定不经过输入：`LiveTimeline.AdvanceAutoJudgment` 按时间给音符写 `Auto` 结果，并跳过装饰。自动播放走的是同一套结果槽与发布回调，因此计分路径与手动一致。

自动播放也会在扫描音符前先推进到期判定，避免掉帧时先超时。它不使用玩家的判定偏移 J；`Auto` 的显示／连击还受 Fake Perfect 设置影响。自动播放的输入、效果和结果显示并不等于模拟一组真实手指操作。

---

## 十二、追踪与现场排查

曲库「设置 → 测试模式」是追踪总开关；「追踪选项」中分别选择类别。设置通过 `PlayerPrefs` 保存，立即生效；总开关默认关闭，首次无分类记录时默认只选 `Judge`。所有分类都关闭时不会自动变成全部开启。开启总开关后，`Invariant` 异常仍会写入，不受类别筛选。追踪写入 Unity 日志、内存中最近 4096 行的环形缓冲，以及 `persistentDataPath/logs/live-*.log`；文件采用自动刷新，暂停时也会显式刷新。开启高频项会增加 CPU、分配和日志 I/O，采集性能数据时应只开必要项。

| 设置项／类别 | 主要内容 | 阅读时的限制 |
| --- | --- | --- |
| 原始触摸 `RawTouch` | `rawTouch`：手指 ID、触摸 ID、相位、世界坐标、事件时间、是否通过初步处理 | 在按下候选和车道判定之前；这是高频日志。没有该行只能说明这次采集未记录到该样本，不能单独证明平台哪里丢了输入。 |
| 按下事件 `Press` | `pressCandidates` 为收集后的原始候选，`pressSelected` 为排序分配后的候选，`pressPick` 为实际交给音符后的结果 | 候选列表已经过种类、车道、时间及音符自身条件过滤，不是谱面中所有邻近音符；`pressSelected` 仍可能因同手指本帧额度已消费而未实际执行。列表中的 `grade` 是按样本时间预测值。 |
| 常驻通道 `Resident` | 活动输入与候选数量、条身覆盖、划尾接触等逐帧情况 | 逐帧输出可能很密集；列表中的某个音符出现在日志里不等于它发布了判定。 |
| 抬起事件 `Release` | `liftCandidates`、`liftCrowded`、`liftSelected`、`liftResult` | 初始候选指已通过普通尾类型、车道和时间门的条；`crowded=true` 表示因其他触摸而跳过。选择不保证结算成功，以 `liftResult.settled` 为准。 |
| 判定结果 `Judge` | `judge`、`combo`、重复发布拒绝等 | 判定点 ID 不总是唯一，生成的内部点可能共享 ID；结合类型、谱面时间与日志顺序看。 |
| 长条持握 `Hold`、长条尾键 `Tail`、划动 `Swipe` | 条身状态变化、尾窗／尾结算、划动方向与速度等专项信息 | 必要时组合开启；不自动包含按下或抬起候选。 |
| 对象回收 `Pool`、画面表现 `View`、画面逐帧 `Frame` | 回收、几何／显示以及逐帧视图状态 | 画面状态不是判定结果；`Frame` 是高频项。 |

「性能 HUD」和「Last 长条标红」各有独立开关，均不依赖测试模式。性能 HUD 显示 FPS、最差帧时、分辨率和当前 Tick；Tick 根据当前逻辑帧的小节与进度以及谱面时间签名换算，非活动打歌或缺少谱面时显示 `--`。标红只把 `State == Last` 的长条**条身**画成红色，不改变状态或判定；退出 Last 或关掉选项恢复原色。长条的 Last 是丢手宽限，内部 combo 点的 Last 是到点待判，普通音符的 Last 通常是超时流程中的状态；不要仅凭名称把三者混为一谈。日志提取方法见 [Android 日志](../operations/android-logs.md)。

---

## 十三、可以写成断言的不变量

1. 每个可计分判定点只发布一次。
2. 一根手指一帧最多消费一次按下。
3. 头的结果与条的状态无关：条 Done 之后、头窗口内按下仍然判得上。
4. 自判定尾的结果与条的状态无关：条 Done 之后尾仍然按时钟结算或 Miss。
5. 普通头在条结束后仍由自己的时钟维护到窗口结束。
6. 当前样本越界时不能建立条身覆盖或命中头。划键尾最后一段位移只能交给同一触摸起点车道关联的条。
7. 无判定的隐藏点不会发布可计分结果。
8. 同一手指的抬手一帧最多结算一个普通尾。划键尾的位移归属独立于普通尾选择。

---

## 十四、不覆盖范围

- **完整计分公式**（技能权重、gauge、rank）不在本文范围，基础倍率与发布关系见第九节，完整说明见 `docs/decisions/0004-cumulative-scoring.md`。
- **谱面转换**（`score.json` → 运行时音符）见 `docs/architecture/chart-package.md`。
- **音频与时钟漂移**不影响判定规则本身：判定只用音乐时间，时钟校正见 `docs/architecture/audio.md`。

---

## 附：与本文对应的回归用例

| 主题 | 用例 |
| --- | --- |
| 判定窗口与换算 | `JudgeFrameTableTests`、`JudgeWindowTests`、`JudgeTuningTests` |
| 头的独立性 | `LongNoteHeadTests`、`HeadDecouplingTests` |
| 跨窗口掉帧 | `LiveLogicInputTests.BufferedPressUsesItsSampleTimeWhenTheFrameCrossesTheWindow`、`AutoplayFrameSkipTests` |
| 同时按下两条条（含超短条） | `SimultaneousBarHeadsTests` |
| 越界手指 | `OutOfLaneFingerTests` |
| 按下通道只收按下步 | `PressCandidateRuleTests`、`LiveLogicInputTests`、`HiddenCarrierJudgingTests`、`NotePressDispositionTests` |
| 自己判定的尾 | `TailBehaviourContractTests`、`FrictionFlickTailArmingTests`、`LongNoteViewLifetimeTests` |
| 短条与头的窗口 | `ShortBarHeadWindowTests` |
| 贴图生命周期 | `LongNoteKeyLifetimeTests`、`TailKeyRetirementTests`、`FrictionHeadLifetimeTests` |
| 车道、抬起与划键尾归属 | `TouchLaneTests`、`TailReleaseLaneTests`、`HoldQuotaTests` |
| 内部点的时间和结算 | `InteriorPointWindowTests`、`InteriorPointJudgingTests`、`LongHoldPipelineTests` |
| 按下归属与长条宽限 | `PressOwnershipTests`、`LongNoteGraceTests`、`LongHeadGradeTests` |
| 划动门槛与划尾 | `FlickGestureTests`、`FlickEndBehaviourTests` |
| 追踪总开关与分类 | `LiveTraceTests` |

测试名指向仓库中的回归用例，并不表示本文更新时已重新运行这些用例。排查时从实现入口入手：帧内顺序看 `JudgmentScheduler`；候选和分配看 `LiveCandidatePipeline`；持握、抬手和划动归属看 `LiveHoldCoordinator`；头、体、尾和点分别看 `LongNoteHead`、`LongNote.Body`、`LongNoteTail` 与 `LongHoldCombo`；评级与时间换算看 `JudgeWindow`、`LongEndJudgement` 和 `InputTimeMapping`。
