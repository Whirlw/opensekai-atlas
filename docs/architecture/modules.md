# 模块划分

> 目的：给出目录与职责的对应表，说明新旧代码的边界在哪、依赖朝哪边指。
> 适用读者：开发者。
> 前置条件：读过 [架构总览](README.md)。
> 最后更新：2026-11-12

## 概述

运行时的依赖方向是 `OpenSekai.App → OpenSekai.Game → OpenSekai.Core`。编辑器程序集可调用两者，入口位于 `Assets/Game/Editor/`。

| 程序集 | 路径 | 职责与入口 |
| --- | --- | --- |
| OpenSekai.Core | `Assets/Game/Runtime/OpenSekai.Core/` | 输入时钟、候选排序、判定、计分、设置数据、谱面 JSON 与曲包清单、SUS 解析与时间轴（纯阶段） |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/` | 组合根 `AppContext`：装配曲库页需要的 `SongPackageManager` 与 `PlayService`，由 `LibraryEntryPoint` 创建并交给 `LibraryScreen` |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/Bootstrap/` | `LibraryEntryPoint` 装配曲库入口 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/UI/` | `LibraryScreen` 和导入、设置、结算面板 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/Playback/` | `PlayService` 和 `LegacyLiveBootAdapter` 装配开局数据；`IPlayBootPublisher`/`IPlaySurface`/`IPackageAudioRegistrar`/`ILiveDeckSelection` 四个端口及其 `LegacyPlayPorts` 兼容实现（App 里唯一读 `UserDataManager` 的地方） |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/App/Library/` | 玩家曲库的存储与端口实现：`CustomMusicScoreEntry`/`CustomMusicScoreStorage`（`SongPackageStorage` 之上的入口包装）、`CustomMusicScorePlayHistoryStorage`（播放历史 JSONL 的读写与记录模型）、`CustomScorePlayHistoryRecorder`（实现 Game 的 `IPlayHistoryRecorder`） |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Charts/` | `ChartTimeline` 和 `RuntimeChartDefinition` |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Infrastructure/` | `ChartFileReader`、`SongPackageManager` 和曲库导入 |
| OpenSekai.App | `Assets/Game/Runtime/OpenSekai/Live/Conversion/` | `RuntimeChartConverter` 和 `RuntimeChartImporter` 组装运行时谱面 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Screens/` | `ScreenManager` 门面与页面栈、对话框、过场与模糊、页头、页面层基类、子窗口、场景入口与返回键 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Audio/` | `SoundManager` 门面、`AudioCatalog`、`ExternalSoundPack`、`VolumeService`，以及 ACB 兼容入口 `AcbAssetSource` |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Animation/` | `Tween*` 组件与目标适配、加载指示、粒子控制与特效池 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/UI/` | `UIParts*` 部件、自定义 UGUI 控件（`Custom*`、`Touch*` 反馈）、数字与图集工具、字体与颜色工具 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Assets/` | `ResourceCatalog`（固定资源的类型化目录）、AssetBundle 下载、加载与管理、清单、`Resources` 回退与 LRU 缓存 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Data/` | Master 数据模型、曲目与 Live 数据、设置与本地存储入口、玩法枚举 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/` | Live 帧循环与时钟、输入路由与候选、判定调度、会话状态与统计、控制器、技能逻辑、曲目配置与时间轴 DTO；`LiveContext` 是本局从开局边界拿到的唯一一份依赖（开局数据、模式、会话标识、播放历史记录端口）；`IPlayHistoryRecorder` 是 Game 侧定义、App 侧实现的结算写入口 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Live/View/` | 前场与结算视图、背景、曲目信息、屏幕特效与遮罩、特写与技能文字、音符视图与 HUD、判定与计分反馈、Live 过场 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Live/Notes/` | `NoteBase` 与各类音符、长条主体/接缝/尾判、`Tails/` 四种尾行为 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Rendering/` | URP Renderer Feature 与 Pass、UI 模糊与特效参数、相机与背景组件、shader 预载 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Platform/` | 平台输入适配（`InputManager`、`NativeInput`）、ACB 解码器、CRI 音频 shim、`CP` 通用工具、序列化接口、`UiEffect`、`LetterSpacing` |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Shared/` | 判定与触摸的纯移植逻辑、SUS 运行时转换、曲包模型与存储、播放会话上下文、设置存储、诊断 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/Extensions/` | 通用 `GameObject` 扩展 |
| OpenSekai.Game | `Assets/Game/Runtime/OpenSekai.Game/OpenSekai.Game.asmdef` | 程序集定义；`Runtime/` 下只有 App、Core、Game 三个程序集目录 |
| OpenSekai.Editor | `Assets/Game/Editor/` | Android 构建、AssetBundle 构建、资源检查和场景校验 |

构建场景在 `Assets/Game/Scenes/`。依次为 `Library.unity`、`TransitionBlank.unity` 和 `Core.unity`。

