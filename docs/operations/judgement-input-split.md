# 判定与输入拆分（任务书）

> 状态：进行中（2026-11-10）。基线 `main = 0ca52c3`。
> 本文件是这项工作唯一的事实源：改判定顺序、归属、中间点之前先读它。

## 一、要解决的问题

现在的判定把两件事揉在一套调度里：

- **按下类**音符（普通键、普通划键、长条头）本来就该抢「一根手指每帧只判一条」的名额
- **其他所有音符**（长条中间点、结尾、摩擦、隐藏、连接、装饰）也被塞进同一套候选集，于是出现：
  中间点被别的手指或长条体占了名额后这一帧就判不到；两条长条的自动点重叠时只判一个

## 二、目标规则（原始表述，不要改写）

### 2.1 长条状态机（任务 1）

| 阶段 | 状态 | 视觉 |
| --- | --- | --- |
| 显示之前 | `Wait` | 无 |
| 显示之后、到判定线之前 | `Scrolling` | 亮 |
| 到达判定线的瞬间 | 按「当前长条车道上是否有触摸」决定 → `Playing` 或 `Release` | 亮 / 灰 |
| 长条结束（任意状态） | `End` | — |

转移：

1. `Release` 且车道上有触摸 → `Playing`
2. `Playing` 且车道上没有触摸 → `Last`
3. `Last` 超过 **3 帧**（`16.7 ms × 3 ≈ 50 ms`）→ `Release`
4. `Last` 且车道上有触摸 → `Playing`
5. 任意状态长条结束 → `End`

视觉：`Release`、`Last` 是灰的；`Scrolling`、`Playing` 是亮的。

**输入阶段（Began/Input）归具体音符**：开头、中间点、结尾各自在自己的 State 上记；长条本体只留生命周期。

### 2.2 中间点判定

中间点的判定条件：**判定区间内，所归属的长条状态出现过 `Playing` 或 `Last`**；并且当前帧有触摸落在该点的车道内。

不是「此刻必须是 Playing/Last」——区间比判定瞬间长，所以要**记住**区间内是否出现过，而不是在判定那一帧采样。

满足条件时每帧独立判定：遍历窗口内所有触摸，只要有一个落在该点车道内就判定，判完跳出。不挑手指、不占名额、重叠时各判各的（判两次正常）。

### 2.3 其余规则

3. 普通头判定上等价于 1.5 车道容差的普通键，其他只有视觉上的区别。
4. 只有普通头 / 普通键 / 普通划键在「一根手指每帧只判一条」的规则里面。其他所有键都不受这个约束。
   完整流程：每次收到一个或多个同时按下的触摸事件，先列出每个按下事件所能判定的、只包括「普通头 / 普通键 / 普通划键」的列表；找到它们列表里重叠的项目，按离中心点的距离分配给唯一的按下事件，从其他按下事件的列表里去除；之后每个按下事件判定它列表里最早的那个键。同时，其他键的判定完全不受这个流程影响。

## 三、当前代码地图

| 关注点 | 位置 |
| --- | --- |
| 帧入口 `Judgment()`：`_connectionNoteList.Clear()` → `JudgeDueComboPoints()` → `AllocateInputJudgableEachNotes()` → `OverlapJudgmentSorting()` → `LeaveNotesFastGeneration()` → `PickUpNotBeginInputNotes()` → `RefreshSustainedHolds()` → `EvaluateJudgment()` | `LiveLogic.cs` 约 625–640 行 |
| 中间点判定（新） | `LiveLogic.JudgeDueComboPoints()`（约 643 行） |
| 中间点自身的「是否出现过 Playing/Last」记录 | `LongHoldCombo._barWasInPlay` / `MarkBarInPlay()` / `WasBarInPlay` |
| 候选集填充、排序、剪枝、归属预留 | `LiveCandidatePipeline.cs`（`Collect` / `Sort` / `DropExtraFastGeneration` / `PickUp` / `IsReservedForAnotherInput`） |
| 长条体续命、持有者刷新 | `LiveHoldCoordinator.cs`（`AssignContinuingHolds` / `Refresh` / `CoverHoldsWithAnyFinger`） |
| 长条状态机（待重写） | `LongNote.cs`（`Excute` / `Judgment` / `WasTouched` / `IsHoldFresh` / `PublishesHeadMiss` / `IsOrdinaryHead`） |
| 中间点 | `LongHoldCombo.cs`；挂到长条上：`LongNote.AddHoldCombo`（`LongNote.cs:226`） |
| 车道容差 | `JudgeTuning.cs`：普通 1.25、长条头 1.5、划键 1.5 |
| 「判完关槽」概念（第 4 步要删） | `NoteBase.ClosesSlotAfterPress`、`JudgmentSchedule` |

