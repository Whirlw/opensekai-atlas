# 触摸源抽象：交给外部执行者的任务书

> 目的：把「输入从哪来」从判定主链里分离出来，让判定可以被 EditMode 测试驱动；同时给出可验收的边界。
> 适用读者：承接本任务的外部执行者，以及复核这个分支的人。
> 前置条件：读过 [测试](testing.md) 与 [编辑器菜单](editor-menus.md)。
> 最后更新：2026-11-03

## 概述

`LiveLogic.OnInput()`（`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:330`）直接枚举 `NativeInput.ActiveTouches`，类型是 Input System 的 `ReadOnlyArray<EnhancedTouch>`。`EnhancedTouch` 是 struct，测试里造不出来，于是「一帧触摸 → 判到哪个音符 → 判成什么」这条主链只能靠开一局真打来验。

目前纯函数部分已经抽出并被测试覆盖：`JudgeWindow`（判定阶梯）、`TouchLane`（触摸位置 → 车道）、`JudgeTuning`（容差与加宽）、`FlickGesture`（滑动/方向）。剩下没被覆盖的正是「输入怎么进来、怎么落到音符上」。

本任务只做**可测性**，不改判定行为。

## 快速开始

```powershell
cd E:\DeepseekHarness\OpenSekai
git switch -c design/touch-source 348841b
# … 按下方三步实现 …
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\run-editmode-tests.ps1   # 编辑器关闭时
```

## 详细说明

### 目标形态（三步，每步都要能独立通过门禁）

**第 1 步：只加类型，不改行为**

- 新增 `readonly struct LiveTouchSample`：`touchId` / `screenPosition` / `worldPosition` / `time` / `phase` / `deltaPosition`，字段对齐 `EnhancedTouch`。
- 新增 `interface ILiveTouchSource { int Count { get; } LiveTouchSample this[int index] { get; } }`。
- 新增 `InputSystemTouchSource : ILiveTouchSource`，内部读 `NativeInput.ActiveTouches`。**这个文件之外不允许再出现 `EnhancedTouch`。**
- 测试程序集里新增 `FakeTouchSource`（可 add/clear）。

**第 2 步：迁移 `LiveLogic`，行为必须完全不变**

- `LiveLogic` 持有可注入的触摸源（默认设备源），提供 internal 注入点。
- 下列方法从 `ref EnhancedTouch` 改为读 `in LiveTouchSample`：`ShouldProcessTouch`(726)、`UpdateLastTouch`(741)、`TouchExecution`(746)、`CreateLiveTouch`(770)、`UpdateTouchPositionCache`(878)、`OnInput`(330)。（行号基于 `348841b`，仅供参考。）
- 不改判定逻辑本身，不顺手重构别的，不改 public API。

**第 3 步：补测试（至少 6 条，用 `FakeTouchSource` 驱动一次完整 `OnInput`）**

1. 单指落在音符车道、时间命中 → `JustPerfect`（含 ±`JustPerfectJudgeTimeHalf` 边界）
2. 同一触摸晚 0.3 s → 按现有表降级
3. 触摸在判定线上方且超出容差 → 不产生判定（音符状态不变）
4. 触摸在最左/最右车道之外 → 不产生判定
5. 同一 `touchId` 保持按住不重复产生「按下」判定（对应 `ShouldProcessTouch` 的 `lastTouches` 规则）
6. 两指同时落在同一音符 → 只判一次（现有代码若无此保护，就写一条把这个现状钉住的测试并在报告里说明）

测试必须确定性：不依赖设备、不依赖帧率、不 `sleep`。

### 约束

- 不改 `Assets/Game/Runtime/Sekai` 的 public 访问级别（测试已通过 `InternalsVisibleTo` 拿到 internal）。
- 不动 `Assets/Graphics/`、`Assets/Game/Resources/`、构建脚本、签名文件。
- 不重命名任何既有类型/方法/字段（本轮只做抽象）。
- 新增文件必须带 `.meta`。
- 提交信息用英文 conventional commits，一步一个提交，正文写「为什么」。

### 怎么跑

| 场景 | 做法 | 期望 |
| --- | --- | --- |
| 编译 | 编辑器打开工程自动编译 | 0 error 0 warning |
| 测试（编辑器关闭） | `Tools\run-editmode-tests.ps1` | 末行 `failed=0`，报告 `TestResults/editmode-ci.xml` |
| 测试（编辑器打开） | 菜单 `OpenSekai/Dev/Run EditMode Tests`，或 `TestRunnerBridge.Run("EditMode", "TestResults/editmode.xml")` | 打 `[Tests] Finished: passed=… failed=…` |
| 基线 | 任务开始前 | **181 passed / 0 failed** |
| 曲库门禁 | 进 Play → 菜单 `OpenSekai/Library/Play Local Baseline Automatically` | 结算面板 `1,000,000 / S / 总音符 221` |

