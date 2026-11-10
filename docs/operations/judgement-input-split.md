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
