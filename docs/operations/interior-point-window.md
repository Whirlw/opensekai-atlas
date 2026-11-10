# 中间点的判定窗口（交给外部实现者的任务书）

状态：待实现 · 基线 `main = 211ce06` · EditMode `516 / 512 通过 / 0 失败 / 4 跳过`

## 1. 现象

真机（`combo` 计数在屏幕上）：**friction 开头的长条，手指一按上去，连击就会一次涨一大截**。普通长条看不出来。

用户给的规格（权威）：

> 中间点的判定范围（时间 + 车道）内存在触摸就直接判定；判定区间内，所归属的长条状态出现过 Playing 或者 Last。
> 两个长条的自动判定点重叠，完全可以判两次，这完全正常。

即：**中间点是「自己窗口内的点」，不是「按住就补判的点」**。

## 2. 已确认的根因

`Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs:114`：

```csharp
for (var i = 1; i < _bar.NoteList.Count - 1; i++)
{
    var note = _bar.NoteList[i];
    if (note != null)
    {
        note.Judgment(ref touch, lane);   // 没有任何时间门禁
    }
}
```

`ConnectionNoteJudgment` 在「条处于 InputBegan / Input / Release」时每帧被调用一次，这段循环把 NoteList 里**所有**中间点无条件判一遍。

- 中间点一旦过了自己的时间就停在 `NoteState.Last`（`ConnectionNote.Excute` / `LongHoldCombo.Excute`）。
- 普通长条：`LiveLogic.JudgeDueComboPoints` 每帧把到期点吃掉，积压不起来，所以看不出问题。
- friction 条：手指晚按，之前过期的中间点全部攒在 `Last`，一次按下整批判掉 → 连击飞涨。

已验证：把该行改成「只判窗口内的点」后，**14 条测试变红**，原因是测试把触摸时间戳写成 `0`、中间点在 `0.125 s`，并要求「按住就必须判」——契约本身固化了「无门禁」。因此必须**同时改契约与测试**，不能只改一方。

## 3. 目标契约（改完之后的语义）

1. 中间点只有在**自己的判定窗口**内、且**车道内有触摸**时才被判。
2. 窗口以**帧时钟**（`MusicScore.CurrentFrameInfo.time`，即 `currentFrameInfo.time`）为准，不以触摸事件自带的时间戳为准（触摸戳可能是按下那一刻，早于点的到达时间）。
3. 窗口之外（早于或晚于）**不判**，也不留待下次补判——过期就是过期。
4. 同一帧里两个条各自的中间点重叠，仍各自判一次（不得因为「同一位置已判过」而跳过）。
5. 长条状态门禁保持现状：只有 `InputBegan / Input / Release` 期间才走这段循环；`LongHoldCombo` 另有 `JudgeDueComboPoints` 的 `_barWasInPlay` 记忆语义，**不要动**。

## 4. 改动清单

### 4.1 生产代码（只此一处）

`Assets/Game/Runtime/Sekai/Live/LongNoteHead.cs` 的 `ConnectionNoteJudgment` 循环内，把无条件调用改成窗口内调用：

```csharp
float now = Math.Max(touch.musicTime, /* 帧时钟 */);
if (note != null && /* 该点自身窗口含 now */)
{
    note.Judgment(ref touch, lane);
}
```

帧时钟来源二选一（实现者自行判断哪个更干净，但必须只有一个来源）：

- 给 `ConnectionNoteJudgment` 增加 `float currentTime` 参数，由两条调用链（`LongNote.RefreshHeldInput`、`FrictionLongNote.Judgment`）传入 `currentFrameInfo.time`；
- 或读 `Sekai.Live.MusicScore.CurrentFrameInfo.time`（注意它是全局静态，测试里可能未初始化，需兜底）。

窗口常量：早 ≤ **125 ms**、晚 ≤ **141.7 ms**（见 `Assets/Game/Runtime/Shared/Live/LongEndJudgement.cs` 与判定表；中间点不区分 Perfect/Great 的分档，只需要「在不在窗口内」）。

**注意**：`NoteBase.JudgeOffsetAt(float time)` 的方向/语义要现场确认再使用（上一轮尝试用 `IsJudgmentTiming(type, JudgeOffsetAt(now))` 判，14 条测试仍然红，说明该组合的语义与直觉不同）。宁可直接写 `now - note.MusicScoreInfo.time` 的区间比较，也不要猜辅助函数的符号。

