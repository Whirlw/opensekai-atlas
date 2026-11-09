# OpenSekai 项目总览（系统层面）

> 目的：重构前（尚有谱面编辑器时）的工程总览，仅作历史参考。
> 适用读者：需要了解旧结构的人。
> 前置条件：无。
> 最后更新：2026-11-02（仅目录整理）

> ⚠️ 历史文档：文中的谱面编辑器、`MusicScoreMakerData`、旧曲库页等代码已从工程移除，
> 备份目录 `_removed/` 已于 2026-11-03 删除；当前结构请看 [这是什么](../../getting-started/what-is-this.md)。


> 面向「准备动手改这个工程」的人。读完你应该能回答：这个工程由哪几块组成、按下 Play 之后发生了什么、哪些代码是活的、哪些只是空壳、改一个功能应该落在哪一层。
>
> 具体到方法级 / 字段级的细节见同目录 [`实现细节（已过期）.md`](old-implementation-notes.md)。文中 `路径:行号` 均为相对仓库根目录的真实位置，可直接跳转核对。

---

## 1. 这个工程是什么

**Open Sekai Community（旧名；项目现称 Open Sekai Atlas）是一个 Project Sekai 玩法复刻工程**：可用的**谱面编辑器** + **第三方曲包** + **从编辑器直接进入 live 试玩**，外加一个**谱面视频导出**功能。它不是完整的商业游戏复刻，也没有联网。

血统关系（决定了代码形态）：

| 阶段 | 产物 | 对本仓库的影响 |
|---|---|---|
| 原作客户端 | 反编译得到 `Assembly-CSharp` 的原始类型骨架 | 大量 `Sekai.*` 类型名、MessagePack `[Key]` 标注、日文日志字符串都来自这里 |
| `kamcdev/OpenSekai_Community` | 上游社区版，已归档 | 现有主体结构、谱面编辑器、曲包、live 内核 |
| 本仓库（Whirlw fork） | 在上游基础上继续开发 | 高帧率档位、装饰音符、单键变速、负流速、视频导出、PC 操作等 |

**最关键的认知**：这份代码是「**被裁剪过的反编译工程**」，不是从零写的项目。表现为三类代码混在一起：

1. **活的实现**：编辑器、live 判定、音频解码、曲包、录屏 —— 这些是本工程真正的价值所在。
2. **反编译骨架**：方法体是 `throw null;`，类型签名完整但无行为（典型：`AssetBundleDownloadHandler`、`LruAssetCache`、`InputManager`、`PublishExecutor`）。
3. **带 `TODO(original)` 的精简实现**：行为被简化但能跑，注释明确写了「原版是怎么做的」。全仓 **100 处**。

规模感：`grep -rn "throw null" Assets --include=*.cs | wc -l` ≈ **4,760 处** —— 反编译骨架是这份代码的底色，不是少量残留。所以读代码时永远先问一句：**这段是活的，还是只剩壳？** 第 9 节给了一份现成清单。

授权：代码 MIT，但第三方库、字体、音频、贴图、prefab、shader 以及任何可能来自原作或其他权利方的资源**不在 MIT 范围内**（`README.md:19-23`）。

---

## 2. 技术栈与工程形态

| 项 | 值 | 说明 |
|---|---|---|
| Unity | **2022.3.62f3** | `ProjectSettings/ProjectVersion.txt:1`。README 里写的 `2022.3.62f2` 是旧值 |
| 渲染管线 | URP 14.0.12 | `Packages/manifest.json` |
| 关键包 | UniTask、MessagePack、uPalette、TMP 3.0.7、Input System 1.19.0、Timeline、NuGetForUnity、unity-mcp、NativeFilePicker、SoftMaskForUGUI、UI Particle | 同上 |
| 插件 DLL | DOTween、`cri_ware_unity.dll`、Ookii.Dialogs、System.Windows.Forms | `Assets/Plugins/**` |
| 输入 | `activeInputHandler = 2`（Both） | 新旧输入系统并存 |
| 平台 | Windows / Android 为主 | Android 有高刷请求、录屏（MediaCodec）、存储权限等专门分支 |

### 2.1 程序集：几乎全在一个 Assembly-CSharp 里

全仓只有 **3 个 asmdef**：

- `Assets/CRIMW/CriWare/Runtime/CriMw.CriWare.Runtime.asmdef`（官方 CRI 运行时）
- `Assets/CRIMW/CriWare/Editor/CriMw.CriWare.Editor.asmdef`
- `Assets/Scripts/ZString/ZString.asmdef`（随包附带，**本工程代码没有引用它**）