## 四、实施顺序

| 步 | 内容 | 状态 |
| --- | --- | --- |
| 3 | 普通头 = 1.5 车道容差的普通键 | 完成（`eab742c`，`LaneToleranceTests`） |
| 2a | 中间点带上父长条的车道 | 完成（`6bc63f5`） |
| 2b | 中间点每帧独立判定、不占名额 | 完成（`0ca52c3`） |
| 2c | 中间点判定改为「区间内出现过 Playing/Last」的**记忆**语义 | 已实现 `_barWasInPlay` 记号，待补测试 |
| 1 | 长条只留生命周期（2.1 的完整状态机，3 帧 ≈ 50 ms） | 未开始 |
| 4 | 候选集只收普通头/普通键/普通划键；重叠按中心距离归一；各判列表里最早一条 | 未开始 |

**注意**：2c 现在跨在 2b 与 1 之间——状态机重写（第 1 步）之后，`Playing/Last` 的含义会由 `LongNote` 自己的状态机给出，届时 `JudgeDueComboPoints` 里的 `bar.State` 判断应改为读新的状态机，`_barWasInPlay` 记号的触发点也随之调整。

## 五、测试脚手架（已验证可用）

```csharp
private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);
private static LiveLogic.InputTmp Input(int finger, float lane) => new LiveLogic.InputTmp {
    Lane = lane,
    Touch = new LiveTouch { fingerId = finger, touchId = finger, musicTime = 0, phase = TouchPhase.Stationary }
};
private static T Field<T>(LiveLogic logic, string name) =>
    (T)typeof(LiveLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
private static void Stage(LiveLogic logic, string name) =>
    typeof(LiveLogic).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
```

参考：`Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs`（`HeldNote` 夹具、`Stage`、`Field`）。

**待补的红/绿测试**：

1. 非持有手指落在车道内 → 中间点被判到
2. 两条长条的中间点重叠 → 各判一次
3. 长条**从未**出现过 Playing/Last（一直 Release）→ 中间点不判
4. `Last` 超过 50 ms → 回到 `Release`（第 1 步完成后）

## 六、验证门禁

1. 先跑相关类：`LongHoldPipelineTests`、`TailReleaseLaneTests`、`DecorationTailJudgmentTests`
2. 再跑全量 EditMode（当前 506 条，基线 502 通过 / 4 跳过）
3. 真机：普通谱一首、长条密集谱一首（BOF）、变速谱一首
4. 编译校验：`dotnet "<Unity>/Editor/Data/DotNetSdk/dotnet.exe" "<Unity>/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@<最新 Library\Bee\artifacts\**\OpenSekai.Game.rsp>" -nologo`，随后 `AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`，等 40–60 秒再跑测试

## 七、纪律

- 不动「普通头 / 普通键 / 普通划键」的每指每帧一条，直到第 4 步
- 每次只做一步，未通过门禁先撤工作树，不要带红测试往下做
- 判定改动必须用「普通 4/4 谱 + 长条密集谱 + 变速谱」验证

## 八、任务 1 的执行方案（2026-11-10 修订，一步到位）

### 8.1 现状事实（已核实）

`LongNote` 上已有两个同名家族成员，新属性必须另起名：