固定资源走 `Sekai.ResourceCatalog`：字体、`LiveBundleBuildData`、几个固定 prefab 与音符线材质都以 `ResourceEntry<T>`（路径 + 可选大小写回退 + 类型）登记在那里，调用点写 `ResourceCatalog.LiveBundleConfig.Load()`，不再各自拼 `Resources` 路径。它是静态类，没有实例也没有初始化顺序问题，曲库与 Live 场景取到的是同一份条目。按数据取址的资源（按名字的页面 prefab、按配置选的音符 prefab、文案表、图集 sprite）继续走 `ResourceLoader` 的 `Resources.Load`/`LoadAsync`，那是动态路径。`Tools/verify-resource-paths.ps1` 同时校验这两种写法的字面路径。

`Assets/Game/Runtime/OpenSekai.Game/` 是 P3-1 目录收敛的结果，按职责分为 `Screens`、`Audio`、`Assets`、`UI`、`Animation`、`Data`、`Live/{Runtime,View,Notes}`、`Rendering`、`Platform`、`Shared`、`Extensions`。代码命名空间保持 `Sekai` 不变，所以搬目录不影响序列化引用，但 `.meta` 必须随文件一起移动（GUID 不变，场景与 prefab 才不会断）。

`OpenSekai.Game.asmdef` 已随目录收敛搬进 `Assets/Game/Runtime/OpenSekai.Game/`（GUID `ba080aef…` 保留），所以它的作用范围就是该目录。`Runtime/` 下现在只有三个程序集目录：`OpenSekai/`（App）、`OpenSekai.Core/`、`OpenSekai.Game/`；新增 Game 侧代码必须落在 `OpenSekai.Game/` 内，落在 `Runtime/` 根下会被编进 `Assembly-CSharp` 并失去对 Core 与插件程序集的引用。

玩家曲库的存储住在 `App/Library/`。Game 侧只剩两件事：解析曲包里的文件路径（`Background2DView`、`MusicInfoView` 直接 `SongPackageStorage.LoadEntry(...)` 取 `JacketPath`/`VideoPath`），以及结算时写播放历史——后者走 Game 定义的 `IPlayHistoryRecorder`，实现在 App 的 `CustomScorePlayHistoryRecorder`，由 `PlayService` 在发布开局数据前写进 `PlayHistoryRecorder.Current`，再由 `LiveContext.FromRetainedBoot()` 读出来交给控制器。写槽-读槽与 `EntryPoint.PlayMode`、`PlaySessionContext` 是同一套边界约定：live 场景的 `LiveContext` 由 Game 的 `EntryPoint` 自己建，App 在切场景时没有传参通道，而 Game 不能引用 App。

### 已移出工程的部分（备份目录已于 2026-11-03 删除，源码见重构前的 git 提交）

| 备份目录 | 内容 |
| --- | --- |
| `editor-tree-2026-11-01/` | 谱面编辑器 527 个脚本 |
| `editor-assets-2026-11-01/` | 编辑器画面 prefab、12 个编辑器 `ScreenLayerData`、编辑器场景 |
| `legacy-library-ui-2026-11-01/` | 旧曲库页 `ScreenLayerCustomMusicScoreManager`（3409 行） |
| `screens-registry-2026-11-01/` | 未被引用的页面注册表与 213 个 `ScreenLayerData` |
| `video-export-2026-11-02/` | 录屏/视频导出 15 个脚本 |
| `messagepack-2026-11-02/` | 标注备份、formatter、NuGet DLL |
| `palette-2026-11-02/` | uPalette 生成的 17 个脚本与 PaletteStore |
| `uiparticle-2026-11-02/` | SoftMask/UI Particle 的最初拷贝 |
| `recording-seam-2026-11-02/` | 录屏接口缝 `LiveRecordingHooks.cs` |
| `module-readmes-2026-11-02/` | `Assets/` 下 5 份过期模块 README（内容已吸收进 [曲包与谱面数据](chart-package.md) 等文档） |
| `ffmpeg-2026-11-02/` | 185.3 MB ffmpeg 二进制 |

备份目录 `_removed/` 已删除；`.gitignore` 仍保留 `/_removed/` 规则，将来再建备份时不会误提交。

## 示例

新增一个「曲库页按钮」应该动哪些文件：

```text
1. App/UI/LibraryScreen.cs          → 加按钮与回调
2. App/UI/LibraryXxxPanel.cs        → 如果要做浮层，单独一个文件
3. App/Playback/PlayService.cs      → 只有涉及开局流程才改
4. 编辑器验证：OpenSekai/Library/Verify Running Scene 仍应 valid:true
```

## 常见问题与故障排查

- **新代码引用了 `Sekai.MusicScoreMaker.Ingame.*`**：该命名空间已随编辑器删除，编译必然失败。
- **想知道某文件属于哪一层**：查看包含它的最近一层 `.asmdef`。

## 相关文档

- [曲包与谱面数据](chart-package.md)
- [打歌运行时](live-runtime.md)
- [ADR-0001 去掉谱面编辑器](../decisions/0001-drop-chart-editor.md)
