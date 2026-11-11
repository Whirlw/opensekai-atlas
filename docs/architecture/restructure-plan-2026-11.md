# Open Sekai Atlas 结构重整计划书

> 目的：把当前“旧引擎 + 新播放器外壳”的代码结构整理成边界清晰、可测试、可继续裁剪的工程，并让文档与真实目录重新一致。
> 适用读者：接手代码的开发者、做重构的人、发版前做门禁的人。
> 前置条件：读过 [架构总览](README.md) 与 [模块划分](modules.md)；当前基线为 `f14b717c`。
> 最后更新：2026-11-11
> 状态：**提案，未执行**。执行时按阶段开分支，每阶段独立提交、独立验收。

## 1. 结论与目标

功能清理已经把运行时压到 **440 个 `.cs` / 60,809 行**，但结构问题没有解决：

- 文档还在描述 `Assets/OpenSekai/`、`Assets/Scripts/Assembly-CSharp/` 这类不存在的路径；
- 运行时代码只有两个程序集，`OpenSekai.Game` 一个就装了 413 个文件，纯逻辑和 Unity 引擎混在一起；
- `ScreenManager` 3,234 行、`LongNote` 1,346 行、`SoundManager` 1,204 行，仍是上帝对象；
- `Sekai/` 顶层还有 191 个扁平文件；
- `AcbDecoder` 12 个文件已经没有任何外部引用，空目录还有 12 个；
- 56 处 `Resources.Load` 靠字符串路径，7 个以上全局单例，测试不 hermetic。

本计划的目标不是“再删多少行”，而是把下面五件事做成：

1. **文档说真话**：文档里的路径、页面数、包体数字与代码一致。
2. **程序集有边界**：纯逻辑 `OpenSekai.Core`、Unity 引擎 `OpenSekai.Game`、播放器 `OpenSekai.App`、编辑器 `OpenSekai.Editor`。
3. **大类型有职责**：`ScreenManager`、`LibraryScreen`、`LongNote`、`LiveLogic`、`SoundManager`、`SusRuntimeConverter` 拆到可维护的规模。
4. **资源加载有入口**：`Resources.Load` 字符串收敛到一个类型化目录，不再散落。
5. **测试可重复**：不依赖 `Temp/test16-score.json` 之类的环境残留，纯逻辑测试不背 Unity。

## 2. 当前基线（2026-11-11，`f14b717c`）

| 指标 | 当前值 |
| --- | ---: |
| 运行时 `.cs` | 440 |
| 运行时行数 | 60,809 |
| 运行时程序集 | 2（`OpenSekai.Game` 约 413 文件、`OpenSekai.App` 30 文件） |
| `Sekai/` 顶层 `.cs` | 191 |
| 最大文件 | `ScreenManager.cs` 3,234 行 |
| 第二/第三 | `LongNote.cs` 1,346、`SoundManager.cs` 1,204 |
| `Resources.Load` | 56 处 |
| 全局单例/静态入口 | 7 个以上 |
| 空代码目录 | 12 个 |
| 无外部引用的解码器 | `Platform/AcbDecoder`，12 文件 / 1,006 行 HCA 解码 |
| 引用失效路径的文档 | 24 篇 |
| 提已删页面/旧包体的文档 | 30 篇 |
| EditMode 测试文件 / PlayMode 测试文件 | 54 / 1 |
| 当前 EditMode 结果 | 508 passed / 4 failed / 4 skipped |

已知的 4 个失败不是新逻辑问题：

- 3 个 `HeadSettlementTests` 依赖 `Temp/test16-score.json`，Unity 启动会清空 `Temp`；
- 1 个 `LongNoteGraceTests` 在其文件头注明“expected to fail until the four rows land”。

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
    Charts/              ChartDocument、序列化、SongPackageManifest
    Scoring/             JudgeWindow、ScoreGauge、JudgeTuning
    Simulation/          InputTimeMapping、CandidateOrder 等纯模拟
    Settings/            LiveSettingData DTO、PlaybackSettingsSnapshot
    Packaging/           曲包契约与 manifest DTO
  OpenSekai.Game/
    Screens/             ScreenStack、Dialog、Transition、Header
    Audio/               SoundManager 门面、AudioCatalog、ExternalSoundPack
    Assets/              AssetBundleManager、清单、加载器
    UI/                  通用 UGUI 组件
    Live/
      Runtime/           LiveLogic、控制器、输入、判定
      View/              FrontUIView、Background2DView、HUD
      Notes/             NoteBase、LongNote、各类尾
  OpenSekai.App/
    Bootstrap/
    Library/
    Playback/
  Platform/              窄适配层：CriWare 加载、文件选择、Android
  Vendor/                第三方
