# 条尾三类：谁影响谁

> 任务书。2026-11-11 起。用户定义的三类尾与代码现状的核对，以及还剩下的三件事。

## 1. 用户定义（权威）

长条（`LongNote`）与它的尾键，只有三种关系：

| 类 | 尾的种类 | 条 → 尾 | 尾 → 条 |
| --- | --- | --- | --- |
| 1 | 隐藏、friction、friction-flick | 完全不影响 | 完全不影响 |
| 2 | 普通尾（抬手尾） | 不影响 | 尾判定后结束条 |
| 3 | flick 尾 | 只传递「可以判定了」这一条信息 | 只结束条 |

另有装饰条（`SilentTail`）：条自己走完，尾不参与判定。

推论三条，后面每一处改动都要能对上其中一条：

1. 抬手判定只看「抬手动作」本身：抬手落在该键车道内 **且** 落在该键判定时间窗内 → 进候选；多个候选取**最早**的。
2. 条的外观（亮/灰）只由「此刻有没有手指在该条车道内」决定，不由 `NoteState` 决定。
3. 条何时结束，只由尾的判定结果决定；条的状态不能反过来让尾判不到。

## 2. 代码现状

四个尾行为已经存在，分派在 `LongNote.TailBehaviour`：

- `Assets/Game/Runtime/Sekai/Live/LongNote.cs:582` `internal ITailBehaviour TailBehaviour => SkipsTailJudgement ? … : …`
- `Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs` — `AcceptsRelease => true`（类 2）
- `Assets/Game/Runtime/Sekai/Live/Tails/SwipeTail.cs` — `AcceptsSwipe => true`（类 3）
- `Assets/Game/Runtime/Sekai/Live/Tails/SelfJudgedTail.cs` — `JudgesItself => true`（类 1）
- `Assets/Game/Runtime/Sekai/Live/Tails/SilentTail.cs` — 三个都 false（装饰条）

每帧的驱动点只有一处：

- `Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs:148` `CoverHoldsWithAnyFinger(...)`
- 遍历**所有未结束的条**（`:187` 只排除 `State == NoteState.Done`），逐条聚合 `covered` / `releasedInside` / `swipeDelta`，然后：
  - `:273` `held.TailBehaviour.TryFinish(held, new TailFrame(...))`
  - `:284` `held.ExpireTail(frame.time)`

抬手归属（2026-11-11 已改，未提交时验证 `513/509/0/4`）：

- `LiveHoldCoordinator.PickLiftTarget`：候选 = `candidate.ContainsLane(lane)`（条**自己**的车道）+ `candidate.IsTailJudgeableAt(time)`（尾键此刻可判）；多个候选取**尾时间最早**者，同时间才比车道中心距离。已删除「放宽 ±1.5 道」的兜底。

## 3. 逐类核对

### 类 1（独立尾）：合规

`SelfJudgedTail.TryFinish` → `LongNote.DetachStandaloneTail(frame.Now)`（`LongNote.cs:904`）：只解绑，不写任何结果；`ExpireTail` 在 `:804` 对 `JudgesItself` 的条也走 `DetachStandaloneTail`，不写 Miss。因为 `CoverHoldsWithAnyFinger` 遍历的是所有未结束的条，灰条同样会被评估，所以「条变灰 → 独立尾再也没人解绑」这个担心**不成立**。

仍待真机确认（不是代码问题）：friction 尾被判完之后，条是否在同一帧结束（`DetachStandaloneTail` 的 `currentTime` 判据）。

### 类 2（普通尾）：**违规一处**

`Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs:19`

```csharp
if (!frame.ReleasedInside || !frame.IsBeingHeld)
{
    return false;
}
```

`frame.IsBeingHeld` 就是条自己的 `IsHeld(time)`（`LiveHoldCoordinator.cs:262`）。抬手那一帧手指已经离开，`IsHeld` 恒 false → **按住到尾再抬手永远判不到尾**。这正是「条反向影响尾」。

修法：只留 `frame.ReleasedInside`；窗口判断本来就在 `LongNote.JudgeTailAsRelease`（`LongNote.cs:816`）里，提前抬手的保护不丢。

### 类 3（flick 尾）：合规，但输入要确认

`SwipeTail.TryFinish` 使用的是 `frame.ReleasedInside` / `frame.Covered` / `frame.SwipeDelta`，不再读 `NoteState`；`LongNote.TickSwipeTail`（`LongNote.cs:1023`）的守卫只剩 `State == Done || !AcceptsSwipe`。「提前划 → 延迟 3 帧 → 松手/移出车道/到点后 50 ms 定案」的机制已在 `aad9160` 修好。

判定：`Covered` 由「手指是否在该条车道内」算出，属于**手指信息**而非条状态，符合类 3 的定义。无改动。

### 附带：外观（不属于三类，但同一批反馈）

「松手后再按回来还是灰」= 外观由 `NoteState` 决定，而恢复路径 `LongNote.RestoreHeldState()`（`LongNote.cs:1325`）只在「未过判定线」时恢复。

目标语义：`playing → last → playing`（或 `playing → last → release → playing`），只要手指在条上就亮。

改法（**单独一步，不要和类 2 一起做**，见 §5 教训）：

```csharp
public override bool ShowsAsReleased => !HasFinger && (State == NoteState.Release || State == NoteState.Last);
```

`RestoreHeldState()` 相应改为「`HasFinger` 且状态是 `Release`/`Last` → `SetStateUnnotice(NoteState.Input)`」，不再看时间线。

代价：`HeldBodyNeverEntersAnInputPhase`、`IsHeld_follows_the_finger_not_the_state`、`GreatHeadStillKeepsTheHoldAlive`、`ARerefreshKeepsTheBarLitWithoutRepublishing` 这 4 条断言的是「条体状态不得出现 `Input`」——那是**头体解耦之前**的契约（当时设 `Input` 会经 setter 重发判定）。解耦（`6238145`）之后理由已消失，这 4 条要改写成「亮 + 只发布一次」。