门禁那局是自动模式，只覆盖「不崩、流程通」；判定细节靠新写的单元测试。测试谱由 `OpenSekai/Dev/Install Test Chart` 装进 `persistentDataPath`。

### 交付报告（严格按此结构）

1. 改了哪些文件（路径 + 一句话作用）
2. 三个提交的 hash，以及受影响的既有测试
3. EditMode 最终 passed/failed，新增测试逐条名字与断言点
4. 门禁截图路径与面板上的分数
5. **没能做到或不确定的地方**（写「未验证」，不要写「应该没问题」）
6. 与本任务书的偏离及原因

## 验收标准

### 硬门禁（不满足即退回）

| # | 判据 | 检查方式 |
| --- | --- | --- |
| 1 | 编译 0 error 0 warning | Console，或 `script.recompile_status` 的 `failed=false, errors=[]` |
| 2 | EditMode `failed=0` 且 `passed ≥ 187`（181 + ≥6） | `TestResults/editmode.xml` |
| 3 | 曲库门禁 `1,000,000 / S / 221` | 结算面板截图 |
| 4 | `EnhancedTouch` 只出现在 `InputSystemTouchSource.cs` | `Select-String -Pattern EnhancedTouch -Path Assets/Game/Runtime -Recurse` |
| 5 | 恰好 3 个提交，工作区干净 | `git log --oneline 348841b..HEAD`；`git status --porcelain` 为空 |
| 6 | 没有重命名、public 面不变 | `git diff 348841b..HEAD --stat`；`LiveLogic` 的 public 方法集合一致 |

### 质量判据

- **行为等价**：`git diff` 里判定分支（`AllocateInputJudgableEachNotes` / `OverlapJudgmentSorting` / `EvaluateJudgment`）应零改动；若改了，必须有测试覆盖并说明理由。
- **每帧分配**：`OnInput` 不得新增每帧 `new`（数组/List/闭包），报告里要明确声明。
- **接口边界**：`FakeTouchSource` 只能待在测试程序集；`ILiveTouchSource` 不向测试泄漏 `EnhancedTouch`。
- **测试有效性**：每条新测试都要能说清「判定表被改坏时它会红」。只断言「没抛异常」的不计入 6 条。
- **报告诚实度**：第 5 节至少列 1 项未验证项，空着说明没有认真做。

### 复核方会做的三件事

1. `git diff 348841b..design/touch-source --stat` 看改动面是否收敛（预期 8–14 个文件）。
2. 通读 `InputSystemTouchSource.cs`，确认 `phase`/`delta` 没有丢。
3. 随机挑一条新测试，把 `JudgeWindow` 的某个阈值改错，确认测试变红，再改回。

## 常见问题

**为什么不允许 PlayMode 端到端测试？** 已经在 `testing.md` 里记过：单场景加载会销毁 Test Runner 的宿主 `EntryPoint`，additive 方案下切 `Core` 也会把它拖死；同时进播放模式触发域重载会让 `TestRunnerApi` 回调丢失。所以整首打歌仍然是真机门禁。

**为什么先抽触摸源而不是直接重写 `LiveLogic`？** 没有表征测试的重写等于盲改。先让主链可测，再动结构，才谈得上「先测后换」。

## 相关文档

- [测试](testing.md)
- [判定窗口与容差](../reference/judging.md)
- [Live 运行时](../architecture/live-runtime.md)

## 复核记录（2026-11-03，分支 `design/touch-source`）

按上面「复核方会做的三件事」逐项做完，另外补了两项：

| 项目 | 结果 |
| --- | --- |
| 改动面 | 3 个提交、13 个文件、+420/−70，工作区干净 |
| EditMode | 194 passed / 0 failed（181 既有 + 13 新增），本地重新构建后复跑一致 |
| 门禁 | `1,000,000 / S / 221`，在分支上本地复跑 |
| `EnhancedTouch` 收敛 | 仅出现在 `InputSystemTouchSource.cs`；`NativeInput` 以同文件 partial 保住原有 public 面 |
| 判定分支未动 | `AllocateInputJudgableEachNotes` / `OverlapJudgmentSorting` / `EvaluateJudgment` 在 diff 中零改动 |
| 变异实验 | 把 `JudgeWindow` 的 JustPerfect 半窗改成一半 → 191 passed / 3 failed，红的含 2 条新增边界用例，改回后工作区 0 变更 |

合并为 `codex/player-lite-refactor` 上的 `6bf0011`（`--no-ff`）。

一处遗留：报告里那条 `NullReferenceException`（`ScreenLayerTouchEffect.PlayTapEffect` 访问空的 `_touchEffectMap`）确认**是基线既有问题**——该文件不在本分支改动范围内，编辑器的鼠标回落可以早于初始化走到这里。已单独修（`_touchEffectMap ??= new ...`），与本次验收分开提交。
