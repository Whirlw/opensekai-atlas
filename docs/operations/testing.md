# 测试

工程使用 Unity Test Framework（`com.unity.test-framework` 1.8.0）。测试分两层：

| 层 | 位置 | 运行环境 | 用途 |
| --- | --- | --- | --- |
| EditMode | `Assets/Tests/EditMode` | 编辑器、不进包 | 纯逻辑、文件 IO、数据契约 |
| PlayMode | `Assets/Tests/PlayMode` | 播放模式 | 曲库场景装配冒烟 |

两个测试程序集都是 `autoReferenced: false`、`defineConstraints: ["UNITY_INCLUDE_TESTS"]`，EditMode 还额外限制为 `Editor` 平台，因此**不会进入 Android 包**。

## 怎么跑

Unity 命令行（`unity test`）会另起一个编辑器实例，**与本机已打开的编辑器互斥**，所以日常用工程内的桥接菜单：

1. 编辑器里 `OpenSekai/Dev/Run EditMode Tests`（或 `Run PlayMode Tests`）。
2. 结果写到 `TestResults/editmode.xml`（或 `playmode.xml`），控制台打一行
   `[Tests] Finished: passed=… failed=… duration=…`。

> **PlayMode 的报告拿不到。** 进播放模式会触发域重载，`TestRunnerBridge` 注册的回调随编辑器脚本域一起被销毁，`RunFinished` 不会回来，所以 `playmode.xml` 不会生成。PlayMode 用例请用 Test Runner 窗口跑，或关掉编辑器后用下面的 `unity test --mode PlayMode`。EditMode 不受影响（不重载域）。

也可以在编辑器已打开时用脚本触发（自动化、CI 前置检查）：

```csharp
OpenSekai.EditorTools.TestRunnerBridge.Run("EditMode", "TestResults/editmode.xml");
```

需要独立进程时（CI、没有打开编辑器），先关闭编辑器再跑：

```powershell
unity test . --mode EditMode --output TestResults/editmode.xml --report-format nunit,junit
unity test . --mode PlayMode --filter "OpenSekai.Tests.PlayMode" --retries 1
```

报告是 NUnit 3 XML，可以直接被 CI 的测试面板读取。

### 结构与缺失脚本基线

```powershell
pwsh -NoProfile -File Tools/verify-structure.ps1
pwsh -NoProfile -File Tools/verify-structure.ps1 -CheckProjects
unity run . --non-interactive -- -executeMethod OpenSekai.EditorTools.MissingScriptAudit.Run
```

结构检查覆盖文档链接、标题锚点和空运行时目录。`-CheckProjects` 在 Unity 重新生成 IDE 项目后检查 `.csproj` 中的源码引用。
Unity 扫描将缺失脚本的资产路径、GUID、fileID 和数量写入 `TestResults/missing-scripts.tsv`。
打开编辑器时也可运行 `OpenSekai/Dev/Audit Missing Scripts`。清理前保存一份扫描结果，清理后再运行并比较。

## 覆盖范围

EditMode：

