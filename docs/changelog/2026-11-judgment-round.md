# 2026-11 判定轮：名额、协议、时钟、通道

> 目的：记录 11 月这一轮判定与长条改动的顺序、每步为什么做、怎么验证的，方便回溯「当时为什么这么改」。
> 适用读者：要改判定/长条/尾键的开发者。
> 前置条件：读过 [判定核心任务书](../operations/judgment-core-brief.md) 与 [判定参考](../reference/judging.md)。
> 最后更新：2026-11-07

## 概述

这一轮从「长条重叠时第二根手指按下会断连」开始，结束时把判定链路拆成四件互不纠缠的事，并给尾键定义了唯一的相位函数。中间有三次失败的修复和一次错误的根因假设，都记在下面——它们的价值在于划出边界，避免下次再走。

## 详细说明

### 1. 起点：断连（三次失败）

现象：两条长条车道重叠或中途交叉时，第二根手指一按，被持有的长条立刻断连。

| 提交 | 做法 | 结果 |
| --- | --- | --- |
| `ffc42cc` | 持握优先排序 + 剪枝保护 | 无效 |
| `9814956` | 持握者优先候选归属 | 无效 |
| `94330df` | `LongNote.ShouldIgnoreTouch` + 保留未 Done 长条在判定列表 | 无效 |

三次都在「归属」上打转。真正的原因是结构性的：

- 判定入口 `EvaluateJudgment` 每指每帧只判**一条**，且未消费也 `break`；
- 长条要靠每帧被选中才「活着」（`lastInputFrame + MissMesh < frameCount` 就释放）。

两者相乘，第二根手指必然抢走名额。用户给出的规则事实是：**保持只在 BPM 划分的八分音符（半拍）上检查**，检查点之间松手只变灰不断，因此一根手指可以同时续多条长条，根本不需要归属竞争。combo 只在 16 分处增长，说明网格本来就在引擎里（`LongHoldCombo`，`longNoteComboBeat = 8`）。

### 2. 四件事拆开（任务书 A/C/D）

`docs/operations/judgment-core-brief.md` 把链路切成四块，逐个改：

| 代号 | 问题 | 落点 |
| --- | --- | --- |
| A | 一帧只判一条，名额隐式 | `Assets/Game/Runtime/Shared/Live/JudgmentSchedule.cs`（`IJudgmentPass` + `Run<T>`，struct 零分配） |
| B | 「完成」由任意子音符触发 | `NoteParentRole`（Tail/Connection/Combo）+ `NotifyParentFinished()`，只有 Tail 能终结条 |
| C | 时间窗口用帧数表示 | `Assets/Game/Runtime/Shared/Live/ReferenceFrame.cs`（`Seconds = 1f/60f`），判定窗口一律改秒 |
| D | 显示提前量与可判提前量混用 | `JudgeListLead.cs`：`Seconds = 0.35f`、`IsStillTooFarAhead`，与显示提前量分成两个闸门 |

改秒之后踩到的坑：旧测试里「在一帧内模拟多帧」的写法全部失效——时间不再由 `Time.frameCount` 推进，测试必须**逐帧驱动**。

### 3. 尾判：从缓存到时钟

尾键「中途松手不判」的原因是 `OffsetJudgeTime` 是缓存值，尾音符还没 `Excute` 之前恒为 0，判定读到的是过期时间。改成一律读时钟：`NoteBase.JudgeOffsetAt`、`IsTailJudgeableAt(time)`。

同一时期修掉的两个显示问题：

- 结算面板的「JP」标签是 `LibraryResultPanel` 自造的，改成五档，`PERFECT` 已含 JP；
- PERFECT 曾只给 90% 分，改为与 JP 同分，`AnAllPerfectPlayReachesTheFullScore` 断言恰好 1,000,000。

### 4. 对象池与「重置顺序」这个坑

最大的一个坑：`MusicScore.ResetNote()` 在谱面**绑定之后**才跑，会把绑定期写入的字段清空。所有「在绑定时缓存行为」的写法（`_tailBehaviour`、`ParentRole`、`_skipTailJudgement` 之类）在真机上都是空值。

原则：**从绑定或时钟推导，不要缓存**。这条后来直接决定了 `TailKeyLifetime` 的形状（纯函数、无状态、无毫秒常量）。

