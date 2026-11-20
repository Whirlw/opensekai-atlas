# LiveLogic 现状依赖图与分层拆分计划

## 1. 职责清单

### 核查口径与基线

本文是代码审计与增量迁移设计，不实施重构。核查日期：2026-11-09；仓库 HEAD：`0616d5735e1ff01553acf799a6d609b0a588bc67`。下列行号对应本次工作区源码；“现状”是读到的实现，“建议”是待实施设计，“可能”表示风险推演，不等同于已经复现的故障。主体确为普通类、2062 行。证据：`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:15`、`Assets/Game/Runtime/Sekai/Core/Live/LiveLogic.cs:2062`。

为使表格可读，正文采用唯一文件名简写，路径按下表展开；`文件名:行号` 均指源文件的实际行号，多个位置不表示执行顺序。

| 简写范围 | 仓库相对路径 |
| --- | --- |
| `LiveLogic.cs`、`SoloLiveController.cs`、`BaseLiveController.cs`、`LiveViewBase.cs`、`LiveViewExt.cs`、`ScoreLogic.cs`、`SkillLogic.cs`、`LiveScore.cs`、`ILiveTouchSource.cs`、`LiveTouchSample.cs`、`InputSystemTouchSource.cs` | `Assets/Game/Runtime/Sekai/Core/Live/` 下同名文件 |
| `NoteBase.cs`、`LongNote.cs`、`MusicScore.cs`、`MusicScoreInfo.cs`、`LiveConfig.cs`、`LiveTouch.cs`、`LiveBundleBuildData.cs`、`NotesViewManager.cs`、`LiveFrontView.cs`、`Background2DView.cs`、各具体音符类 | `Assets/Game/Runtime/Sekai/Live/` 下同名文件 |
| `ITailBehaviour.cs`、`TailFrame.cs`、`TailBehaviours.cs`、`ReleaseTail.cs`、`SwipeTail.cs`、`SelfJudgedTail.cs`、`SilentTail.cs` | `Assets/Game/Runtime/Sekai/Live/Tails/` 下同名文件 |
| `LiveUtility.cs`、`NoteLineView.cs`、`TapEffectView.cs`、`ScoreView.cs`、`JudgmentView.cs` | `Assets/Game/Runtime/Sekai/` 下同名文件 |
| `TailKeyLifetime.cs` | `Assets/Game/Runtime/Sekai/View/TailKeyLifetime.cs` |
| `CandidateOrder.cs`、`CandidateOwnership.cs`、`HoldInputMatching.cs`、`HoldSweep.cs`、`JudgmentSchedule.cs`、`ReferenceFrame.cs`、`LongEndJudgement.cs`、`JudgeListLead.cs`、`FlickGesture.cs`、`NoteSpeedRatio.cs`、`NoteDisplayOffset.cs` | `Assets/Game/Runtime/Shared/Live/` 下同名文件 |
| `JudgeTuning.cs`、`JudgeWindow.cs`、`ScoreGauge.cs` | `Assets/Game/Runtime/Shared/Scoring/` 下同名文件 |
| `RuntimeChartConverter.cs` | `Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs` |
| `LiveSettingData.cs` | `Assets/Game/Runtime/Shared/Settings/LiveSettingData.cs` |
| `LibrarySettingsPanel.cs` | `Assets/Game/Runtime/OpenSekai/App/UI/LibrarySettingsPanel.cs` |
| 本文出现的 `*Tests.cs`、`LiveHarness.cs`、`FakeTouchSource.cs` | 默认位于 `Assets/Tests/EditMode/Live/`；设置测试在 `Assets/Tests/EditMode/Settings/`，图集测试在 `Assets/Tests/EditMode/Graphics/` |

| 任务书前提 | 实际核查结果 |
| --- | --- |
| Unity 6.0 / URP / Android IL2CPP / 0.3.0 | 当前编辑器是 **6000.6.7f1**，URP **17.6.0**，版本号 0.3.0，Android `scriptingBackend: 1`；迁移基线应锁当前工程版本，不能据“6.0”升级或降级。证据：`ProjectSettings/ProjectVersion.txt:1`、`Packages/manifest.json:11`、`ProjectSettings/ProjectSettings.asset:152`、`ProjectSettings/ProjectSettings.asset:694`。 |
| 22 个私有字段 | 按 **LiveLogic 外层显式声明的非 const 私有实例字段**计，共 **33** 个：82–112 行 26 个、117/123/129/135/143 行 5 个、757/760 行 2 个；不含嵌套类的 3 个私有字段、6 个常量和编译器生成字段。证据：`LiveLogic.cs:82`、`LiveLogic.cs:117`、`LiveLogic.cs:143`、`LiveLogic.cs:757`、`LiveLogic.cs:760`。 |
| 唯一持有者、23 处引用 | 运行时代码中外部持有者确为 `SoloLiveController`；`_liveLogic` 标识符出现 **25 次**（含声明，不是 25 个独立调用点）。测试也实例化它，不能把“唯一持有者”扩展为“没有其他调用者”。证据：`SoloLiveController.cs:20`、`SoloLiveController.cs:201`、`LiveHarness.cs:39`、`LongHoldPipelineTests.cs:45`。 |
| 496 条，492 通过 / 4 跳过 | 存量 `pre-release.xml` 记录 2026-11-09 08:46:42Z：496/492/0 failed/4 skipped；4 条来自被 Ignore 的装饰尾手工构造测试。本次仅审计文档，**没有重新运行 Unity 或真机门禁**。证据：`TestResults/pre-release.xml:2`、`TestResults/pre-release.xml:555`、`DecorationTailJudgmentTests.cs:27`。 |
| 已抽出的模块都在 Shared/Live | `ScoreGauge`、`JudgeTuning`、`JudgeWindow` 实际在 `Shared/Scoring`；`ScoreLogic` 在 `Sekai/Core/Live`，仍读 `LiveConfig.Damages`；`WidenLaneRange` 是 `JudgeTuning` 的方法。目录名 Shared 不足以证明依赖已经纯化。证据：`ScoreLogic.cs:225`、`JudgeTuning.cs:76`、`ScoreGauge.cs:13`。 |

### 方法与状态的职责归属

占比以 2062 行为分母，按所列方法及相邻注释的跨度粗估，不是有效代码行数；一个方法兼任多责时允许重复归类，因此各行不能相加为 100%。表内“Unity”区分直接设备/全局 API、仅值运算及经协作者间接调用；不能把 `static` 或“不继承 MonoBehaviour”当作纯计算。证据：`LiveLogic.cs:159`、`LiveLogic.cs:181`、`LiveLogic.cs:1300`。

| 职责 | 成员名（起始行；覆盖主要重载/辅助方法） | 跨度估计 / 占比 | 纯计算？ | Unity API / 副作用边界 |
| --- | --- | --- | --- | --- |
| 输入采集 | `InputTmp` 构造 40/45、`Reset` 58、`SetTouchSource` 98、`RefreshInput` 224、`OnInput` 352、`ShouldProcessTouch` 869、`UpdateLastTouch` 884、`TouchExecution` 889、`CreateLiveTouch` 913、`OnProcessTouch` 940、`ProcessMouseFallback` 957、`UpdateTouchPositionCache` 1018 | 约 247 行 / 12.0% | 否；校验本身可纯化，但当前更新字典与缓冲 | `InputSystem.ResetDevice`、`NativeInput`、`Mouse`、相机投影、`Time.realtimeSinceStartupAsDouble`。证据：`LiveLogic.cs:224`、`LiveLogic.cs:985`。 |
| 时间轴推进 | `OnUpdate` 258、`OnAutoInput` 315、`ResolveInputMusicTime` 389、两套 `Compute*` 404/416、`InitializeUpdate` 436、`UpdateEvent` 441、`UpdateSkill` 465、`UpdateNote` 470、`AdvanceFinishedNotes` 706、显示门 719/733、`UpdateJudgmentNoteArray` 821 | 约 441 行 / 21.4% | 混合；404/416 可纯计算，扫描与推进会改状态 | `Mathf` 值运算，间接进入音符、技能、视图；`AdvanceFinishedNotes` 只有定义，未见调用，不能计作有效主流程。证据：`LiveLogic.cs:404`、`LiveLogic.cs:514`、`LiveLogic.cs:706`。 |
| 候选分配 | `AllocateInputJudgableEachNotes` 1052、`OverlapJudgmentSorting` 1376、跨输入去重 1427、`IsHeld` 1485、快生成裁剪 1490、延迟拾取 1551、`ShouldReserveHold` 1578、`CandidateRank` 1590、归属保留 1604、单/长输入检查 1661/1690、`ConvertLaneBy` 1748、轨道转换 1813、连接候选 1827、首尾解析 1844/1859/1878/1883、同时间 1888、结束相位 1893 | 约 495 行 / 24.0% | 否；规则内核已有纯函数，外围修改列表/查询音符 | `Vector3`/`Mathf`，读取 `LiveConfig.JudgmentPositions`；比较器捕获可变输入。证据：`LiveLogic.cs:1388`、`LiveLogic.cs:1813`。 |
| 判定派发 | `Judgment` 1039、`EvaluateJudgment` 1626、嵌套 `JudgmentPass.Judge/Consumed` 1774/1806；装饰触发 585、装饰判定 618/643 | 约 210 行 / 10.2% | 否；会驱动音符状态机与装饰视觉 | 通知 `LiveViewExt`、调用 `NoteBase.Judgment`、删除候选；装饰路径直接写 `Done`。证据：`LiveLogic.cs:635`、`LiveLogic.cs:1635`、`LiveLogic.cs:1783`。 |
| 长条保持 | `CanContinueHold` 1116、`HasContinuingHold` 1124、`AssignContinuingHoldInputs` 1130、`RefreshSustainedHolds` 1165、`CoverHoldsWithAnyFinger` 1211、`TryRefreshFromOwner` 1316、`HasLiveOwner` 1338、`AdoptHoldFromAnyFinger` 1352 | 约 258 行 / 12.5% | 否；候选匹配、保活、刻度判定、尾判混合 | 直接 `Time.deltaTime`，经 `KeepAlive/RefreshHeldInput/TailBehaviour` 改长条与子音符。证据：`LiveLogic.cs:1161`、`LiveLogic.cs:1293`、`LiveLogic.cs:1302`、`LongNote.cs:1273`。 |
| 命中结算 | `SetSkillLogic` 211、`SetScoreLogic` 217、`Continue` 431、`Result` 293、`ExecuteEvent` 1898、`OnJudgmentNote` 1920、连击/分数/伤害 1935/1941/1948、`OnUpdateResult` 1954、`ScoreLogicExtensions.HasValue` 2057 | 约 84 行 / 4.1% | 否；算分、技能修正、结果事件交织 | 算分主体不直接读设备，但会读配置并发视图通知；扩展方法属于另一类型，不算 LiveLogic 实例成员。证据：`LiveLogic.cs:1943`、`LiveLogic.cs:2055`、`ScoreLogic.cs:225`。 |
| 对象池与生命周期 | 构造 159、`Setup` 181、`ResetScoreState` 245、`BindNoteCallbacks` 1983/1998、空的 `BindEventCallbacks` 2025、`SortNotes/SortEvents` 2040/2047 | 约 120 行 / 5.8% | 否；排序是无原地修改的转换，但会分配 | 初始化设备、读存储、创建视图池、找相机、绑定回调、重置谱面。`MusicScore.ResetNote` 是音符复用重置，`NotesViewManager/NotePool` 是视图池，两者不要混称一个池。证据：`LiveLogic.cs:200`、`LiveLogic.cs:254`、`MusicScore.cs:168`、`NotesViewManager.cs:159`。 |
| 视图通知 | `PlayableTailOf` 748、`TickTailKeyViews` 762、`TickRetiringViews` 800、`OnSpawnNote` 1904、`OnUnSpawnNote` 1915；结算方法兼任 HUD 通知 | 约 87 行 / 4.2%，另与结算重叠 | 否 | `RemoveView/TickRetire` 把视觉生命周期写回音符；`LiveViewExt` 传递可变音符引用。证据：`LiveLogic.cs:794`、`LiveLogic.cs:811`、`LiveLogic.cs:1912`。 |
| 配置读取与显示计算 | `CalcTimeOffset` 1959、`CalcNoteSpeedRatio` 1977、`UpdateSeVolume` 2029；兼有 `Setup` 181、时间模式 389、轨道 1813 | 核心约 33 行 / 1.6%，另有交叉 | 部分；`CalcTimeOffset` 会写 `IsNegativeSpeed`，不是纯函数 | `ApplicationLocalSettings`、`LiveConfig`、构建数据；`UpdateSeVolume` 仅更新缓存，路由音量仍是 TODO，`_seBaseVolume` 未见使用。证据：`LiveLogic.cs:185`、`LiveLogic.cs:1973`、`LiveLogic.cs:2036`。 |
| 调试日志 | 嵌套 `InputLogInfo` 66（构造 76）、`_recordedInputLogger` 102 | 类型约 15 行 / 0.7% | 构造集合会分配；没有实际录入/回放流程 | 当前 LiveLogic 内未见 logger 的读写调用；真实 trace 在 LongNote/视图等协作者，不能据此宣称支持输入回放。证据：`LiveLogic.cs:66`、`LiveLogic.cs:102`、`LongNote.cs:499`。 |