- `Charts/ChartDocumentTests`：深拷贝、`Migrate` 的版本与事件过滤、空值容忍、`HasPlayableNotes`。
- `Charts/ChartJsonTests`：JSON 往返、旧类型名映射（`MusicScoreMakerData` → `ChartDocument`）、`MusicId` 覆盖。
- `Charts/ChartTimelineTests`：tick ↔ 秒（120 BPM 4/4 基准）、往返一致性、`ConvertMusicScoreInfo` 排序。
- `Charts/RuntimeChartDefinitionTests`：空文档/空分数、有 tick 无音符、`HasPlayableNotes` 的三个条件、转换后音符数与文档一致。
- `Packages/SongPackageStorageTests`：目录名生成（空白折叠、裁剪、空标题回落）、12 位短 id、`SongPackage` 的派生路径与 `MusicId` 稳定性。
- `Packages/ChartFileReaderTests`：写读往返、`MusicId` 打戳、迁移、缺失文件、null 参数、拒绝非谱面扩展名。
- `Playback/PlaySessionTests`：四个 `ArgumentNullException` 守卫、session 字段保持同一引用、`PlaybackSettingsSnapshot` 与后续编辑解耦。
- `Live/ScoreLogicTests`：1/7/64/221/348/1000/2019 音符全 JustPerfect 严格合计 1,000,000、Perfect/Great/Good 的 0.9/0.7/0.5、技能倍率、装饰音符不计分、连击在 Great 及以上累加、空谱基分为 0。
- `Live/ScoreGaugeCalculatorTests`：S/A/B/C 阈值、gauge 单调与夹紧。
- `Live/PlayResultTests`：`IsAllPerfectClear` 的六种边界、`KindText`、`PlayResultHandoff` 单槽语义。
- `Charts/RuntimeChartConverterTests`：七类 `noteBaseType` 到具体 Note 的映射、长条是「一个 root + hold 子节点」、连接音符并入同一 root、装饰音符不参与计数、Critical 保留类型、BPM 事件进时间轴、只有链头成为 root、孤立尾巴被丢弃、悬空 `nextConnectionId` 不致命、环形连接抛 `InvalidDataException`、FrictionLong 对折叠成 `FrictionHideLongNote`。
- `Packages/PlayHistoryRecordTests`：旧记录（无 `chartHash`/`rulesVersion`/`sessionId`）仍可反序列化、新字段回落、新记录往返、未知字段忽略。
- `Packages/SongPackageManagerTests`：新建/列出/删除/复制、导出 `.oskp` 后导入回读、导出归档里含 `manifest.json` 与 `score.json`、导入的副本沿用 manifest id 但拿到新目录、坏包与缺失文件的处理。
- `Settings/LiveSettingsStoreTests`：缺文件默认值、保存读回、自动建目录、旧的 `Use120FPS` 升级成 `FrameRateLimit`、坏 JSON 回落、`null` 路径拒绝。
- `Live/DecorationTailFromChartTests`：**真实谱面驱动**——读 `Tools/fixtures/deco-tail-test10/score.json`（取自报告者 `Test10.zip`），`ChartJson.Read` → `RuntimeChartConverter.Convert` → 用真实 `OnUpdate`/`OnInput` 把手指依次送到每根 `GuideNote` 条的尾时间（flick / friction-flick 给划动采样，普通尾给按下），断言每种尾都判出结果。这是目前唯一能验证「尾进不进候选」的用例，**动到 `_judgmentNoteList` 注册时必须跑它**。
- `Live/DecorationTailJudgmentTests`：**已停放**（`[Ignore]`）。它手工 `new` 条与尾再反射调内部阶段，结果连普通尾都进不了候选——说明缺了 `OnUpdate` 的整条扫描，子音符的进度与候选根本没推进。不要再拿它下结论；需要新用例请照 `DecorationTailFromChartTests` 的写法。
- `Live/TailReleaseLaneTests`：真实谱面 + `LiveHarness` 逐帧驱动一根普通尾长条，验证**抬手归属**——另一根手指在别的轨道按下再抬起时尾不许被判定、条也不许结束；持握者自己在条内抬手时尾必须被判。**动到 `CoverHoldsWithAnyFinger` 的松手分支时必须跑它**。
- `Live/PressOwnershipTests`：按下归属。收集候选不写 `_judgedTouchTimes`、尚未命中的候选不挡后续候选；同帧双押各取更近的音符；共享音符按车道中心距离、再按较小 `fingerId` 归属；单指按谱面时间、再按 `ChartOrder` 取候选，与注册顺序无关；同一手指同帧多个 `Began` 样本共用一个按下额度；「两指争同一音符、其中一指还够得到更早一颗」时落选者不重配，两种注册顺序结果相同。**动到 `LiveCandidatePipeline.Collect` / `SortPressCandidates` 或 `JudgmentScheduler.EvaluateJudgment` 的消费路径时必须跑它**。
- `Live/DecouplingAcceptanceTests`：判定解耦的六条验收用例（[交接](handoff-2026-11-11.md) §10 第 1 条）。两条头（过线未按仍然发布 Miss、被尾结束时头的结果不被覆盖）、两条抬手归属（尾键横移到另一条车道后要在尾的位置抬手；抬手落在相邻装饰条车道、但仍在条身放宽范围内时不判目标条）、两条 flick 尾（标准时间之前的样本既不判也不记；判定之后继续驱动 30 帧，尾只发布一次、连击只 +1）。前四条走真实 `OnUpdate`/`OnInput` 并断言手指确实被候选路径接住，后两条只驱动尾的决策入口。**动到 `PickLiftTarget`、`Head.Settle`、`LongNoteTail` 的窗口或发布路径时必须跑它**。六条在写入时全部通过：它们固定的是现有行为，不是待修的缺口。
- `Live/HeadDecouplingTests`：头的离场出口。除原有的「按住头只发布一次」「判过的头不被宽限结束覆盖」外，新增三条盯 `LongNote.LeavePlayState()`：条在头结算前被 `ForceTerminate(tail)` 结束仍要补发 Miss（**这条是先红后绿的**）、重复调用只发布一次、已结束的条再按不改变任何结果。**动到 `ForceTerminate` / `FinishSilently` / `SettleExpiredHead` 或 `LongNoteHead.TryJudge` 时必须跑它**。
- `Live/TailBehaviourContractTests`：尾的策略契约（`ITailBehaviour.TryFinish` 一处决定，bar 上不许再出现三个布尔开关）。另有两项盯「**同一根尾只发布一次**」：把已判过的条硬塞回 `Scrolling` 后，窗口过期 / 抬手 / 自判解绑三条路径都必须被拒且不改写任何结果（**先红后绿**）；以及池复用后能重新发布一次。**动到 `LongNoteTail` 的 `PublishTail` / `FailTail` / `DetachStandaloneTail` 或 `Reset` 时必须跑它**。
- `Live/FlickEndBehaviourTests`：划键尾的窗口、延迟结算、方向与速度门槛。其中的独立尾（摩擦划）几条按 `tail-classes.md` §3 的 R1/R2/R3 改写：尾自己写 Miss（条只驱动与读取）、尾判定不结束条、条结束把没判的中间点判 Perfect。**动到 `DetachStandaloneTail`、`ForceTerminate` 对尾的判定、或 `SettleLeftoverAsPerfect` 时必须跑它**。
- `App/PlayServiceDependencyTests`：开局路径依赖全部换成假端口，覆盖四种前置拒绝（曲库未就绪、加载界面未就绪、请求目标不是曲库、当前不在曲库场景）、推入加载界面失败后的回滚与锁释放、开局成功后的持锁与场景退场释放、以及连续两局；并断言发布的 `FreeLiveBootData.DeckData.UserInfo.DeckId` 取自端口而不是单例。**动开局流程或 `PlayService` 的前置判断时必须跑它**。
- `LiveContextTests`：`LiveContext.FromRetainedBoot()` 把 `UserDataManager`、`EntryPoint.PlayMode`、`PlaySessionContext` 的五个事实逐项抄进上下文；缺 Boot 时其余字段仍完整，null 会话字段降级成空串，两次读取互不影响。用例在 `[SetUp]/[TearDown]` 里存旧值并写回，不会污染同一进程里的其它用例。
- `Resources/ResourceCatalogTests`：`ResourceCatalog` 里每条固定 `Resources` 路径都能加载、类型正确、回退只在主路径失败时生效；另含两条字体相关断言——`Fonts/SourceHanSansSC-Medium` 能被 TMP 建成字面、以及 `LibraryUi.EnsureRequiredGlyphs` 能对三条来源（bundled / OS / 项目资产）补上省略号字形。项目 SDF 资产在用例里是**克隆**后操作的，避免编辑器回写被跟踪的 `.asset`。