配套：`LiveTrace.Invariant` 断言池交出的音符是干净的，据此找到 `_swipeAccum`、`_swipeWindowStart`、`offsetTime`、`ParentRole` 四个泄漏（`a0bcff4`）。

### 5. 划键语义

- 反划不判 Miss，封顶 **Great**；`_swipeTailCapped` 必须活到写判定那一刻（`207d746`）。
- 速度门从「速度」改成「一个 60 fps 帧内的净位移」（`flickDistance = 5 cm/s`，`LiveBundleBuildData._flickDistance = 0.083333`）。**单位 bug**：`RuntimeChartConverter.cs:535` 给 `FlickNote` 传厘米未换算，差 155 倍。
- 快速 flick 常常只有一帧 `Ended` 采样，被垂直门判 Outside 后丢弃 → `endsNow` 分支提到车道判断之前（`b76be5e`）。

### 6. 装饰条（Guide）：错误的根因与真正的根因

现象：装饰长条按上去出判定特效，尾键只有 friction 能判。

我先按「装饰条 = `IsDecoration`」写了一串判断（`SkipTailJudgement` → 类型级 `SkipsTailJudgement` → 装饰条不进判定列表 → 尾巴不绑父子关系），四次真机全败。用户提示「记得之前有代码专门将有尾的装饰长条放入判定列表」，也提示「不要走老路」。

真正的根因在读真实谱面之后才出来：把 `Test10.zip` 的 `score.json` 过一遍 `RuntimeChartConverter.Convert`，逐条打印——装饰条是 `Guide/GuideNote`，**`IsDecoration` 是 false、`HasJudgment` 也是 false**，它的 `child` 才是 `Long`/`Flick`/`FrictionFlick`。谱面里根本没有装饰标志位，我写的那一串按 `IsDecoration` 的判断对它们全是死代码。

修复（`0203afa`，`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs` 的 `UpdateJudgmentNoteArray`）：谁回答输入，谁进判定列表。

```csharp
bool barAnswersInput = (note.HasJudgment && !note.IsDecoration) || note is FrictionHideLongNote;
if (barAnswersInput) _judgmentNoteList.Add(note);
if (!barAnswersInput && tailNote != null && !_judgmentNoteList.Contains(tailNote)) _judgmentNoteList.Add(tailNote);
```

`FrictionHideLongNote` 保留在列表里，它要收持握输入驱动内部隐藏点。用户验收：「通过了」。

验证用 `Assets/Tests/EditMode/Live/DecorationTailFromChartTests.cs`——真实谱面 + `FakeTouchSource` + `OnUpdate`/`OnInput`，覆盖 5 根 Guide 条（Long / Flick / Flick / FrictionFlick / FrictionFlick）。

### 7. 尾键生命周期（WP0–WP2）

尾键的可见性此前由散落的布尔量决定（`viewReleased`、`KeepsViewUntilParentEnds`、`ShouldDeferViewRemoval`、`KeepsChildViewsUntilEnd`……），每个尾类型各写一遍。

第一步把它收敛成一个纯函数 `Assets/Game/Runtime/Sekai/View/TailKeyLifetime.cs`：

```csharp
public static TailKeyPhase Of(float tailTime, float visibleFrom, float now, NoteResult result, bool barEnded)
{
    if (result != NoteResult.None || barEnded) return TailKeyPhase.Retired;
    if (now < visibleFrom) return TailKeyPhase.Hidden;
    if (now < tailTime) return TailKeyPhase.Following;
    return TailKeyPhase.SlidingOut;
}
```

第一版外包交付时把 `lead = 0.125`、`grace = 0.1417` 写进去，取自 `LongEndJudgement` 的**判定**表——被打回。依据是 `NoteBase.ReleaseTailKey:650` 的 `currentTime < MusicScoreInfo.time`：尾键在尾音符**自己的时间点**释放，不是尾前 125 ms；释放后沿判定线滑出屏幕。**判定窗口不等于视图窗口**，这是那一次打回的全部内容。

第二步（`7f82109`）把相位接进视图 tick，唯一调用点 `LongNote.GetTailKeyPhase`：

```csharp
float visibleFrom = childNote.MusicScoreInfo.time - (CalcTimeOffset?.Invoke(childNote) ?? offsetTime);
return TailKeyLifetime.Of(childNote.MusicScoreInfo.time, visibleFrom, currentTime, childNote.Result, State == NoteState.Done);
```