### 4.2 契约测试（必须一起改）

文件 `Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs`，14 条：

- `SustainedHoldStillJudgesItsInteriorTicks`
- `SustainedRefreshKeepsOffsetAndInteriorParentWithoutRepublishingHead`
- `OverlappingHoldsJudgeBothInteriorComboPoints(Long|FrictionLong|FrictionHideLong, ×, ×)` 12 条

改法：这些用例当前用 `Input(finger, lane)`（`musicTime = 0`）配 `Info(0.125f)` 的中间点，然后 `Stage(logic, "RefreshSustainedHolds")`。改为**把帧时钟推到窗口内**再驱动：

- 用 `logic.currentFrameInfo = Info(0.125f)`（或 `NoteBase` 相应用例）让帧时间落在点的时间上；
- 触点时间戳可以保持 0，但**判定必须由帧时间决定**（这正是第 3.2 条）。
- 断言保持不变：点被判、两条各判一次、结果 `JustPerfect`。

**不许**为了让测试绿而放宽生产代码的窗口。

### 4.3 新增反向测试（防复发，必须有）

新文件 `Assets/Tests/EditMode/Live/InteriorPointWindowTests.cs`：

1. `AnExpiredInteriorPointIsNotJudgedByALatePress` —— 中间点在 `0.125 s`，帧时钟推到 `1.0 s`，手指按下并 `Stage(logic, "RefreshSustainedHolds")`，断言该点 `Result == NoteResult.None` 且 `State != NoteState.Done`（或按过期语义直接 Miss，实现者需在报告里说明选择）。
2. `ATouchStampedEarlyStillJudgesAPointThatHasArrived` —— 触摸戳 0、帧时钟 0.125，点必须被判（防止「用触摸戳当基准」的回归）。

## 5. 禁改红线

- 不动 `Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.JudgeDueComboPoints()` 的 `_barWasInPlay` 记忆语义（那是 `LongHoldCombo` 的另一条路径，与本次无关）。
- 不动 `LongNote` 生命周期状态机（`Wait / Scrolling / InputBegan / Input / Last / Release / Done` 与三帧宽限）。
- 不动划键尾判（`TickSwipeTail` / `_swipeTailDeferred` / `FailTail`）。
- 不动 `Assets/Game/Runtime/OpenSekai/Live/Conversion/` 下的谱面转换（本次不涉及生成侧）。
- 不改 `Assets/Game/Runtime/Sekai/Live/ConnectionNote.cs` 与 `LongHoldCombo.cs` 的 `Judgment` 语义（它们只负责「被判」，不负责「该不该判」）。

## 6. 工具链

编译校验（不需要开 Unity）：

```
C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe ^
  C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll ^
  @<Library\Bee\artifacts 下最新的 OpenSekai.Game.rsp / OpenSekai.Tests.EditMode.rsp> -nologo
```

改完要让 Unity 重编：

1. `unity_eval`（端口 7800，项目 `E:\DeepseekHarness\OpenSekai`）：
   `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` +
   `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`
2. 盯 `Library\ScriptAssemblies\OpenSekai.Tests.EditMode.dll` 的 `LastWriteTime` 变新（RSP 会过期，`Refresh` 必须先跑）
3. `OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/<名字>.xml")`，读 XML 根节点 `test-run` 的 `total / passed / failed / skipped`

基线：`516 / 512 / 0 / 4`。交付要求：**failed = 0**，且新增 2 条测试在内。

## 7. 交付物

1. 代码改动（生产 1 处 + 测试 14 条改基准 + 新增 2 条）。
2. 一份报告 `docs/operations/interior-point-window-report.md`：改了什么、每处为什么、14 条测试各自怎么改的、新增 2 条的断言、最终测试数字、以及**你认为仍有风险的地方**。
3. 截图或数字证明 friction 场景不再整批判定（可以是单测里的组合断言，不需要真机）。

## 8. 验收（由本仓库维护者执行）

- 全量 EditMode `failed = 0`。
- 逐条核对 14 条测试的改动：确认改的是**时钟基准**，不是断言放松。
- 真机：friction 开头的长条按上去连击应逐点 +1，不再整批跳；普通长条、长条换手、划键尾判无回归。
