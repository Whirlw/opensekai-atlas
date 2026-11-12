# Open Sekai Atlas 结构重整计划书

> 目的：把当前“旧引擎 + 新播放器外壳”的代码结构整理成边界清晰、可测试、可继续裁剪的工程，并让文档与真实目录重新一致。
> 适用读者：接手代码的开发者、做重构的人、发版前做门禁的人。
> 前置条件：读过 [架构总览](README.md) 与 [模块划分](modules.md)；当前基线为 `f14b717c`。
> 最后更新：2026-11-11
> 状态：**M1 已达标，Phase 2 执行中**。按任务依赖推进，每个工作包独立提交并记录验收结果。

## 1. 结论与目标

功能清理后的运行时包含 **443 个 `.cs` / 60,885 个物理行**，仍有以下结构问题：

- 文档还在描述 `Assets/OpenSekai/`、`Assets/Scripts/Assembly-CSharp/` 这类不存在的路径；
- 运行时代码只有两个程序集，`OpenSekai.Game` 一个就装了 413 个文件，纯逻辑和 Unity 引擎混在一起；
- `ScreenManager` 3,234 行、`LongNote` 1,346 行、`SoundManager` 1,204 行，仍是上帝对象；
- `Sekai/` 顶层还有 191 个扁平文件；
- `AcbDecoder` 有 12 个文件，仍由 `CriAtomShim` 的 ACB 加载分支引用，空目录还有 12 个；
- 资源加载仍分散在多处，启动和打歌仍依赖全局状态，部分测试依赖本地临时文件。

本计划的目标不是“再删多少行”，而是把下面五件事做成：

1. **文档说真话**：文档里的路径、页面数、包体数字与代码一致。
2. **程序集有边界**：纯逻辑 `OpenSekai.Core`、Unity 引擎 `OpenSekai.Game`、播放器 `OpenSekai.App`、编辑器 `OpenSekai.Editor`。
3. **大类型有职责**：`ScreenManager`、`LibraryScreen`、`LongNote`、`LiveLogic`、`SoundManager`、`SusRuntimeConverter` 拆到可维护的规模。
4. **资源加载有入口**：`Resources.Load` 字符串收敛到一个类型化目录，不再散落。
5. **测试可重复**：谱面样本随仓库提交，纯逻辑测试可在 Unity Test Runner 中脱离引擎 API、场景和 prefab 运行。

## 2. 当前基线（2026-11-11，`f14b717c`）

| 指标 | 当前值 |
| --- | ---: |
| 运行时 `.cs` | 443 |
| 运行时行数 | 60,885 个物理行，其中 52,703 个非空行 |
| 运行时程序集 | 2（`OpenSekai.Game` 413 文件、`OpenSekai.App` 30 文件） |
| `Sekai/` 顶层 `.cs` | 191 |
| 最大文件 | `ScreenManager.cs` 3,234 行 |
| 第二/第三 | `LongNote.cs` 1,346、`SoundManager.cs` 1,204 |
| `Resources.Load*` | 48 个匹配行，含同步、异步和批量加载 |
| 全局单例/静态入口 | 7 个以上 |
| 空代码目录 | 12 个 |
| ACB 解码器 | `Platform/AcbDecoder`，12 文件 / 1,006 行，调用链为 `SoundManager → CriAtomShim → AcbDecoder` |
| EditMode 测试文件 / PlayMode 测试文件 | 54 / 1 |
| 当前 EditMode 结果 | 508 passed / 4 failed / 4 skipped |

统计口径：运行时文件以 `git ls-files 'Assets/Game/Runtime/*.cs'` 为准，单文件规模统一按非空行计；`Resources.Load*` 按该目录下 `.cs` 文件中的文本匹配行计，调用清单以 P3-2 的实际调用点为准。

基线来自 `post-text-cleanup-editmode.xml` 和 `post-text-cleanup-editmode-2.xml`，两次结果一致：

- 3 个 `HeadSettlementTests` 因 `Temp/test16-score.json` 缺失而失败，P0-4 改用随仓库提交的谱面 fixture。
- `LongNoteGraceTests.AFingerReturningInsideTheGraceTakesTheBarBack` 触发了长条宽限期断言。P0-4 对照现行规则和最近通过的版本确认原因，文件头的旧注释仅作线索。

## 3. 目标结构

### 3.1 程序集

```text
OpenSekai.Core     纯 C#，noEngineReferences = true
      ▲
OpenSekai.Game     Unity 运行时：屏幕、音频、AB、UI、Live 内核与视图
      ▲
OpenSekai.App      播放器外壳：Bootstrap、Library、Playback
      ▲
OpenSekai.Editor   构建、素材、菜单、测试桥

OpenSekai.Tests.Core      只引用 Core
OpenSekai.Tests.EditMode  引用 Core/Game/App
OpenSekai.Tests.PlayMode  引用 Game/App
```

规则：

- `Core` 不得出现 `UnityEngine`、`UnityEditor`、`Resources`、`MonoBehaviour`。
- `Game` 不得引用 `App`。
- `App` 不得引用 `Editor`。
- 测试程序集不 `autoReferenced`。
- 新增跨层引用必须走 ADR，不允许“先加了再说”。

### 3.2 目录

```text
Assets/Game/Runtime/
  OpenSekai.Core/
    Charts/              ChartDocument、谱面序列化
    Scoring/             JudgeWindow、ScoreGauge、JudgeTuning
    Simulation/          InputTimeMapping、CandidateOrder 等纯模拟
    Settings/            LiveSettingData DTO、PlaybackSettingsSnapshot
    Packaging/           曲包契约与 manifest DTO
  OpenSekai.Game/
    Screens/             ScreenStack、Dialog、Transition、Header
    Audio/               SoundManager 门面、AudioCatalog、ExternalSoundPack
    Assets/              AssetBundleManager、清单、加载器
    UI/                  通用 UGUI 组件
    Animation/           动画组件与适配
    Data/                Unity 侧数据资产和读写
    Platform/            CriWare、文件选择、Android 适配
    Live/
      Runtime/           LiveLogic、控制器、输入、判定
      View/              FrontUIView、Background2DView、HUD
      Notes/             NoteBase、LongNote、各类尾
  OpenSekai.App/
    Bootstrap/
    Library/
    Playback/
```

历史设计稿 `docs/architecture/plan/` 移到 `docs/history/`，不再作为现行架构说明。

## 4. 工作原则

1. **先边界，后搬家**：先建立程序集和依赖规则，再移动文件；不要一边改行为一边搬目录。
2. **保留 GUID**：所有移动用 `git mv`，连同 `.meta` 一起移动；禁止删除后重建。
3. **一次一件事**：每个任务独立提交，提交信息写清“移动/拆分/改名”，不混行为改动。
4. **每步可编译**：涉及运行时或资源的提交使用仓库指定的 Unity 版本，记录编译结果与测试报告。
5. **序列化兼容**：改字段名用 `[FormerlySerializedAs]`。移动类型时同时核对脚本 GUID、程序集限定类型名、`link.xml` 和 JSON 类型元数据，并用既有数据样本验证读取结果。
6. **文档同步**：移动或删除文件时，同一次提交内更新文档路径与 `docs/INDEX.md`。
7. **测试先行**：拆 `ScreenManager`、`LongNote` 之前，先补对应行为测试；拆完测试必须保持绿。