`offsetTime` 就是显示提前量（`LongNote.cs:296` 用它判断「太远先不画」），所以 `visibleFrom` 与音符真正开始被绘制的时刻一致。

第三步（`3f72c08`）让`LiveLogic.TickTailKeyViews` 只按相位决定去留，把原先散在 `NoteBase`/`LongNote` 里的六个视图开关收进这条路径。随后真机自查发现一处缺口：相位函数里的 `result != None → Retired` 只是「不再等」，键还会继续沿判定线滑，**判过的尾会拖出一颗幻影键**。`5efbd55` 把它补成「判过就走」——`TickTailKeyViews` 里 `tail.Result != NoteResult.None` 的键当帧 `RemoveView()` 并同时从 `_tailKeyViews`/`_retiringViews` 摘掉；没人判过的键仍然滑出去，因为「条结束了而手指在别处」应该表现为音符消失，而不是一颗键停在线上。

### 8. 摩擦/隐藏被合并、尾键不画、抬手无归属（2026-11-07 下午，第四轮）

这一轮三个故障，前两个的共同点是**症状都在视图层，根因都不在视图层**。

**① 摩擦与隐藏在转换层被合并成同一个东西。** 「摩擦长条没有声音、没有尾键」查了三轮：先在 `TapEffectView` 放松 `Pass` 判定、又在 `ScoreLogic` 加 `HasJudgment` 过滤、还给 `FrictionHideLongNote` 补了 `PublishResult()`——结果音效没回来，反而多了一次 MISS、结算超过 1,000,000。

真因在 `RuntimeChartConverter.ConvertLongNoteTraceToNoJudgment`（已删除）：它把带连接的 `FrictionLong` 头改写成 `FrictionHideLong`、把链尾的 `Friction` 改写成 `FrictionHide`，再塞一个 `isSkip = true` 的假摩擦音符。谱面里「摩擦」和「隐藏」**原本是两个不同类别**，转换后变成同一个无判定载体，于是摩擦没有结果可发布、没有声音、也没有尾键。用户给的判据很直接：每段尾巴的顺序是「普通 / 上划 / 左划 / 摩擦上划 / 摩擦左划 / **摩擦** / **隐形**」，前六个要和第七个区分开。删除合并后，`FrictionLongNote`（有判定）与 `FrictionHideLongNote`（静音载体）各自归位。

**代价教会我的事：`PublishResult()` 不是显示通道。** 它同时驱动 `onUpdateScore` / `onUpdateCombo` / `onDamage`：`ScoreLogic.TakeNextNoteBaseScore` 按 `_scoredNoteCount` 累积摊分，凭空多算一个音符就使总分冲破 1,000,000；`UpdateCombo` 把 `Pass` 当作低于 Great 而重置连击。想让视图出声，只能走视图自己的通道（`LongTapEffectView` 的循环音），不能补一个假结果。

**② 尾键按「开头的类型」决定画不画。** `LongNote.ShowsTailKey` 曾经把 friction / friction-hide / guide 开头的条整体排除出 `ViewNoteList`，而 `NoteBase.OnSpawnNote` 又有 `if (parentNote != null && !parentNote.ViewNoteList.Contains(this)) return;`——于是这些条的尾键**连视图都不生成**，但它们照样在 `_judgmentNoteList` 里被判：隐形可判。结论：**要不要画，是音符自己的性质**（`IsSkip` / 隐藏 / 有没有判定），不是长条看自己的头型。现在规则是 `if (!note.IsSkip) ViewNoteList.Add(note)`，隐藏标记靠 `HasJudgment == false` 自然静音。

**③ 普通尾的判定没有归属检查。** `LiveLogic.CoverHoldsWithAnyFinger` 对每个 `Ended/Canceled` 样本先置 `releasedInside = true` 再做车道判断，等于**任意手指在任意位置抬手都会结束任意一根正在按着的条**。改成「持握者 id 或落点在条的车道范围内」才算松手；`JudgeLane*` 已经是 `lane ± LongLaneOffset`，即左右各 **1.5 轨**，不需要再加容差。

### 9. 尾的结束通道收口（WP1，2026-11-08）

任务书 WP1 只做了一半：`ITailBehaviour`、`ReleaseTail`、`SwipeTail`、`SelfJudgedTail` 都在，但 `LiveLogic` 仍按三个派生布尔走六条具名路径，等于把「哪种尾怎么结束」这件事留在了两个地方。这次收尾：