| 名字 | 语义 | 位置 |
| --- | --- | --- |
| `HoldingTouchId` | 持有者手指 id（`>= 0` 即有人按着） | `LongNote.cs:512` |
| `IsHeld(float currentTime)` | 有手指 **且** 未过期（判定用） | `LongNote.cs:519` |
| `IsHeldByAnotherFinger(int, float)` | 别的指头占着 | `LongNote.cs:536` |
| `IsHeld(NoteBase)` | pipeline 私有静态，读 `State` | `LiveCandidatePipeline.cs:237` |

`NoteState` 目前是 `Wait / Playing / InputBegan / Input / Release / Last / Done`；长条「被按住」用 `InputBegan`/`Input`，`Playing` 实际等于规格里的 `Scrolling`。

`LongNote.cs:99-100` 的状态 setter 里藏着业务副作用：`(Playing→InputBegan) || (Last→InputBegan)` 会触发 `onJudgment / onUpdateCombo / onUpdateScore / onDamage`——这就是「状态迁移顺便表达按住了」的根源。

### 8.2 三步（一步到位地做完）

1. **加真源**：`public bool HasFinger => _holdingTouchId >= 0;` 并补等价性测试（按住时 `HasFinger` 为真且 `State` 为 `InputBegan/Input`；未按住时两者皆假）
2. **换读方**（只读取，不改行为）：
   - `LiveHoldCoordinator.cs` 5 处（约 80 / 159 / 209 / 278 / 300 行附近）
   - `LiveCandidatePipeline.cs:211-212` + 私有 `IsHeld`（237 行）改读 `HasFinger`
   - `LongTapEffectView.cs:151`
   - `LiveLogic.cs` 里自动接取的两处
   - `LongNoteView.cs` 的 z 序判断
3. **删写入点**：长条自己写 `InputBegan/Input` 的 6 处（约 547 / 551 / 612 / 1343 / 1451 及 setter 副作用），把 `onJudgment/onUpdateCombo/onUpdateScore/onDamage` 搬进显式的 `OnHoldStarted()`；测试里 `SetStateUnnotice(InputBegan)` 换成「按住」构造助手

做完后长条状态收敛为 `Wait / Playing / Release / Last / Done`，此时再加 `Scrolling` 几乎免费：

| 状态 | 含义 |
| --- | --- |
| `Wait` | 还没出现 |
| `Playing`（原 Scrolling） | 已出现、还没被按 |
| `Input`/`InputBegan` | **删除**，改由 `HasFinger` 表达 |
| `Release` | 灰（没被按 / 松手超时） |
| `Last` | 松手宽限（3 帧 ≈ 16.7 ms × 3 ≈ 50 ms，用时间比较） |
| `Done` | 结束 |

### 8.3 门禁

每一小步都跑全量 EditMode（506 条 / 基线 502 通过 / 4 跳过）；第 3 步后必须装机验证：普通谱 + 长条密集谱（BOF）+ 变速谱各一首，看长条是否按预期变灰、中间点是否仍可判。

## 九、另一轮尝试的经验（2026-11-10，务必先读）

### 9.1 已试过的四条路与结果

| 尝试 | 结果 |
| --- | --- |
| 基线 | 502 条 / 498 通过 / 0 失败 |
| 步骤 1：按下通道只收三类 + 自由通道 | 10 失败 |
| 协调器改调 `Judgment` | 6 失败 |
| 方案 A：协调器独占长条（`Judgment` + sweep 合并） | 23 失败 |
| 方案 B′：长条留在按下通道，只让「未结束的长条不关槽」 | 未做（被更彻底的规则取代） |

### 9.2 两堵墙（结论，不要重复踩）

1. **长条每帧的判定今天是由按下通道送进去的**。一旦它退出按下通道，就没有任何东西再调 `LongNote.Judgment`——`LiveHoldCoordinator.RefreshHeldInput` 只刷新「手指还在」，不推进内部点、不判尾、不处理松手。
2. **划键尾巴依赖协调器那趟 sweep**：`CoverHoldsWithAnyFinger` 读 delta、判 ended 样本、调 `TryFinish`。如果让 `Judgment` 先跑，sweep 被跳过，划尾用例全红。