## 5. 阶段计划

### Phase 0：文档对齐与基线

**P0-1 文档对齐**

- 核对现行文档中指向旧路径的引用，逐条确认替换目标：
  - `Assets/OpenSekai/` → `Assets/Game/Runtime/OpenSekai/`
  - `Assets/Scripts/Assembly-CSharp/` → `Assets/Game/Runtime/Sekai/`
  - `Assets/Editor/` → `Assets/Game/Editor/`
- 更新页面数：`7 个可达页面` → `3 个 ScreenLayerData + LibraryScreen`。
- 更新现行文档里的包体记录，标明对应的 APK 文件、构建版本、构建选项和测量单位。历史版本的测量值保留原上下文。
- 删除已删功能的描述：`InsertNoti`、`ScreenLayerLoading`、`NetworkIndicator`、`AssetRemoving`、`3D MV`、`MultiLive`、`MySekai`、`Gacha`。
- 历史设计稿移到 `docs/history/`，`docs/INDEX.md` 标注“历史”。
- 修 `docs/INDEX.md` 末尾拼错的表格行（现有几行挂在“相关文档”之后）。

验收：现行文档中的项目路径可解析，历史文档的相对 Markdown 链接随迁移更新；全量本地链接检查通过，并按快速开始、曲包导入、测试、Android 构建和发布五条路径逐项核对。

**P0-2 空目录与死平台清理**

- 核对以下 12 个目录为空后移除目录及其 `.meta`，路径均相对 `Assets/Game/Runtime/`。
  - `CustomMusicScoreManager`
  - `OpenSekai/Charts/Formats`
  - `OpenSekai/CustomMusicScoreManager/{Controllers,Freecam,Helpers,UI}`
  - `OpenSekai/Infrastructure/{Audio,Settings}`
  - `Sekai/{APIData,Honor,UI/Interface}`
  - `Platform/OverdrawForURP`
- 仓库内 ACB 资产为 0，`SoundManager` 仍通过 `CriAtomShim` 保留 ACB 加载分支。将解码器清理移至 P2-5，随该分支一起评估，并验证 WAV 与曲包音频。
- 评估 `Platform/Beebyte` 与 `Platform/MessagePack`：
  - `Beebyte` 仍被 5 个文件用 `[Skip]`，保留现有标记存根，注明当前构建使用的是本地存根。
  - `MessagePack` 接口仍被 5 个 Master 类实现，保留，但把目录改名为 `Platform/Serialization`，避免误以为依赖 MessagePack 包。

验收：清单中的空代码目录已移除；Unity 编译 0 error；与 P0-3 记录的缺失脚本基线相比，新增 0 处。

**P0-3 生成文件与检查脚本**

- 本地 `Assembly-CSharp.csproj` 有 890 个 Compile 条目、361 个已不存在；删除后让 Unity 重新生成，不要提交。
- 确认 `.gitignore` 覆盖根目录 `*.csproj`、`*.sln`、`TestResults/`、`Builds/`、`Temp/`。
- 把文档链接和空代码目录检查写成 `Tools/verify-structure.ps1`。通过 Unity 解析相关场景、prefab 和资产，记录缺失脚本的资产路径与 GUID；重点核实 `XRSystemData.asset` 和 `EventSystem.prefab` 两个候选。删除资产前保存这份基线，Phase 4 接入 CI。

验收：重新生成的 IDE 项目没有失效源文件引用；脚本可重复运行；缺失脚本清单可供后续阶段逐项比较。

**P0-4 建立可重复的 EditMode 基线**

- 为三个 `HeadSettlementTests` 提交最小谱面 fixture，保留原测试需要的重叠长条和速度倍率场景。优先复用 `Tools/fixtures/hold-overlap-regression/`，先确认它能覆盖各用例的前置条件。需要落盘的测试使用独占临时目录，并在 `TearDown` 清理。
- 单独复现 `AFingerReturningInsideTheGraceTakesTheBarBack`，核对宽限期规则和最近通过的版本。确认是回归时单独修复并保留原断言；确认是未交付契约时，登记具体测试名、原因和跟踪项。结论未确认前暂停 P2-3 的长条拆分。
- 从干净检出用 [现有 EditMode 脚本](../../Tools/run-editmode-tests.ps1) 连续跑两次，分别指定 `-ReportPath`，保存命令、Unity 版本、两份 XML 报告及跳过用例清单。后续以这份基线比较用例结果。

验收：三例可脱离本地 `Temp` 运行；两次结果一致；所有失败或跳过均有具体用例与原因，新增失败为 0。

**P0-5 验证整首自动化测试的运行方式**