- 接口收窄为 `bool TryFinish(LongNote bar, in TailFrame frame)` + 三个能力属性；`TailFrame` 只携带一帧的事实（时间、帧长、滑动量、是否在条内松手、是否被覆盖、是否处于按住态）。
- 新增 `SilentTail`：装饰条与显式跳过尾判的条不是「按 release 结束」，而是「什么都不判」。以前这层语义藏在 `! _skipTailJudgement &&` 里，现在是一个能被命名的实现。
- `LiveLogic.CoverHoldsWithAnyFinger` 的三岔口收敛为一次 `TryFinish`，超时仍由 `ExpireTail` 兜底；`LongNote` 上三个布尔适配器连同 6 处内部分支一起删除。
- 守住它的是源码扫描用例 `TailBehaviourContractTests.NoBarAdapterBooleansComeBack`：三个布尔名一旦回到 `Assets/Game/Runtime` 就变红。
- 数字：482 → **485 条 / 481 passed / 0 failed / 4 skipped**（`TestResults/wp1-tail-contract2.xml`），包 `Builds/wp1-tails-2026-11-08`。

### 10. 谁会黏在判定线上（2026-11-08，三轮才对）

老 bug，这一轮才看见：长条的头过了自己的时间点后，视图把它按在判定线上，于是**任何**没被判的长条都停在那里——普通条是有意的（身体从这条线开始收），摩擦条和 Guide 条则是冻结，看起来像「按住了但没反应」。

**第一轮失败**：我改的是 `viewProgress` 那一层，但 `rawProgress` 在更上面就已经被 `Mathf.Clamp01` 夹成 1，条件永远不成立——写进去的是死代码，真机零变化。同一轮加的测试只断言了谓词（`IsOrdinaryHead`），所以全绿也没拦住。

**第二轮过头**：把身体采样也放行，结果身体跟着流下判定线，而且**尾键之前缺一截**。缺的那截有确定原因：`startProgress` 超过 1 后 `splitCount = ceil((start - end) * 100)` 会超过网格缓冲的 `SplitMeshCount + 10 = 110` 格，循环在缓冲用完时停住，尾侧样本根本没进网格。

**第三轮定稿**：两条规则分开——身体永远止于判定线（`NoteLineProgress.Body`，无条件夹），只有条头那颗键在「没人碰过」时流下（`NoteLineProgress.BarKey`，判据 `LongNote.KeepsHeadOnTheLine => IsOrdinaryHead`）。测试改成断言数值（1.2 → 1.0 / 1.2），不再断言意图。

数字：485 → 488 → 495 → **494 条 / 490 passed / 0 failed / 4 skipped**（`TestResults/head-pin6.xml`），包 `Builds/head-pin3-2026-11-08`。

## 验证

| 阶段 | 数字 | 报告 |
| --- | --- | --- |
| 装饰条修复 | 430 passed / 0 failed / 4 skipped | `TestResults/deco-chart3.xml` |
| 尾键纯函数（修订后） | 472 / 468 passed / 0 failed | `TestResults/tail-key-revise-verify.xml` |
| 尾键接线（WP2） | 474 / 470 passed / 0 failed | `TestResults/wp2-verify-mine.xml` |
| 判过的尾即刻离场（`5efbd55`） | 479 条 / 475 passed / 0 failed / 4 skipped | `TestResults/tail-fix6.xml` |
| 尾键按音符性质绘制 + Guide 尾键可见 | 479 / 475 passed / 0 failed / 4 skipped | `TestResults/tailvisible2.xml` |
| 摩擦/隐藏类别保真（转换器） | 480 / 476 passed / 0 failed / 4 skipped | `TestResults/friction-cat2.xml` |
| 抬手归属（`TailReleaseLaneTests`） | 482 / 478 passed / 0 failed / 4 skipped | `TestResults/tail-release-lane6.xml` |
| 真机 | `Builds/guide-tail-2026-11-07` 用户验收「通过了」 | — |
| 真机 | `Builds/tail-release-lane-2026-11-07` 用户验收「全部通过」 | — |

## 相关文档

- [判定核心任务书](../operations/judgment-core-brief.md)
- [判定链路](../architecture/README.md)
- [判定参考](../reference/judging.md)
- [测试](../operations/testing.md)
- [发布](../operations/release.md)