PlayMode：

- `LibrarySmokeTests.LibrarySceneBootsAndShowsThePage`：载入 `Assets/Game/Scenes/Library.unity`，等 `LibraryScreen` 起来并断言 `IsReady` 与 `ScreenManager.ExistsInstance`。单独跑 0.43 s，不依赖同一轮里前面的用例留下的场景。
- `LibrarySmokeTests.BaselineTapAutoplaysAndReturnsToTheLibrary`：**整首打歌**。从曲库结算面板的「再玩一次」触发 `PlayService` 自动开局，等 Core 场景建出 `SoloLiveController`，再等返回 Library 并检查结算 `Completed / IsAuto / Score=1,000,000 / Rank S`；谱面用仓库里的 `Tools/fixtures/baseline_tap`。约 47.5 s。
- `LibrarySmokeTests.ShippedWavePackRegistersEveryManifestCue`：按 `StreamingAssets/sound/manifest.csv` 核对 42 个内置 WAV 全部注册、长度非零、结算音能起播，以及缺失 cue 只警告一次并返回 0。

跑 PlayMode **必须走异步**：进播放模式会触发域重载，同步请求会被丢掉（CLI 会直接拒绝并提示改用 `--async_tests`）。

```powershell
unity command test run --mode playmode --async_tests true --timeout 420
unity command test status --format json
```

2026-11-12 实测：3 项全部通过，总 48.08 s（整首 47.47 s、装配 0.03 s、音频 0.54 s）。不要再走「桥接注册回调 + `playmode.xml`」那条路：域重载会把回调一起销毁，报告不会生成，编辑器还会停在播放模式。