```

历史设计稿 `docs/architecture/plan/` 移到 `docs/history/`，不再作为现行架构说明。

## 4. 工作原则

1. **先边界，后搬家**：先建立程序集和依赖规则，再移动文件；不要一边改行为一边搬目录。
2. **保留 GUID**：所有移动用 `git mv`，连同 `.meta` 一起移动；禁止删除后重建。
3. **一次一件事**：每个任务独立提交，提交信息写清“移动/拆分/改名”，不混行为改动。
4. **每步可编译**：任何阶段结束时 Unity 编译 0 error，`EditorUtility.scriptCompilationFailed = false`。
5. **序列化兼容**：改字段名用 `[FormerlySerializedAs]`；改类名/命名空间不动 GUID，但要检查 `Resources.Load` 路径、`link.xml`、asmdef 与 prefab 中的字符串引用。
6. **文档同步**：移动或删除文件时，同一次提交内更新文档路径与 `docs/INDEX.md`。
7. **测试先行**：拆 `ScreenManager`、`LongNote` 之前，先补对应行为测试；拆完测试必须保持绿。

## 5. 阶段计划

### Phase 0：止血与对齐（预计 3–5 人日）

**P0-1 文档对齐**

- 全量替换失效路径：
  - `Assets/OpenSekai/` → `Assets/Game/Runtime/OpenSekai/`
  - `Assets/Scripts/Assembly-CSharp/` → `Assets/Game/Runtime/Sekai/`
  - `Assets/Editor/` → `Assets/Game/Editor/`
- 更新页面数：`7 个可达页面` → `3 个 ScreenLayerData + LibraryScreen`。
- 更新包体：`113–119 MB` / `123.9 MB` → `79.65 MB`。
- 删除已删功能的描述：`InsertNoti`、`ScreenLayerLoading`、`NetworkIndicator`、`AssetRemoving`、`3D MV`、`MultiLive`、`MySekai`、`Gacha`。
- 历史设计稿移到 `docs/history/`，`docs/INDEX.md` 标注“历史”。
- 修 `docs/INDEX.md` 末尾拼错的表格行（现有几行挂在“相关文档”之后）。

验收：`docs` 中不再出现上述失效路径；文档链接检查脚本 0 broken；随机抽 5 篇按文档能走通。

**P0-2 空目录与死平台清理**

- 删除 12 个空目录及其 `.meta`：
  - `Runtime/CustomMusicScoreManager`
  - `OpenSekai/Charts/Formats`
  - `OpenSekai/CustomMusicScoreManager/{Controllers,Freecam,Helpers,UI}`
  - `OpenSekai/Infrastructure/{Audio,Settings}`
  - `Sekai/{APIData,Honor,UI/Interface}`
  - `Platform/OverdrawForURP`
- 删除 `Platform/AcbDecoder`（12 文件、1,006 行）：全仓无外部引用，ACB 资产为 0；如果以后要支持 ACB，放回 `Tools/` 作为离线工具，不进运行时。
- 评估 `Platform/Beebyte` 与 `Platform/MessagePack`：
  - `Beebyte` 仍被 5 个文件用 `[Skip]`，保留存根并在文件头写清“仅为序列化兼容”。
  - `MessagePack` 接口仍被 5 个 Master 类实现，保留，但把目录改名为 `Platform/Serialization`，避免误以为依赖 MessagePack 包。

验收：空目录 0；`AcbDecoder` 删除后编译 0 error；缺失脚本仍为 2 处（见 Phase 4 收口）。

**P0-3 生成文件与检查脚本**

- 本地 `Assembly-CSharp.csproj` 有 890 个 Compile 条目、361 个已不存在；删除后让 Unity 重新生成，不要提交。
- 确认 `.gitignore` 覆盖根目录 `*.csproj`、`*.sln`、`TestResults/`、`Builds/`、`Temp/`。
- 把文档链接检查、缺失脚本扫描、空目录检查写成 `Tools/verify-structure.ps1`，Phase 4 接进 CI。

验收：IDE 不再显示幽灵文件；`verify-structure.ps1` 可重复运行。

### Phase 1：程序集边界（预计 8–10 人日）

**P1-1 建立 `OpenSekai.Core`**

- 新增 `Assets/Game/Runtime/OpenSekai.Core/OpenSekai.Core.asmdef`：
  - `noEngineReferences: true`
  - 引用 `Newtonsoft.Json`（如果曲包/设置序列化需要）
  - 不引用 `UnityEngine.*`
- 第一批移动（低风险、纯逻辑）：
  - `Shared/Scoring/*`
  - `Shared/Live/Simulation/*`
  - `Shared/Settings/LiveSettingData.cs`（只移动 DTO，Unity 读取逻辑留在 Game）
  - `Shared/Conversion/*`（先移纯转换，`SusRuntimeConverter` 拆分后再移）
  - `Shared/SUS/*`
- `OpenSekai.Game` 增加对 `OpenSekai.Core` 的引用；`OpenSekai.App` 同时引用两者。

**P1-2 测试拆分**

- 新增 `Assets/Tests/Core/OpenSekai.Tests.Core.asmdef`，只引用 `OpenSekai.Core` + NUnit。
- 把纯逻辑测试移过去：`JudgeWindowTests`、`ScoreGaugeTests`、`CandidateOrderTests`、`InputTimeMappingTests`、SUS 转换测试。
- EditMode 测试保留 Unity 相关：场景、prefab、资源、输入、Live 视图。

**P1-3 依赖门禁**

- `Tools/verify-structure.ps1` 增加：
  - `OpenSekai.Core/**/*.cs` 出现 `UnityEngine`/`UnityEditor` → fail；
  - `OpenSekai.Game` 引用 `OpenSekai.App` → fail；
  - 测试程序集 `autoReferenced: true` → fail。
- 在 CI 或发版前跑。

验收：Core 程序集编译不依赖 Unity；Core 测试在无场景、无 prefab 的情况下可跑；依赖门禁 0 violation。

### Phase 2：拆上帝对象（预计 15–20 人日）

按“先补测试、再拆、行为不变”的顺序做，每次只拆一个类。

**P2-1 `ScreenManager`（3,234 行）**

拆成四个服务 + 一个门面：

| 新类型 | 职责 | 目标 |
| --- | --- | --- |
| `ScreenStackService` | `screenMap`、Add/Push/Exit/Back、排序、`GetLayerComponent` | < 600 行 |
| `DialogService` | `Show*Dialog`、队列、`ChainDialogPlayer`、`DialogType` | < 500 行 |
| `TransitionService` | `ColorFader`、模糊、`UIBlurLayerManager`、`ScreenCameraStack` | < 500 行 |
| `HeaderService` | `HeaderUtility`、`ScreenLayerHeader`、HeaderCategory | < 300 行 |
| `ScreenManager` | 兼容门面，转发调用 | < 500 行 |

验收：现有公开方法签名不变；EditMode 场景测试全绿；`ScreenManager` 行数 < 800；无新增单例。

**P2-2 `LibraryScreen`（881 行）**

- `LibraryView`：UGUI 构建、行池、布局、动画。
- `LibraryPresenter`：选择、导入、设置、开始游玩、结果回填。
- `LibraryModel`：条目、筛选、排序、历史。
- `LibraryScreen`：MonoBehaviour 生命周期与依赖装配。

验收：每类 < 350 行；曲库导入、手动/自动开局、结算返回行为不变；新增 Presenter 单测（不依赖场景）。

**P2-3 `LongNote`（1,346 行）**

- 保留 `LongNote` 门面与状态机。
- 拆 `LongNoteHead`、`LongNoteBody`、`LongNoteTail`；尾的四种实现已经在 `Live/Tails/`，把判定/显示/回收分别归位。
- 先补 `LongNoteGraceTests`、`TailReleaseLaneTests`、`HoldQuotaTests` 的回归，再移动代码。

验收：`LongNote` < 500 行；长条相关测试全绿；`LongNoteGraceTests` 的已知失败要么修掉，要么显式 `[Ignore]` 并注明原因。

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

验收：`SoundManager` < 500 行；42 个 wav 的 `AUDIT OK` 不变；真机开局/结算音效无回归。

**P2-6 `SusRuntimeConverter`（1,158 行）**

- 拆 `SusParser`、`SusTimingConverter`、`SusEventConverter`、`SusNoteConverter`。
- 每个转换阶段可单测，输入/输出用纯 DTO。

验收：每类 < 400 行；SUS 相关测试全绿。

### Phase 3：目录、命名与资源入口（预计 8–10 人日）

**P3-1 `Sekai/` 顶层收敛**

- 建 `Game/{Screens,Audio,Assets,UI,Animation,Data}`，按职责移动顶层 191 个文件。
- `Sekai/Core/Live` 与 `Sekai/Live` 合并为 `Live/{Runtime,View,Notes}`，消除“Core 里有 Live、Live 里有 Logic”的分裂。
- 保留 `Sekai` 命名空间不动，只动目录；避免一次性改命名空间导致大量 serialized 引用与文档失效。

验收：`Sekai/` 顶层 `.cs` < 20；每个子目录有 README 或职责说明；文件移动保留 `.meta`，缺失脚本仍为 0 新增。

**P3-2 `ResourceCatalog`**

- 新增 `Assets/Game/Runtime/OpenSekai.Game/Assets/ResourceCatalog.cs`（ScriptableObject）：
  - 页面 prefab、live view prefab、UI prefab、字体、材质；
  - 每个条目有稳定 `id` 与 `Resources` 路径；
  - 提供 `LoadPrefab(id)`、`LoadSprite(id)`、`LoadFont(id)`。
- 替换 56 处 `Resources.Load`；只允许 `ResourceCatalog` 和平台加载器出现字符串路径。
- `verify-resource-paths.ps1` 保留，作为回归检查。

验收：`git grep 'Resources.Load'` 只出现在 catalog/loader；曲库、打歌、结算、加载全部资源可解析；缺失引用 0。

**P3-3 命名与 typo**

- `ScreenLayerExtention` → `ScreenLayerExtension`（目录、文件、namespace 同步）。
- `OpenSekai/CustomMusicScoreManager` → `OpenSekai/Storage`（或 `Library/Storage`）；删除根目录空壳。
- `Platform/CP` → `Platform/Common` 或 `Platform/Core`，并在 README 写清 `CP` 的原意。
- `RankLive/NumberView`：如果只是通用数字组件，移到 `UI/NumberView`；不要用一个玩法名目录装通用组件。
- `Sekai` 命名空间是否改成 `OpenSekai.Game`：单独开 ADR，不混在本次目录整理里。

验收：目录名与职责一致；`ScreenLayerExtention` 0 处；文档同步。

**P3-4 动画入口统一**

- 现状：9 个自研 `Tween*` 组件 + 25 处直接 `DG.Tweening` 调用。
- 决策：保留 `Tween*` 作为 Inspector 可配入口，禁止新代码直接 `DG.Tweening`；或者反过来统一到 DOTween。
- 在 `docs/coding-standards` 加一条规则，并在 `Tools/verify-structure.ps1` 里检查新增文件。

验收：新代码不再出现第二种用法；现有调用有清单，不强行迁移。

### Phase 4：状态、测试与发布门禁（预计 8–10 人日）

**P4-1 单例收敛**

- 新增 `AppContext`，在 `LibraryEntryPoint` 创建，持有：
  - `IScreenService`、`IAudioService`、`IAssetService`、`ISceneService`、`IUserDataService`、`IPlayService`。
- `LibraryScreen`、`PlayService`、`SoloLiveController` 通过构造/方法参数拿依赖，不再直接 `ScreenManager.Instance`。
- `LiveLogic` 不再访问 `ScreenManager`/`SoundManager` 单例；通过 `LiveContext` 注入。
- 保留单例作为兼容门面，逐步删调用点。

验收：新增代码 0 处 `*.Instance`；EditMode 测试可构造 fake context；现有行为不变。

**P4-2 测试 hermetic**

- 3 个 `HeadSettlementTests`：在 `SetUp` 里生成 fixture 到 `Application.temporaryCachePath`，`TearDown` 删除；不再依赖 `Temp/test16-score.json`。
- `LongNoteGraceTests`：确认是未实现还是回归；未实现则 `[Ignore("waiting for hold work")]` 并记录在 `docs/operations/tail-classes.md`；回归则修。
- 测试不允许写项目根 `Temp/`、`TestResults/` 之外的固定路径。

验收：连续跑两次 EditMode，结果一致；无环境型失败。

**P4-3 PlayMode 冒烟**

- 新增 1 个 PlayMode 用例：加载 `Library.unity` → 确保 `[LibraryCheck] valid:true` → 自动打一首基线谱 → 断言 `1,000,000 / S` → 返回曲库。
- 该用例进 CI，作为“播放器还能跑”的最低保障。

验收：PlayMode 用例稳定通过；失败时能定位到场景/prefab/资源。

**P4-4 结构门禁与文档检查**

- `Tools/verify-structure.ps1` 检查：
  - 空目录 0；
  - Core 无 Unity 引用；
  - 程序集依赖方向；
  - `Resources.Load` 白名单；
  - 缺失脚本 = 0（修掉 `XRSystemData.asset` 与 `EventSystem.prefab` 的 2 处）；
  - 文档路径 0 broken；
  - `ScreenManager`/`LibraryScreen`/`LongNote` 行数上限。
- 接进 CI 或 `.github/workflows`。

验收：门禁全绿；新增违规会 fail。

## 6. 任务清单（可勾选）

| ID | 任务 | 产出 | 验收 | 预计 |
| --- | --- | --- | --- | ---: |
| P0-1 | 文档路径/页面数/包体对齐 | 24 篇路径、30 处说明 | 链接检查 0 broken | 2d |
| P0-2 | 空目录 + AcbDecoder | 删除 12 空目录、12 文件 | 编译 0 error | 1d |
| P0-3 | 生成文件 + 检查脚本 | 重新生成 csproj、`verify-structure.ps1` | 脚本可跑 | 1–2d |
| P1-1 | Core 程序集 | asmdef + 首批移动 | Core 无 Unity 引用 | 4–5d |
| P1-2 | Core 测试拆分 | Tests.Core asmdef | 纯逻辑测试可跑 | 2d |
| P1-3 | 依赖门禁 | 脚本规则 | 0 violation | 1d |
| P2-1 | 拆 ScreenManager | 4 服务 + 门面 | < 800 行、测试绿 | 3–4d |
| P2-2 | 拆 LibraryScreen | View/Presenter/Model | 各 < 350 行 | 2–3d |
| P2-3 | 拆 LongNote | Head/Body/Tail | < 500 行、测试绿 | 3–4d |
| P2-4 | 拆 LiveLogic | Clock/Input/Judgment/State | 各 < 400 行 | 3d |
| P2-5 | 拆 SoundManager | Catalog/ExternalPack/Volume | < 500 行、音频无回归 | 2d |
| P2-6 | 拆 SusRuntimeConverter | Parser/Timing/Event/Note | 各 < 400 行 | 2d |
| P3-1 | Sekai 顶层收敛 | 6 个职责目录 | 顶层 < 20 | 3d |
| P3-2 | ResourceCatalog | 类型化目录 | 无散落 Resources.Load | 3d |
| P3-3 | 命名/typo | 目录与文件名 | 0 `Extention` | 1–2d |
| P3-4 | 动画入口统一 | 规范 + 检查 | 新代码 0 第二种用法 | 1d |
| P4-1 | 单例收敛 | AppContext/LiveContext | 新增代码 0 `.Instance` | 3–4d |
| P4-2 | 测试 hermetic | fixture 修正 | 连续两次一致 | 2d |
| P4-3 | PlayMode 冒烟 | 1 个端到端用例 | 稳定通过 | 2d |
| P4-4 | 门禁/CI | verify 脚本接入 | 全绿 | 1–2d |

合计约 **40–55 人日**，按单人 6–8 周排；如果多人并行，Phase 2 的五个拆分可以按文件所有权拆开，但 Phase 1 必须先完成。

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

- 改 namespace/类名不改 GUID，prefab 的 `m_Script` 仍能解析；但以下位置要一起改：
  - `Resources.Load` 的路径字符串；
  - `link.xml` 的 `type fullname`；
  - asmdef 的 `rootNamespace`（如使用）；
  - 文档与 `Tools` 脚本中的路径；
  - 测试里的 `typeof(...)`。
- 改序列化字段名用 `[FormerlySerializedAs("OldName")]`。

### 7.3 asmdef

- 移动文件到新程序集时，先建 asmdef，再移动一批，再编译；不要一次移动整个 `Sekai/`。
- 每次移动后检查：
  - EditMode 测试程序集引用是否完整；
  - `OpenSekai.Editor` 是否仍能引用；
  - `link.xml` 是否仍写对程序集名。

### 7.4 Resources 与 AB

- 移动 `Resources` 下的 prefab/asset 会改变 `Resources.Load` 路径；必须先建 `ResourceCatalog`，再移动。
- AssetBundle 白名单在 `OpenSekaiAssetBundleBuildPipeline`，移动源资源后同步改白名单与 `AssetBundleInfo`。
- 每次构建后跑 `Tools/verify-resource-paths.ps1` 与真机开局。

## 8. 验收门禁

每个阶段结束必须全部满足：

1. Unity 编译 **0 error**，`EditorUtility.scriptCompilationFailed = false`。
2. `Tools/verify-structure.ps1` 全绿（空目录、Core 依赖、Resources 白名单、缺失脚本、文档链接）。
3. 缺失脚本 **0**（当前 2 处必须在 Phase 4 前修掉）。
4. EditMode 测试：除显式 `[Ignore]` 外全绿；连续跑两次结果一致。
5. PlayMode 冒烟：Library → 自动打歌 → 结算 → 返回，分数 `1,000,000 / S`。
6. Android 构建可安装，冷启动进曲库，手动/自动各打一首。
7. 文档路径 0 broken，页面数/包体数字与当前一致。
8. `git status` 干净，提交信息能看出“移动/拆分/改名”，不混行为改动。

## 9. 风险与回滚

| 风险 | 影响 | 处理 |
| --- | --- | --- |
| 移动文件丢 `.meta`，GUID 变化 | prefab/scene 引用断，missing script 激增 | 只用 `git mv` 连同 `.meta`；每批跑缺失扫描 |
| 改 namespace 导致 `Resources.Load` 路径失效 | 运行时报“找不到 prefab” | 先建 ResourceCatalog；路径字符串集中管理 |
| asmdef 拆分后 Editor 程序集引用断 | Unity 编译失败 | 每批只移动一个目录，先编译再继续 |
| 拆 ScreenManager 改变公开 API | 调用点大面积编译错误 | 保留门面，方法签名不变；先加委托再搬实现 |
| 拆 LongNote 改判定行为 | 手感回归，测试红 | 先补行为测试；纯移动，不改公式 |
| 测试 fixture 写固定 Temp | CI/本地随机失败 | 用 `Application.temporaryCachePath` + `TearDown` |
| 文档再次落后 | 新人误读 | 移动/删除文件时同提交改文档；CI 链接检查 |
| 一次提交太大无法回滚 | 定位困难 | 每任务一提交；Phase 结束打 tag |

回滚策略：

- Phase 0/1 的移动类改动可以直接 revert 单个提交；
- Phase 2 的拆分保留原类作门面，回滚只需恢复门面实现；
- 每个 Phase 结束在分支打 `restructure/phase-N` tag，出问题回 tag，不在主干上硬回滚。

## 10. 里程碑

| 里程碑 | 内容 | 预计 |
| --- | --- | --- |
| M0 | 文档/空目录/死平台/检查脚本完成 | 第 1 周 |
| M1 | Core 程序集 + Core 测试 + 依赖门禁 | 第 3 周 |
| M2 | ScreenManager / LibraryScreen / LongNote / LiveLogic 拆分 | 第 6 周 |
| M3 | SoundManager / SUS 拆分 + 目录/资源入口 | 第 8 周 |
| M4 | 单例收敛 + 测试 hermetic + PlayMode + CI 门禁 | 第 9–10 周 |

## 11. 本计划不做什么

- 不改玩法、不改判定数值、不改 UI 视觉；所有拆分以行为不变为前提。
- 不在本计划里重命名 `Sekai` 命名空间；单独 ADR 评估。
- 不重写渲染管线；只保留已收敛的两个 Renderer。
- 不引入 Addressables/ECS/新框架；先把现有 Resources + AB 收进类型化入口。
- 不重写全部测试；只修 hermetic 问题并补一条端到端冒烟。

## 12. 相关文档

- [架构总览](README.md)
- [模块划分](modules.md)
- [构建与运行](../getting-started/build-and-run.md)
- [测试](../operations/testing.md)
- [文档索引](../INDEX.md)