- 从现有 `LibrarySmokeTests` 出发，使用 `Tools/fixtures/baseline_tap` 验证跨 `Library → Core → Library` 的测试运行方式。先解决 [测试文档记录的跨场景销毁问题](../operations/testing.md#为什么没有整首打歌的自动化测试)，用实际场景和组件状态断言启动、结算和返回。
- 保存运行命令、测试报告和所需资源准备步骤。原型通过后作为 P4-3 的输入；若仍受 Test Runner 生命周期阻塞，记录失败证据，继续保留现有曲库 PlayMode 冒烟和 [Android 整首验收](../operations/android-build.md)。

验收：有一份可复现的原型结果。Phase 1 依据 EditMode 和 Android 基线验收，整首 PlayMode 接入由原型结果决定。

### Phase 1：程序集边界

**P1-1 建立 `OpenSekai.Core`**

- 新增 `Assets/Game/Runtime/OpenSekai.Core/OpenSekai.Core.asmdef`，设置 `noEngineReferences: true`。首批迁移 `Shared/Live/Simulation/InputTimeMapping.cs`，保留命名空间，`OpenSekai.Game` 增加对 Core 的引用。App 和 Editor 在直接使用 Core 类型的提交中增加引用。
- 为后续每批列出文件、直接依赖和归属。按下面的拆分点逐批迁移：

| 候选 | 迁移前提 |
| --- | --- |
| `SimTouch` | 当前依赖 Input System 的 `TouchPhase`，先留在 Game。纯模拟需要触摸数据时，由 Game 在边界把原生输入转成 Core 数据。 |
| `CandidateOrder`、`JudgeWindow`、`JudgeTuning`、`ScoreGauge` | 先核对并迁移各自使用的纯判定枚举和数据结构，例如 `NoteResult`、`JudgeTimeData`。逐个排除 Unity 和 Game 依赖，保留判定枚举值和窗口边界。 |
| `LiveSettingData`、`PlaybackSettingsSnapshot` | 先把 `LoadFromStorage`、`SaveToStorage` 调用归到 Game 的 `LiveSettingsStore`，再迁移 DTO 和快照。保留设置字段、缺省值和旧数据读取。 |
| `Shared/SUS`、`SusRuntimeConverter` | SUS DTO 按依赖迁移，`NoteInfo` 随其用到的纯谱面枚举一起处理。转换器在 P2-6 拆出纯阶段后再迁移，运行时 `NoteBase` 构建留在 Game。 |

每批验收：Unity 编译通过，迁移类型的公开行为有原测试或等价断言覆盖，Core 依赖检查通过。涉及反射序列化的批次同时执行 P1-4 的兼容检查。

**P1-2 测试拆分**

- 新增 `Assets/Tests/Core/OpenSekai.Tests.Core.asmdef`，作为 Unity EditMode 测试程序集。项目程序集只引用 Core，测试框架沿用现有 NUnit 和 Unity Test Runner 配置。
- 先迁移或提取直接测试 `InputTimeMapping` 的用例。`JudgeWindowTests`、`ScoreGaugeTests`、`CandidateOrderTests` 随依赖迁移；SUS 纯转换用例随 P2-6 迁移。按测试实际依赖拆分混合文件。
- 保留 Game/App 行为测试，包括场景、prefab、资源、输入、Live 视图和运行时音符构建。核对内部 API 的测试可见性与原用例覆盖。

**P1-3 依赖门禁**

- `Tools/verify-structure.ps1` 核对 Core 的 `noEngineReferences`、测试程序集的 `autoReferenced` 和程序集依赖方向。解析按名称和 GUID 声明的引用，检查 Core 无 Unity 或项目上层依赖、Game 无 App/Editor 依赖、App 无 Editor 依赖。
- 每批迁移运行该脚本和 Unity 编译；Phase 4 接入 CI。

**P1-4 迁移谱面与曲包契约，验证 Android 兼容**

- 按 P1-1 的依赖清单迁移纯 `ChartDocument` 模型、`ChartJson` 和曲包 manifest DTO。先处理模型依赖的谱面类型，保留 Game 中负责资源读取或构造运行时音符的代码。
- 对照现有 `Assets/link.xml` 的 `Assembly-CSharp` 条目逐项修正程序集和完整类型名。迁移时同步检查受影响的配置资产和 prefab，以及 `ChartJson` 对既有 `$type`、`$id`、`$ref` 元数据的读写行为。
- 提交或复用现有旧谱面、设置、曲包清单和曲库历史样本。用 EditMode 验证读取和写回，再用 Android IL2CPP 包覆盖安装，验证既有数据和曲库可读、既有曲包可开局及结算。

Phase 1 验收：Core 无 Unity API 及上层依赖，Core 测试可在 Unity EditMode 下脱离场景和 prefab 执行，依赖检查通过，迁移后的 Android 包通过旧数据兼容检查。

### Phase 2：拆分大型类型

按“先补测试、再拆、行为不变”的顺序做，每次只拆一个类。

**P2-1 `ScreenManager`（3,234 行）**

拆成四个服务 + 一个门面：

| 新类型 | 职责 | 目标 |
| --- | --- | --- |
| `ScreenStackService` | `screenMap`、Add/Push/Exit/Back、排序、`GetLayerComponent` | < 600 行 |
| `DialogService` | `Show*Dialog`、队列、`ChainDialogPlayer`、`DialogType` | < 500 行 |
| `TransitionService` | `ColorFader`、模糊、`UIBlurLayerManager`、`ScreenCameraStack` | < 500 行 |
| `HeaderService` | `HeaderUtility`、`ScreenLayerHeader`、HeaderCategory | < 300 行 |
| `ScreenManager` | 兼容门面，转发调用 | < 800 行 |

验收：现有公开方法签名不变；EditMode 场景测试全绿；`ScreenManager` 行数 < 800；无新增单例。

**P2-2 `LibraryScreen`（881 行）**

- `LibraryView`：UGUI 构建、行池、布局、动画。
- `LibraryPresenter`：选择、导入、设置、开始游玩、结果回填。
- `LibraryModel`：条目、筛选、排序、历史。
- `LibraryScreen`：MonoBehaviour 生命周期与依赖装配。

验收：每类 < 350 行；曲库导入、手动/自动开局、结算返回行为不变；新增 Presenter 单测（不依赖场景）。

**P2-3 `LongNote`（1,346 行）**

- 保留 `LongNote` 门面与状态机。
- 复用现有 `LongNoteHead` 和 `Live/Tails/` 的四种尾行为；分离条体状态、尾判和显示回收职责，沿用现有的状态与判定契约。
- P0-4 确认宽限期契约后，先补 `LongNoteGraceTests`、`TailReleaseLaneTests`、`HoldQuotaTests` 的缺失覆盖，再移动代码。

验收：`LongNote` < 500 个非空行；长条测试满足 P0-4 基线；按 [真机验收清单](../operations/device-acceptance-checklist.md) 核对宽限期内回按、换手、双长条重叠和尾判时序。

**P2-4 `LiveLogic`（933 行）**

- `LiveClock`：音乐时间、帧时间、暂停。
- `LiveInputRouter`：触摸源、候选、去重。
- `JudgmentScheduler`：判定循环、窗口、结果发布。
- `LiveSessionState`：会话状态、统计。
- `LiveLogic` 只做协调。

验收：每类 < 400 行；`LiveLogicInputTests`、`JudgeFrameTableTests`、判定相关测试全绿。

**P2-5 `SoundManager`（1,204 行）**

- `AudioCatalog`：bundle/cue 名单、预解码列表。
- `ExternalSoundPack`：`manifest.csv`、wav 注册、加载。
- `VolumeService`：BGM/SE/Voice 音量与持久化。
- `SoundManager`：门面与播放调度。
- 评估删除 `CriAtomShim` 的空实现分支；如果外部 wav 已完全覆盖，保留最小 shim。
- 跟踪 `SoundManager` 到 `CriAtomShim` 的 ACB 调用，验证内置 WAV、曲包音频和缺失资源处理后，再清理确认可移除的分支及 `Platform/AcbDecoder`。

验收：`SoundManager` < 500 行；42 个 wav 的 `AUDIT OK` 不变；真机开局/结算音效无回归。

**P2-6 `SusRuntimeConverter`（1,158 行）**

- 拆 `SusParser`、`SusTimingConverter`、`SusEventConverter`、`SusNoteConverter`。纯阶段以 DTO 为输入和输出，按 P1-1 迁入 Core；运行时音符与 Unity 对象的组装留在 Game。
- 同一输入对照拆分前后的时间戳、事件顺序、音符类型和长条连接关系。已有转换测试继续覆盖运行时产物，纯阶段单独验证。

验收：每类 < 400 行；SUS 相关测试全绿。

### Phase 3：目录、命名与资源入口

**P3-1 `Sekai/` 顶层收敛**

- 在 `Assets/Game/Runtime/OpenSekai.Game/` 下建立 `Screens`、`Audio`、`Assets`、`UI`、`Animation`、`Data` 等职责目录，分批移动现有 `Sekai/` 顶层代码。
- 移动 `OpenSekai.Game.asmdef` 时保留 `.meta`，核对所有归属 Game 的源码均位于其作用范围内；单独移动 App 的目录与 asmdef，确保每批之后编译通过。
- `Sekai/Core/Live` 与 `Sekai/Live` 合并为 `Live/{Runtime,View,Notes}`，消除“Core 里有 Live、Live 里有 Logic”的分裂。
- 保留 `Sekai` 命名空间不动，只动目录；避免一次性改命名空间导致大量 serialized 引用与文档失效。

验收：`Sekai/` 顶层 `.cs` < 20；每个子目录有 README 或职责说明；文件移动保留 `.meta`，缺失脚本仍为 0 新增。

**P3-2 `ResourceCatalog`**

- 先清点运行时代码里的 `Resources.Load`、`LoadAll`、`LoadAsync` 调用，记录资源类型、来源、路径构造方式、同步或异步、查找顺序和缺失行为。
- 固定资源走 `ResourceCatalog`，为页面 prefab、Live 视图、字体、材质和固定配置定义稳定的类型化 ID。明确启动时如何取得目录本身，以及曲库和 Live 场景如何共用它。
- 动态资源继续通过 `ResourceLoader` 或 `AssetBundleUtility`，保留现有动态键、AssetBundle 与 Resources 回退优先级、大小写回退、批量及异步行为。静态条目和动态加载分批迁移。
- `verify-resource-paths.ps1` 继续检查字面路径。用 Unity 加载测试逐条核对目录 ID 和资源类型，并用代表性用例覆盖动态路径、回退命中及全部缺失。

验收：运行时 `Resources.Load*` 只出现在列出的目录和加载器实现中；曲库、打歌、结算能加载所需资源；动态回退、异步、批量和缺失行为的测试通过。

**P3-3 命名与 typo**

- `ScreenLayerExtention` → `ScreenLayerExtension`（目录、文件、namespace 同步）。
- `CustomMusicScoreManager` 中与玩家曲库相关的存储代码归入 App 的 `Library/Storage`；按 P3-1 的文件归属清单核对共享类型后移除空壳。
- `Platform/CP` 中继续使用的平台适配代码归入 Game 的 `Platform/Common`，在目录说明中记录旧路径与职责。
- `RankLive/NumberView`：如果只是通用数字组件，移到 `UI/NumberView`；不要用一个玩法名目录装通用组件。
- `Sekai` 命名空间是否改成 `OpenSekai.Game`：单独开 ADR，不混在本次目录整理里。

验收：目录名与职责一致；`ScreenLayerExtention` 0 处；文档同步。

**P3-4 动画入口统一**

- `TweenBase` 已使用 DOTween。由 Inspector 配置和驱动的动画继续使用现有 `Tween*` 组件，代码管理生命周期的动画沿用 DOTween。
- 在 `docs/coding-standards` 记录这两个入口的适用场景、现有调用以及动画随组件禁用、销毁或场景切换时的清理责任。

验收：新增或迁移的动画有明确的入口和生命周期归属，既有 Inspector 引用和动画行为保持一致。

### Phase 4：状态、测试与发布门禁

**P4-1 单例收敛**

- `LibraryEntryPoint` 装配 App 层的 `AppContext`。`LibraryScreen` 和 `PlayService` 只接收各自需要的依赖。
- `SoloLiveController` 和 `LiveLogic` 使用 Game 层的 `LiveContext`，沿现有 `LegacyLiveBootAdapter` 和 `FreeLiveBootData` 开局边界传递本局依赖。由 Game 持有其使用的接口，保持依赖方向为 App → Game。Unity 组件在现有初始化入口接收依赖，普通服务可用构造参数。
- 明确依赖的生命周期。开局失败时恢复原有启动状态，退出、返回曲库和重新开局时释放上一局的引用。迁移选定调用点，其余调用点继续经兼容门面运行。

验收：迁移的调用点通过显式依赖访问服务；EditMode 可替换服务实现；验证开局失败后重试、退出返回和连续两局，且 Game 不引用 App。

**P4-2 核对测试隔离**

- 复用 P0-4 的基线，从干净检出核对测试所需的谱面、设置和历史样本。需要写入文件时按用例使用独占临时目录，并在结束后清理；报告统一输出到 `TestResults/`。
- 核对基线登记的跳过项。原因已消除的用例恢复执行，仍未交付的行为保留具体跟踪项。

验收：连续跑两次 EditMode，结果一致；无环境型失败。

**P4-3 PlayMode 冒烟**

- P0-5 原型通过后，接入整首 PlayMode 测试。准备 `Tools/fixtures/baseline_tap`，从 `Library.unity` 启动，断言曲库入口就绪、基线谱自动结算为 `1,000,000 / S`、返回曲库。读取场景和结果状态，编辑器菜单日志仅用于诊断。
- 若整首自动化仍受跨场景生命周期阻塞，CI 执行现有曲库 PlayMode 冒烟，整首流程由 Android 发布验收覆盖。记录阻塞证据和后续跟踪项。

验收：记录 CI 实际覆盖的 PlayMode 范围和报告；整首流程保留可复核的自动化或 Android 验收记录。

**P4-4 结构门禁与文档检查**

- `Tools/verify-structure.ps1` 检查：
  - 运行时代码目录无遗留空壳；
  - Core 无 Unity 引用；
  - 程序集依赖方向；
  - 运行时 `Resources.Load*` 白名单；
  - Markdown 本地链接和现行文档中的项目路径；
  - `ScreenManager`、`LibraryScreen`、`LongNote` 的非空行上限，沿用各任务的同一口径。
- 修复 P0-3 确认的缺失脚本。对 `XRSystemData.asset`、`EventSystem.prefab` 先确认解析结果和资产用途，再决定是否修改。用同一 Unity 扫描复核，相关资产的缺失脚本为 0。
- 接入 `.github/workflows`。结构脚本独立于 Unity 许可证执行；Unity 测试和资产扫描记录实际运行、通过或因许可证缺失而跳过的状态。发布验收需要实际通过的 Unity 报告。

验收：各门禁均有可追溯结果，新增违规使对应检查失败。

## 6. 任务清单与依赖

| ID | 任务 | 直接依赖 | 交付与验收证据 |
| --- | --- | --- | --- |
| P0-1 | 文档对齐 | P0-3 的链接检查 | 现行路径和本地链接通过，五条流程核对记录 |
| P0-2 | 清理空目录和平台存根目录 | P0-3 的缺失脚本基线 | 编译通过，缺失脚本新增 0 |
| P0-3 | 生成项目和基线检查 | 当前基线 | 可重复运行的脚本、缺失脚本清单 |
| P0-4 | 建立 EditMode 基线 | 当前基线 | 仓库 fixture、两份 XML、宽限期用例结论 |
| P0-5 | 验证整首自动化运行方式 | 现有曲库冒烟、基线谱 | 原型命令和报告，或可复现的阻塞证据 |
| P1-1 | 建立 Core，分批迁移 | P0-3、P0-4 | asmdef、依赖清单、每批编译和行为测试 |
| P1-2 | 拆分 Core 测试 | P1-1 的对应类型 | Tests.Core asmdef，既有覆盖可追溯 |
| P1-3 | 核对程序集边界 | P0-3、P1-1 首批 | 依赖检查加入结构脚本 |
| P1-4 | 迁移谱面及曲包契约 | P1-1 的相关依赖、P1-3 | 类型与序列化映射、Android 旧数据验收 |
| P2-1 | 拆 ScreenManager | M1 | 门面兼容，场景行为测试通过 |
| P2-2 | 拆 LibraryScreen | M1 | Presenter 测试和曲库流程通过 |
| P2-3 | 拆 LongNote | M1、P0-4 宽限期结论 | 长条回归和真机判定清单 |
| P2-4 | 拆 LiveLogic | M1；与 P2-3 共用判定代码时串行 | 输入、判定顺序和统计测试通过 |
| P2-5 | 拆 SoundManager | M1 | 音频审计和真机开局、结算通过 |
| P2-6 | 拆 SusRuntimeConverter | M1 | 纯阶段测试与运行时转换等价验证 |
| P3-1 | 移动目录 | M2 | `.meta` 成对迁移、编译和缺失脚本扫描 |
| P3-2 | 收敛资源入口 | P3-1 | 加载调用清单、目录校验、动态回退测试 |
| P3-3 | 统一目录与命名 | P3-1 | 更新引用、序列化检查和文档链接 |
| P3-4 | 明确动画入口 | P3-1 | 使用规范、既有调用清单和生命周期检查 |
| P4-1 | 收敛启动和会话依赖 | M3 | 显式依赖，失败重试和连续两局通过 |
| P4-2 | 核对测试隔离 | P0-4、P4-1 | 干净检出连续两次测试结果一致 |
| P4-3 | 接入 PlayMode 冒烟 | P0-5、P4-1 | 实际冒烟范围、测试报告和整首验收记录 |
| P4-4 | 接入结构与发布门禁 | P1-3、M3、P4-1 至 P4-3 | CI 结果、缺失脚本为 0、发布验收记录 |

按直接依赖领取任务。每个工作包记录基线提交、改动范围、验证命令和结果；多个任务涉及相同文件或 Unity 工程状态时按顺序执行。阶段里程碑见 §10。

### Phase 0 执行记录（2026-11-11）

工作分支为 `codex/long-note-head-split`，起点 `4249628b`。

| 任务 | 当前结果 | 验收缺口 |
| --- | --- | --- |
| P0-1 | 已对照源码、设置、注册表与 APK 更新文档，核对快速开始、曲包导入、测试、Android 构建和发布五条路径。 | 110 份 Markdown 的本地链接及锚点检查通过，`docs/` 文档均已入索引。 |
| P0-2 | 核实 12 个空目录、五处 `[Skip]` 和五个序列化接口实现。发现 ACB 解码器仍被调用，已调整其清理顺序。 | 等 P0-3 缺失脚本基线后再移动或删除。 |
| P0-3 | 结构脚本确认忽略规则及失效的 IDE 项目引用，其中 `Assembly-CSharp.csproj` 有 361 项。新增 Unity 缺失脚本扫描入口。 | Unity 授权服务连接超时，扫描与 IDE 项目重新生成待验证。 |
| P0-4 | 三个头判用例已改用仓库内合成谱面，Editor 和 EditMode 代码通过 Unity 自带 Roslyn 类型检查。 | Unity EditMode 报告和宽限期修复验证待完成。 |
| P0-5 | 已核对曲库冒烟入口与 `baseline_tap` 准备步骤。现有 PlayMode 冒烟在启动时因授权连接超时。 | 需要可运行的 PlayMode 环境复现跨场景问题，再验证原型。 |

两份历史 EditMode 报告中，宽限期用例都通过了 `HasFinger` 断言，失败点是回按后仍显示释放态。上一份已找到的通过记录是 `TestResults/grace50ms.xml`。
`e2d6c125b` 为防止过线后的长条回到 `Scrolling` 并在下一帧再次进入宽限期，保留了过线后的 `Last/Release` 状态。
现用例还直接调用私有阶段，使用共享帧时钟。复验应使用真实谱面和 `LiveHarness`，同时覆盖回按后显示与尾判，避免恢复会重复进入宽限期的旧行为。

当前验证日志位于 `TestResults/restructure-grace-before.log`、`TestResults/restructure-missing-scripts.log` 和 `TestResults/restructure-library-smoke.log`。三次 Unity 运行均在授权服务初始化时超时，未生成扫描清单或新的测试 XML。

上轮环境的 Git 索引为只读。本轮已在原分支提交 P0-1（`ea184b93`），包含 `docs/history/` 下迁移的七份文档。运行 `Tools/verify-structure.ps1` 复核，110 份 Markdown 的本地链接和锚点通过。

#### 本地 Unity 复验

- P0-3 通过。`unity status --project-path E:\DeepseekHarness\OpenSekai --format json` 在允许访问本机编辑器后返回 `ready`，版本为 `6000.6.7f1`。通过 `unity command eval --caller plugin --skill unity-cli` 执行 `OpenSekai.EditorTools.MissingScriptAudit.Run()`，生成 `TestResults/missing-scripts.tsv`，清理前副本为 `TestResults/missing-scripts-before-cleanup.tsv`。
- `XRSystemData.asset` 的脚本 GUID 无映射，`LoadMainAssetAtPath` 返回空。`EventSystem.prefab` 的两条引用使用同一无映射 GUID、不同 fileID。Unity 的根对象缺失组件计数返回 0，序列化扫描仍检出这两条引用。直接解析见 `TestResults/restructure-p0-3-resolve.json`。三条引用保留至 P4-4 核对用途。
- 移开四份含失效引用的旧项目文件至 `TestResults/restructure-p0-3-old-projects/`，通过 CLI 调用 `Unity.CodeEditor.CodeEditor.Editor.CurrentCodeEditor.SyncAll()` 重新同步，再运行 `Tools/verify-structure.ps1 -CheckProjects`，失效源码引用为 0。检查输出为 `TestResults/restructure-p0-3-project-check.txt`。

- P0-2 通过。通过 `unity command eval_file` 执行 `TestResults/restructure-p0-2-cleanup.cs`，删除 12 个空目录及其 `.meta`，将 `Platform/MessagePack` 移到 `Platform/Serialization`，接口 GUID 保持 `daf6a325cfd18fad813b65bd79814943`。Beebyte 存根继续提供本地 `[Skip]`。Unity 编译 0 error（`TestResults/restructure-p0-compile.json`）。重新执行 `MissingScriptAudit.Run()` 后，`TestResults/missing-scripts-after-cleanup.tsv` 与清理前副本逐字节相同，新增 0。移开旧的本地检查项目文件后，`Tools/verify-structure.ps1 -CheckProjects -RequireNoEmptyDirectories` 通过（`TestResults/restructure-p0-2-structure.txt`）。

- P0-4 通过。定向 `unity command test run --mode editmode --filter HeadSettlementTests` 为 3/3 通过（`TestResults/restructure-p0-4-head-before.json`）。旧宽限期用例单跑会通过。换成仓库谱面和 `LiveHarness` 后稳定复现回按有手指但仍显示灰色。修复仅令当前手指覆盖恢复亮态，保留 `e2d6c125b` 的过线后 `Last/Release`，并验证回按手指抬起取得 `JustPerfect` 尾判。定向 `--filter LongNoteGraceTests` 为 5/5 通过，复现和修复后报告分别在 `TestResults/restructure-p0-4-grace-real-before.json` 和 `TestResults/restructure-p0-4-grace-final.json`。提交为 `1d625591`（头判 fixture）和 `9967b333`（宽限期）。
- 在干净检出 `TestResults/restructure-clean-editmode`（`9967b333`）使用 Unity `6000.6.7f1`，连续运行 `Tools/run-editmode-tests.ps1 -ProjectPath $checkout -ReportPath TestResults/restructure-p0-4-a.xml` 和同命令的 `-b.xml`。两份 NUnit XML 已复制到主工作区的 `TestResults/`，均为 512 通过、0 失败、4 跳过。逐项核对全名、结果、标签和跳过原因，共 516 例一致。与历史报告相比，无新增失败或跳过。四个跳过来自 `DecorationTailJudgmentTests`，现有手造音符无法进入候选，需改用转换后的谱面。用例名及比较见 `TestResults/restructure-p0-4-comparison.txt`。首次导入在隔离检出中更新了 4 份 Unity 资源或设置，主工作区未采用这些变更。
- M0 已达标。P0-1、P0-2、P0-3 的提交分别为 `ea184b93`、`f95b32c1`、`5fdec47e`。进入 Phase 1。

- P0-5 已取得可复现结果。用 `unity command test run --caller plugin --skill unity-cli --mode playmode --filter LibrarySmokeTests --async_tests --include_explicit` 启动，再用 `unity command test status --caller plugin --skill unity-cli` 读取结果。曲库装配冒烟通过。整首原型直接读取仓库 `Tools/fixtures/baseline_tap` 的 manifest、谱面、音频和封面，经 `PlayService` 启动，断言 Core 控制器、返回曲库及结算组件。两次均完成 `Library → Core → Library`，结算为 466,063 / C，自动判定 103/221，Miss 为 0。验收缺口为自动游玩的 1,000,000 / S，跟踪至 P4-3。原型保留为显式用例，默认曲库冒烟照常运行。报告为 `TestResults/restructure-p0-5-first.json` 和 `TestResults/restructure-p0-5-status.json`。自动判定游标每次调用后越过长条是待验证的排查点。

- P0-5 后续复验通过。对照 `b9aa453d` 前后的 `OnAutoInput`，发现自动判定开始写回场景的已完成游标，长条头已处理便从游标前消失。修复后判定扫描每帧从已完成游标读取，长条仍参与后续帧。沿用上面的异步命令，曲库装配和整首原型 2/2 通过，整首达到 1,000,000 / S；报告为 `TestResults/restructure-p0-5-autoplay-status.json`。全量 EditMode 为 512 通过、0 失败、4 跳过，结构和缺失脚本检查通过。原型的默认 CI 接入仍归 P4-3。PlayMode 生成的字体纹理数据已撤回，两份 Medium 字体资产恢复到开工时记录的大小，未纳入提交。开工时未记录文件哈希。

### Phase 1 执行记录（2026-11-11）

- P1-1 首批完成。Unity CLI 编译 0 error，并在编辑器中确认 `InputTimeMapping` 归属 `OpenSekai.Core`。该类型只有基础数值运算，无项目或 Unity 依赖。脚本 GUID 和命名空间保留，Game 引用 Core，原 LiveLogic 入口继续转发。定向 `JudgeFrameTableTests` 11/11、`LiveLogicCharacterizationTests` 5/5 通过。编译、归属和测试报告在 `TestResults/restructure-p1-1-*.json`。

- P1-2 首批通过。将四个时间映射用例从两份混合测试迁入 `OpenSekai.Tests.Core.InputTimeMappingTests`，保留 LiveLogic 实际帧输入和设置开关用例。Unity 编译 0 error，`test run --mode editmode --filter InputTimeMappingTests` 为 4/4。通过 `TestRunnerBridge.Run("EditMode", reportPath)` 生成 `TestResults/restructure-p1-2-editmode.xml`，全量仍为 512 通过、0 失败、4 跳过，按四例迁移后的全名对照 M0 结果一致。

- P1-3 通过。`Tools/verify-structure.ps1 -CheckAssemblies -RequireNoEmptyDirectories` 校验 Core 的 Unity 引用限制、Game/App 的依赖方向和测试程序集的自动引用设置。在隔离样本中分别用名称和 GUID 让 Game 引用 App，两次均按预期返回 1，详见 `TestResults/restructure-p1-3-probe.txt`。

- P1-1 / P1-2 候选排序和计分批次通过。`CandidateOrder` 随 `NoteResult` 迁入 Core，`ScoreGauge` 随 `ScoreRank`、`LiveScore`、`MasterPlayLevelScore` 迁入，均只使用基础类型。Game、App 及直接调用的测试程序集按需引用 Core。两个测试类随实现移动，保留用例全名和脚本 GUID。通过 `unity recompile`、`Tools/verify-structure.ps1 -CheckAssemblies -CheckProjects -RequireNoEmptyDirectories`、缺失脚本清单比对和全量 EditMode。512 通过、0 失败、4 跳过，逐项与上一批一致。报告在 `TestResults/restructure-p1-1-scoring-*`。

- P1-1 / P1-2 判定批次通过。`JudgeWindow` 随 `JudgeTimeData`、`JudgeFrameType`、`NoteResultDescription` 迁入 Core，`JudgeTuning` 随 `NoteCategory` 迁入。移除仓库中没有调用的 Master 对象构造入口，保留同一数值构造方式。ScriptableObject 配置适配留在 Game 的 `LiveBundleBuildData.GetJudgeTuning`，两例资产检查随之拆出。编译和程序集/IDE 检查通过，缺失脚本新增 0。全量 512 通过、0 失败、4 跳过，计入两例资产测试改归属后与上一批逐项一致。报告在 `TestResults/restructure-p1-1-judgement-*`。

- P1-1 设置批次通过。现有读写统一改用 Game 的 `LiveSettingsStore`。DTO 和快照随 `MVQualityType`、本地 `[Skip]` 标记迁入 Core，保留字段、缺省值和快照复制行为。Core 仅显式引用 Newtonsoft.Json，`link.xml` 的设置 DTO 条目指向新程序集。完整 EditMode 包含旧设置读取、写回和开局快照检查，512 通过、0 失败、4 跳过。程序集/IDE 检查通过，缺失脚本新增 0。报告在 `TestResults/restructure-p1-1-settings-*`。

- P1-4 桌面验证通过。谱面 JSON 模型随 `NoteType`、`NoteLineType`、`NoteDirection` 和 `MusicScoreInfo` 迁入 Core；曲包 manifest 与原短 ID 生成一并迁移，存储留在 Game。旧 `$type`、`$id`、`$ref` 回读和重写经 `ChartJsonTests` 验证。按实际程序集修正 `Assets/link.xml`，Unity 反射逐条验证 13 个保留类型。程序集、IDE、缺失脚本差异检查通过；全量 EditMode 512 通过、0 失败、4 跳过。报告在 `TestResults/restructure-p1-4-contracts-*` 和 `TestResults/restructure-p1-4-link-check.json`。Android IL2CPP 覆盖安装与既有数据、曲包回读待验证。

- P1-1 的 SUS 数据批次通过。七个 `Sekai.SUS` 数据类型随既有谱面枚举归入 Core，保留源码、命名空间和 GUID。实际 Unity 输入和资源转换仍由 Game 提供，`SusRuntimeConverter` 的纯阶段按计划等 P2-6。全量 512 通过、0 失败、4 跳过，程序集与 IDE 检查通过，缺失脚本新增 0。报告在 `TestResults/restructure-p1-1-sus-*`。

- P1-4 Android 通过，M1 已达标。将隔离检出切到 `cac16b65`，使用 `unity build $checkout --target Android --execute-method PhaseOneAndroidBuild.Run --output-path $apk --timeout 900 --non-interactive --no-tail --format json`。隔离的构建入口重建 Android AssetBundles，再生成 ARM64 IL2CPP APK。构建报告为 `TestResults/restructure-p1-4-android-final-build.json`，产物为 `Builds/restructure-phase1/OpenSekaiAtlas-compat-final.apk`。
- 验证新旧 APK 签名一致后，使用 `TestResults/restructure-p1-4-device-smoke.ps1` 执行 `adb install -r`。隔离测试包通过启动参数运行检查，从已有设置读取音符速度，从已有历史读取成绩，曲库识别 20 个可播放曲包。已存在的 `baseline_tap` 经 `Library → Core → Library` 结算为 1,000,000 / S，自动判定 221/221。恢复原 APK 后，设置与历史文件的 SHA-256 和覆盖前一致。设备报告为 `TestResults/restructure-p1-4-device-smoke.txt`。验收脚本和探针保留在本地报告目录与隔离检出中。
- Phase 1 批次提交为 `f5fadb0e`（Core）、`37ab4a8f`（Core 测试）、`1b35d8e5`（程序集门禁）、`aba7a970`（候选排序和计分）、`bd0f79f1`（判定窗口和容差）、`257bbfbb`（设置）、`7b2a5e08`（谱面和曲包契约）、`7066ac1a`（SUS 数据）。整首自动判定修复为 `cac16b65`。Phase 2 可按依赖领取。

### Phase 2 执行记录（2026-11-12）

- P2-1 通过，起点 `1a7c084d`。拆分前后均通过 `unity command test run --mode editmode --filter ScreenManagerTests` 所覆盖的五项契约。通过 Unity 反射逐项比对 `ScreenManager`、其公开嵌套类型和 `HeaderUtility`，225 项公开 API 的签名、参数名、默认值和泛型约束一致。门面保留 Unity 组件、序列化字段和单例入口，页面栈、对话框、过场和页头由实例服务处理。门面 658 行，四个服务依次为 597、304、369、174 行，模糊捕获辅助类由过场服务持有。
- `unity recompile` 为 0 error。全量 EditMode 517 通过、0 失败、4 跳过，原 516 例结果逐项一致。通过 `Tools/verify-structure.ps1 -CheckProjects -CheckAssemblies -RequireNoEmptyDirectories`，缺失脚本清单与基线一致。异步 `LibrarySmokeTests` 两例均通过，整首结算为 1,000,000 / S 并返回曲库。报告、命令与差异检查在 `TestResults/restructure-p2-1-*`。

- P2-2 通过，起点 `76f2c656`。拆出 `LibraryView`、`LibraryPresenter`、`LibraryModel` 和控件辅助类 `LibraryUi`，`LibraryScreen` 保留组件生命周期和依赖装配。门面、视图、Presenter、Model、控件辅助类依次为 50、342、326、85、298 行。沿用原有行重建行为和曲包存储入口。
- `unity recompile` 为 0 error。五个无场景 Presenter 用例验证排序、文件夹导入、手动/自动请求及失败重开。全量 EditMode 522 通过、0 失败、4 跳过，上一批 521 例结果逐项一致。异步 `LibrarySmokeTests` 两例均通过，整首用例从实际结算面板「再玩一次」进入 Core，再返回曲库结算为 1,000,000 / S。程序集与 IDE 检查通过，缺失脚本新增 0。报告在 `TestResults/restructure-p2-2-*`。

- P2-3 宽限期契约确认，起点 `280a3244`。真实谱面经 LiveHarness 在 60 Hz 和 165 Hz 复现下一帧提前结束宽限期。保留未结算头判的漏击路径，已结算长条在首次失去覆盖时变灰，并按经过 50 ms 结束宽限期。原有回按恢复亮态、尾判和头判通知顺序均通过。真实谱面另覆盖重叠尾需要两次抬指和超窗抬指保持 Miss。
- `unity recompile` 为 0 error。通过 `unity command eval_file` 调用 TestRunnerBridge 运行全量 EditMode，525 通过、0 失败、4 跳过。修正前复现见 `TestResults/restructure-p2-3-early-release-before.xml`，修正后见 `TestResults/restructure-p2-3-baseline-editmode.xml`。已将一项以毫秒间隔硬编码帧数的模拟用例替换为真实谱面边界用例。拆分以这份 EditMode 报告为基线。

- P2-3 代码拆分完成。门面保留状态机、继承入口和回调顺序。尾部窗口、释放、划键和尾判复位移到 `LongNoteTail`，继续使用既有尾行为。条体覆盖和宽限期移到 `HoldBody`，轨道显示与回收由 `BarView` 处理，谱面部件装配单独归档。门面 454 个非空行。
- `unity recompile` 为 0 error。最终全量 EditMode 525 通过、0 失败、4 跳过，529 例与修正后的基线逐项一致。异步 `LibrarySmokeTests` 两例通过，整首结算 1,000,000 / S 并返回曲库。程序集、IDE 项目和空目录检查通过，缺失脚本新增 0。报告在 `TestResults/restructure-p2-3-*`。
- P2-3 已完成 Android 验收准备。设备连接后从 `76ad6c33` 的隔离检出使用 `unity build --target Android --execute-method PhaseTwoDeviceBuild.Run` 重建 AssetBundles 和 ARM64 IL2CPP APK，产物为 `Builds/restructure-phase2/OpenSekai-P2-3-76ad6c33.apk`。通过 `adb install -r` 安装独立应用 `io.github.opensekai.p23check`，启动并导入专用六长条谱。原版 APK 路径、设置与历史 SHA-256 在安装前后一致。构建报告为 `TestResults/restructure-p2-3-android-build.json`，装机报告为 `TestResults/restructure-p2-3-device-setup.txt`，谱面时间及操作表见 `TestResults/restructure-p2-3-device-chart-validation.txt` 和 `TestResults/restructure-p2-3-device-checklist.md`。操作者确认宽限期回按、换手、双条重叠和尾判时序四项均已实际测试并通过。该局中途退出，设备未生成可供复核的判定追踪文件，四项结果以操作者观察为证据。手测后再次核对原版 APK 路径、设置与历史 SHA-256，均一致。验收记录为 `TestResults/restructure-p2-3-device-acceptance.txt`。
- P2-5 已留存独立音频基线。`Tools/audit-audio-provenance.ps1` 核对 42 个内置 WAV，结果为 `AUDIT OK`。报告在 `TestResults/restructure-p2-5-audio-baseline.txt`。

## 7. 迁移规则

### 7.1 文件移动

```powershell
git mv Old/Path.cs New/Path.cs
git mv Old/Path.cs.meta New/Path.cs.meta
```

- 只移动 `.cs` 不移动 `.meta` 会生成新 GUID，prefab/scene 引用会断。
- 移动后跑缺失脚本扫描；预期新增 0。
- 目录移动先移动目录，再修 namespace；不要同时改类名。

### 7.2 命名空间与类名

- 改 namespace、类名或程序集时保留 GUID，并在 Unity 重新导入后检查场景和 prefab 的脚本与序列化字段。核对 `link.xml`、JSON 类型别名、反射引用和项目中的程序集限定类型名。
- 移动实际资源时同步核对 `Resources` 相对路径。移动源码时更新 `Tools` 脚本、文档和构建配置中受影响的路径。
- 改序列化字段名用 `[FormerlySerializedAs("OldName")]`。

### 7.3 asmdef

- 先建 asmdef，再按依赖清单移动一批。用 Unity 编译和依赖检查验证归属，核对运行时、测试和 Editor 的直接引用。
- 受影响的 `link.xml` 和兼容测试与类型迁移同一批提交；涉及反射读写时运行 P1-4 的 Android 兼容检查。

### 7.4 Resources 与 AB

- 移动 `Resources` 下的 prefab/asset 会改变 `Resources.Load` 路径；必须先建 `ResourceCatalog`，再移动。
- AssetBundle 白名单在 `OpenSekaiAssetBundleBuildPipeline`，移动源资源后同步改白名单与 `AssetBundleInfo`。
- 资源相关提交执行 `Tools/verify-resource-paths.ps1`、P3-2 的目录和回退检查，并在 Android 上走一遍受影响的加载流程。

## 8. 验收门禁

各阶段执行当前已具备的门禁。运行时或资源变更须通过 Unity 编译、受影响的测试和资源检查；EditMode 相比 P0-4 基线新增失败或跳过均为 0。移动、删除或改名涉及的文档链接在同一提交中通过检查。

| 阶段 | 退出条件 | 验收证据 |
| --- | --- | --- |
| Phase 0 | 现行文档对齐；缺失脚本基线可复现且清理工作无新增缺失；EditMode 基线可复现 | 本地链接检查、Unity 扫描清单、两份 EditMode XML 和已知例外 |
| Phase 1 | Core 边界和测试就绪；迁移后的 Android 包能读取现有数据 | 依赖检查、Core/EditMode 报告、旧数据覆盖安装记录 |
| Phase 2 | 六项拆分满足各自的行为验收；判定和音频在真机通过针对性回归 | 相关测试、Android 判定清单和音频验收记录 |
| Phase 3 | 目录、资源入口与命名检查通过；无新增缺失引用 | 结构检查、动态加载测试、Android 冷启动与打歌验收 |
| Phase 4 | CI 执行已具备的结构、EditMode 和 PlayMode 检查；确认的缺失脚本清零；发布包可安装、冷启动，手动与自动各打一首 | CI 和 Unity 报告、缺失脚本扫描、Android 整首验收。自动谱结算 `1,000,000 / S` 并返回曲库 |

每阶段结束记录提交 ID、实际运行命令、报告位置和阻塞项。P0-5 的结果用于确定 Phase 4 的 PlayMode 范围。Phase 0 至 Phase 3 对已登记的缺失脚本执行新增为零的检查，Phase 4 执行清零检查。

## 9. 风险与回滚

| 风险 | 影响 | 处理 |
| --- | --- | --- |
| 移动文件丢 `.meta`，GUID 变化 | prefab/scene 引用断，missing script 激增 | 只用 `git mv` 连同 `.meta`；每批跑缺失扫描 |
| 移动资源导致 `Resources` 路径或 AssetBundle 键失效 | 曲库、打歌或结算缺资源 | 先建目录与加载器入口，同步更新路径和清单，验证现有回退 |
| asmdef 拆分后项目引用或反射保留条目失效 | Unity 编译失败或 Android 读档失败 | 按依赖分批移动，逐批编译，更新 `link.xml` 并验证旧数据 |
| 拆 ScreenManager 改变公开 API | 调用点大面积编译错误 | 保留门面，方法签名不变；先加委托再搬实现 |
| 拆 LongNote 改判定行为 | 手感回归，测试红 | 先补行为测试；纯移动，不改公式 |
| 测试依赖本地固定路径 | CI 或干净检出失败 | 复用仓库 fixture，需要落盘时使用独占临时目录并清理 |
| 整首冒烟跨场景时销毁测试宿主 | CI 无法可靠验收整首流程 | 先完成 P0-5 原型，保留现有曲库冒烟和 Android 整首验收 |
| 文档再次落后 | 新人误读 | 移动/删除文件时同提交改文档；CI 链接检查 |
| 一次提交太大无法回滚 | 定位困难 | 同一迁移的源码、`.meta`、配置与兼容测试同批提交，保留阶段通过验收的提交 ID |

### 回滚策略

- 从失败的工作包定位依赖它的后续提交。若存在后续依赖，按依赖的逆序回退，再回退原变更。
- 同时恢复该工作包的实现、程序集引用、`.meta`、资源映射及兼容配置。回退后重跑受影响的编译、测试和场景检查。
- 需要对照上一个已通过的阶段时，从里程碑记录的提交 ID 检出复验。

## 10. 里程碑

| 里程碑 | 完成条件 | 可开始的后续工作 |
| --- | --- | --- |
| M0 | P0-1 至 P0-4 通过，文档、缺失脚本和 EditMode 有可复现基线 | Phase 1；P0-5 可独立验证 |
| M1 | P1-1 至 P1-4 通过，Core 与 Android 旧数据兼容得到验证 | Phase 2 的六项拆分 |
| M2 | P2-1 至 P2-6 通过，六项拆分各有行为验收 | Phase 3 的目录和资源入口迁移 |
| M3 | P3-1 至 P3-4 通过，目录、加载行为和命名检查有结果 | Phase 4 的会话依赖与发布门禁 |
| M4 | P4-1 至 P4-4 通过，实际 PlayMode 范围和 Android 整首验收有记录 | 提交发布 |

## 11. 本计划不做什么

- 不改玩法、不改判定数值、不改 UI 视觉；所有拆分以行为不变为前提。
- 不在本计划里重命名 `Sekai` 命名空间；单独 ADR 评估。
- 不重写渲染管线；只保留已收敛的两个 Renderer。
- 不引入 Addressables/ECS/新框架；先把现有 Resources + AB 收进类型化入口。
- 保留现有测试体系，补齐迁移所需的行为回归、可重复基线和整首流程验收。

## 12. 相关文档

- [架构总览](README.md)
- [模块划分](modules.md)
- [构建与运行](../getting-started/build-and-run.md)
- [测试](../operations/testing.md)
- [文档索引](../INDEX.md)