## 两次运行一致性（P4-2）

`TestRunnerBridge`（菜单 `OpenSekai/Dev/Run EditMode Tests`，或在 `unity_eval` 里调 `TestRunnerBridge.RunEditMode()`）把报告写到 `TestResults/editmode.xml`；两次运行各存一份后比对：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\compare-test-runs.ps1 `
  -First TestResults/p4-2-editmode-run1.xml -Second TestResults/p4-2-editmode-run2.xml
```

脚本按「测试全名 + 结果」逐项比较，出现缺项、多项或结果变化就以非零退出，因此也可以直接进 CI。

2026-11-12 的结果：两次都是 **569 项 / 565 通过 / 0 失败 / 4 跳过**，逐项完全相同，耗时 8.11 s 与 8.11 s。4 个跳过全部来自已停放的 `Live/DecorationTailJudgmentTests`（原因见上文「覆盖范围」，替代用例是 `DecorationTailFromChartTests`）。

> 此后基线继续增长：2026-11-12 判定解耦这一轮收尾时为 **598 项 / 594 通过 / 0 失败 / 4 跳过**（`Live/DecouplingAcceptanceTests` 六条 + `Live/HeadDecouplingTests` 的出口三条 + `Live/TailBehaviourContractTests` 的发布与「Done 不再判」三条 + `Live/FlickEndBehaviourTests` 类 1 的改写三条与新增五条）。上面的 569 是那次一致性实验的记录，不是当前基线。

结构门禁（P4-4 起，`Tools/verify-structure.ps1` 默认执行，无需额外开关）：

- **门面规模**：`ScreenManager.cs` ≤ 600、`LibraryScreen.cs` ≤ 100、`LongNote.cs` ≤ 500 个非空行。超过就报错并提示「拆开而不是把门面养大」；要放宽得和用到它的代码同批提交，改动可见。
- **直接 `Resources.Load*` 白名单**：只有 12 个文件可以直连（目录与加载器 4 个 + P3-2 记录在案的动态取址 8 个，每个都在脚本里带一句理由）。新文件要直连就得往白名单里加，否则失败——正常路径是 `ResourceCatalog`（固定资产）或 `ResourceLoader`（动态键）。
- **缺失脚本为 0**：`MissingScriptAuditTests` 在 EditMode 里跑一遍扫描，断言 `TestResults/missing-scripts.tsv` 只有表头。P4-4 之前这张表有 3 行，来自两个真缺失的脚本；修完为 0，之后由这条测试盯住。

隔离审计（同轮核对）：

- 测试输入全部来自仓库内的 `Tools/fixtures/`，没有本机绝对路径。这些路径按**项目根**解析，所以跑测试的进程工作目录必须是项目根——编辑器内与 `-executeMethod` 都满足，用 `unity test` 从别的目录起会读不到文件。
- 需要落盘的 5 个 fixture 都用 `Path.GetTempPath() + Guid` 建独占目录，并在 `[TearDown]` 删除：`ChartFileReaderTests`、`SongPackageManagerTests`、`PlaySessionTests`、`LiveSettingsStoreTests`、`LibraryPresenterTests`。
- 没有测试写仓库目录，也没有写 `persistentDataPath`。唯一的共享状态读是开局路径：`FreeLiveBootData` 构造时读 `LiveSettingsStore.Shared`，而 `Load()` 只读缓存、只有 `Save()` 会写盘，已核对无副作用。
- 静态状态在用例内配对恢复：`LiveContextTests` 在 `[SetUp]` 存 `UserDataManager.FreeLiveBootData` 与 `EntryPoint.PlayMode` 的旧值，在 `[TearDown]` 写回并 `PlaySessionContext.Clear()`。

## 写测试的约定