| 状态/公开面 | 当前归属与拆分注意事项 |
| --- | --- |
| 输入缓冲 | `lastTouches`、`touchPositionList`、`_inputTmpArray`、`_activeInputList`、`InputedNormalNoteTouch` 跟输入阶段走；`_usedFingerList` 是派发输出，不要混入采集设备。证据：`LiveLogic.cs:90`、`LiveLogic.cs:104`、`LiveLogic.cs:1644`。 |
| 谱面、扫描、候选 | `_musicScore/_noteArray/_highSpeedNoteArray/_eventArray/_eventStartIndex` 归时间轴/会话；`_judgmentNoteList/_connectionNoteList` 归候选管线；`_holdInputNotes/_holdInputVisited/_holdInputEligible` 是本阶段的匹配工作区，不应成为第二份持久 owner。证据：`LiveLogic.cs:85`、`LiveLogic.cs:103`、`LiveLogic.cs:1133`。 |
| 配置与设备 | `_liveBundleBuildData/_bootData` 在装配层转换成快照；`_calcCamera/_deviceTouchSource/_touchSource` 在输入适配层；音量/显示参数分别归展示层与显式规则参数。证据：`LiveLogic.cs:82`、`LiveLogic.cs:185`、`LiveLogic.cs:206`。 |
| 对外 API | 保留 `scoreLogic/skillLogic` 的 getter/setter、两个 startIndex setter、`currentGameTime` setter、公开字段 `result/currentFrameInfo`、`TapCount`、各只读 Score/完成/全连属性、`Result`、事件及所有 public 方法；不能只保留 Setup/OnUpdate 就声称兼容。公开字段无法拦截写入，迁移期用门面边界导入/导出兼容值；此临时镜像只可单向按阶段同步。证据：`LiveLogic.cs:114`、`LiveLogic.cs:117`、`LiveLogic.cs:129`、`LiveLogic.cs:141`、`LiveLogic.cs:149`、`LiveLogic.cs:293`。 |

## 2. 依赖图

### 当前真实的阶段顺序

```text
BaseLiveController：设备墙钟 / 音频毫秒时钟
  --[BaseLiveController.cs:209,225,48]--> SoloLiveController：转秒、减 filler
  --[SoloLiveController.cs:65,67]--> LiveLogic.OnUpdate
       --[LiveLogic.cs:269,270]--> MusicScore.Update --> static CurrentFrameInfo
       --[LiveLogic.cs:274,275,276,277]--> 清候选 → 事件 → 技能 → 音符 Excute
       --[LiveLogic.cs:278,279]--> 尾键/退场推进
       --[LiveLogic.cs:281,286]--> 失败/完成通知

SoloLiveController（上一步返回后）
  --[SoloLiveController.cs:72,74,78]--> OnAutoInput 或 OnInput
       OnInput --[LiveLogic.cs:358,363]--> ILiveTouchSource 索引读取
       --[LiveLogic.cs:908,909,910]--> 时间映射 → LiveTouch → 复用输入槽
       --[LiveLogic.cs:380,1039]--> Judgment：
          Allocate → Overlap → FastGeneration → PickUp → HoldRefresh → Evaluate
          （精确顺序：[LiveLogic.cs:1042,1043,1044,1045,1046,1047]）
       --[LiveLogic.cs:1048,1049]--> 再次推进尾键/退场

SoloLiveController
  --[SoloLiveController.cs:80]--> LiveViewExt.OnUpdate → LiveFrontView
  --[LiveFrontView.cs:200,201]--> 再减 filler → NotesViewManager.OnUpdate
  --[NotesViewManager.cs:115,118,119]--> 键 Move + 长条/引导线网格
```

`OnUpdate` **不包含真人输入**；把以上所有工作合为一个 Tick，或将输入提前到音符推进前，都属于改变可观测顺序。`result > 2` 后仍推进尾键和退场，但不再更新 `currentGameTime`；完成判定发生在本次 OnInput/OnAutoInput 之前。迁移要逐项保留这些事实，不能为了“统一主循环”顺手改掉。证据：`LiveLogic.cs:260`、`LiveLogic.cs:271`、`LiveLogic.cs:286`、`SoloLiveController.cs:67`、`SoloLiveController.cs:78`。

### 调用、数据读取与回写的边

`→` 表示调用/通知，`⇢` 表示读共享状态，`↔` 是两条有证据的反向边，`[写]` 明确谁修改谁；这不是声明所有边都已造成 bug。

| 边 | 真实数据与方向 | 定位证据 |
| --- | --- | --- |
| 设备 → 输入适配 → LiveLogic | `EnhancedTouch.touchId` 与 `finger.index` 分别进入 sample；相机先转世界坐标，LiveLogic 再转轨道 | `InputSystemTouchSource.cs:19`、`InputSystemTouchSource.cs:23`、`LiveLogic.cs:942`。 |
| LiveLogic → Shared 规则 | 候选排序/归属/匹配/派发复用 `CandidateOrder`、`CandidateOwnership`、`HoldInputMatching`、`JudgmentSchedule`，返回值驱动本地容器修改 | `LiveLogic.cs:1395`、`LiveLogic.cs:1467`、`LiveLogic.cs:1156`、`LiveLogic.cs:1635`。 |
| **LiveLogic ↔ 音符状态机** | 正向 `Excute/Judgment`；反向音符调用先前绑定的生成/回收/结算/技能修正方法，LongNote 还回调 `CalcTimeOffset`。存在执行期间重入 | `LiveLogic.cs:514`、`LiveLogic.cs:1783`、`LiveLogic.cs:2005`、`NoteBase.cs:653`、`LongNote.cs:306`。 |
| **长条 ↔ 子音符状态机** | 长条执行/判定子音符，子音符完成可通知父条 `ForceTerminate`；条也可对尾写 `FinishWith(Miss)` | `LongNote.cs:312`、`LongNote.cs:1297`、`NoteBase.cs:518`、`LongNote.cs:1069`。 |
| **判定/音符 ↔ 视图数据依赖** | 判定发 Spawn/Judgment/Unspawn；视图保存同一 INote 对象，后续读 State/Progress/Result 决定网格/效果。这是“通知 + 反向读取”的双向耦合，**没有确认视图直接调用判定或写判定状态** | `LiveLogic.cs:1912`、`LiveLogic.cs:1932`、`NotesViewManager.cs:163`、`NoteLineView.cs:212`、`NoteLineView.cs:242`、`TapEffectView.cs:292`。 |
| LiveLogic → 音符 `[写，跨职责]` | 装饰判定绕过状态通知写 `JudgeInfo/Done`，显示计算写 `IsNegativeSpeed`，退场更新 `Progress`/`viewReleased` | `LiveLogic.cs:636`、`LiveLogic.cs:639`、`LiveLogic.cs:1973`、`LiveLogic.cs:811`、`NoteBase.cs:629`、`NoteBase.cs:642`。 |
| Hold 管线 → LongNote `[写]` | 匹配写 holder，coverage 写保活时间/状态，owner refresh 再写时间并判内部刻度；尾行为可能写尾结果与父条状态 | `LiveLogic.cs:1161`、`LiveLogic.cs:1293`、`LiveLogic.cs:1330`、`LongNote.cs:1273`、`SwipeTail.cs:22`。 |
| LiveUtility → LongNote `[写]` | 轨道插值工具写 LaneStartF/LaneEndF 和“跨整条区间”的 OffsetJudgeTime；这是规则/几何工具回写模型，不是 NoteLineView 回写 | `LiveUtility.cs:354`、`LiveUtility.cs:362`、`LiveUtility.cs:372`。 |
| 音符回调 → LiveLogic → ScoreLogic/SkillLogic | 结果计数 → combo → 加分 → damage 按回调顺序执行；技能 `OnUpdateResult` 是返回值修正，不能误放入只读视图通知 | `NoteBase.cs:655`、`NoteBase.cs:656`、`NoteBase.cs:657`、`NoteBase.cs:658`、`LiveLogic.cs:1954`。 |
| LiveLogic → HUD 的 ref 接口 | `LiveScore` 是 struct，LiveLogic 先取局部副本，再 ref 传入视图；当前 `ScoreView.UpdateScore` 读取分数，没有看到修改权威 ScoreLogic 的回路。接口允许写局部副本，不等于实际回写计分 | `LiveScore.cs:3`、`LiveLogic.cs:1944`、`LiveViewExt.cs:95`、`ScoreView.cs:124`。 |
| Setup → BootData `[写]` | Setup 不只是读配置：会向 BootData.MusicData 写 MusicScore、重置音符、绑定回调、初始化视图池 | `LiveLogic.cs:191`、`LiveLogic.cs:198`、`LiveLogic.cs:200`、`LiveLogic.cs:254`。 |
| MusicScore → 全局帧状态 `[写]` | Update 写 static CurrentFrameInfo；LiveLogic 复制一份，测试另外保存/恢复全局值，多会话隔离尚未形成 | `MusicScore.cs:9`、`MusicScore.cs:94`、`LiveLogic.cs:270`、`LiveHarness.cs:98`。 |
| 外部控制器 → 背景视图 | 背景接受 OnUpdate/MusicStart/Pause/Resume 等生命周期；不是音符判定主链，保持在展示边界，无须为了拆 LiveLogic 搬入仿真 | `Background2DView.cs:138`、`Background2DView.cs:157`、`Background2DView.cs:167`、`Background2DView.cs:175`。 |

## 3. 多真源清单

“多个位置”不自动等于“同一语义多真源”。下表分别标注已成立的冲突边界、已修复/显式兼容的分支和有待复现的风险；所有建议先封装、保持两种原语义，再在独立规则变更中收敛。