即：长条每帧该由谁判，两套机制重叠，各自都背了一部分语义（内部点/尾判 vs 滑动/松手）。

### 9.3 方案 A 为什么崩（23 条）

失败集中在三族：

- `OverlappingHoldsJudgeBothInteriorComboPoints`（12 条）
- `ContinuousInputKeepsBothMovingHoldsThroughOverlap`（3 条）
- `PickupPreservesBothHoldersRegardlessOfInputOrder`（4 条）、`SecondFingerDoesNotStaleTheFirstHold`（2 条）、`OverlappingTapDoesNotConsumeTheHeldNotesDeferredCandidate`（2 条）

根因：长条重叠时「哪根手指喂哪一条」本来是按下通道用候选/归属算出来的。A 把长条摘出按下通道后，协调器的 sweep 变成「哪条被覆盖就判哪条」，两指两条重叠时每个覆盖帧把两条都判一遍，内部点被**重复结算**。

**关键判断：这 12 条不是回归，是旧契约在挡路。** 按新规则「一次按下判两次」，它们本就该断言双判。

### 9.4 权威规则（2026-11-10，取代此前所有方案）

> 完全舍弃按指分配逻辑，内部点逻辑需要重写：所有长条，除了头尾，只要在当期 BPM 段全局的 8 分音符线上，就生成中间点。中间点允许完全重叠，按上判两次完全正常。

由此**整体作废**的机制（不只是绕过）：

- `ShouldReserveHold`
- `CandidateOrder.HeldRank`
- `NoteBase.ClosesSlotAfterPress`
- `PickUpNotBeginInputNotes`
- `_connectionNoteList`

### 9.5 生成侧（只差核对，不是重写）

`RuntimeChartConverter.AddComboNote`（`RuntimeChartConverter.cs:339`）现在用 `LongNoteComboBeat`（工程值 8）把每一小节等分。4/4 下每小节 8 份正好是 8 分音符；需要核对的是**非 4/4 小节**与**中途变速段**是否仍落在「当期 BPM 段的全局 8 分音符线」上，而不是落在「该小节等分」上。

### 9.6 执行顺序（取代 8.2 的第 2、3 步）

1. `IsPressStepNote` 对长条一律返回 false——长条彻底退出按下通道
2. `ConnectionNoteJudgment` 去掉「必须被按住」前置：改为在 `LiveHoldCoordinator.Refresh` 里每帧对每条未结束长条、每个活跃触摸跑一遍——**存在即判，重叠各判各**
3. 头部/尾部保持「车道内任意手指可判」，去掉窗口内的竞争保留逻辑
4. 删除 9.4 列出的五个机制，并把相关测试按新契约重写（断言双判）
5. 全量回归 + 装机验：Test13、重叠长条、长条换手

> 注意：第 2 步与「协调器 sweep 管划尾」冲突（见 9.2 第 2 堵墙），所以内部点与尾判必须**在同一个遍历里**分派，不能一个在 `Judgment`、一个在 sweep。

## 十、最终结论（2026-11-10 收口，取代 9.4 / 9.6）

> 中间点的判定：判定区间内，**所归属的长条状态出现过 `Playing` 或 `Last`**。

上一节「完全舍弃按指分配、以点为中心」的说法**作废**。中间点仍然隶属于长条，判定条件依赖长条状态；因此按下通道里的归属/预留机制（`ShouldReserveHold`、`HeldRank`、`ClosesSlotAfterPress`）**保留，不删**。

状态名对照（重要）：规格里的 `Playing` 在现有枚举里是 `InputBegan` / `Input`，而枚举里的 `Playing` 等于规格里的 `Scrolling`。

已实现（`5b6e38a feat(live): an interior point remembers the bar was in play during its window`）：