- 一条测试一个行为，名字是「被断言的事实」，例如 `OneBarAt120BpmIsTwoSeconds`。
- 用 `Assert.That(actual, Is.EqualTo(expected).Within(tolerance))` 比较浮点。
- 不写依赖 `persistentDataPath` 的测试；需要落盘时用 `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))` 并在 `[TearDown]` 删掉。
- 需要 `ScriptableObject` 时用 `ScriptableObject.CreateInstance<T>()`，测试结束 `Object.DestroyImmediate`。
- 不要为了测试改生产代码的可见性；确实需要时优先抽纯函数（例如把「哪个数字位该显示」从 `NumberView` 抽出来）。
- **判定链路不要手搓音符**：`new LongNote(...)` + 反射调 `UpdateJudgmentNoteArray` / `AllocateInputJudgableEachNotes` 得到的结论不可信，子音符的进度、视图与候选只在 `OnUpdate` 的整条扫描里推进。要测判定，用真实谱面转换后的音符图，并按 `LiveLogicInputTests` 的方式驱动 `OnUpdate` / `OnInput`。
- 判定相关的用例必须显式设置 `LiveConfig.LoadMasterConfig()` 与 `LiveConfig.UseLegacyJudgeTiming = false`，否则秒级窗口全部判为非法，所有音符都会「不判」。
- **脚手架时钟只能前进**：逐帧驱动时不要先推过头再回头补一帧（`Step(0.95)` 后 `Step(0.938)`）。时间倒退会让长条的 `lastInputTime` 落在未来、持握失效，用例会以「产品 bug」的样子失败。写法是停在事件前一帧，把事件本身作为下一步。
- **断言消息带状态**：失败信息里拼上 `state / holder / IsTailJudgeableAt / 相关时间`，否则一次运行什么也看不出（这是 2026-11-07 用一轮就定位「时钟倒退」的原因）。

## 编译报错但 Unity 不显示

改完脚本先看编译，但 **Unity 的 `unity command console status` 会撒谎**：搬动/删除 `.cs` 之后它可能长时间报 `compilationFailed: true` 而 `entries` 是空的，同时 `Library/ScriptAssemblies/OpenSekai.Game.dll` 的时间戳不动——也就是说它既没告诉你错在哪，也没真的重编。2026-11-09 抽 `TouchDeduplication` 时就卡在这上面十几分钟。

可靠的替代路径是把 Unity 自己生成的编译参数直接喂给它自带的 Roslyn：

```powershell
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\dotnet.exe'
$csc    = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$rsp    = (Get-ChildItem 'Library\Bee\artifacts' -Recurse -Filter 'OpenSekai.Game.rsp' |
           Select-Object -First 1).FullName
& $dotnet $csc "@$rsp" -nologo 2>&1 | Select-String 'error CS'
```

几点经验：

- 没有 `Editor\Data\Tools\Roslyn\csc.exe`，只有 `DotNetSdk\...\Roslyn\bincore\csc.dll`，要经 `dotnet.exe` 起。
- `.rsp` 是这次编译实际用的文件列表，**新增的 `.cs` 不在里面**时它会报 `CS0246` 找不到类型：先在 Unity 里 `AssetDatabase.Refresh(ForceSynchronousImport)` + `CompilationPipeline.RequestScriptCompilation()`，等 rsp 重新生成再跑。
- 一条命令就能拿到全部错误（含 `CS0051` 这类可访问性不一致），比在编辑器里翻 Console 快得多。
- 这条路径**只验证语法与类型**：它不重新生成 `Library/ScriptAssemblies`。要通过还要回到 Unity 确认 `OpenSekai.Game.dll` 的时间戳变新，再跑测试。

## 「整首打歌」的自动化

`LibrarySmokeTests.BaselineTapAutoplaysAndReturnsToTheLibrary` 是完整的整首往返：仓库里的 `Tools/fixtures/baseline_tap` → 曲库结算面板的「再玩一次」→ `PlayService` 自动开局 → Core 场景的 `SoloLiveController` → 返回 Library → 断言结算 `1,000,000 / S`。**P4-3 起不再标 `[Explicit]`**，默认参与 PlayMode 运行，单次约 47.5 s。

它历史上抓到过一次真实回归：完整场景往返后计分缺口（`466,063 / C`，自动判定 103/221），修正自动判定游标后回到 `1,000,000 / S`。**改判定游标、开局边界或结算回传时必须跑它**。

跨域重载和场景切换靠 Unity Pipeline 的异步测试支持：`test run --async_tests` 立即返回、结果用 `test status` 轮询。此前文档里那句「从桥接跑 PlayMode 会丢回调」只适用于自建回调 + `playmode.xml` 的写法，不适用于这条异步路径。

## 待补

- `NumberView` 的数字位布局：目前埋在 MonoBehaviour 里，要测就得先把「哪位该显示、用哪张精灵」抽成纯函数。
- 视觉与手感（音符渲染、判定窗口）：单测覆盖不到，见上面的真机门禁。

## CI