| 语义 / 假设 | N 处位置与验证结论 | 不一致时的表现 / 确认范围 | 收敛位置与迁移约束 |
| --- | --- | --- | --- |
| **H1：时间真源不唯一** | **部分成立，原定位不准确。** ①墙钟秒 `currentGameTime`；②谱面秒 `currentFrameInfo.time`；③输入 sample 秒；④ `Time.deltaTime`；⑤旧/新映射显式开关。前三者是不同坐标系，不能删成一个时钟；判定侧未发现 `Time.frameCount` 使用。证据：`BaseLiveController.cs:209`、`LiveLogic.cs:389`、`LiveLogic.cs:1302`、`InputSystemTouchSource.cs:24`。 | 新映射夹到 [-0.25s,0]，旧映射 delta/60 且不夹紧，属于被测试覆盖的兼容模式；删除旧公式会改变设置行为。视图的 frameCount 用于同帧反馈去重，应保留在展示层。证据：`LiveLogic.cs:404`、`LiveLogic.cs:416`、`JudgeFrameTableTests.cs:64`、`JudgeFrameTableTests.cs:83`、`JudgmentView.cs:79`、`TapEffectView.cs:259`。 | Timeline 接受明确的墙钟/谱面秒与 delta，拥有输入映射策略；保持 legacy 分支。引擎帧去重仍由展示适配处理。证据：`LiveLogic.cs:391`、`ReferenceFrame.cs:15`。 |
| **H1 补充：真正残留的帧时序** | **成立。** ①保持宽限是 `ReferenceFrame.FromFrames(3+MissMesh)` 秒；②早划尾延迟是 `_framesSinceRelease++` 后达到 3，按 `TickSwipeTail` 调用次数，不是秒。没有 Time.frameCount 也仍存在帧制逻辑。证据：`LongNote.cs:1236`、`LongNote.cs:1243`、`LongNote.cs:1021`、`SwipeTail.cs:27`。 | 每次调用与渲染帧一一对应时，3 次在 60/120/165Hz 分别约 50/25/18ms；这是条件推演，不是本次真机测量。测试明确断言第三次调用，不能当机械重构改成 50ms。证据：`FlickEndBehaviourTests.cs:570`、`FlickEndBehaviourTests.cs:1194`、`LongNote.cs:1022`。 | 把“每次输入阶段推进一次”的序号/计数留在明确尾策略状态中；秒化作为后续产品规则变更，需新基线，不属于这 12 步。证据：`SwipeTail.cs:25`、`LongNote.cs:999`。 |
| **H2：每指每帧名额使续命与新增争抢** | **旧问题已缓解，不能按当前完整事实成立。** ①最终派发仍在第一个 `ConsumesSlot` 停；②`RefreshSustainedHolds` 独立于派发并在其前执行；③held 排序靠后；④同一指的长条匹配保留只限制另一长条，不禁止覆盖普通音符。证据：`JudgmentSchedule.cs:73`、`LiveLogic.cs:1046`、`LiveLogic.cs:1072`、`LiveLogic.cs:1597`。 | 新命中仍可能竞争候选槽；保持/内部刻度无需靠赢得槽存活。快生成还有 held 保留分支，注释“held 必须排最前”已落后于 CandidateRank 的规则，不能照注释重写。证据：`LiveLogic.cs:1480`、`LiveLogic.cs:1536`、`LongNote.cs:1277`、`LongHoldPipelineTests.cs:308`、`LongHoldPipelineTests.cs:515`。 | CandidatePipeline 管一次派发的消费规则；HoldCoordinator 管保活/刻度。先原顺序整体迁移，不删除 held 裁剪保护，也不承诺一指可以新增命中任意多条。证据：`LiveLogic.cs:1042`、`LiveLogic.cs:1047`、`JudgmentScheduleTests.cs:95`。 |
| **H3：长条归属靠每帧重新选中，多份同义实现** | **部分成立，但这些不是同义函数。** ①匹配写 owner；②跨输入候选去重；③任意指覆盖保活/尾输入；④owner 刷新刻度；⑤owner 消失时接管；⑥Note 自身 freshness 保留。证据：`LiveLogic.cs:1130`、`LiveLogic.cs:1427`、`LiveLogic.cs:1211`、`LiveLogic.cs:1316`、`LiveLogic.cs:1352`、`LongNote.cs:456`。 | 候选归属、身体是否覆盖、尾是否抬手/划动、刻度由谁驱动是四种职责；合并为“唯一 owner 才可保持”会破坏换指/漏头补接，全部改为任意指命中又会改变头判与重叠条。证据：`LongHoldPipelineTests.cs:463`、`LongHoldPipelineTests.cs:657`、`LiveLogicInputTests.cs:80`。 | HoldCoordinator 统一组织但保留上述阶段；持久 holder 只存 Session 一份，`_holdInputNotes` 仅是匹配工作区；CandidateOwnership 继续负责候选去重。证据：`LongNote.cs:1182`、`LiveLogic.cs:1133`、`LiveLogic.cs:1467`。 |
| **H4：绑定缓存被 reset 清掉，与推导并存** | **部分成立，至少三种不同情况。** ①ParentRole 已由父子引用推导，role 参数不再存储；②TailBehaviour 由 child 类型推导；③`_skipTailJudgement` 仍在转换期设置，Reset 清 false；Guide 另有虚属性推导绕开此 flag。证据：`NoteBase.cs:465`、`NoteBase.cs:489`、`LongNote.cs:565`、`RuntimeChartConverter.cs:574`、`LongNote.cs:1424`、`GuideNote.cs:8`。 | ParentRole/TailBehaviour 不是“两份仍同时工作的缓存”。但普通 `IsDecoration` 条与 Guide 的 reset 后路径不相同，可能改变谁结算尾；**本次未复现所有装饰类型的 reset 前后差异**。证据：`LiveLogic.cs:198`、`LiveLogic.cs:207`、`TailBehaviourTests.cs:58`、`TailBehaviourTests.cs:94`。 | 分清不可变谱面绑定与每局瞬态；角色/尾策略从不可变定义取值，reset 只清瞬态。先把现有 flag 生命周期原样搬迁，另用真实谱面测试界定后再修复，不能悄悄把 false 改回 true。证据：`MusicScore.cs:182`、`LongNote.cs:1407`、`RuntimeChartConverter.cs:576`。 |
| **H4 延伸：时差缓存与时钟推导** | **成立且仍用于判定准入。** ①`JudgeOffsetAt(now)`；②NoteBase.Excute 写缓存；③LiveUtility 为长条写区间时差；④LongNote/FrictionLongNote/FrictionNote 的 IsJudgment 直接读无前缀的 `OffsetJudgeTime`。证据：`NoteBase.cs:197`、`NoteBase.cs:565`、`LiveUtility.cs:372`、`LongNote.cs:1342`、`FrictionLongNote.cs:47`、`FrictionNote.cs:92`。 | 将所有缓存替换成 `now-headTime` 会使条中段准入关闭：当前长条区间时差由 min(afterTail,fromHead) 定义，常为 0；这是有意的阶段契约。尾窗则已经从当前时间推导，不能把两者混为同一个 offset。证据：`LongNote.cs:1340`、`LiveUtility.cs:370`、`LongNote.cs:719`。 | 明确拆为 `HeadOffset(now)`、`TailOffset(now)`、`BarAdmissionOffset(frame)` 与显示 Progress；先保留各公式/采样时机，再把值归于一个 frame snapshot，不能机械只留一个函数。证据：`NoteBase.cs:199`、`LiveUtility.cs:372`、`LongNote.cs:701`。 |
| **H5：视图可见性三处决定** | **多层存在，但原描述部分过时。** ①NoteBase spawn 的 ViewNoteList 门；②LongNote 绑定时 `!IsSkip`；③LiveLogic 的 TailKeyPhase spawn/退场；④NotesViewManager 对 Guide/Long 的分流及子视图清理；⑤NoteLineView 主要读 **LineNoteList**，不是只读 ViewNoteList。证据：`NoteBase.cs:614`、`LongNote.cs:269`、`LiveLogic.cs:1909`、`NotesViewManager.cs:148`、`NotesViewManager.cs:189`、`NoteLineView.cs:242`。 | 几何端点/可绘键/可判定尾本来就是不同集合；合并会让隐形端点消失导致条体缺段，或把不可见刻度画成键。类型禁画尾的旧规则不在绑定处，当前是 IsSkip。证据：`LongNote.cs:15`、`LongNote.cs:260`、`LongNoteViewLifetimeTests.cs:81`、`LongNoteViewLifetimeTests.cs:92`。 | 展示 Bridge 独占键可见性/退场；从仿真只接收值快照与几何端点。保留几何集合与可绘键集合的区别，判定是否 Done 不由可见性反推。证据：`NoteBase.cs:626`、`NoteBase.cs:647`、`LongNoteViewLifetimeTests.cs:201`。 |
| **H6：Setup 与 OnUpdate 边界模糊** | **是职责集中，不是证实二者重复初始化。** Setup 排序、绑回调、建池、reset；OnUpdate 才推进。明确问题是 Setup 多外部副作用，构造即启用设备，以及输入在另一入口、每帧视图推进两处。证据：`LiveLogic.cs:163`、`LiveLogic.cs:181`、`LiveLogic.cs:258`、`LiveLogic.cs:1039`。 | 在同一 LiveLogic 上重复 Setup 时，ResetScoreState 未清 `_judgmentNoteList/_connectionNoteList/last frame`，输入刷新只清输入缓存；下一帧会清候选，但 Setup 后立刻 OnInput 是否安全 **[未能确认]**，正常 controller 是 new 后 Setup。证据：`LiveLogic.cs:245`、`LiveLogic.cs:224`、`LiveLogic.cs:436`、`SoloLiveController.cs:201`。 | 保持现有 new/Setup/SetSkill/SetScore 顺序；Session.Reset 与 Presentation.Reset/Input.Refresh 分离并由门面协调；重复 Setup 的完整语义先补测试后界定。证据：`SoloLiveController.cs:201`、`SoloLiveController.cs:203`、`SoloLiveController.cs:204`。 |
| **新增：触摸身份两种 ID 混用** | ①设备 sample 中 touchId 与 fingerId 是不同来源；②holder 存 fingerId；③owner 匹配用 fingerId；④release 分支却比较 touchId 与 HoldingTouchId。测试 sample 默认 fingerId=touchId，可能掩盖差异。证据：`InputSystemTouchSource.cs:23`、`LongNote.cs:1182`、`LiveLogic.cs:1321`、`LiveLogic.cs:1252`、`LiveTouchSample.cs:20`。 | 持握者 Ended 在轨道/Y 门外时，若两 ID 不等，owner fallback 可能失效；分支还有 lane fallback，所以不能断言所有抬手都失败。**[未能确认]** 真机是否触发，现有 TailReleaseLaneTests 不足以证明所有 ID 组合。证据：`LiveLogic.cs:1252`、`TailReleaseLaneTests.cs:72`、`TailReleaseLaneTests.cs:86`。 | 中性输入中保留 `ContactId` 和 `FingerId` 两字段并分别命名；不在机械迁移中统一值。身份修复另立含 ID 不等的用例与变更。证据：`LiveTouch.cs:10`、`LiveTouch.cs:12`。 |
| **新增：保活时间多写入路径** | ①coverage 用当前谱面秒 KeepAlive；②随后 owner refresh 用 sample 映射秒 RecordHoldingInput；③Judgment 再记录 sample；④首按补到 last frame。证据：`LiveLogic.cs:1293`、`LiveLogic.cs:1330`、`LongNote.cs:1275`、`LongNote.cs:497`、`LongNote.cs:1198`。 | 输入较旧时，后续写入可能把本帧 KeepAlive 的较新值覆盖为较旧值；是否最终误 Release 取决于后续覆盖与宽限，**未在本次复现**。不能因变量同名就直接改成 Max，已有 freshness 行为可能改变。证据：`LiveLogic.cs:418`、`LongNote.cs:1181`、`LongNote.cs:1248`。 | Session 记录事实来源，Coordinator 统一安排写入顺序；兼容期保持值与覆盖次序，后续才讨论分别记录 lastCoverage/lastOwnerSample。证据：`LiveLogic.cs:1177`、`LiveLogic.cs:1192`。 |
| **新增：全局帧副本与两条扫描** | ①MusicScore.static CurrentFrameInfo；②LiveLogic.currentFrameInfo；③普通数组含全部 root，非 1x 子集又进入 highSpeed 数组；④两个 cursor 都可外部写。证据：`MusicScore.cs:9`、`LiveLogic.cs:149`、`LiveLogic.cs:195`、`LiveLogic.cs:196`、`LiveLogic.cs:129`。 | 两会话互相污染的可能性、快慢路径漂移、重置 cursor 不一致；普通扫描会跳过非 1x，故不能因数组重复就说同帧必定双判。AutoInput 扫描两数组但跳过 Done，仍需保留根/子音符副作用。证据：`LiveLogic.cs:496`、`LiveLogic.cs:334`、`LiveHarness.cs:98`。 | Timeline/Session 使用实例帧状态；两条扫描先共用相同实现参数化但保留当前差异/顺序；静态字段仅做旧 API 边界镜像，核心不可读取。证据：`LiveLogic.cs:505`、`LiveLogic.cs:557`。 |
| **新增：尾窗参数两套表达** | ①LiveConfig 的帧表；②LongEndJudgement 的毫秒常量/无 Bad 带；③窗口开放用 Evaluate，其他尾/头方法仍走 LiveConfig。不能直接宣称两套完全等价。证据：`LiveConfig.cs:190`、`LiveConfig.cs:349`、`LongEndJudgement.cs:16`、`LongEndJudgement.cs:29`、`LongNote.cs:719`。 | 58.3ms 与 3.5/60 秒边界存在舍入差别，Bad 带也不相同；统一成一张表可能改变边界等级。证据：`LongEndJudgement.cs:16`、`LiveConfig.cs:190`、`LongHoldPipelineTests.cs:409`。 | SessionConfig 分别保留“准入/结果/尾释放表”并命名；重构仅统一注入入口，不合并常量值。证据：`LiveConfig.cs:369`、`LongEndJudgement.cs:29`。 |
| **新增：防回归测试的覆盖错觉** | ①SingleTimeSourceTests 搜索 `.OffsetJudgeTime`，漏掉裸属性；②只搜 `Time.frameCount`，漏掉调用计数；③PoolHygiene 只扫值类型字段，跳过只读字段与 identity，不能覆盖回调、引用集合、定义 flag 的分类正确性。证据：`SingleTimeSourceTests.cs:60`、`SingleTimeSourceTests.cs:83`、`PoolHygieneTests.cs:84`、`PoolHygieneTests.cs:89`。 | 492 通过不足以证明“全部判定无缓存/帧制”或“所有生命周期干净”。这是测试能力边界，不是要求本次新增一组必红的禁词测试。证据：`LongNote.cs:1342`、`LongNote.cs:1021`、`TestResults/pre-release.xml:2`。 | 基线阶段为允许的缓存/调用计数建立显式契约；未来语义调整用真实谱面输入覆盖，静态检查只管层间依赖。证据：`docs/operations/testing.md:71`、`LiveHarness.cs:76`。 |