| 项 | 位置 |
| --- | --- |
| 记忆位 `_barWasInPlay` + `MarkBarInPlay()` / `WasBarInPlay` | `Assets/Game/Runtime/Sekai/Live/LongHoldCombo.cs` |
| 每帧先刷新记忆位，再按触摸判定 | `LiveLogic.JudgeDueComboPoints()`（在 `AllocateInputJudgableEachNotes()` 之前） |

判定流程：

1. 点到期（`State == Last`）后，每帧检查其长条状态
2. 若长条处于 `InputBegan` / `Input` / `Last`，把 `_barWasInPlay` 置真（记忆，不是当场采样）
3. 只要 `_barWasInPlay` 为真，且有任意触摸落在该点车道内 → 判定该点
4. 每个点独立判定，重叠时各判各的；不占「每指每帧一条」的名额

剩余待办（不阻塞，按价值排序）：

1. `Last` 的 3 帧宽限改成时间比较（16.7 ms × 3 ≈ 50 ms），现在按帧计数
2. 长条状态收敛（`HasFinger` 已加在 `LongNote.cs:518`，读方切换与写入点清理尚未做，见第八节）
3. 非 4/4 小节与变速段的中间点生成核对（见 9.5）

## 十一、长条状态机对齐：四轮尝试后的执行顺序（2026-11-10）

### 11.1 四轮的净结果（不要再重复）

| 轮次 | 做法 | 结果 |
| --- | --- | --- |
| 1 | 按规格对齐（丢手进 `Last`、`Last` 显灰） | 5 红 → 回退 |
| 2 | 方案 B：去掉 `IsHoldFresh` 的时间容差，宽限只留 `Last` 3 帧 | 6 红 → 回退 |
| 3 | B + 摩擦条的 `Last` 出口 | 编辑损坏未编译 → 回退 |
| 4 | B + 摩擦条 + `RestoreHeldState` | 行号手术把括号改坏 → 回退 |

基线始终是 `main = 65e18ba`，全量 `506 / 502 通过 / 4 跳过`。

### 11.2 真正卡住的地方：测试契约先于实现

失败集中在 6 条，它们钉的是**旧语义**：

| 测试 | 旧契约 | 新语义 |
| --- | --- | --- |
| `AHoldSurvivesThreeFramesWithoutAFingerAndDiesOnTheFourth` | 3 次 `Refresh` + 1 次 `Excute` 同帧到 `Release` | 到 `Release` 需要第 4 次 `Excute`（3 帧在 `Last` 里数） |
| `IsHeld_follows_the_finger_not_the_state` | `IsHeld` 忽略状态、会因过期变假 | `IsHeld == HasFinger`，只看触摸 id |
| `ContinuousInputKeepsBothMovingHoldsThroughOverlap(FrictionLong/FrictionHideLong)` ×4 | 摩擦条不会进 `Last` | 进 `Last` 后必须能被回来的手拉回 `Input` |

**顺序：先改这 6 条（或新增 3 条新契约测试并让它们红），再改实现。** 反过来做，每轮都会因为旧断言变红而只能回退。

### 11.3 先写这 3 条新测试（预期先红）

文件名建议 `Assets/Tests/EditMode/Live/LongNoteGraceTests.cs`，复用 `LongHoldPipelineTests` 的 `Info(float)` / `Input(int,float)` / `Field<T>` / `Stage` 脚手架。

1. `ALostFingerGreysTheBarAndReleasesThreeFramesLater`
   `HeldNote`（State=`Input`，无手指在道内）→ 第 1 次 `Excute` 后断言 `State == Last`（灰）→ 继续 3 次后断言 `State == Release`
2. `AFingerReturningInsideTheGraceTakesTheBarBack`
   同上先到 `Last` → 注入 `Input(finger, 4.5f)` 调 `Stage(logic,"RefreshSustainedHolds")` → 断言 `State == InputBegan || State == Input`
3. `HasFingerFollowsTheTouchIdOnly`
   构造按住态 → `HasFinger` 真且 `IsHeld(0f)` 真；清空 `HoldingTouchId` → 两者俱假（不管 `State`）