其余 **1283 个 `.cs` 全部落在预定义的 `Assembly-CSharp`**，编辑器脚本（`Assets/Editor`）落在 `Assembly-CSharp-Editor`。

后果很实际：

- 任意两个脚本之间可以随意互相引用，**没有编译期模块边界**；
- 改一行代码就是要重编整个程序集，迭代偏慢；
- 命名空间是唯一的分层约定：`Sekai`、`Sekai.Core.*`、`Sekai.Live`、`Sekai.MusicScoreMaker.*`、`CP`。

### 2.2 命名空间约定

| 命名空间 | 目录 | 职责 |
|---|---|---|
| `Sekai` | `Assets/Scripts/Assembly-CSharp/Sekai/*.cs`（根目录 449 个扁平文件） | 页面层与领域模型的「大杂烩」：ScreenManager、ScreenLayer、User*、Master*、APIData |
| `Sekai.Core` / `Sekai.Core.Live` | `Sekai/Core/**` | 场景级控制器：EntryPoint、BaseController、Live 控制器、计分、技能 |
| `Sekai.Live` | `Sekai/Live/**` | 打歌的模型与视图：MusicScore、NoteBase 及各种 Note、LiveFrontView、LiveConfig |
| `Sekai.MusicScoreMaker.*` | `Sekai/MusicScoreMaker/**` | 谱面编辑器（Ingame 编辑 + OutGame 列表/发布） |
| `Sekai.MusicScoreMaker.Common` | `Sekai/MusicScoreMaker/Common/**` | 存档、曲包存储、难度估算 |
| `Sekai.UI` | `Sekai/UI/**` | 通用 UGUI 控件（ListView、CustomScrollRect 等） |
| `Sekai.Rendering` | `Sekai/Rendering/**` | URP RendererFeature |
| `Sekai.ApiData` | `Sekai/APIData/**` | 接口 DTO |
| `CP` | `Assets/Scripts/Assembly-CSharp/CP/**` | 基础设施：日志、单例、限制栈、帧率工具、HTML 解码等 |
| `AcbDecoder` | `Assets/Scripts/Assembly-CSharp/AcbDecoder/**` | 纯 C# 的 ACB/HCA 解码器 |
| `CriWare` | `Assets/Scripts/Assembly-CSharp/CriWare/CriAtomShim.cs`（**1 个文件**） | 与官方 CRI 同名的兼容层，实际生效的就是它 |

### 2.3 代码规模（按模块）

| 模块 | 文件数 | 约行数 | 说明 |
|---|---:|---:|---|
| `Sekai/MusicScoreMaker` | 536 | 58,200 | 编辑器，最大头。其中 `Ingame/Events` 157 个是消息 DTO |
| `Sekai/Live` + `Sekai/Core/Live` | 80 | 10,200 | 打歌 |
| `Assets/CustomMusicScoreManager` | 17 | 9,200 | 曲包管理 UI + 视频导出（单文件 3905 行） |
| `Sekai/UI` | 52 | 6,700 | 通用控件 |
| `Sekai`（根目录扁平文件） | 449 | 39,600 | 页面层与数据模型 |
| `Sekai/SUS` | 8 | 1,400 | SUS 谱面导入 |
| `AcbDecoder` | 12 | 2,000 | HCA 解码 |
| `Sekai/Rendering` | 29 | 1,050 | 约 2/3 是空壳 |
| `CP` | 12 | 1,100 | 基础设施 |
| **`Assembly-CSharp` 合计** | **1,283** | **≈125,800** | 另有 CRIMW 65 个、ZString 47 个 |

---

## 3. 三个场景

工程只有 3 个进包场景（`ProjectSettings/EditorBuildSettings.asset:8-16`）：

| 场景 | 资产 | 内容 | 角色 |
|---|---|---|---|
| `MusicScoreMaker.unity` | `Assets/Sekai/MusicScoreMaker/Ingame/Scenes/` | `CanvasRoot`（含 8 个 `Layer_*` 子物体）、`UICamera`、`EntryPoint` | **主场景**：整个 UI 壳 + 编辑器 |
| `Core.unity` | `Assets/Sekai/Scenes/` | 只有 `Singleton`（SekaiSingletonManager）和 `EntryPoint`（`Sekai.Core.EntryPoint`） | **live 场景**：打歌 |
| `TransitionBlank.unity` | `Assets/Sekai/Scenes/` | 只有 `Main Camera` + `SceneTransitionBlank` | 转场用的空场景，避免大场景同时驻留 |