## 4. 目标分层

### 六个职责类型与依赖边界

以下是**六个新增职责类型**；小型参数/结果 struct、枚举、快照 DTO 属于数据契约，不另增加管理器。保留 `LiveLogic` 为兼容门面；保留具体音符类/ScoreLogic/SkillLogic 的旧 API 适配壳。不能把现有 NoteBase 原封不动搬进 Shared 就声称纯化：它仍调用 LiveConfig，具体 Flick 类还直接读 Time。证据：`NoteBase.cs:579`、`LongNote.cs:1237`、`FlickNote.cs:259`、`FrictionFlickNote.cs:201`。

纯仿真层选择的线：仅允许 `using Vector2 = UnityEngine.Vector2;` 和 `using Mathf = UnityEngine.Mathf;` 之类**精确别名的值运算**；不用宽泛 `using UnityEngine;`，不用 Camera/Time/InputSystem/Resources/Object/MonoBehaviour/ScriptableObject/存储/单例。`Mathf` 是静态数学工具，不称为值类型。中性输入使用自有相位枚举和 lane，不把 InputSystem.TouchPhase 或 Vector3 世界坐标传进纯层；现有 LiveTouch 只留在兼容边界。证据（当前需要隔离的依赖）：`LiveTouch.cs:16`、`LiveTouch.cs:18`、`LiveLogic.cs:1814`、`LiveLogic.cs:1302`。

| 类型 / 命名空间 | 一句话职责 | 依赖谁 | 不依赖谁 / 谁不得依赖它 | 当前抽取依据 |
| --- | --- | --- | --- | --- |
| `LiveInputCollector` / `Sekai.Core.Live.Input` | 从 ILiveTouchSource 读取、去重、相位转换、缓存位移、投影并产生 lane 输入 | 现有 ILiveTouchSource、Camera/设备适配、Timeline 的输入时间映射、注入的轨道配置 | 纯层不得依赖 Collector；Collector 不持有视图、ScoreLogic、可写音符图 | `LiveLogic.cs:352`、`LiveLogic.cs:889`、`LiveLogic.cs:913`、`LiveLogic.cs:1813`。 |
| `LiveTimeline` / `OpenSekai.Shared.Live.Simulation` | 形成实例帧上下文、映射输入时间、保持事件与双扫描顺序 | 注入的时间轴值数据、Session、规则快照 | 不依赖 Collector、Bridge、旧 MusicScore.static 或 Unity 时钟 | `LiveLogic.cs:258`、`LiveLogic.cs:389`、`LiveLogic.cs:441`、`LiveLogic.cs:470`。 |
| `LiveNoteSession` / `OpenSekai.Shared.Live.Simulation` | 持有唯一运行态、谱面定义、每局结算与音符状态转移 | 不可变 Note/Event 定义、配置值、移出的现有音符规则、现有 Shared 数学/计分规则 | 不依赖 Timeline/Collector/Bridge，不持有 NoteBase、LiveViewBase、构建 ScriptableObject 或 BootData 引用 | `NoteBase.cs:221`、`LongNote.cs:91`、`ScoreLogic.cs:160`、`MusicScore.cs:168`。 |
| `LiveCandidatePipeline` / `OpenSekai.Shared.Live.Simulation` | 候选注册、分配、排序、快生成裁剪、连接拾取及消费派发 | Session、HoldCoordinator 的匹配结果、CandidateOrder/Ownership/JudgmentSchedule | 不读设备/视图；HoldCoordinator 不反向调用 Pipeline，Bridge 不得依赖它 | `LiveLogic.cs:821`、`LiveLogic.cs:1052`、`LiveLogic.cs:1376`、`LiveLogic.cs:1626`。 |
| `LiveHoldCoordinator` / `OpenSekai.Shared.Live.Simulation` | 组织匹配、coverage、owner refresh、接管与尾行为，保留分阶段语义 | Session 的窄状态操作、HoldInputMatching/HoldSweep、现有尾策略规则 | 不依赖 CandidatePipeline、Collector、Bridge；不再持久保存第二份 owner/结果 | `LiveLogic.cs:1130`、`LiveLogic.cs:1165`、`LiveLogic.cs:1211`、`ITailBehaviour.cs:15`。 |
| `LivePresentationBridge` / `Sekai.Core.Live.Presentation` | 消费值快照，管理键/条/HUD/音效/皮肤资源和退场视图 | 注入的展示资源/配置、现有 LiveViewBase/NotesViewManager/效果适配、输出 DTO | 无 Session/Timeline/Pipeline/Collector 引用；仿真层也不知道 Bridge 类型 | `LiveLogic.cs:762`、`LiveLogic.cs:1904`、`LiveFrontView.cs:244`、`NotesViewManager.cs:209`。 |

目标数据流如下；所有权与通知以设计约束为准，箭头后的源码只说明现有逻辑迁移来源，并非声称新类型已经存在。

```text
SoloLiveController → LiveLogic（保留公开门面与装配）
  → Collector：设备 samples → 中性 SimTouch（显式保留两种 ID）
  → Timeline：FrameContext、事件/音符推进
  → CandidatePipeline：注册/匹配/排序/裁剪/拾取
       → HoldCoordinator：按原阶段保活/尾处理
       → Session：状态/结算是唯一权威
  ← 纯层追加 LiveOutput 值记录（不含任何可写模型引用）
  → PresentationBridge：顺序消费输出、绘制/声音/池/退场

仿真层没有 Bridge 的回调；Bridge 没有 Session 的查询口/写入口。
旧音符对象如仍被旧视图使用，必须是 Bridge 拥有的展示副本或只读快照适配。
来源：SoloLiveController.cs:67,78,80；LiveLogic.cs:1039,2005；NoteBase.cs:627。
```

### 公开签名与数据契约（设计草案，不是现有实现）

下列签名中的 DTO 需要随首个使用者一起定义：`SessionConfig` 是每局深拷贝规则（legacy、各窗口、DPI 导出的划动阈值、宽限、显示流速等）；`PresentationConfig` 包含皮肤/效果/音效资源与音量；`InputGeometry` 是轨道坐标与偏移快照。`FrameContext` 显式包含谱面秒、墙钟秒、设备 delta 秒以及阶段调用序号；delta 初期照传现有 `Time.deltaTime`，不可改成音乐时间差。依据：`LiveLogic.cs:185`、`LiveLogic.cs:391`、`LiveLogic.cs:1302`、`LiveLogic.cs:1814`。

`SimTouch` 保留 ContactId、FingerId、InputPhase、MusicSeconds、Lane、必要的二维位移与候选轨道变体；`NoteHandle` 是会话内索引加代际号，不假定谱面 Id 全局唯一；`NoteSnapshot` 不含 NoteBase/父子引用/List。`LiveOutput` 是带阶段顺序、Handle 和值载荷的事件（生成、消失、判定、连击、分数、生命、装饰效果、帧姿态、结束）；`JudgmentOutcome` 保留“无完成/消费槽”的区别。输出缓冲由门面预分配，DTO 不捕获闭包、不暴露底层可写 Session 数组。依据：`LiveTouchSample.cs:8`、`LiveTouchSample.cs:9`、`LiveLogic.cs:1768`、`LiveLogic.cs:1803`、`NoteBase.cs:604`。

```csharp
// Sekai.Core.Live.Input；可使用 Unity，相位/投影在这里截断。
// 当前 ILiveTouchSource 为 internal，故新类型与签名先保持 internal，避免可访问性冲突。
internal sealed class LiveInputCollector
{
    public LiveInputCollector(ILiveTouchSource source, Camera camera, InputGeometry geometry);
    public void SetSource(ILiveTouchSource source);
    public void Refresh();
    public int Collect(in FrameContext frame, LiveTimeline timeline, SimTouch[] destination);
}
```