### 11.4 再改实现（4 处，一次做完）

`Assets/Game/Runtime/Sekai/Live/LongNote.cs`

| 位置（约） | 改成 |
| --- | --- |
| `:390` `bool covered = IsHoldFresh(...)` | `bool covered = HasFinger;` |
| `:434` `else if (!IsDecoration && !IsHoldFresh(...)) { Release }` | `else if (!IsDecoration) { Last; _framesInLast = 0 }` |
| `:527` `IsHeld(time)` 函数体 | `return HasFinger;` |
| `:1378` `RestoreHeldState()` 条件 | `State == Release \|\| State == Last` |

`Assets/Game/Runtime/Sekai/Live/FrictionLongNote.cs`

| 位置 | 改成 |
| --- | --- |
| `:66` `if (State == Release \|\| State == InputBegan)` | 追加 `\|\| State == Last` |

四处合起来才构成「宽限只有一条（`Last` 3 帧）+ 五条转换」。少任何一处都会出现：进 `Last` 没有出口（摩擦条那 4 条）、或 `Release` 早一帧（`AHoldSurvives…`）。

### 11.5 编辑方法（这轮踩的坑）

- **不要用行号替换**：`LongNote.cs` 有 1500 行、制表符缩进、中文注释，行号会连续偏移；这轮因此三次损坏括号。
- 用**带锚点的整段替换**（锚点取函数签名或唯一注释），改完立刻 `git diff --stat` 看差异行数；一次改动应在个位行，超出就是锚点错了。
- 保存必须 `Set-Content -Encoding UTF8 -NoNewline` + `-join "\`r\`n"`；用 `Get-Content` 数组直接 `-NoNewline` 会把整个文件压成一行。
- 编译校验：`dotnet.exe` + `csc.dll`（Unity 自带 SDK）+ 最新 `Library\Bee\artifacts\**\OpenSekai.Game.rsp`；注意删过文件后 rsp 会过期，需先 `AssetDatabase.Refresh`。

### 11.6 门禁

`LongHoldPipelineTests` → `LiveLogicInputTests` → 全量（基线 506 / 502 / 4）→ 装机验：普通谱、BOF 长条密集谱、变速谱各一首，看长条丢手后是否「先灰、3 帧后消失」，以及摩擦条手指回来能否续上。

## 十二、2026-11-10 进展：中间点已经退出按下通道（收口记录）

### 12.1 这轮落地的三件事

| 提交 | 内容 | 门禁 |
| --- | --- | --- |
| `c42fb7e` | `NoteBase.ClosesSlotAfterPress` 改为 `virtual`，`LongHoldCombo` 覆写为 `false`：中间点不再占用手指这一帧的名额 | 510 / 506 通过 / 0 失败 / 4 跳过 |
| `dc3e107` | `LiveCandidatePipeline.Collect` 跳过 `LongHoldCombo`：中间点只由 `LiveLogic.JudgeDueComboPoints` 结算，不再进候选集 | 同上 |
| （测试） | `NotePressDispositionTests.AnInteriorPointNeverClosesTheSlot` | 同上 |

至此「按下通道只服务会结束的音符、中间点各判各的」在**中间点这一侧**已经成立：

- 中间点不占名额（`ClosesSlotAfterPress == false`）
- 中间点不进候选集（`Collect` 直接 `continue`）
- 中间点每帧由 `JudgeDueComboPoints()` 对每个活跃触摸跑一次 `IsJudgment`，存在即判，重叠各判各

### 12.2 仍然挂在候选集里的东西，以及为什么

`Collect` 现在仍收：普通键、划键、长条头（`LongNote`）、连接键、搓键（`FrictionNote`）、隐藏键、尾键、装饰条。

原因是**没人替代它们**：

- 长条头：它的每帧 `Judgment` 是「按下即转 `InputBegan`」的唯一入口，协调器只做 `RefreshHeldInput`（不结算头部）。
- 搓键 / 隐藏键 / 连接键：只有候选通道每帧调它们的 `Judgment`，没有第二条路了。
- 尾键：由长条的 `Judgment` 与协调器 sweep（`TailBehaviour.TryFinish`）共同处理。