> 注意：`SceneManager.Scene` 枚举里还有 `Title / OutGame / VRMode / Splash / VirtualLiveLobby / Mysekai`，但**没有对应场景文件**。`GetSceneName()` 只映射 Core / TransitionBlank / MusicScoreMaker 三个（`Sekai/SceneManager.cs:148-161`），其余会被当成「无场景」处理。

**在编辑器里从哪个场景启动**：直接打开 `MusicScoreMaker.unity` 按 Play。这是唯一能完整跑起来的主场景（`MusicScoreMakerEntryPoint.cs:174-175` 的注释也写明了这点）。

---

## 4. 启动之后发生了什么

### 4.1 主场景启动时序

```text
MusicScoreMaker.unity
└─ EntryPoint (MusicScoreMakerEntryPoint : SceneEntryPoint)
   Awake  : 日志 / EnsureEventSystem / TMP 内置字体 / SetFrameRate(跟随屏幕)
            / AssetBundleMetaManager.Initialize / AssetBundleManager.Initialize
            / SoundUtility.SetupGlobalSeSettings / OtaChecker.CheckForUpdates()   ← 唯一联网点
   Start  : ApplicationLocalSettings.LoadFromStorage()
            SoundManager.SetupVolume(...) + LoadSoundBundle("sound/menu/menu_common")
            ScreenManager.CreateEmptyBaseCamera()
            ScreenManager.FadeIn(...)
            ScreenManager.AddScreen(Header / TouchEffect / InsertNoti)   ← 常驻层
            ScreenManager.PushUIScreen(MenuScreenType.MusicScoreMakerTop) ← 首页
```

代码：`Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Presenters/MusicScoreMakerEntryPoint.cs:30-113`。

**首页 = `ScreenLayerCustomMusicScoreManager`**（`MenuScreenType.MusicScoreMakerTop = 3001`）。它的 prefab 不在 `Assets/Resources` 下，而在 `Assets/CustomMusicScoreManager/Resources/Screen/Prefabs/ScreenLayerMusicScoreMakerTop.prefab` —— Unity 会把所有 `Resources` 目录合并成一个虚拟根，所以 `Resources.Load("Screen/Prefabs/...")` 能找到它。

### 4.2 场景切换：为什么要有 TransitionBlank

`SceneManager.RequestScene(scene)`（`Sekai/SceneManager.cs:60-115`）不是简单 `LoadScene`：

1. 先算 `ResolveLoadScene()`（同文件 `:85-115`），决定这次是否**绕道 TransitionBlank**；
2. 若是，就先加载空场景，再由空场景的 `SceneTransitionBlank` 继续请求目标场景；
3. 真正加载前会调 `ScreenManager.ExitScene()` 并广播 `OnExitScene`（各 `SceneEntryPoint` 借此做清理）。

绕道的 4 条规则（同一函数）：

| 当前场景 | 目标场景 | 结果 |
|---|---|---|
| 任意 | 无场景文件的枚举值 | 不切场景 |
| `TransitionBlank` | 任意 | 直接去目标（防止来回弹） |
| `VRMode / Core / VirtualLiveLobby / SuperVirtualLiveLobby / MusicScoreMaker` | `OutGame / MusicScoreMaker` | 经 TransitionBlank |
| `OutGame` | `VRMode / Core / … / Mysekai / MusicScoreMaker` | 经 TransitionBlank |
| `Mysekai` | `Title / OutGame / Mysekai` | 经 TransitionBlank |

在当前这份裁剪版里，实际会走到的只有 **MusicScoreMaker ⇄ Core**（编辑器 ⇄ 打歌），TransitonBlank 是兜底。

### 4.3 完整主线（这是全工程最重要的一条链）