保留鼠标 fallback 的 Editor 条件编译、128 槽上限、同时间相位处理和 source.Refresh 时设备 reset 行为；这些都是适配行为，不移入纯层。来源：`LiveLogic.cs:17`、`LiveLogic.cs:224`、`LiveLogic.cs:894`、`LiveLogic.cs:943`、`LiveLogic.cs:956`。

```csharp
// OpenSekai.Shared.Live.Simulation
public sealed class LiveTimeline
{
    public LiveTimeline(MusicScoreInfo[] chartFrames, SessionConfig config);
    public void Reset();
    public FrameContext BeginFrame(float musicSeconds, double gameSeconds, float deltaSeconds);
    public float ResolveInputMusicTime(double sampleSeconds, in FrameContext frame);
    public static float ComputeLegacyInputMusicTime(double music, double game, double sample);
    public static float ComputeInputMusicTime(double music, double game, double sample);
    public void Advance(in FrameContext frame, LiveNoteSession session, List<LiveOutput> output);
    public void AutoInput(in FrameContext frame, LiveNoteSession session, List<LiveOutput> output);
}
```

Timeline 的 Advance 与 AutoInput 保留分离；两条扫描不能先去重再执行，普通/高速 offset 的差异也须保存。`MusicScoreInfo` 本身只有值字段与字符串，可作为只读帧数据使用；构造时复制数组而非共享可变数组。来源：`LiveLogic.cs:315`、`LiveLogic.cs:505`、`LiveLogic.cs:557`、`MusicScoreInfo.cs:3`。

签名复用的 `MusicScoreInfo`、`LiveScore`、`NoteResult` 等中性契约应在移入纯程序集时移动其定义文件/程序集归属，保留原命名空间与公开类型名；“命名空间里有 Core”不等于必须反向引用整个 Game 程序集。若某契约含引擎对象，则在适配侧映射成中性 DTO，禁止以引用原程序集绕过层次检查。`NoteDefinition/EventDefinition` 只包含转换后的数据，转换器和旧对象图留在装配侧。来源：`MusicScoreInfo.cs:3`、`LiveScore.cs:3`、`RuntimeChartConverter.cs:564`。

```csharp
public sealed class LiveNoteSession
{
    public LiveNoteSession(NoteDefinition[] notes, EventDefinition[] events, SessionConfig config);
    public void Reset();
    public NoteSnapshot Read(NoteHandle note);
    public LiveScore ReadScore();
    public bool CanJudge(NoteHandle note, in FrameContext frame, in SimTouch touch);
    public void AdvanceNote(NoteHandle note, in FrameContext frame, float displayLead,
                            List<LiveOutput> output);
    public JudgmentOutcome Judge(NoteHandle note, in FrameContext frame, in SimTouch touch,
                                 List<LiveOutput> output);
    public void AssignOwner(NoteHandle hold, int fingerId);
    public void RefreshHold(NoteHandle hold, in FrameContext frame, in SimTouch touch,
                            List<LiveOutput> output);
    public void ExecuteEvent(int eventIndex, in FrameContext frame, List<LiveOutput> output);
}
```

Session 的运行态只在内部可写，操作方法供 Timeline/Pipeline/Coordinator 调用，展示层不能拿到 Session 实例。保持技能结果修正在判定内部、结算顺序不变；旧 ScoreLogic/SkillLogic 的可替换 API 由门面适配到内部规则，不能把未知旧子类作为纯层依赖传入。当前 SkillLogic 基类为空实现/倍率 1，仍需保留扩展点的可观测调用顺序。来源：`LiveLogic.cs:211`、`LiveLogic.cs:1954`、`NoteBase.cs:653`、`SkillLogic.cs:8`、`SkillLogic.cs:25`。

```csharp
public sealed class LiveCandidatePipeline
{
    public LiveCandidatePipeline(LiveNoteSession session, SessionConfig config,
                                 LiveHoldCoordinator holds);
    public void Reset();
    public void Register(in FrameContext frame, List<LiveOutput> output);
    public void Process(in FrameContext frame, SimTouch[] inputs, int count,
                        List<LiveOutput> output);
}

public sealed class LiveHoldCoordinator
{
    public LiveHoldCoordinator(LiveNoteSession session, SessionConfig config);
    public void Reset();
    public void AssignContinuingInputs(in FrameContext frame, SimTouch[] inputs, int count);
    public bool IsReserved(NoteHandle note, in SimTouch touch);
    public void RefreshSustained(in FrameContext frame, SimTouch[] inputs, int count,
                                 List<LiveOutput> output);
}
```

`Process` 内精确保留 Allocate → Overlap → FastGeneration → PickUp → RefreshSustained → Evaluate 顺序；AssignContinuingInputs 仍在 Allocate 开头，不能先 Refresh 再分配。Coordinator 通过 Session 操作保活/尾规则，不创建第二份状态机，也不重新发明已有 Release/Swipe/SelfJudged/Silent 策略。来源：`LiveLogic.cs:1042`、`LiveLogic.cs:1054`、`TailBehaviours.cs:11`、`LongNote.cs:565`。

```csharp
// Sekai.Core.Live.Presentation；这里允许 Unity 资源/视图依赖。
public sealed class LivePresentationBridge : IDisposable
{
    public LivePresentationBridge(LiveViewBase[] views, PresentationConfig config);
    public void Reset();
    public void Consume(LiveOutput[] output, int count);
    public void AdvanceVisuals(float musicSeconds);
    public void ApplyPresentationConfig(PresentationConfig config);
    public void Dispose();
}
```

Bridge 只能消费完成的值记录；旧视图的 `UpdateScore(ref LiveScore,...)` 在适配边界传局部副本，`SpawnNote(NoteBase)` 的展示对象不再是 Session 权威对象。`ApplyPresentationConfig` 只是明确的目标接口，**不是声称现在支持曲中热换皮肤**；装配层可在切歌/重建视图时传新配置。来源：`LiveViewBase.cs:38`、`LiveViewBase.cs:70`、`LiveLogic.cs:1944`、`NotesViewManager.cs:38`。

### 配置、事件与兼容性的落地点

| 边界 | 必须做到的事 | 依据 |
| --- | --- | --- |
| 构造注入 | 门面在 Setup 的现有读设置时机形成规则、轨道、展示三份快照；Scene/MonoBehaviour 可以保留原构造方式，由装配入口做转换。皮肤/SE/效果只进 Bridge，流速与判定表作为值进 Timeline/Session，纯层不读 LiveConfig/存储/单例 | `LiveLogic.cs:185`、`LiveLogic.cs:186`、`LiveConfig.cs:257`、`BaseLiveController.cs:54`。 |
| 设置变化 | 不在每帧重新 Load。从外部收到设置更新时由门面显式换配置；迁移先保持原来的生效时点，不擅自新增曲中应用。旧可变 LiveConfig 的外部变更若要兼容，只能在外边界检测/传值，内部不主动查 | `LiveConfig.cs:268`、`NotesViewManager.cs:36`、`LibrarySettingsPanel.cs:155`。 |
| 通知顺序 | 先把同步通知映射成同位置的值事件，并在原有阶段出口立即排空；不能直接改成“整帧末统一通知”。尤其长条头部回调发生在 state 更新前，要保存当时的快照；技能结果修正仍同步发生在内部 | `LongNote.cs:99`、`LongNote.cs:102`、`LongNote.cs:106`、`LiveLogic.cs:1954`。 |
| 输入前/后展示 | 同一帧 OnUpdate 和 Judgment 都会推进尾键；初期保留两个 AdvanceVisuals 调用。之后可以使回收幂等，但删除其中之一必须有逐阶段等价证据 | `LiveLogic.cs:278`、`LiveLogic.cs:1048`、`NoteBase.cs:629`。 |
| 旧公开写入口 | public 字段继续作为兼容表面，在每个旧入口导入一次、出口导出一次；旧属性/音符/计分 facade 直接转发唯一运行态。自定义 ScoreLogic/SkillLogic 子类经门面兼容路径同步，不进入纯层；默认纯路径与兼容路径每局只选其一，不能并行双结算 | `LiveLogic.cs:117`、`LiveLogic.cs:141`、`LiveLogic.cs:149`、`LiveLogic.cs:211`、`ScoreLogic.cs:69`。 |
| 何时才算纯化 | 第 2–8 步仅是抽取过渡，仍可能通过旧音符读 LiveConfig；第 9–11 步切断传递依赖、可写引用与旧类型回调后才验收目标层。不得让纯程序集反向引用 Core/Game 来“暂时兼容” | `NoteBase.cs:579`、`LongNote.cs:306`、`ScoreLogic.cs:225`。 |

## 5. 迁移步骤（12 步）

本次只提交本文；下表是未来执行顺序，不是已经完成的修改。每一行是一个可独立提交/回滚的工作包，按编号顺序落地；表内“直接前置”列出必须已存在的成果，其传递依赖也必须完成，均只引用更早步骤。“独立回滚”指撤销本步的路由与实现即可恢复上一步可运行状态。若已有后续步骤依赖，应逆序撤销，不能要求删除一个被后续类型引用的提交仍能编译。当前公开 API/控制器调用顺序按第 1、2 节固定。依据：`SoloLiveController.cs:201`、`SoloLiveController.cs:67`、`SoloLiveController.cs:78`。

统一门禁 **G**：本步相关 EditMode 类先跑，再全量跑；以迁移时重新取得的完整 XML 基线为准，当前存量是 496/492/4，新增特征测试后总数可增加，失败为 0，原 4 条 Ignore 不得被当成通过。测试基线依据：`TestResults/pre-release.xml:2`。随后在 Android IL2CPP 包跑固定自动谱面，要求结算 **1,000,000 / S / 总音符 221** 并保存证据；这组验收值在本计划中明确作为 **项目约定（非现有文档条目）**，不以 `android-build.md` 的行号为其依据。自动门禁不覆盖真人换指/抬手/方向手感，各步再做所列真人场景。未运行门禁则标“未验收”，不可写通过；自动演奏装配与真人逻辑验证的分工依据：`docs/operations/testing.md:83`。

本节已将原第 9 步的中性契约提前为新第 5 步：原第 5–8 步依次变为第 6–9 步，第 10–12 步编号不变。新第 5 步只修改当时已存在的 Collector、Timeline、LiveLogic 和音符/计分适配，不依赖尚未抽出的 Session、Bridge、HoldCoordinator、CandidatePipeline；这些后续类型从创建时就消费中性契约，避免先按旧类型迁移再返工。该顺序是迁移设计，旧设备输入/音符接口的待隔离位置见：`LiveTouch.cs:18`、`FlickNote.cs:259`、`LiveLogic.cs:1302`。