所以 9.6 的第 1 步（`IsPressStepNote` 让长条一律退出按下通道）**不能先做**：做了就是 §9.2 的第 1 堵墙。

### 12.3 下一步的唯一入口：部件 1 的前半段

要让长条离开候选集，必须先让协调器负责长条的每帧判定：在 `LiveHoldCoordinator` 里对「已被覆盖」的长条调用一次 `held.Judgment(ref touch, lane)`（而不是只 `RefreshHeldInput`），使头部结算、内部点、尾键都有归属。

顺序必须是：

1. 协调器每帧对覆盖中的长条调 `Judgment` —— 先与候选通道**并存**，跑全量确认不双判（内部点已 Done 会早退，风险集中在尾键与 `OffsetJudgeTime`）
2. 全绿后，`Collect` 跳过 `LongNote`
3. 再跑全量；这一步才是真正的 9.6 第 1 步
4. 之后再谈搓键/隐藏键/连接键的「自由通道」——它们比长条简单，但要等第 3 步稳定

### 12.4 状态

- `main = dc3e107`，工作区干净
- EditMode `510 / 506 / 0 / 4`
- 部件 3（容差）、部件 2（中间点任意手指 + 长条状态记忆）、宽限期、`Playing → Scrolling` 改名、中间点退出按下通道：已完成
- 部件 1（长条只留生命周期）、部件 4 的剩余部分（搓键/隐藏/连接/尾键走自由通道）：未完成，入口见 12.3

### 12.5 12.3 那个实验的结果（2026-11-10，已回退，不要重复）

试了两步，都没过门禁，代码已 `git checkout` 回 `d7c8cf0`：

| 尝试 | 改动 | 结果 |
| --- | --- | --- |
| 并存 | `TryRefreshFromOwner` 里把 `held.RefreshHeldInput` 换成 `held.Judgment` | 510 / 503 通过 / **3 失败**：`OverlappingTapDoesNotConsumeTheHeldNotesDeferredCandidate`×2、`SustainedHoldOnlyRefreshesWhileTheHolderStaysInLane(4.5,True)` |
| 一并摘除 | 上一步 + `Collect` 跳过 `LongNote` | 510 / **468** 通过 / **38 失败**（`AuthoredCrossingAllowsFollowingOrExchangingFingers` 全族、`LiveLogicCharacterizationTests` 一条等） |

结论：**长条不能靠「换一个调用点」离开按下通道。** `LongNote.Judgment` 不只是「判一下」——它同时做头部结算、`RecordHoldingInput`、`MarkPressedThisFrame`、`OffsetJudgeTime` 清零、内部点、尾键；把它塞进协调器等于把按下通道的语义复制一份，两条路同时存在就双判（3 条红），只留协调器则没被覆盖的长条整帧拿不到判定（38 条红）。

真正的部件 1 要把 `LongNote` 拆成两半：

- 生命周期（`Wait`/`Scrolling`/`InputBegan`/`Input`/`Last`/`Release`/`Done`、灰化、结束）留在条上
- 判定（头部结算、内部点、尾键）下沉到各自的音符对象，由协调器每帧喂 `IsJudgment`/`Judgment`

在此之前，候选集必须继续收长条。

## 十三、部件 1 已转为外部实现者任务（2026-11-10）

任务书：`docs/operations/long-note-head-split.md`（自包含，含代码地图、失败记录、工具链、验收标准）。

本仓库内的判定相关工作在此暂停，接手顺序：

1. 外部实现者按该文 §3.1 做结构拆分（零行为变化），交付后由本侧验收
2. 验收通过后再做 §3.2 的换入口（协调器喂判定 + 候选集跳过长条）
3. 之后才是搓键/隐藏键/连接键的自由通道

验收方（本侧）只需跑 §5.1 的三项硬门禁 + §5.2 的红线测试清单 + §5.3 的真机三张谱。