```text
首页 ScreenLayerCustomMusicScoreManager（曲包列表 / 设置）
   │  点「编辑谱面」→ PushUIScreen(MusicScoreMaker, ScreenLayerMusicScoreMaker.BootArg)
   ▼
ScreenLayerMusicScoreMaker.Setup()            （Assets/.../Presenters/ScreenLayerMusicScoreMaker.cs:117-166）
   ├─ new MusicScoreMakerModel(...)
   ├─ MusicScoreMakerPresenter.Create(model, view, token, cb)
   ├─ CreateMusicScore / CreateCustomMusicScore（决定载入哪份谱面）
   └─ presenter.Setup(fromScreenType, token)
   ▼
编辑器（MusicScoreMakerPresenter 8195 行 + 57 个 View + 157 个事件 DTO）
   │  点「试玩」→ OnTestPlay → PlayTestLive → PlayTestLiveCore
   │      （MusicScoreMakerPresenter.cs:6944-7021，关键动作：SaveUndoRedoStack /
   │        SetFreeLiveBootData / PlayMode = SoloLive / PushUIScreen(LiveLoading)）
   ▼
ScreenLayerLiveLoading（Assets/.../Sekai/ScreenLayerLiveLoading.cs:189-191）
   └─ SceneManager.RequestScene(Core)
   ▼
Core.unity → Sekai.Core.EntryPoint → AddComponent<SoloLiveController>()
   └─ OnAwake / Setup / LoadSound / LiveViewFactory.Create / LiveStart(6f)
   ▼
BaseController.LateUpdate → SoloLiveController.OnUpdate（每帧）
   ├─ UpdateMusicTime()                       ← 音乐时钟（来自音频播放器）
   ├─ liveLogic.OnUpdate(...)                 ← 事件、技能、音符状态
   ├─ liveLogic.OnInput() / OnAutoInput()     ← 判定
   └─ LiveViewExt.OnUpdate(liveViews, ...)    ← 视图
   ▼
结束 / 退出 → RequestScene(MusicScoreMaker) → 回到编辑器（或首页）
```

---