| 步 / 风险 | 动哪些文件（新文件为建议路径） | 机械变换与行为保持边界 | 验证（每步均含 G） | 回退方式 |
| --- | --- | --- | --- | --- |
| **1 / 低** | `Assets/Tests/EditMode/Live/LiveHarness.cs`、现有 Input/Pool/Tail 测试；新增同目录特征测试 | **直接前置：无。Characterization Tests**：固定 OnUpdate→Input→View 的分段快照/通知序列；为 legacy、三次 Tick 延迟、frame admission offset、非相等 ID 和 reset flag 建立现状记录。未确认的疑点只记录或待补，不用“理想行为”制造红测试 | `LiveLogicInputTests`、`SingleTimeSourceTests`、`FlickEndBehaviourTests`、`PoolHygieneTests`；真人 60/120/165Hz 可用档位各记录一次早划/抬手；未支持档位标未测。证据：`LiveHarness.cs:76`、`FlickEndBehaviourTests.cs:570`、`SingleTimeSourceTests.cs:60` | 仅撤销新增特征测试/脚手架调整；不动生产路由 |
| **2 / 低** | `LiveLogic.cs`、`BaseLiveController.cs`；新增 `Shared/Live/Simulation` 下配置 DTO、`Core/Live` 下快照转换 | **直接前置：1。Introduce Parameter Object / Extract Function**：在现有 Setup/配置加载时点生成快照，旧入口签名保持；先让原方法读取等值参数，不改缓存时机、表值、DPI、旧设置生效时点。新规则参数通过内部构造注入，外部仍用旧构造 | JudgeTuning/Window/FrameTable、NoteDisplayOffset/SpeedRatio、Settings；真人切皮肤/SE 后重新开曲。证据：`LiveLogic.cs:185`、`JudgeTuningTests.cs:37`、`JudgeFrameTableTests.cs:94`、`LiveConfig.cs:263` | 撤销参数转发和 DTO 使用；恢复原读取位置；无资产数据迁移 |
| **3 / 中** | `LiveLogic.cs`；新增 `Sekai/Core/Live/Input/LiveInputCollector.cs`；保留现有 source/sample；对应输入测试 | **直接前置：2。Extract Class / Move Method**：移动输入缓冲、去重、位移缓存、Editor fallback；LiveLogic.SetTouchSource/RefreshInput/OnInput 转发。暂时继续输出旧 LiveTouch/InputTmp，再在第 5 步转中性 DTO；保留容量、TapCount、Ended/Canceled 分支差异 | Input/TouchLane/JudgeFrameTable 与 FakeTouchSource；真人暂停/恢复、连续重开、鼠标 fallback、ID 不等的诊断采样。证据：`LiveLogic.cs:224`、`LiveLogic.cs:869`、`LiveLogic.cs:943`、`LiveLogicInputTests.cs:369` | 撤销 Collector 转发，输入逻辑回到 LiveLogic；不改设备/source API |
| **4 / 中** | `LiveLogic.cs`、`MusicScore.cs`；新增 `Shared/Live/Simulation/LiveTimeline.cs`（过渡实现先留 Core 侧，纯化后移入 Shared） | **直接前置：3。Extract Class / Move Method**：搬时间映射/事件游标/双扫描，用实例帧替代内部静态读取；旧 MusicScore.CurrentFrameInfo 仍边界同步。Compute* 的 public 静态方法留壳；保留普通/高速分支差异、OnAutoInput 分离和 result>2 路径 | JudgeFrameTable/ReferenceFrame/JudgeListLead、Input、DecorationTailFromChart；真人快慢/负流速、最后一键退场。证据：`LiveLogic.cs:260`、`LiveLogic.cs:404`、`LiveLogic.cs:557`、`MusicScore.cs:94` | 撤销门面 Timeline 路由，恢复静态帧读取；保留原测试报告比较 |
| **5 / 中** | 第 3–4 步已存在的 Collector/Timeline、`LiveLogic.cs`；`LiveTouch.cs`、具体音符类、`ScoreLogic.cs` 的内部适配；新增中性输入/帧/输出契约 | **直接前置：4（含第 2 步配置快照与第 3 步输入适配）。Introduce Parameter Object / Replace Global Query with Parameter**：引擎 Time/DPI/TouchPhase、窗口/伤害/显示配置从外边界显式传入；定义 SimTouch、FrameContext、LiveOutput 及必要载荷，内层入口使用中性类型，旧 LiveTouch 仅在兼容边界转换。对尚未抽类的保持/候选逻辑，先在 LiveLogic 内改参数；不提前引用第 6–9 步类型。先保留缓存准入值/秒值/调用计数三类事实，不把它们数学上合并；**旧 public 签名始终可用，无持久化变更** | 时间/窗口/FlickGesture/Input/Score 全部相关类；增加“相同输入+相同 frame 得到同样输出”验证，真人不同刷新率/暂停。证据：`FlickNote.cs:259`、`LiveLogic.cs:1302`、`ScoreLogic.cs:225`、`LiveTouch.cs:18` | 撤销中性参数转发与契约接线，回到第 4 步内部 API；旧 public 签名始终可用，无持久化变更 |
| **6 / 中** | `LiveLogic.cs`、`MusicScore.cs`、`NoteBase.cs`、`LongNote.cs`；新增 `Core/Live` 过渡 `LiveNoteSession.cs`，最终移 Shared | **直接前置：5。Extract Class / Encapsulate Collection**：使用第 5 步中性契约集中音符图、cursor、reset 与绑定遍历，仍经兼容适配调用旧音符行为；列清定义态/局态/展示态，不改 `_skipTailJudgement` reset、不改重复 root reset 次数；把回调解绑责任明确交给会话结束 | PoolHygiene/LongHoldPipeline.Reset/TailBehaviour/LongNoteViewLifetime；真人重试两次、退出再入、暂停恢复。证据：`MusicScore.cs:182`、`MusicScore.cs:190`、`LongNote.cs:1407`、`LiveLogic.cs:1990` | 撤销 Session 路由与解绑接线，恢复第 5 步 reset/bind 路径；保留中性契约，不得同时运行两套 reset |
| **7 / 中** | `LiveLogic.cs`、`LiveFrontView.cs`、`NotesViewManager.cs`；新增 `Core/Live/Presentation/LivePresentationBridge.cs`，复用第 5 步输出 DTO | **直接前置：5、6。Extract Class / Introduce Notification**：以已定义的中性输出搬原 Spawn/Unspawn/尾键/退场/通知，并在原阶段消费。Bridge 暂可读旧音符的展示适配，但不可新增判定写口；记明这一过渡尚未满足终态纯化，不再先设计一套旧类型的通知接口 | LongNoteViewLifetime/TailKeyLifetime/DecorationTailFromChart、ScoreLogic；真人隐形尾、Guide/摩擦尾、Miss 无 SE、最后一个未判尾滑出，查回收只发生一次。证据：`LiveLogic.cs:762`、`NoteBase.cs:627`、`TapEffectView.cs:296`、`LongNoteViewLifetimeTests.cs:226` | 撤销通知转换，恢复第 6 步 LiveLogic 直接通知；保留中性契约和 Session，删除新增展示映射但不触碰 prefab 字段 |
| **8 / 高** | `LiveLogic.cs`、`LongNote.cs`、`Live/Tails/*`；新增过渡 `Core/Live/LiveHoldCoordinator.cs`，最终移 Shared | **直接前置：5、6、7。Extract Class / Move Method**：使用中性输入/帧/输出契约将匹配/coverage/owner refresh/接管整体搬移，沿用第 7 步通知接线；原有策略经兼容边界转发，不让新 Coordinator 接口重新暴露 LiveTouch。先不做 Replace Conditional，因为尾多态已存在。严格保留 KeepAlive→owner refresh、Tick→NotifyRelease 次序、两种 ID 与三次调用计数 | HoldInputMatching/LongHoldOwnership/LongHoldPipeline、FlickEndBehaviour/TailBehaviourContract/TailReleaseLane、真实 Input 交叉测试；真人双指宽窄交叉、漏头补接、异轨抬手、快划只报 Ended。证据：`LiveLogic.cs:1177`、`LiveLogic.cs:1192`、`SwipeTail.cs:27`、`LiveLogicInputTests.cs:80` | 撤销本步统一路由，恢复第 7 步仍在 LiveLogic 内的完整保持阶段；保留中性契约，不逐个混接新旧保活函数，防止双尾判 |
| **9 / 高** | `LiveLogic.cs`；新增过渡 `Core/Live/LiveCandidatePipeline.cs`，最终移 Shared；保留 Shared 候选规则；相关反射测试 | **直接前置：5、6、7、8。Extract Class / Move Method / Encapsulate Field**：以中性输入/帧/输出整体移动候选容器和 6 阶段管线，接入已存在的 Session、Bridge 通知接线与 HoldCoordinator；方法/字段兼容包装暂留原门面供反射测试转发，逐步让真实谱面脚手架接入；不“优化”排序、同时间比较、快生成和延迟拾取 | CandidateOrder/Ownership/JudgmentSchedule/NotePressDisposition、LongHoldPipeline、DecorationTailFromChart；真人同轨长条上叠 tap/rub/flick、多个输入抢同一 note。证据：`LiveLogic.cs:1039`、`LiveLogic.cs:1388`、`LiveLogic.cs:1490`、`LongHoldPipelineTests.cs:326` | 一次性切回第 8 步仍由 LiveLogic 编排的 Judgment 管线，保留已抽出的 HoldCoordinator；Shared 规则版本不变，禁止新旧管线各判一遍 |
| **10 / 高** | Session 与 `NoteBase.cs`、普通/连接/摩擦/划键/长条/Guide/隐藏音符、现有尾策略、ScoreLogic/SkillLogic；Shared 下现有规则按需收口 | **直接前置：6、8、9（含第 5 步契约与第 7 步通知接线）。Move Field / Move Method / Preserve Facade**：在同一提交切换唯一运行态到 Session；具体音符与计分旧对象变转发壳，规则逐类移到原有对应实现的纯内部部分。保留每类状态转移、技能修正与通知时刻；不能构造第二套全局判定表、另写评分算法或只迁字段留双写。新增状态类型仅为 Session 内部数据 | G 之外逐类运行现有 NotePress/Tail/Pool/Score 用例，以第 1 步快照逐阶段对照；真人全类别与第二次播放。公开自定义 ScoreLogic/SkillLogic 子类兼容路径需同步调用契约测试。证据：`NoteBase.cs:221`、`LongNote.cs:99`、`FrictionNote.cs:102`、`ScoreLogic.cs:160`、`LiveLogic.cs:1954` | 撤销本步“权威状态切换”及纯实现接线，回到第 9 步旧模型唯一持有；不保留一半字段在旧类一半在 Session 的状态 |
| **11 / 高** | Bridge、NotesViewManager/NoteLineView/LongTapEffect/现有 BaseNoteView 适配、Session 输出、Shared 程序集边界 | **直接前置：7、10。Replace Mutable Reference with Snapshot / Move Presentation State**：以 Handle+代际号和值快照驱动视图；Progress/退场/viewReleased 的展示状态由 Bridge 独占；旧视图如需 NoteBase 接口只得到展示副本。消除到 Session 的读回调/模型引用，纯类移入 Shared；旧静态/公开字段仅由门面兼容同步 | LongNoteViewLifetime/TailKeyLifetime/NoteLineProgress/HeadPin/PoolHygiene、图集设置；新增无视图消费与正常消费产生同样仿真结果的对照；真人尾滑出、条体、皮肤切换后重开。证据：`NoteBase.cs:642`、`NoteLineView.cs:242`、`NotesViewManager.cs:163`、`LongNoteViewLifetimeTests.cs:201` | 撤销快照适配，恢复第 10 步 Bridge 对旧模型的过渡映射；保留模型 authority 仍在 Session，勿回退到双写 |
| **12 / 中** | LiveLogic 门面与六个类型、相关测试与程序集引用；移除仅私有死代码/过渡接线；新增 `Tools/LiveSimulationDependencyCheck/` 下检查器、显式 allowlist 与检查器用例 | **直接前置：11。Inline Delegate Wrappers / Remove Dead Private Code**：消除迁移支架与引用环；保留所有承诺的公开 API、legacy 模式、三次 Tick、现有准入公式。按下述 Roslyn 方案实现并运行纯层依赖检查，验证 GC，记录疑点为独立规则变更；不趁收尾修正第 3 节未确认行为 | G + 第 7 节全矩阵；检查器自身正/反例通过，实际纯层检查无违规且无未解析符号；长谱 Profiler 比较 GC/耗时、退出无残余回调。证据（待清理/测量位置）：`LiveLogic.cs:706`、`LiveLogic.cs:2025`、`LiveLogic.cs:776`、`docs/operations/android-performance.md:1` | 撤销清理、检查器及最终切换；保持第 11 步可运行结构。需回到单类基线时逆序撤销 11→1 |

