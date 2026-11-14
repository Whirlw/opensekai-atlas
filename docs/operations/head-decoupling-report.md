# 长条头与条体解耦报告

> **状态：第一阶段已验收合入。** 但其上层问题（头仍挂在条的生命周期上）未解决，见 `tail-classes.md` §十二。

日期：2026-11-10。任务书：[head-decoupling.md](head-decoupling.md)。起点 HEAD：`58d2d8d`，开始时工作区干净。

## 当前问题与本次范围

按用户最新补充，当前未解决的真机问题只有 **friction 连击暴涨**。提前划额外 Miss 已在 `eac5b99` 修复，本次相关测试仅防回归，不计作本次修复成果。

本次实施头部结果与条体状态发布职责的解耦，不重复尝试任务书列出的划尾、窗口或状态回退补丁。没有真机验证，不能仅凭 EditMode 通过断言 friction 的真机现象已消失。

## 耦合点逐项处理

| 原耦合点 | 处理 |
|---|---|
| `LongNote.State` 在 Scrolling/Last → InputBegan 发布四个回调 | 整段删除。setter 仅保留状态、出生/退场和生命周期处理；进入任何视觉状态不再发布头部判定 |
| `LongNoteHead.Judgment` 靠 setter 发布 | 普通头按下时显式调用 `Head.Settle`。头部自行保存结果、写兼容镜像、发布；随后仍按原路径更新条体外观、持握与中间点 |
| 发布次数受条体 State 控制 | `LongNoteHead` 持有 `_result` 与 `_headPublished`。结果非 None 后不允许再次结算，发布标志在任何回调之前置位，防止回调重入与重复按下再次发布 |
| `Excute` 离开 Last 时发头部 Miss | 保留 Last → Release 原条件，仅在头部尚未结算时调用 PublishHeadMiss |
| `PublishHeadMiss` 与超时路径各自写结果/发回调 | 两条路径合并到相同 `Settle` 入口；既检查普通可判头，也检查头部自身是否已结算，不再依赖条体当前 JudgeInfo 判断 |
| `NotifyHeadMiss` 是无保护的直接发布入口 | 删除该方法；条体只提供内部回调访问桥，实际顺序和一次性保护都由 LongNoteHead 负责 |
| `RestoreHeldState` 恢复外观可能重发 | 保留现场已有 Last/Release → Input 的状态选择与条件，只更新注释；即使其他代码显式进入 InputBegan，setter 也不再发布。未把它改回旧 InputBegan 补丁 |
| 摩擦头在 `FrictionLongNote.Excute` 中直接写结果 | 保留 isTouched、Progress、State 门禁和视觉更新，结果改交 `Head.Settle(CompletedTouchResult)`，避免移除 setter 后漏发摩擦头 |
| 隐藏摩擦头的备用直接写结果路径 | 同样交 Head.Settle，保留 Pass，HasJudgment=false 时不发头部四个回调 |
| 自动演奏通过 setter 发布 | `AutoJudgment` 显式交 Head.Settle(Auto)，中间点与尾部自动演奏路径保持不变，重复自动刷新不重发头部 |
| 池复用 | 现有 ResetNote → Head.Reset 清空独立结果与发布标志；不新建头对象，不沿用上局结果 |

头部回调顺序按任务书统一为 **onDamage → onUpdateCombo → onJudgment → onUpdateScore**；超时/Last 引发的头部 Miss 继续省略 score 回调，保持原 Miss 约定。按下命中（包括按下计算出 Miss）保留 score 回调。现有 `LongNoteHeadTests.HeadResultAndCallbacksAreVisibleInThePressFrameOnlyOnce` 仅更新顺序期望，其余结果、次数、外观状态和 offset 断言保留。

头部拥有独立且稳定的 `Head.Result`。`LongNote.JudgeInfo` 继续作为旧接口镜像，头部结算时写入；尾部既有代码仍可能把尾判写到这个旧字段，供旧调用者读取。本次没有改这些尾判写入及其判定语义，尾判也不能改写 Head 内部结果。新增提前划测试同时检查结尾后 Head.Result 仍是头部 JustPerfect。

生产改动为四个文件：`LongNote.cs`、`LongNoteHead.cs`、`FrictionLongNote.cs`、`FrictionHideLongNote.cs`。后两者是移走 setter 发布时必须覆盖的显式头部入口。测试为新增文件加一条既有顺序期望调整，另有 Unity 生成的 `.meta`；没有其他重构。

## 四条新增测试

文件：`Assets/Tests/EditMode/Live/HeadDecouplingTests.cs`。