## 4. 执行顺序

每步单独提交、单独跑门禁；红就回退，不叠着改。

### 第 1 步：`ReleaseTail` 去掉 `IsBeingHeld`

- 改 `Assets/Game/Runtime/Sekai/Live/Tails/ReleaseTail.cs:19`。
- 新测试（`Assets/Tests/EditMode/Live/`）：按住到尾、在车道内抬手 → 尾键 `Result != NoteResult.None` 且条 `State == Done`；对照组：提前抬手（窗口外）→ 不判。
- 门禁：`LongHoldPipelineTests`、`TailReleaseLaneTests` → 全量。

### 第 2 步：抬手一个动作只结一条，但每条各判各的

当前 `_liftTargets` 是「每根抬手手指 → 至多一条条」。若两根条尾时间重叠且手指同时在两者车道内，第二条永远拿不到 release。按用户定义，候选取「最早」，命中后**其余条保持可判**（下一次抬手或它们的超时自己处理）。

- 只需在 `PickLiftTarget` 选中的条判完之后不再复用同一份 `_liftTargets`，或把「本帧已被某次抬手结束的条」从后续候选里剔除。
- 新测试：两条重叠、同时结尾，一次抬手只结一条；再抬一次结第二条。

### 第 3 步：外观解耦（§3 附带）

- 改 `ShowsAsReleased` + `RestoreHeldState`，改写那 4 条旧契约断言。
- 门禁：`LongNoteGraceTests`、`HeadDecouplingTests`、`LiveLogicInputTests` → 全量。

## 5. 已踩过的坑（不要重复）

| 尝试 | 结果 | 原因 |
| --- | --- | --- |
| 一次同时改 `ReleaseTail` + `RestoreHeldState` + `ShowsAsReleased` | `513/487/22 失败` | 三类问题混在一个 diff 里，22 条红分不清是哪一处；且删掉 `IsBeingHeld` 后「谁在按」这条信息在换手用例里彻底丢失 |
| `RestoreHeldState` 对普通条无条件恢复 `Scrolling` | 26 帧循环：`Scrolling→Last` 每帧重跑，50 ms 宽限永远归零，尾窗关闭吃 `tailMiss`（`live-20261111-045825.log`） | `Scrolling` 的语义是「从没被按过」，不能拿来当「重新按住」 |
| 抬手归属用放宽后的 `JudgeLaneStart/End` | 装饰条车道内抬手判到旁边长条的尾 | 放宽范围 ±1.5 道，且按中心距离挑，不按「手指站在谁的车道上」挑 |
| 用 `HasFinger` 当「上一帧是否覆盖」 | `ALiftInAnotherLaneDoesNotGradeTheTail` 等 3 条红 | `HasFinger` 由任意 `Judgment` 调用置位，不代表覆盖；要单独记 `_wasCovered` |

## 6. 工具链

```powershell
$dotnet='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe'
$csc='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$rsp=Get-ChildItem 'Library\Bee\artifacts' -Recurse -Filter 'OpenSekai.Game.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
& $dotnet $csc "@$($rsp.FullName)" -nologo 2>&1 | Select-String 'error CS'
```

改完必须让 Unity 真重编（`csc` 干净 ≠ Unity 用了新代码）：

1. `unity_command editor focus`
2. `unity_eval`：`AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`
3. 等 40–60 s，确认 `Library\ScriptAssemblies\OpenSekai.Game.dll` 时间戳变新
4. `unity_eval`：`OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<name>.xml")`，再读 xml 的 `testcasecount/passed/failed/skipped`

装机：

```powershell
$adb='C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
unity command build run --target Android --outputPath "Builds/<name>/OpenSekaiAtlas.apk" --confirm --result-only
& $adb -s 3B164A00Y5600000 install -r "<apk>"
& $adb -s 3B164A00Y5600000 shell rm -rf /sdcard/Android/data/io.github.opensekai/files/logs
```

失败取证：`adb pull /sdcard/Android/data/io.github.opensekai/files/logs/live-*.log`，看 `[Tail] liftPick` / `tailFrame` / `release` / `tailMiss` 四类行。

## 7. 红线

- 不许改 `Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs`（头部判定已收口）。
- 不许把内部点（`LongHoldCombo`）放回候选集。
- 不许修改判定窗口常量（`LongEndJudgement`、`JudgeTuning`）。
- 不许在 `ShowsAsReleased` 里读 `MusicScore.CurrentFrameInfo`（视图每帧都要读，静态读会把帧序绑死）。

## 8. 基线

- `main = e2d6c12`（`fix(live): a bar past its line is not sent back to "never taken"`）
- 未提交：`Assets/Game/Runtime/Sekai/Core/Live/LiveHoldCoordinator.cs`（抬手归属 + `liftPick`/`tailFrame` 两条追踪）
- EditMode：`513 / 509 通过 / 0 失败 / 4 跳过`
- 装机包：`Builds/liftownlane-2026-11-11/OpenSekaiAtlas.apk`（79.59 MB，2026-11-11 06:02:54）
- 待验：Test16 尾判是否恢复、装饰条车道抬手是否不再误判

## 9. 验收

真机三谱，逐项打勾（配合 `docs/operations/device-acceptance-checklist.md`）：

- 普通谱：长条按住到尾 → 车道内抬手 → 出判定；提前抬手 → 不判
- 长条密集谱（BOF）：重叠条各判各的；换手不断；松手再接回亮着
- 变速谱：尾判时机不随流速漂移
- Test16：两根长条都能判到尾，装饰条车道抬手不再误判