### 第 12 步静态依赖检查的实现方案

此检查器属于第 12 步的待实现交付物，本次只修订计划，不能将以下文件或命令视为已经存在、已经运行。检查对象是第 11 步确定的纯仿真类型及其数据契约和可达的项目内帮助代码；检查其实际语义引用，避免重复当前字符串测试漏掉裸属性或换写法的局限。现有检查局限的依据：`SingleTimeSourceTests.cs:60`、`SingleTimeSourceTests.cs:83`。

| 交付物 / 检查阶段 | 具体实现与失败条件 |
| --- | --- |
| 工具与输入 | 在 `Tools/LiveSimulationDependencyCheck/` 新建基于 `Microsoft.CodeAnalysis.CSharp.Workspaces`、`Microsoft.CodeAnalysis.Workspaces.MSBuild` 的命令行工具。用 `MSBuildWorkspace` 加载 `OpenSekai.sln` 对应项目的真实编译引用与条件符号，取得 Compilation/SemanticModel；分别检查 Editor 与 Android 生产代码配置。编译引用不全、工作区加载失败、相关符号无法解析时返回非零并报告“未完成检查”，不得回退为字符串扫描后报通过。 |
| 显式 allowlist | 同目录新增 `allowlist.json`：记录纯层入口类型的完整元数据名、允许的基础/契约程序集身份，以及 Unity 例外的**完整类型名 + 精确成员签名**；初始例外只列迁移实际需要的 `UnityEngine.Vector2` 和 `UnityEngine.Mathf` 成员，每项写允许原因。禁止 `UnityEngine.*` 或整个 Unity 程序集通配放行；新成员须显式修改 allowlist 并复核。 |
| using 与符号检查 | 用语法树拒绝纯层宽泛 `using UnityEngine;` 和 `using static` 引擎类型；精确别名也要通过 `GetAliasInfo`/`GetSymbolInfo` 解析目标，不能凭别名文本放行。用语义模型及 `IOperation` 遍历声明类型、基类/接口、泛型实参、属性/字段/方法签名、attribute、typeof、构造、成员访问、转换与运算符；别名、全限定名称、类型推断、扩展方法与 global using 均按实际符号归属检查。实际使用的 Unity 类型/成员不在 allowlist 即失败，Time/Camera/Resources/Object/InputSystem 等不得借改名绕过。 |
| 传递依赖 | 对入口可达的项目内方法、字段/属性初始化器及静态构造器递归检查；禁止通过帮助类、属性或委托签名绕回 Core/Game 的适配实现。检查项目引用与外部程序集身份：除明确批准的标准库、纯契约与数学例外外，未审查的外部依赖失败；不能只检查顶层文件没有 `Time` 就通过。旧 public 兼容门面在纯层入口外，纯层不得反向引用它。 |
| 自测与报告 | 正例覆盖允许的 Vector2/Mathf 精确别名及成员、纯 DTO 与标准数学；反例覆盖别名 Time、全限定 Time、global using、推断类型、隐藏在帮助属性/静态初始化器内的服务调用、InputSystem 相位、适配层回调签名、未登记成员与未解析符号。报告输出源文件/行号、违规符号、引用链与失败规则；有违规或未解析项返回非零。 |
| 落地门禁 | 第 12 步先跑检查器正/反例，再对实际工程运行；建议入口形态为 `dotnet run --project Tools/LiveSimulationDependencyCheck -- --solution OpenSekai.sln --allowlist Tools/LiveSimulationDependencyCheck/allowlist.json`，具体配置参数在实现时一并固化。只有工具自测及两种配置的实际扫描均通过，才能勾选“纯层依赖检查通过”；G 和 Profiler 验证仍需分别完成。 |

第 10 步涉及最多音符子类型，是该计划的主要风险包；每个类型搬迁应复用其原实现、在同一提交前做逐类对照。若实现时发现该步无法做到单一状态切换，应先缩小一个独立、可转发的音符族，再更新本计划与验收范围，不能以“类型太多”为由切换半套规则并交付。约束来源：`LongNote.cs:1280`、`FrictionLongNote.cs:50`、`GuideNote.cs:18`、`NoteBase.cs:653`。

**不纳入上述行为保持迁移的修复**：把三次调用改秒、修改 touchId/fingerId 比较、修复装饰条 skip flag reset、取 Max 合并保活时间、统一两种尾窗舍入、合并显示提前量与候选提前量。每项都应有独立的预期行为、真实谱面重现与回退提交；在未确认之前保持当前输出。依据：`LongNote.cs:1021`、`LiveLogic.cs:1252`、`LongNote.cs:1424`、`LongNote.cs:1181`、`LongEndJudgement.cs:16`、`JudgeListLead.cs:26`。

## 6. 风险与陷阱

| 风险 | 现状证据 / 不能作出的假设 | 迁移防护与验证 |
| --- | --- | --- |
| Unity 序列化字段/脚本身份变化 | LiveLogic 本体无 MonoBehaviour 序列化属性，但视图与 LiveBundleBuildData 有 `[SerializeField]`；不能因为抽普通类就顺手改 prefab 绑定字段。证据：`LiveLogic.cs:15`、`LiveFrontView.cs:15`、`LiveBundleBuildData.cs:7`。 | 保留 serialized 字段名、类型、脚本 .meta GUID 与组件路径；确需改名时单独做可核查资产迁移，验证 prefab/场景引用。不要认为改成同名 property 会自动保留绑定；本计划前期不改 prefab 格式。依据：`LiveFrontView.cs:18`、`LiveBundleBuildData.cs:10`。 |
| `[Serializable]` 嵌套类兼容 | InputTmp/InputLogInfo 有 Serializable，成员有从字段改成私有 backing+property 的形态；目前 logger 只声明，未找到序列化入口，故不能断言有旧存档，也不能断言绝无外部引用。证据：`LiveLogic.cs:23`、`LiveLogic.cs:27`、`LiveLogic.cs:66`、`LiveLogic.cs:102`。 | 保留原嵌套类型全名与 public 构造/属性，作为适配壳；先查 Unity YAML/JSON/外部使用后再考虑删除或改 DTO。不要直接把 InputTmp 移走造成反射测试/类型名变化。依据：`LongHoldPipelineTests.cs:45`、`FlickEndBehaviourTests.cs:706`。 |
| 对象复用脏状态与定义态被误清 | LongNote 清 deferred/capped/计数/swipe/skip flag；NoteBase 保留 parent/child/list/callback 引用，MusicScore 会重复 reset root 与 list 内对象。证据：`LongNote.cs:1407`、`NoteBase.cs:697`、`MusicScore.cs:182`、`MusicScore.cs:188`。 | 明确 Session.Reset、输入刷新、Bridge.Reset/Dispose 三种生命周期；代际号防旧输出命中新局同索引。PoolHygiene 只覆值字段，额外用真实谱面重开覆盖引用/回调和定义 flag，不凭它单独验收。依据：`PoolHygieneTests.cs:84`、`FlickEndBehaviourTests.cs:394`。 |
| 每帧 GC | 输入槽预分配 128、匹配委托在构造绑定、JudgmentPass 是 struct，是应保留的优点；但候选 Sort 捕获 input、TickTailKeyViews 延迟 new List、LiveViewExt 的通知 lambda 仍有分配风险，不能承诺当前零 GC。证据：`LiveLogic.cs:167`、`LiveLogic.cs:161`、`LiveLogic.cs:1768`、`LiveLogic.cs:1388`、`LiveLogic.cs:776`、`LiveViewExt.cs:46`。 | 构造期分配数组/工作区/缓存比较器，缓冲预热；不在每指每帧 LINQ/ToArray/闭包/装箱。用 Profiler 区分原分配与新增分配，输出 DTO 不携带新集合；不要为了无分配任意截断同帧事件。依据：`LiveLogic.cs:357`、`JudgmentSchedule.cs:66`。 |
| 从帧改秒的历史坑 | 墙钟→谱面秒的旧版 /60 是公开兼容选项；HoldGrace 已秒化、早划尾仍三次调用、Flick delta 标准化是另一语义。证据：`LiveLogic.cs:404`、`LiveLogic.cs:416`、`LongNote.cs:1236`、`LongNote.cs:1021`、`FlickGesture.cs:34`。 | 单位写进参数名；保持 legacy 与三次调用，禁止把所有 “3 frame” 直接替换为 3/60 秒。按 60/120/165Hz、同墙钟不同 sample 延迟、暂停恢复验证。依据：`JudgeFrameTableTests.cs:83`、`ReferenceFrameTests.cs:16`、`FlickEndBehaviourTests.cs:1194`。 |
| 回调与“闭包”生命周期 | BindNoteCallbacks 实际主要是**实例方法组**，不是逐 note 捕获 lambda；持有 LiveLogic，且 LongNote setter 向子传播，再递归遍历绑定。HashSet 只防本次遍历重复，不负责解绑。Score/结果/CalcTimeOffset 回调也有返回值或重入语义。证据：`LiveLogic.cs:1990`、`LiveLogic.cs:2005`、`LongNote.cs:160`、`LongNote.cs:306`、`NoteBase.cs:653`。 | 会话终止按原图清所有委托/父子持有映射，重开前防旧会话响应；先保存同步时刻，再换快照。不要只解绑 controller.OnFinished/OnFailure 就认为 note callback 也释放了。依据：`SoloLiveController.cs:334`、`SoloLiveController.cs:336`、`LiveLogic.cs:2012`。 |
| 同步事件/重入与双结算 | 普通 NoteBase.Done 发布四个回调；LongNote 头部在 InputBegan 转换时发布，Done 则清子节点和视图；尾 Done 还可结束父条。证据：`NoteBase.cs:653`、`LongNote.cs:99`、`LongNote.cs:436`、`NoteBase.cs:518`。 | 不把所有类型统一成 Done 时算分，不将技能修正异步排队；事件记录按原时刻形成快照。逐阶段断言计数/得分/通知一次，回收视图不发结果。依据：`LongNoteViewLifetimeTests.cs:201`、`LongNoteViewLifetimeTests.cs:237`。 |
| 可见性/判定/几何耦合 | 条体几何包括不可绘端点；spawn gate、IsSkip、TailKeyPhase、条回收子键清理分散，视图还读取模型状态。证据：`LongNote.cs:15`、`NoteBase.cs:614`、`LiveLogic.cs:1909`、`NotesViewManager.cs:189`。 | 快照有独立键状态/条几何/判定状态；Bridge 不因回收键设置 NoteState.Done，不因条已 Done 丢掉尚未滑完的键；最后一键结束后仍可视觉推进。依据：`LiveLogic.cs:260`、`LongNoteViewLifetimeTests.cs:226`。 |
| Shared 并不代表程序集纯净 | 现有 Shared 工具仍有 Unity using、Sekai enum/类型引用；如果新纯类需要旧 NoteBase/LiveConfig/BootData，就仍有传递依赖。证据：`JudgeListLead.cs:1`、`LongEndJudgement.cs:1`、`JudgeTuning.cs:44`。 | 准入检查到“使用的类型与访问成员”，不要只查目录/using 字符串；配置 From(ScriptableObject) 工厂放在适配侧，纯数值部分可留 Shared。依据：`JudgeTuning.cs:44`、`NoteBase.cs:579`。 |
| 现有测试依赖内部结构 | 多个 Pipeline/Flick 测试反射字段/私有方法；删除成员会导致测试架构破坏，而不是玩法差异。SingleTimeSource 还是字符串检查并有漏报。证据：`LongHoldPipelineTests.cs:76`、`FlickEndBehaviourTests.cs:1366`、`SingleTimeSourceTests.cs:60`。 | 迁移期保留转发包装，优先把关键场景换成真实谱面+LiveHarness；不能改掉失败断言后宣称行为相同。Ignore 的 4 条不解除就不计作新覆盖。依据：`docs/operations/testing.md:55`、`docs/operations/testing.md:71`、`DecorationTailJudgmentTests.cs:27`。 |
| 稳定 API 与可替换逻辑 | 当前 ScoreLogic、SkillLogic 暴露可写属性/替换方法及 virtual 扩展；public 字段也可外部写。证据：`LiveLogic.cs:117`、`LiveLogic.cs:123`、`LiveLogic.cs:141`、`ScoreLogic.cs:69`、`SkillLogic.cs:25`。 | 标准路径纯化之外，保留外部 subclass 兼容适配模式，每局择一路由；未审计外部二进制调用者的序列化/ABI **[未能确认]**，本地源码 API 稳定不等于承诺跨程序集二进制兼容。依据：`LiveLogic.cs:211`、`LiveLogic.cs:217`。 |