| 用例 | 关键断言 |
|---|---|
| `AHeldBarPublishesItsHeadOnce` | 普通头按下并连续执行/按住 10 帧，每帧 onJudgment 均为 1；四回调严格按 damage/combo/judge/score。池复位后两次 AutoJudgment 只新增 1 次头判。摩擦头与隐藏摩擦头各持续 10 帧，头判分别为 1 和 0，结果分别 JustPerfect 与 Pass |
| `AJudgedHeadIsNotOverwrittenByTheGraceEnding` | JustPerfect 后由实际 Excute 进入并离开 Last，再调用 PublishHeadMiss，结果不变、头判仍 1 次；通过测试子类调用真实 setter 重放 Last → InputBegan，仍不发布 |
| `AnEarlySwipeAddsNoMiss` | 1 秒划尾于 0.9 秒提前划，记录 deferred；松手使条进 Last，并显式发出输入协调器对应的 NotifyLaneRelease，经过三帧倒数完成。头判列表严格只有 JustPerfect；尾判列表只有 1 项，等于 childNote.Result 且非 Miss；独立 Head.Result 不被尾部覆盖 |
| `ARerefreshKeepsTheBarLitWithoutRepublishing` | 连续 10 帧 Excute + RefreshHeldInput，每帧保持 Input/InputBegan 外观，onUpdateCombo 始终严格为 1 |

测试的直接状态重放专门触发真实 setter，不使用绕开 setter 的 SetStateUnnotice 来证明发布路径已移走。没有放松头部次数或结果断言。

## 先红后绿与最终验证

先仅增加测试、尚未改生产代码时运行了全量：`TestResults/head-decoupling-red.xml`，**522 total / 515 passed / 3 failed / 4 skipped**。

- `AHeldBarPublishesItsHeadOnce` 在发布顺序断言失败：旧实现先 judge。
- `AJudgedHeadIsNotOverwrittenByTheGraceEnding` 在重放 Last → InputBegan 后失败：头判从 1 增为 2。
- `AnEarlySwipeAddsNoMiss` 首版测试漏了输入协调器的抬手通知，倒数尚未启动即期待 Done。这是测试驱动错误，不能当作旧 flick Miss bug 再现；补齐 NotifyLaneRelease，尾判生产代码未改。
- `ARerefreshKeepsTheBarLitWithoutRepublishing` 在基线已通过：已有补丁已避免该具体刷新路径重发。本次如实保留它作回归保护，没有人为改坏旧代码制造四条全红。

解耦并补齐上述测试驱动后，全量最终结果：**522 total / 518 passed / 0 failed / 4 skipped**。四条新测试均 Passed，比基线 518 / 514 / 0 / 4 恰好多 4 条通过。

最终 XML：`TestResults/head-decoupling-green.xml`，根节点 test-run 开始/结束时间：**2026-11-10 15:37:45Z / 15:37:48Z**。XML 为 TestResults 输出，不纳入源代码交付。

工具链检查：

1. 本机 Unity 6000.6.7f1、7800 接口执行 AssetDatabase.Refresh(ForceSynchronousImport)，随后 RequestScriptCompilation(CleanBuildCache)。测试 DLL 时间从 15:03:50 更新到红测 15:34:52，再更新至最终 **15:37:08**。
2. 通过编辑器确认 isCompiling=false，且已加载最终两参数的 Head.Settle，避免旧程序集假绿。
3. 指定 Unity DotNetSdk/dotnet.exe + SDK 8.0.318/Roslyn/bincore/csc.dll，分别使用最新 `Library/Bee/artifacts/1300b0aE.dag/OpenSekai.Game.rsp` 和 `OpenSekai.Tests.EditMode.rsp`：**均 exit 0，无编译诊断**。
4. 通过任务书指定 TestRunnerBridge.Run 执行 EditMode，读取 test-run 根节点并逐条核对四条新测试 Passed。
5. `git diff --check` 通过。核对差异：中间点 EarlyGoodMs/LateGoodMs 循环、JudgeDueComboPoints/_barWasInPlay、HoldGraceFrames=3、Last 进出条件、LiveHoldCoordinator、Conversion 和划尾方法均未改。

## 可能影响手感但未改的地方

- 摩擦头仍按既有 isTouched + Progress/State 门禁在 Excute 中结算，没有提前到触摸回调，也没有改变到达线附近的采样时机。这仍依赖帧更新顺序；为避免改变手感，本次仅搬移结果所有权和发布职责。
- 沿用已有 Last 进出与三帧宽限，即使实际代码中离开 Last 的路径与任务书的概述不同，也没有重写状态机。没有调整换手、归属、持续按压的时间容差。
- 划尾提前窗口、速度/方向要求、延迟倒数及 capped/deferred 全部保留。eac5b99 的 flick Miss 修复只接受防回归验证。
- 中间点仍使用全局帧时钟与原窗口；没有改变点的 Last/Miss 生命周期，也没有扩大窗口来掩盖 friction 现象。
- 头部一旦结算，后续 AutoJudgment 也不会重新写成 Auto 或再发布；这是“一次性头部”的契约。旧的条体结果字段仍兼容尾判用途，外部工具若要查看不可变头判，应读取头部所有者，而非在结束后把条体字段当头判。
- 回调顺序按任务书调整；音效/视图回调观察到的先后次序可能随之改变，但时窗、分数公式和 offset 清零时机没有改。命中发布仍在视觉状态切换之前，与原调用位置保持一致。
- 普通持续按住和摩擦持续按住的简单循环在基线并不都能复现真机整批暴涨；本次确实移除了可证明会重发的 setter 路径，但 **friction 真机问题仍须以设备上原谱面复测为准**。尚未构建或安装真机包。

本次未提交、未推送，也未切换工作区分支。