## 5. 分层结构

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ 场景层   MusicScoreMaker（主）      Core（live）      TransitionBlank（转场）│
├──────────────────────────────────────────────────────────────────────────┤
│ 入口层   MusicScoreMakerEntryPoint  Sekai.Core.EntryPoint  SceneTransitionBlank
├──────────────────────────────────────────────────────────────────────────┤
│ 路由层   SceneManager（换场景）       ScreenManager（换页面/弹窗/加载/网络）│
├──────────────────────────────────────────────────────────────────────────┤
│ 页面层   ScreenLayer 子类（一屏一 prefab）                                  │
│          + ScreenLayerData（配置资产） + MenuScreenType（枚举注册）          │
│          + DisplayLayerType（8 个 Canvas 层）+ ScreenLayer.BootArgBase（参数）│
├──────────────────────────────────────────────────────────────────────────┤
│ 领域层   谱面编辑器（MusicScoreMaker）                                      │
│          live（Sekai.Live + Sekai.Core.Live）                               │
│          曲包与录屏（CustomMusicScoreManager）                              │
│          SUS 导入（Sekai.SUS）                                              │
├──────────────────────────────────────────────────────────────────────────┤
│ 基础层   资源：AssetManager / AssetBundleManager / ResourceLoader           │
│          音频：SoundManager + CriAtomShim + AcbDecoder                      │
│          持久化：json 文件落 persistentDataPath                             │
│          表现：DOTween 封装（Tween*）、ColorFader、ScreenInOutAnim*          │
│          输入：NativeInput(新) / TouchController(旧) / BackKeyChecker        │
│          渲染：Sekai.Rendering 的 URP RendererFeature                       │
└──────────────────────────────────────────────────────────────────────────┘
```

**改东西先定位到层**：

- 想加/改一个页面 → 页面层（新 `ScreenLayer` 子类 + `ScreenLayerData` 资产 + prefab + `MenuScreenType` 枚举）
- 想改判定 / 音符行为 → 领域层 live（`Sekai/Live/*Note*.cs`、`Sekai/Core/Live/LiveLogic.cs`）
- 想改编辑器交互 → 领域层编辑器（`MusicScoreMaker/Ingame/**`）
- 想改加载 / 音频 / 存档 → 基础层
- 想让某个页面动起来、切换有动画 → 页面层 + `ScreenLayer` 生命周期（当前动画是空壳，见 §5.2）

### 5.1 页面系统：注册了 230 个，能跑的只有 9 个

`ScreenManager` 启动时读 `Resources/Screen/SceneEntry/EntryScreenLayers`（`ScreenManager.cs:1833-1855`），得到一个 `ScreenLayerDataList`，里面挂 **230 条注册**（`data` 目录里共 232 个 `ScreenLayerData` 资产，有 2 个没挂进注册表），每条都是一个 `ScreenLayerData`（`Assets/Resources/screen/data/**`）。

每个 `ScreenLayerData` 记录：`ScreenType`（== `MenuScreenType` 值）、`DisplayLayer`、入/出场动画类型、Header 显示策略、BGM / 背景类型等；prefab 是**按资产名**去 `Resources` 里找的：

```csharp
// Sekai/ScreenLayerData.cs:30-37
Resources.Load<GameObject>("Screen/Prefabs/" + name)
```

而 `Assets/Resources/screen/prefabs/` 下**只有 8 个 prefab**：

| prefab | 对应页面 |
|---|---|
| `ScreenLayerMusicScoreMaker` | 谱面编辑器（3000） |
| `ScreenLayerLiveLoading` | 进入 live 的加载页 |
| `ScreenLayerLoading` | 通用加载 |
| `ScreenLayerHeader` | 顶栏 |
| `ScreenLayerTouchEffect` | 点击特效 |
| `ScreenLayerInsertNoti` | 插入式通知 |
| `ScreenLayerNetworkIndicator` | 网络指示 |
| `ScreenLayerAssetRemoving` | 资源清理提示 |

加上 `Assets/CustomMusicScoreManager/Resources/Screen/Prefabs/ScreenLayerMusicScoreMakerTop.prefab`（首页，3001），**一共 9 个页面是真正可用的**。

其余 221 条注册只有数据资产、没有 prefab。`PushUIScreen` 走到它们会：

```csharp
LogUtility.LogError("Not Found ScreenLayer;" + layer.Data.name);   // ScreenManager.cs:2327
```

然后这一屏不出现——**表现为「点了没反应」**。这是你在本工程里最容易踩的坑，务必先记住。

### 5.2 页面生命周期是「简化版」

`ScreenLayer` 定义了完整状态机 `None → Booting → InitComponent → Starting → Playing → Exiting → Exit`，并暴露 6 个可重写钩子（`OnBoot / OnInitComponent / OnFinishStartAnimation / OnScreenStart / OnExitStart / OnExited`）。

但当前实现里：

- 入/出场动画是空壳：`PlayStartAnimation()` / `PlayExitAnimation()` 直接调 `*Done()`（`Sekai/ScreenLayer.cs:158-161`、`:194-202`）；
- `ChangeStartAnimationType / ReverseStartAnimationType` 等是 `TODO(original)` 空方法（`:126-144`）；
- `OnScreenLayerExitStart()` 是同步协程，`yield break`（`:175-182`）。

**结论：现在换页面是硬切，没有滑动/遮罩动画。** 想做动画，`ScreenInOutAnimBase` / `ScreenSlideInOut`（DOTween）已经写好了，只是没接到 `ScreenLayer` 上。

### 5.3 页面栈是「有限的」

`PushUIScreen` 用 `LimitedStack(16)` 存返回链（`ScreenManager.cs:1828`）。超 16 层会**静默丢弃最老的一层**（`CP/LimitedStack.cs:31-38`），表现为深路径下连按返回会跳页。

---

## 6. 三条主要数据流

### A. 谱面：编辑 → 存档 → 试玩

| 环节 | 位置 | 数据形态 |
|---|---|---|
| 内存模型 | `MusicScoreMaker/Ingame/Models/MusicScoreMakerData.cs` | `NoteList` + `MusicScoreEventDataList` + `MusicScoreTicksMax`，**时间单位是 tick**（1920/小节，480/拍） |
| 磁盘 | `persistentDataPath/music_score_maker/*.json` | Newtonsoft JSON（不是 MessagePack），有 `autosave_` 前缀的自动存档 |
| 曲包内 | `persistentDataPath/CustomMusicScores/<title>_<id>/score.json` | 同上 + `manifest.json` |
| SUS 导入 | `Sekai/SUS/Converter.cs` | SUS 文本 → `MusicScore`（live 模型）→ `MusicScoreMakerData` |
| 试玩 | `_model.ToMusicScore(focusTicks)` → `FreeLiveBootData`（`MusicScoreMakerPresenter.cs:7092`） | 转成 live 的 `MusicScore`（Note 数组 + 事件数组） |

### B. 曲包：导入 → 播放

`Assets/CustomMusicScoreManager/Runtime/Services/CustomMusicScoreManagerService.cs`

- 支持**文件夹**和 **zip** 两种导入（`:136-179`），导出 zip（`:181-212`）；
- 每曲一个目录：`audio.ogg|mp3|wav` + `jacket.png` + `score.json`（或 `.sus/.txt` 现场转换）+ 可选视频；
- `MusicId` 由曲包 id 做 FNV-1a 后**取负值**，避免和官方曲 id 撞号（`MusicScoreMaker/Common/CustomMusicScoreEntry.cs:218-233`）；
- 音频解码成 `AudioClip` 后通过 `SoundManager.RegisterExternalAudioClip(...)` 注册进"伪 CRI"，cue 名为 `custom_<id>`。

### C. 录屏：live → mp4

`Assets/CustomMusicScoreManager/Runtime/Services/` 下的 `VideoGenerationService` / `VideoPostProcessor` / `WindowsVideoEncoder`（调 `ffmpeg/ffmpeg.exe`）/ `AndroidVideoEncoder`（MediaCodec）。live 侧有专门的「视频生成模式」（`LiveLogic.DetectVideoGenerationMode`），会禁用暂停等交互。

> 这条链只在 Windows + Android 上做过；`ffmpeg/` 目录（185 MB）就是为 Windows 端录屏准备的，**和音频解码无关**。

---

## 7. 运行时数据落在哪

全部在 `Application.persistentDataPath`（Windows：`%USERPROFILE%\AppData\LocalLow\<company>\<product>\`）：

```text
ApplicationLocalSettings.json        音量等本机设置（ApplicationLocalSettings.cs:10,76）
LiveSettingData.json                 live 设置：延迟、流速、音符皮肤、帧率档位（LiveSettingData.cs:47,318）
版本号配置(Resources)               旧版 `OtaChecker` 读取的只读资源；文件从未入库，`OtaChecker` 已在 2026-11 清理中删除
music_score_maker/                   编辑器存档与自动存档（MusicScoreMakerRepository.cs:176）
  ├─ <id>.json
  └─ autosave_<时间戳>.json
inappcache/
  ├─ MusicScoreMakerSettingData.json 编辑器偏好（吸附、自动播放等）
  └─ ClipboardCacheListData.json     复制粘贴缓存
CustomMusicScores/                   第三方曲包（CustomMusicScoreStorage.cs:15）
  ├─ <title>_<id>/{manifest.json,score.json,audio.*,jacket.png}
  ├─ Exports/*.zip
  └─ PlayHistory.jsonl               播放历史
SavedVideos/                         录屏输出
OpenSekaiLogs/                   日志文件
```

**没有 PlayerPrefs，没有云端，没有数据库**。所有持久化都是「json 文件 + 手写路径」。

---

## 8. 资源与打包

### 8.1 两条并行路径

| 路径 | 用于 | 入口 |
|---|---|---|
| **Resources** | UI prefab、`ScreenLayerData`、配置资产、字体、shader、内置音效 | `Resources.Load` 直接调，或 `ResourceLoader` |
| **AssetBundle** | 可打包更新的资产（`Assets/Sekai/assetbundle/resources/**`、`builtinassets/**`） | `AssetBundleManager` → `AssetBundleLoader` |

工程里名为 `Resources` 的目录有 **3 个**，Unity 把它们合并成一个虚拟根（所以 `Resources.Load("Screen/Prefabs/X")` 可能来自任意一个）：

- `Assets/Resources`（主体：screen、dialog、prefabs、shader、spriteatlas、wording…）
- `Assets/CustomMusicScoreManager/Resources`（只有 `Screen/Prefabs/ScreenLayerMusicScoreMakerTop.prefab`，即首页）
- `Assets/TextMesh Pro/Resources`（插件自带）

另有 `Assets/Sekai/assetbundle/resources` 与 `Assets/Sekai/builtinassets/assetbundle/resources` 两个**小写 `resources`**，它们是 AssetBundle 的源目录（靠 assetBundleName 标签打包），不是 Resources 虚拟根。⚠️ 大小写是否会被 Unity 当成特殊目录，取决于平台文件系统的大小写敏感性，本仓库按「不是 Resources」使用（未在 Android 上实测）。

### 8.2 AssetBundle 构建

菜单：**`OpenSekai / AssetBundles / Build AssetBundles And Info`**（`Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs:17,24`），并且注册了 `BuildPlayerWindow` 钩子——**点 Unity 的 Build 会先自动打 AB**。

- 打包范围**由 assetBundleName 标签决定**，不按目录硬编码（`:34` `AssetDatabase.GetAllAssetBundleNames()`）；
- 输出先到 `Library/OpenSekaiAssetBundles/<BuildTarget>`，再拷到 **`Assets/StreamingAssets/data`**（`:21-22,47-55`）；
- 文件名是 **bundleName 的 md5**（`:169-184`），目录名是父路径 md5 前 4 位 —— 不可读，靠清单反查；
- 清单只有一份：**`Assets/StreamingAssets/data/AssetBundleInfo.bytes`**（MessagePack，`:20,138-146`），内含 `version` + `bundles` 字典（含依赖、crc、size、category）。

### 8.3 编辑器 vs 真机

`AssetBundleManager` 在 `UNITY_EDITOR` 下**完全不读 AB 文件**，直接走 `AssetDatabase` 现场构清单（`Sekai/AssetBundleManager.cs:501-508,558-598`）。所以：

- 编辑器里改资源不用打包，直接生效；
- 打完包在 Windows 上跑，`StreamingAssets/data` 走 `AssetBundle.LoadFromFile`；
- Android 上 `StreamingAssets` 是 `jar:` URI，改走 `UnityWebRequest`（`AssetBundleManager.cs:492-497`）。

**没有远端 CDN**：全仓 grep 不到 AB 下载的 baseUrl，`AssetBundleDownloadHandler` 整体是 `throw null` 空桩。

---

## 9. 模块现状清单（改之前先看这张表）

### 9.1 活的

| 模块 | 证据 / 备注 |
|---|---|
| 谱面编辑器（模型、命令、撤销、视图、吸附、多选） | `MusicScoreMaker/Ingame/**`，58k 行，功能完整 |
| 谱面存档（JSON 读写、自动存档、版本迁移） | `MusicScoreMakerRepository`、`MigrateToCurrentVersion()` |
| SUS 导入 | `Sekai/SUS/Converter.cs`（**只导入，无导出**） |
| live 判定与计分 | `LiveLogic`、`NoteBase` 家族、`ScoreLogic` |
| 音符视图与对象池 | `NotesViewManager`、`NotePool`、`BaseNoteView` 家族 |
| 音乐时钟与音频解码 | `SoundManager` + `CriAtomShim` + `AcbDecoder`（HCA 全解码） |
| 曲包导入/导出/播放 | `CustomMusicScoreManagerService`、`CustomMusicScoreEntry` |
| 视频导出 | Windows（ffmpeg）/ Android（MediaCodec） |
| 高帧率档位 | `CP/FramerateUtility.cs` + `LiveSettingData.FrameRateLimit` |
| 首页设置面板 | `ScreenLayerCustomMusicScoreManager`（3905 行） |

### 9.2 半残（能跑但被简化）

| 模块 | 缺什么 |
|---|---|
| 页面转场动画 | `ScreenLayer.PlayStartAnimation/PlayExitAnimation` 空壳 |
| 页面栈返回语义 | `LimitedStack(16)` 溢出不报警 |
| 背景 / Header / 加载页 | `ScreenLayerBackground`、`ScreenLayerHeader` 里大量 `TODO(original)` |
| 高帧率下的输入时刻 | 触摸回推 `(touch.time - currentGameTime) * 1/60` 量纲不一致，等效于只按当帧音乐时间判定（细节见实现细节 §7.3） |
| 音频时钟 | 实际用的是 `Stopwatch` 而非 `AudioSettings.dspTime`（shim 里明确弃用 dspTime） |
| 编辑器 OutGame 流程 | Create / Publish / SaveDraft 依赖的原版 Service 大量是桩 |

### 9.3 桩（`throw null` 或空实现，别指望）

`APICore.SendRequest`、`APICaller`、`AssetBundleDownloadHandler`、`AssetBundleMetaManager`、`LruAssetCache*`、`InputManager`、`MusicScoreMakerPublishExecutor`、`SoundUtility.SetupGlobalSeSettings`、`Sekai.Rendering` 中约 2/3 的 RendererFeature、`Background3DView`、MultiLive / SuperVirtualLive 的全部玩法，以及 221 个没有 prefab 的页面。

**判定依据**：`grep -rn "throw null" Assets/Scripts/Assembly-CSharp | wc -l`，以及 `grep -rn "TODO(original)"`。

---

## 10. 常见改动索引

| 我想…… | 去哪 |
|---|---|
| 加一个设置项到首页 | `ScreenLayerCustomMusicScoreManager.cs`（UI）+ `LiveSettingData` / `ApplicationLocalSettings`（存储） |
| 改判定窗口 | `Sekai/Live/LiveConfig.cs:186-209`（以**帧**为单位，`CalcFrameToTime = frame/60`） |
| 加一种音符 | `Sekai/Live/NoteCategory.cs` + `NoteBase` 子类 + `NoteType` + `NotesViewManager.CreateNotePool` + 编辑器侧的 `MusicScoreNoteBase` / NoteCategory 映射 |
| 改谱面存档格式 | `MusicScoreMakerData`（`VersionCode` + `MigrateToCurrentVersion`） |
| 加一个页面 | `MenuScreenType`（新枚举值）+ `ScreenLayerData` 资产（`ScreenType` 填同值）+ `Resources/Screen/Prefabs/<同名>.prefab` + `ScreenLayer` 子类 |
| 改曲包格式 | `CustomMusicScoreManifest` + `CustomMusicScoreManagerService` |
| 改录屏 | `CustomMusicScoreManager/Runtime/Services/Video*` |
| 改打包流程 | `Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs` |
| 让页面切换有动画 | `ScreenLayer.PlayStartAnimation/PlayExitAnimation` 接 `ScreenInOutAnimBase`/`ScreenSlideInOut` |
| 找美术资产（图集 / 音符皮肤 / 打击·结算特效 / 字体 / 缺失项） | [`美术资产（已过期）.md`](art-assets.md) |

**连带修改提醒**（这个工程里最容易漏的同步点）：

1. 新增页面：`MenuScreenType` 枚举 ↔ `ScreenLayerData.ScreenType` ↔ `EntryScreenLayers.asset` 的 Entries ↔ prefab 文件名，**四处名字/数值必须一致**；
2. 新增 `DisplayLayerType`：场景里必须有同名子物体（`ScreenManager` 用 `transform.Find(layerType.ToString())`）；
3. 新增音符类型：编辑器侧（`MusicScoreMaker`）和 live 侧（`Sekai.Live`）是**两套独立的类型**，靠 tick 映射，不要只改一边。

---

## 11. 术语表

| 术语 | 含义 |
|---|---|
| **tick** | 编辑器的时间单位。`TICKS_PER_BAR = 1920`、`TICKS_PER_BEAT = 480`（`MusicScoreMakerUtility.cs:62-64`） |
| **BPM / HighSpeed / TimeSignature / SeVolume 事件** | 编辑器的变速、倍速、拍号、SE 音量不是字段，而是「打在某个 tick 上的事件」 |
| **Filler / fillerSec** | 音频开头到谱面第 0  tick 之间的空白秒数，取时间时要减掉 |
| **SUS** | 社区谱面交换格式（文本），本工程只支持导入 |
| **ACB / AWB / HCA** | CRI ADX2 的容器 / 音轨包 / 音频编码；本工程用纯 C# 解码 |
| **Cue / CueSheet** | CRI 的「音效条目 / 音效表」概念，`SoundManager` 的 API 全是 cue 维度 |
| **NoteCategory / NoteType** | 音符的大类（Normal/Long/Flick/Friction…）与具体形态（含 Critical 变体） |
| **Decoration（装饰音符）** | 不参与连击与判分的视觉音符（本仓库新增） |
| **MusicScoreInfo** | 每帧的谱面状态快照：bar、bpm、time、timeSignature、speedRatio |
| **LiveBootData** | 进入 live 时传的参数包（曲目、难度、谱面、音量、返回页、是否自动播放…） |
| **ScreenLayer / ScreenLayerData / MenuScreenType** | 页面基类 / 页面配置资产 / 页面 ID 枚举 |

---

## 12. 上手三步

1. 用 **Unity 2022.3.62f3** 打开本工程，等首次导入（体积 549 MB，包管理会拉 UniTask 等 git 依赖）。
2. 打开 `Assets/Sekai/MusicScoreMaker/Ingame/Scenes/MusicScoreMaker.unity`，Play。首页是曲包管理器；先导入一个曲包或点「编辑」进入编辑器。
3. 想验证「编辑器 → live」这条链，直接在编辑器里按试玩；想单独调 live，可以在 `Core.unity` 里手动设 `Sekai.Core.EntryPoint.PlayMode = SoloLive` 并构造 `UserDataManager.FreeLiveBootData`（细节见 [`实现细节（已过期）.md`](old-implementation-notes.md) 第 8 节）。

> 旧版首次运行会联网检查更新（`OtaChecker` 请求作者自建服务器）。该模块在 2026-11 清理中整类删除，现版本不再发起任何更新检查。
