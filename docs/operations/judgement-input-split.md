# 判定与输入拆分（任务书）

> 状态：进行中（2026-11-10）。基线 `main = eab742c`，EditMode `506 / 502 通过 / 0 失败 / 4 跳过`。
> 本文件是这项工作唯一的事实源：改判定顺序、归属、中间点之前先读它。

## 一、要解决的问题

现在的判定把两件事揉在一套调度里：

- **按下类**音符（普通键、普通划键、长条头）本来就该抢「一根手指每帧只判一条」的名额
- **其他所有音符**（长条中间点、结尾、摩擦、隐藏、连接、装饰）也被塞进同一套候选集，于是出现：
  中间点被别的手指或长条体占了名额后这一帧就判不到；两条长条的自动点重叠时只判一个

## 二、目标规则（原始表述，不要改写）

1. 长条只留生命周期：Wait → 出现 → 被按住 / 未被按住 → 松手宽限 → 灰色 → Done。输入阶段（Began/Input）归具体音符：开头、中间点、结尾各自在自己的 State 上记。
2. 任何手指都能判中间点和结尾，不再有归属的概念。
3. 普通头判定上等价于 1.5 车道容差的普通键，其他只有视觉上的区别。
4. 只有普通头 / 普通键 / 普通划键在「一根手指每帧只判一条」的规则里面。其他所有键都不受这个约束。
   完整流程：每次收到一个或多个同时按下的触摸事件，先列出每个按下事件所能判定的、只包括「普通头 / 普通键 / 普通划键」的列表；找到它们列表里重叠的项目，按离中心点的距离分配给唯一的按下事件，从其他按下事件的列表里去除；之后每个按下事件判定它列表里最早的那个键。同时，其他键的判定完全不受这个流程影响。

补充（2026-11-10）：**中间点**每帧独立判断——若当前时间在该点的判定时间窗口内，遍历窗口内所有触摸，只要有一个落在该点自身的横线范围内（车道内）就判定该点，判完跳出。不挑手指、不占名额、重叠时各判各的（判两次是正常行为）。

## 三、当前代码地图

| 关注点 | 位置 |
| --- | --- |
| 帧入口：`Judgment()` → `AllocateInputJudgableEachNotes()` → `OverlapJudgmentSorting()` → `LeaveNotesFastGeneration()` → `PickUpNotBeginInputNotes()` → `RefreshSustainedHolds()` | `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs`（`Judgment` 附近，约 620–700 行） |
| 候选集填充、排序、剪枝、归属预留 | `Assets/Game/Runtime/Sekai/Core/Live/LiveCandidatePipeline.cs`（`Collect` / `Sort` / `DropExtraFastGeneration` / `PickUp` / `IsReservedForAnotherInput`） |
| 长条体续命、持有者刷新 | `Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs`（`AssignContinuingHolds` / `Refresh` / `CoverHoldsWithAnyFinger`） |
| 长条状态机 + 判定混在一起 | `Assets/Game/Runtime/Sekai/Live/LongNote.cs`（`Excute` / `Judgment` / `WasTouched` / `IsHoldFresh` / `PublishesHeadMiss` / `IsOrdinaryHead`） |
| 中间点 | `Assets/Game/Runtime/Sekai/Live/LongHoldCombo.cs`；挂到长条上：`LongNote.AddHoldCombo`（`LongNote.cs:226`） |
| 车道容差 | `Assets/Game/Runtime/Shared/Scoring/JudgeTuning.cs`：普通 1.25、长条头 1.5、划键 1.5 |
| 「判完关槽」概念 | `NoteBase.ClosesSlotAfterPress`（`NoteBase.cs:612`）、`JudgmentSchedule` |

**已知陷阱**：`LongHoldCombo` 的构造函数把 `laneStart = laneEnd = 0` 传进基类（`LongHoldCombo.cs:12`），车道范围需要由长条在 `AddHoldCombo` 里补。若没补，中间点自己的车道永远是 0 号道——先确认这一点再改判定。

## 四、实施顺序（每步都可独立验证）

| 步 | 内容 | 验证 |
| --- | --- | --- |
| 3 | 普通头 = 1.5 车道容差的普通键 | 已完成：`Assets/Tests/EditMode/Live/LaneToleranceTests.cs`（`eab742c`） |
| 2 | 中间点 / 结尾任意触摸可判，无归属、不占名额 | 两条测试：①非持有手指落在车道内也判到中间点；②两条长条的中间点重叠时各判一次 |
| 1 | 长条只留生命周期，Began/Input 判定下放到头/中间点/结尾各自的 State | 长条相关既有测试全绿 + 真机长条手感 |
| 4 | 候选集只收普通头/普通键/普通划键；重叠按中心距离归一；各判列表里最早一条 | `LongHoldPipelineTests`、`TailReleaseLaneTests` 全绿 + 全量 + 真机 |

**第 2 步的算法**（每帧、每个中间点独立）：

```
若 当前时间 在该点的判定时间窗口 内：
    对窗口内每个触摸 t：
        若 t 的车道 落在 该点 [LaneStart - laneOffset, LaneEnd + laneOffset] 内：
            判定该点；跳出
```

插入位置：`LiveLogic.Judgment()` 里 `AllocateInputJudgableEachNotes()` **之前**，遍历 `_judgmentNoteList` 中 `is LongHoldCombo` 的节点。这批点此后不再进候选集，第 4 步的「只收三类」就是它的自然结果。

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

参考实现：`Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs`（`HeldNote` 夹具、`Stage`、`Field` 都在里面）。

## 六、验证门禁

1. 先跑相关类：`LongHoldPipelineTests`、`TailReleaseLaneTests`、`DecorationTailJudgmentTests`
2. 再跑全量 EditMode（当前 506 条）
3. 真机：普通谱一首、长条密集谱一首（如 BOF）、变速谱一首；看长条续命、中间点、尾判、combo 计数
4. 编译校验：
   `dotnet "<Unity>/Editor/Data/DotNetSdk/dotnet.exe" "<Unity>/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@<最新 Library\Bee\artifacts\**\OpenSekai.Game.rsp>" -nologo`
   然后用 `AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)` 让 Unity 真正重建，等 40–60 秒再跑测试

## 七、纪律

- 不动「普通头 / 普通键 / 普通划键」的每指每帧一条，直到第 4 步
- 不动长条体的续命（`LiveHoldCoordinator`），直到第 1 步
- 每次只做一步，未通过门禁就先撤销工作树，不要带着红测试往下做
- 判定相关的改动必须同时用「普通 4/4 谱 + 长条密集谱 + 变速谱」验证（拍号那条教训同理：单谱验证看不出问题）