## 7. 验收与回归清单

以下“现有测试”均指可定位的测试，不表示本次运行；“真机补充”是迁移时必须执行/记录的用例。自动 1,000,000/S 门禁只验证自动演奏装配，不替代真人触摸验证。依据：`TestResults/pre-release.xml:2`、`docs/operations/testing.md:83`。

| 必须仍成立的行为 | 现有测试 / 已知缺口 | 真机用例与通过条件 |
| --- | --- | --- |
| 判定窗口、legacy、新映射夹紧、边界相等语义不变 | `JudgeFrameTableTests` 22/64/83，`JudgeWindowTests`、`LiveLogicInputTests` 279/290/300；窗口与准入 offset 不得混用。证据：`JudgeFrameTableTests.cs:22`、`JudgeFrameTableTests.cs:64`、`LiveLogicInputTests.cs:279`、`LongNote.cs:1340`。 | 同谱同设置手动早/晚按，legacy 开/关与暂停后首触分别对照基线；无 60 倍意外缩放/旧输入跨暂停落到远处。现有两模式差异需保留。证据：`LiveLogic.cs:404`、`LiveLogic.cs:423`。 |
| 长条连续保持、换指、重叠、漏头补接、内部 combo | HoldInputMatching、LongHoldOwnership、LongHoldPipeline 357/443/463/515/618/657，真实谱面 Input 80/217；不要只依赖反射手搓音符。证据：`HoldInputMatchingTests.cs:9`、`LongHoldPipelineTests.cs:357`、`LongHoldPipelineTests.cs:618`、`LiveLogicInputTests.cs:80`。 | 宽窄交叉两指、保持中另一指命中叠键、抬起 owner 由另一指接管、漏头后中段接条，条不闪断/双算；不同设备刷新率与基线逐项一致。证据：`LongHoldPipelineTests.cs:463`、`LongHoldPipelineTests.cs:515`。 |
| **尾判一：ReleaseTail** 普通/摩擦长尾窗口、异轨抬手不得误判 | TailBehaviour、TailReleaseLane 的真实谱面两例；LongHoldPipeline 的早晚窗/中段拒绝。证据：`TailBehaviourTests.cs:22`、`TailReleaseLaneTests.cs:72`、`TailReleaseLaneTests.cs:86`、`LongHoldPipelineTests.cs:409`、`LongHoldPipelineTests.cs:417`。 | 条内持握者抬手命中；另一轨另一指抬起不结束条；另做 touchId≠fingerId、Ended 位于 Y 门外，记录当前行为差异并单独处理缺陷。证据：`LiveLogic.cs:1252`、`LiveTouchSample.cs:20`。 |
| **尾判二：SwipeTail** 真划、错向上限、早划待释放后三次 Tick、超时 Miss | FlickEndBehaviour 346/546/570/1194/1332；TailBehaviour 验证 Flick 非 release。证据：`FlickEndBehaviourTests.cs:346`、`FlickEndBehaviourTests.cs:570`、`FlickEndBehaviourTests.cs:1332`、`TailBehaviourTests.cs:34`。 | 早划/到点划、只抬不划、抖动、错方向、Ended 单采样快划；按原调用次数延迟，错向仍按现规则封顶 Great，无划最终 Miss。证据：`LongNote.cs:1155`、`LongNote.cs:1021`、`LongNote.cs:1038`。 |
| **尾判三：SelfJudgedTail** FrictionFlick 自判，父条只脱离；SilentTail 不冒充玩家 | TailBehaviour/TailBehaviourContract、FlickEndBehaviour 1252/1298；隐藏/装饰另有静默分支。证据：`TailBehaviourTests.cs:46`、`TailBehaviourTests.cs:77`、`TailBehaviourContractTests.cs:40`、`FlickEndBehaviourTests.cs:1252`。 | 摩擦划尾真实输入命中且只算一次；未触摸到关窗才结束；隐藏尾无多余结果/声音。证据：`TailBehaviours.cs:18`、`LongNote.cs:565`、`FlickEndBehaviourTests.cs:1298`。 |
| 装饰条自身不计分/连击，真实尾可玩；不可见刻度不发声 | **优先 DecorationTailFromChartTests 的真实谱面用例**；DecorationTailJudgmentTests 的 4 条为 Ignore；ScoreLogic 装饰排除。证据：`DecorationTailFromChartTests.cs:106`、`DecorationTailJudgmentTests.cs:27`、`ScoreLogicTests.cs:162`、`ScoreLogicTests.cs:199`。 | `deco-tail-test10` 覆盖 Guide/装饰条各尾类别，尾可判且有相应键；装饰内部刻度无 SE；首次与重试均对照，skip flag 的潜在缺陷单独留档。证据：`LiveLogic.cs:681`、`LongNote.cs:1424`、`docs/operations/testing.md:55`。 |
| 划键方向与速度门、标准 60Hz 位移换算不变 | FlickGesture 18/44/56/80，JudgeTuning 37/48；FlickEndBehaviour 444/472。证据：`FlickGestureTests.cs:18`、`FlickGestureTests.cs:56`、`FlickGestureTests.cs:80`、`JudgeTuningTests.cs:37`、`FlickEndBehaviourTests.cs:444`。 | 低速拖动/静止抖动不得误划；合法方向/上划按原规则；60/120/165Hz 可用档位、设备 DPI 对照，不因参数移动更换单位。证据：`FlickGesture.cs:34`、`LongNote.cs:1129`、`LongNote.cs:1167`。 |
| 连击、计分严格 1,000,000、等级 S、技能倍率与伤害回调顺序 | ScoreLogic 的 AllJustPerfect/Auto、Perfect 等价、Great/Good、装饰/计数/技能、ScoreGauge、PlayResult；现有 testing.md 的 Perfect=0.9 概述已落后，**以代码与测试为准**。证据：`ScoreLogicTests.cs:69`、`ScoreLogicTests.cs:84`、`ScoreLogicTests.cs:111`、`ScoreLogicTests.cs:151`、`ScoreLogicTests.cs:210`、`ScoreGaugeTests.cs:31`、`ScoreLogic.cs:175`、`docs/operations/testing.md:48`。 | 每步 G：固定自动谱面结算 1,000,000 / S / 221；另外真人判定计数、combo 中断、技能倍率、失败/继续/完成各只通知应有次数。证据：`docs/operations/android-build.md:78`、`LiveLogic.cs:304`、`LiveLogic.cs:431`。 |
| 键/条生命周期、尾滑出、一次回收与一次结算独立 | TailKeyLifetime、LongNoteViewLifetime 81/139/201/226/237，HeadPin/NoteLineProgress；包括 Guide、摩擦头的可绘尾。证据：`LongNoteViewLifetimeTests.cs:81`、`LongNoteViewLifetimeTests.cs:139`、`LongNoteViewLifetimeTests.cs:201`、`LongNoteViewLifetimeTests.cs:226`、`HeadPinTests.cs:63`。 | 最后一键未判时继续滑出、条 Done 不使未完成视觉停住；已判键不复活；隐藏端点保留条体几何；重试无旧条/幽灵键。证据：`NoteLineView.cs:200`、`NoteLineView.cs:242`、`LiveLogic.cs:812`。 |
| 皮肤切换与“热切换”能力准确表述 | 现有设置 UI 保存皮肤索引，LiveConfig 在 LoadOptionData 取值，NotesViewManager 只在池未创建时创建；**曲中热切换 [未能确认]**：未找到现有重载/换池实现与专门测试。图集静态设置测试不等于热切换验证。证据：`LibrarySettingsPanel.cs:155`、`LiveConfig.cs:263`、`NotesViewManager.cs:38`、`Assets/Tests/EditMode/Graphics/LiveAtlasSpriteSettingsTests.cs:20`。 | 已确认路径：切皮肤→开曲→退出→切回→重开，两样式资源、键/条/效果正常，计分一致。若验收要求曲中热换，先建立现状可运行证据；否则登记为新增能力，不能在行为保持重构中承诺实现。证据：`NotesViewManager.cs:214`、`BaseLiveController.cs:55`。 |
| 输入生命周期与每局隔离 | Input 的旧采样忽略/Ended ID/同音符双触，PoolHygiene、FlickEndBehaviour 394；测试默认 ID 相等是已知缺口。证据：`LiveLogicInputTests.cs:369`、`LiveLogicInputTests.cs:381`、`LiveLogicInputTests.cs:393`、`FlickEndBehaviourTests.cs:394`、`LiveTouchSample.cs:20`。 | 暂停恢复、焦点切换、重试/退曲重入两次，旧触摸不复活、旧回调不响应、无复用残留提前尾判。证据：`LiveLogic.cs:224`、`SoloLiveController.cs:171`、`LongNote.cs:1413`。 |
| 最终层次约束与性能 | 当前无专门“六层纯化”测试，需新增依赖/输出对照检查；现有测试不能证明纯层无视图回写，也未给本次 GC 实测值。证据：`SingleTimeSourceTests.cs:60`、`LiveViewBase.cs:38`、`LiveLogic.cs:776`。 | 纯层只用允许的数学别名，无设备/单例/可写视图引用；关闭展示消费与正常消费的仿真结果相同；同谱同设备 Profiler 无新增持续每帧分配或明显耗时回退，长谱/密集同帧输出不丢事件。现状分配点供对照：`LiveLogic.cs:1388`、`LiveViewExt.cs:46`。 |

验收归档最少包含：源码提交、Unity/包版本、完整 EditMode XML 与 Ignore 清单、设备/刷新率/设置、自动门禁结算截图、上述真人矩阵记录，以及仍未确认的问题。缺少真机、曲中热切换证据或未覆盖 ID 组合时如实保留 `[未能确认]`，不把文档推断写成通过记录。基线依据：`ProjectSettings/ProjectVersion.txt:1`、`TestResults/pre-release.xml:2`、`docs/operations/android-build.md:78`、`docs/operations/testing.md:88`。