- 本地/流水线通用入口：`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1`（先关掉编辑器，CLI 才肯起实例）。脚本把 nunit 报告写到 `TestResults/editmode-ci.xml`、打印 `total/passed/failed`，并以 Unity 的退出码收尾，失败即非零。
- GitHub Actions：`.github/workflows/structure.yml` 不需要 Unity 许可，跑 `verify-structure.ps1`（含上面三项结构门禁）；`.github/workflows/editmode-tests.yml` 用 `game-ci/unity-test-runner@v4`、`unityVersion: auto`、`testMode: EditMode`，上传 `TestResults` 产物。缺失脚本扫描是 EditMode 里的一条测试（`MissingScriptAuditTests`），所以**现有 EditMode 作业已经覆盖它**，不需要额外的 YAML。
- **PlayMode 没有进 CI**：整首用例需要音频设备与 ~48 s 的连续运行，headless 跑既慢又容易假红。它由本地/发布前跑：`unity command test run --mode playmode --async_tests true` + `test status`。这条是明确的覆盖边界，不是遗漏。

### 为什么不开 license 时是「绿色跳过」而不是「失败」

`secrets` 上下文在 **job 级 `if`** 里不可用。写 `if: ${{ secrets.UNITY_LICENSE != '' }}` 会让 GitHub 在解析阶段就判定工作流无效，运行结果是 **0 秒、没有 job、结论 failure**（本项目 2026-11-09 的 run #1/#2 就是这样）。现在的写法是先跑一个 `Check for a Unity license` 步骤，把 `present=true/false` 写进 `$GITHUB_OUTPUT`，后面的缓存 / 跑测试 / 上传都用 `if: steps.license.outputs.present == 'true'` 门控；没配就发一条 notice 跳过，运行是 success。

### 配 license secret

三种来源，按可用性排序：

1. **从本机已激活的编辑器取 `.ulf`**（最简单，只要这台机器能正常打开工程）

   Unity 把激活文件放在：

   ```
   Windows  C:\ProgramData\Unity\Unity_lic.ulf
   macOS    /Library/Application Support/Unity/Unity_lic.ulf
   Linux    ~/.local/share/unity3d/Unity/Unity_lic.ulf
   ```

   然后：

   ```powershell
   gh secret set UNITY_LICENSE --body (Get-Content C:\ProgramData\Unity\Unity_lic.ulf -Raw)
   ```

   没有 `gh` 也可以在仓库网页 `Settings → Secrets and variables → Actions → New repository secret` 里粘贴文件内容（整个 XML 原样贴）。

   > 该路径不存在（还是走到了方式 2/3）也不奇怪：Unity 6 的 Personal 授权由 Hub 管理，部分安装方式下不会在 `ProgramData` 落这份文件。找不到就直接用下面的手动激活或邮箱密码。

2. **手动激活文件**（机器上没有已激活的编辑器，或不想动本机授权）

   ```powershell
   "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe" -batchmode -nographics -createManualActivationFile -logFile -
   ```

   会生成 `Unity_v6000.6.7f1.alf`，上传到 <https://license.unity3d.com/manual> 选版本、下载 `.ulf`，再按上面的方式塞进 secret。

3. **邮箱密码**（Personal 版最省事，但要求账号没有开二步验证）

   ```powershell
   gh secret set UNITY_EMAIL    --body "you@example.com"
   gh secret set UNITY_PASSWORD --body "<密码>"
   ```

   `game-ci` 会拿这组凭据现场激活；同样的凭据不能同时用于多个并行 job，否则先激活的会把后一个踢下线。

### 已知注意点

- **镜像里未必有 `6000.6.7f1`**：`game-ci` 的 unity3d 镜像按版本打 tag，新版本往往滞后。第一次真跑若卡在「找不到该 Unity 版本」，把 workflow 的 `unityVersion` 临时钉到镜像里已有的版本，或自建镜像。
- **fork / PR 拿不到 secret**：`pull_request` 来自 fork 时 secret 为空，上面那套「未配置则跳过」的逻辑会让它绿着跳过，这是预期的，别误以为测试跑过了。
- **付费额度**：public 仓库的 Actions 免费额度较宽松，但一次 EditMode 跑要下载并解压 Unity 镜像，通常 6–12 分钟，注意别在循环里反复触发。
- **不要提交 `.ulf`**：它是机器绑定的授权文件，属于凭据。仓库的 `.gitignore` 没有专门列它，放本机 `LocalPacks/` 或别处，只通过 secret 传递。

## 相关文档

- [命令行与出包](../operations/android-build.md)
- [编辑器菜单](../operations/editor-menus.md)
- [谱面与曲包](../architecture/chart-package.md)
