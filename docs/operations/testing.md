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
- `App/PlayServiceDependencyTests`：开局路径依赖全部换成假端口，覆盖四种前置拒绝（曲库未就绪、加载界面未就绪、请求目标不是曲库、当前不在曲库场景）、推入加载界面失败后的回滚与锁释放、开局成功后的持锁与场景退场释放、以及连续两局；并断言发布的 `FreeLiveBootData.DeckData.UserInfo.DeckId` 取自端口而不是单例。**动开局流程或 `PlayService` 的前置判断时必须跑它**。
- `LiveContextTests`：`LiveContext.FromRetainedBoot()` 把 `UserDataManager`、`EntryPoint.PlayMode`、`PlaySessionContext` 的五个事实逐项抄进上下文；缺 Boot 时其余字段仍完整，null 会话字段降级成空串，两次读取互不影响。用例在 `[SetUp]/[TearDown]` 里存旧值并写回，不会污染同一进程里的其它用例。
- `Resources/ResourceCatalogTests`：`ResourceCatalog` 里每条固定 `Resources` 路径都能加载、类型正确、回退只在主路径失败时生效；另含两条字体相关断言——`Fonts/SourceHanSansSC-Medium` 能被 TMP 建成字面、以及 `LibraryUi.EnsureRequiredGlyphs` 能对三条来源（bundled / OS / 项目资产）补上省略号字形。项目 SDF 资产在用例里是**克隆**后操作的，避免编辑器回写被跟踪的 `.asset`。

PlayMode：

- `LibrarySmokeTests`：载入 `Assets/Game/Scenes/Library.unity`，等 `LibraryScreen` 起来并断言 `ScreenManager.ExistsInstance`。
- **限制**：从编辑器桥接跑 PlayMode 时，进播放模式会触发域重载，桥接注册的回调随之销毁，`playmode.xml` 不会生成，编辑器会停在播放模式；跑完必须先 `editor.playmode.stop` 才能再跑 EditMode。要看报告请用 Test Runner 窗口，或关掉编辑器后跑 `unity test --mode PlayMode`。

## 两次运行一致性（P4-2）

`TestRunnerBridge`（菜单 `OpenSekai/Dev/Run EditMode Tests`，或在 `unity_eval` 里调 `TestRunnerBridge.RunEditMode()`）把报告写到 `TestResults/editmode.xml`；两次运行各存一份后比对：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\compare-test-runs.ps1 `
  -First TestResults/p4-2-editmode-run1.xml -Second TestResults/p4-2-editmode-run2.xml
```

脚本按「测试全名 + 结果」逐项比较，出现缺项、多项或结果变化就以非零退出，因此也可以直接进 CI。

2026-11-12 的结果：两次都是 **569 项 / 565 通过 / 0 失败 / 4 跳过**，逐项完全相同，耗时 8.11 s 与 8.11 s。4 个跳过全部来自已停放的 `Live/DecorationTailJudgmentTests`（原因见上文「覆盖范围」，替代用例是 `DecorationTailFromChartTests`）。

同轮做的隔离审计：

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

## 为什么没有「整首打歌」的自动化测试

P0-5 的显式用例 `LibrarySmokeTests.BaselineTapAutoplaysAndReturnsToTheLibrary` 使用仓库中的 `Tools/fixtures/baseline_tap`，通过结算面板的「再玩一次」触发 `PlayService` 自动开局，再读取 Core 控制器、返回后的曲库组件和结算结果。Unity Pipeline 的异步测试支持跨域重载和场景切换。

```powershell
unity command test run --caller plugin --skill unity-cli --mode playmode --filter LibrarySmokeTests --async_tests --include_explicit
unity command test status --caller plugin --skill unity-cli --format json
```

本地复现了完整场景往返后的计分缺口（`466,063 / C`，自动判定 103/221）。修正自动判定游标后，整首用例通过 `1,000,000 / S` 断言。默认 PlayMode 运行曲库装配冒烟，整首用例可按上方命令单独运行。后续接入见[结构重整计划](../architecture/restructure-plan-2026-11.md)和[真机门禁](android-build.md)。

## 待补

- `NumberView` 的数字位布局：目前埋在 MonoBehaviour 里，要测就得先把「哪位该显示、用哪张精灵」抽成纯函数。
- 视觉与手感（音符渲染、判定窗口）：单测覆盖不到，见上面的真机门禁。

## CI

- 本地/流水线通用入口：`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1`（先关掉编辑器，CLI 才肯起实例）。脚本把 nunit 报告写到 `TestResults/editmode-ci.xml`、打印 `total/passed/failed`，并以 Unity 的退出码收尾，失败即非零。
- GitHub Actions：`.github/workflows/editmode-tests.yml` 用 `game-ci/unity-test-runner@v4`、`unityVersion: auto`（读 `ProjectSettings/ProjectVersion.txt`）、`testMode: EditMode`，上传 `TestResults` 产物。

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
