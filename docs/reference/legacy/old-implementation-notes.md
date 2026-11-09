# OpenSekai 实现细节

> 目的：重构前对引擎内部实现（页面、live、音频、编辑器）的逐文件梳理，仅作历史参考。
> 适用读者：需要追溯旧实现细节的人。
> 前置条件：无。
> 最后更新：2026-11-02（仅目录整理）

> ⚠️ 历史文档：其中编辑器相关章节描述的对象已被删除；引擎部分（页面系统、live、音频）仍有参考价值，
> 但当前事实以 [架构](../README.md) 下的文档为准。


> 与 [`项目总览（已过期）.md`](old-project-overview.md) 配套：总览讲「有哪几块、怎么串起来」，本文讲「每一块具体怎么实现的、改的时候要注意什么」。
>
> 所有 `路径:行号` 都相对仓库根目录。行号会随代码演进漂移，**以类名 / 方法名为准，行号只用于快速定位**。

---

## 0. 读码指南

### 0.1 三步判断一段代码是活的还是壳

```bash
# 1) 反编译骨架：类型在、方法体是 throw null
grep -rn "throw null" Assets/Scripts/Assembly-CSharp | head

# 2) 被简化的实现：注释里写明原版行为
grep -rn "TODO(original)" Assets/Scripts/Assembly-CSharp

# 3) 空的虚方法 / 空实现
grep -rn "Placeholder\|NotImplemented" Assets --include=*.cs
```

典型对照：`AssetBundleDownloadHandler`（全 `throw null`）、`APICore.SendRequest`（`TODO(original)` + 顺序触发事件）、`ScreenLayer.PlayStartAnimation`（空壳直接 Done）。

### 0.2 三条贯穿全局的约定

| 约定 | 内容 | 影响 |
|---|---|---|
| **时间** | 编辑器用 **tick**（1920/小节、480/拍）；live 用 **毫秒/秒**（float） | 两边转换集中在 `MusicScoreMakerUtility` 与 `MusicScore` |
| **判定窗口** | 以 **帧** 定义再 `/60`（`LiveConfig.CalcFrameToTime`） | 帧率调到 120 后，窗口的物理时长不变 |
| **音轨** | 12 轨（`MusicScoreMakerModel.LaneCount = 12`，`:1101`）；lane 是 float，允许半轨 | 判定用 `LaneDistance` 平方距离匹配 |

### 0.3 坐标系 / 视觉模型速记

- **live 中的音符是 `SpriteRenderer`**（世界空间 2D 精灵），由 `BaseNoteView`（`Sekai/BaseNoteView.cs:5-50`）驱动 `transform.localPosition`；
- **编辑器中的音符是 UGUI `RectTransform`**（`NotePreview` 实例池），按可视 tick 区间实例化（`Ingame/Views/NotesPreview.cs:55-60`）；
- **UI 全部是 UGUI + Canvas 分层**，8 个层见 `Sekai/DisplayLayerType.cs`。

---

## 1. 启动与初始化

### 1.1 两个入口类，别混

| 类 | 所在脚本 | 挂在哪 | 职责 |
|---|---|---|---|
| `SekaiSingletonManager` | `Sekai/SekaiSingletonManager.cs` | `Core.unity` 的 `Singleton` 物体 | 全局初始化 + `DontDestroyOnLoad` + 计算 `DeltaTimeK` |
| `Sekai.SceneEntryPoint` | `Sekai/SceneEntryPoint.cs` | 不直接挂 | 场景入口**基类**：订阅 `SceneManager.OnExitScene`，退出场景时退订 |
| `MusicScoreMakerEntryPoint` | `Sekai/MusicScoreMaker/Ingame/Presenters/MusicScoreMakerEntryPoint.cs` | `MusicScoreMaker.unity` 的 `EntryPoint` | 主场景入口（继承 `SceneEntryPoint`） |
| `Sekai.Core.EntryPoint` | `Sekai/Core/EntryPoint.cs` | `Core.unity` 的 `EntryPoint` | live 场景入口（**不继承** `SceneEntryPoint`） |
| `SceneTransitionBlank` | `Sekai/SceneTransitionBlank.cs` | `TransitionBlank.unity` | 转场空场景的推进器 |

### 1.2 `SekaiSingletonManager.OnInitialize()`（`SekaiSingletonManager.cs:31-39`）

```text
FramerateUtility.SetFrameRate()        // 初始 = 跟随屏幕刷新率
EnsureEventSystem()                    // 优先 Resources "Common/Input/EventSystem"，否则自建 StandaloneInputModule
TextMeshProUtility.SetupBuiltinFontAsset()
AssetBundleMetaManager.Instance.Initialize()   // 空实现
AssetBundleManager.Instance.Initialize()       // 真正读 StreamingAssets/data 清单
SoundUtility.SetupGlobalSeSettings(GlobalSeSettings.Normal)  // 空实现
```

`Update()` 里的小细节（`:41-44`）：

```csharp
deltaK = Time.timeScale * Time.deltaTime * Application.targetFrameRate;
```

`DeltaTimeK` 是「以 60fps 的 1 帧 = 1」为基准的归一化帧间隔。**注意当 `targetFrameRate = -1`（跟随屏幕且未显式设值）时这个值是负或 0**，用到它的逻辑要自己兜底。

### 1.3 主场景入口的实际动作

`MusicScoreMakerEntryPoint`（`.../MusicScoreMakerEntryPoint.cs`）：

- `Awake`（`:30-46`）：日志（日文「譜面ツールへ遷移しました」）、`EnsureEventSystem`、TMP 字体、`FramerateUtility.SetFrameRate()`、`GarbageCollector.GCMode = Enabled`、AB 初始化、`OtaChecker.Instance.CheckForUpdates()`。
- `Start`（`:48-114`）：
  1. `ApplicationLocalSettings.LoadFromStorage()`，必要时 `SetupLiveVolume()`；
  2. `SoundManager.SetupVolume(1, Bgm, Se, Voice)`；
  3. 预载 `sound/menu/menu_common` 音效包；
  4. `ScreenManager.CreateEmptyBaseCamera()` + `FadeIn`；
  5. `AddScreen(Header)`、`AddScreen(TouchEffect)`、`AddScreen(InsertNoti)` —— 常驻叠层，**不进栈**；
  6. `PushUIScreen(MusicScoreMakerTop)` 或（带 bootArg 时）`PushUIScreen(MusicScoreMaker, bootArg)`。

> `:94-123` 有一段「从编辑器带设置返回」的特例：`MusicScoreMakerPresenter._isReturningToEditorWithSettings` 为真时，用反射找 `ScreenLayerCustomMusicScoreManager.OpenSettingsAfterReturnFromEditor()`，延迟两帧再调。**这是本仓库自己加的补丁式实现**，改动首页/设置相关逻辑时留意。

### 1.4 `Sekai.Core.EntryPoint`（live 场景入口）

```csharp
// Sekai/Core/EntryPoint.cs:10-41
Awake → CreateController().Forget()
  await UniTask.WaitUntil(ShaderPreloader 不在 Loading/Compiling)
  ShaderPreloader.Status = None
  销毁残留的 SceneTransitionLoadingIndicator
  switch (PlayMode) { case SoloLive: AddComponent<SoloLiveController>(); }
```

`PlayMode` 是**静态字段**（`EntryPoint.cs:8`），由进 live 之前的人写好（`MusicScoreMakerPresenter.PlayTestLiveCore`）。`default` 分支只有一句 `Debug.LogWarning("Unsupported Core PlayMode")` —— **如果你直接 Play `Core.unity` 而不设 PlayMode，什么都不会发生**。

---

## 2. 场景路由：`Sekai.SceneManager`

文件：`Assets/Scripts/Assembly-CSharp/Sekai/SceneManager.cs`（174 行，纯逻辑，不碰 UI）

### 2.1 结构

```csharp
public enum Scene { None=0, Title=0, OutGame=1, VRMode=2, TransitionBlank=3, Core=4,
                    VirtualLiveLobby=5, Splash=6, SuperVirtualLiveLobby=7, Mysekai=8,
                    MusicScoreMaker=9 }
```

- 单例：`Instance` 走 `GameObject.Find("SceneManager")`，找不到就 `new GameObject`（`:42-54`）——所以它**不依赖场景里预先摆放**。
- 状态：`CurrentScene / PrevScene / NextScene`，以及事件 `OnExitScene`。

### 2.2 `RequestScene(scene)` 的三段式（`:60-83`）

```csharp
NextScene = scene;
Scene load = ResolveLoadScene(scene);      // ① 决定是否绕道 TransitionBlank
if (scene == Scene.MusicScoreMaker) SoundManager.Instance.StopIngame();   // ② 停 live 音乐
if (ScreenManager.ExistsInstance) ScreenManager.Instance.ExitScene();     // ③ 先让页面层收尾
OnExitScene?.Invoke();                     //    再广播给各 SceneEntryPoint
UnitySceneManager.LoadScene(GetSceneName(load));
PrevScene = CurrentScene; CurrentScene = load;
```

**必须有 `GetSceneName` 返回非空**才会真的 `LoadScene`；`Title / OutGame` 等没有场景文件的值会被静默忽略（`:70-72`）。

### 2.3 TransitionBlank 的作用

`TransitionBlank.unity` 只有一个相机 + `SceneTransitionBlank`，它的存在是为了**避免两个重场景同时在内存里**（尤其 MusicScoreMaker 与 Core 都带大量 UI/纹理）。规则见总览 §4.2；对应代码 `:85-146`。

改动提示：如果你新增场景，**必须同时**改 `GetSceneName`（`:148-161`）和 `ResolveLoadScene` 的集合判定，否则新场景永远加载不出来或永远绕道。

---

## 3. 页面路由：`Sekai.ScreenManager`

文件：`Sekai/ScreenManager.cs`（4011 行）。这是全工程最重的「框架类」，同时管：页面注册、实例化、栈、层级 Canvas、相机栈、blur、弹窗、加载/网络指示器、返回键。

### 3.1 关键数据结构

| 字段 | 类型 | 作用 |
|---|---|---|
| `screenMap` | `Dictionary<MenuScreenType, ScreenLayerState>` | 由 `EntryScreenLayers` 资产填充，**注册表** |
| `uiScreenStack` | `CP.LimitedStack<Tuple<ScreenLayerState, object, BootArgBase>>(16)` | 返回栈，元素含页面状态、栈对象、启动参数 |
| `currentUI / nextUI / prevUI` | `ScreenLayerState` | 当前/正在切换/上一页 |
| `cameraStack` | `ScreenCameraStack` | URP overlay 相机栈 |
| `dialogQueue` | 队列 | 弹窗串行化 |
| `disableTapCommandObjects` | 字典 | 切页时屏蔽点击 |

`ScreenLayerState`（`Sekai/ScreenLayerState.cs`）持有：`Data`（ScreenLayerData）、`RefInstance`（实例 GameObject）、`ScreenLayer`（组件）。

### 3.2 注册：230 条注册 → screenMap

```csharp
// ScreenManager.cs:1833-1855
const string path = "Screen/SceneEntry/EntryScreenLayers";
var list = Resources.Load<ScreenLayerDataList>(path);
foreach (var entry in list.Entries) screenMap[entry.ScreenType] = new ScreenLayerState { Data = entry };
```

- 资产：`Assets/Resources/screen/sceneentry/EntryScreenLayers.asset`（**磁盘上是全小写路径**，代码里大小写混写；Windows 下无碍，换平台需自行验证）。
- `ScreenLayerData` 是 ScriptableObject，字段见 `Sekai/ScreenLayerData.cs:5-29`：`ScreenType`、`DisplayLayer`、入/出场动画类型与遮罩、Header 显示策略、`EnableBackUIScreen`、`bgmType`、`BackgroundType`、`hasLayerCamera`/`layerCameraPriority` 等。
- prefab 解析靠**资产名**：

```csharp
// Sekai/ScreenLayerData.cs:30-37
Resources.Load<GameObject>("Screen/Prefabs/" + name) ?? Resources.Load<GameObject>("screen/prefabs/" + name);
```

### 3.3 实例化：一次 push 做了什么

`InstantiateLayerScreen()`（`ScreenManager.cs:2310-2350`）：

```text
GetLayerObject(layer.Data.DisplayLayer)      // transform.Find(layerType.ToString()) → Layer_UI / Layer_Dialog ...
prefab = layer.Data.LoadScreenLayerPrefab()
Instantiate(prefab, layerObject.transform)
instance.name = data.name
layer.ScreenLayer = instance.GetComponent<ScreenLayer>()   // 找不到 → LogError "Not Found ScreenLayer"
if (data.hasLayerCamera) cameraStack.AddToStack(screenLayer.LayerCamera, data.layerCameraPriority)
```

**层物体必须存在**：`GetLayerObject`（`ScreenManager.cs:2147-2160`）在 `CanvasRoot` 下按名字找 `transform.Find(layerType.ToString())`，并缓存到 `displayLayersCache`。场景里那 8 个 `Layer_*` 就是它。新增 `DisplayLayerType` 却没有对应子物体 → `LogError("Not Found Layer;...")`。

### 3.4 栈语义

| API | 行为 |
|---|---|
| `PushUIScreen(type, bootArg, isWaitExitAnimation)` | 当前页入栈 → 新页实例化并启动；`bootArg` 随栈元素保存 |
| `ChangeUIScreen(type, ...)` | 替换当前页，不入栈 |
| `BackUIScreen(...)` | 出栈并回到上一页（把保存的 bootArg 回传） |
| `ForceBackUIScreen(...)` | 跳过 `OnWillExit` 校验直接退 |
| `AddScreen(type)` | 叠加常驻层（Header / TouchEffect / InsertNoti），不进栈 |
| `RemoveScreen(type)` / `RemoveScreenAsync` | 销毁常驻层 |
| `PreloadUIScreen` / `RebuildScreen` | 预实例化 / 强制重建 |
| `PopScreenStack` / `PushScreenStack` / `GetScreenStackList` | 直接操作栈（不负责实例化） |
| `ClearScreenStack` | 清空返回链 |

栈只有 16 深，溢出**静默丢最老**（`CP/LimitedStack.cs:31-38`）。

### 3.5 弹窗

`ScreenManager.ShowSubWindowDialog / ShowDialog / Show1ButtonDialog / Show2ButtonDialog / ShowMultiButtonDialog`（`:3063-3168`），prefab 从 `Resources/Dialog/<DialogType 名>` 或 `dialog/<...>` 加载（`:3041-3050`）。

`DialogBase`（`Sekai/DialogBase.cs`）实现 `IBlurTarget`，弹窗打开时会让其下层 Canvas 参与 blur（见 §3.7）。

### 3.6 返回键

`BackKeyChecker`（`Sekai/BackKeyChecker.cs`）由 `ScreenManager.Awake` 创建，每帧 `Check()`：只看 `Input.GetKeyDown(KeyCode.Escape)`，再做一次 EventSystem 射线判定，命中 UI 就把事件交给 UI；否则走分派：

```text
Mysekai 覆盖 → 当前场景注册的 handler（SetSceneExecuteBackKeyProcess）
            → OnExecuteBackKeyProcess
            → 默认 BackUIScreen()
```

（`ScreenManager.cs:3356-3377`）

> 移动端的实体返回键并没有单独接（Android 的 back 走 Unity 的 Escape 映射）。

### 3.7 相机栈与 blur

- 每个自带相机的页面（`ScreenLayerData.hasLayerCamera`）会被 `ScreenCameraStack.AddToStack` 加入 URP 的 overlay 栈；`IsFinalCamera` 决定 blur 只在最后一台相机上跑（`Sekai/SekaiAdditionalCameraData.cs:9-13`）。
- blur 的实际渲染在 `Sekai/Rendering/SekaiLegacyDrawUIRendererFeature.cs`（5 个 pass 的 UI 模糊链），缓冲区 `SekaiUIBuffer.cs`。

### 3.8 加载 / 网络指示器 / 资源清理

用「持有者（holdObject）引用计数」的方式管理，避免两个系统同时收：

- `ShowLoadingScreen(holdObject)` / `ReleaseLoadingScreen(holdObject)`（`:2693-2734`）
- `ShowNetworkIndicatorScreen` / `ReleaseNetworkIndicatorScreen`
- `ShowAssetRemovingScreen` / `ReleaseAssetRemovingScreen`

---

## 4. `ScreenLayer` 生命周期

文件：`Sekai/ScreenLayer.cs`（233 行）

```text
OnScreenLayerBoot(data, bootArg)   → state=Booting → OnBoot(bootArg) → ScreenBootDone()=InitComponent
OnScreenLayerInitComponent()       → state=InitComponent → OnInitComponent()
OnScreenLayerStart()               → state=Starting  → PlayStartAnimation() → StartAnimationDone()
                                                                              → state=Playing
                                                                              → OnFinishStartAnimation()
                                                                              → OnScreenStart()
OnWillExit()                       → ScreenWillExitDone(OK/Cancel)
OnScreenLayerExitStart()           → state=Exiting → OnExitStart() → PlayExitAnimation() → ScreenExitDone()
                                                                                          → state=Exit
                                                                                          → OnScreenLayerExited() → OnExited()
OnScreenLayerExitScene()           → OnExitScene()（场景切换时）
```

**当前实现的偏差**（很重要）：

| 位置 | 现状 |
|---|---|
| `:115-124` `OnScreenLayerBoot` | 直接 `OnBoot → ScreenBootDone`，无 boot 协程状态机 |
| `:158-161` `PlayStartAnimation` | 直接 `StartAnimationDone()`，**没有入场动画** |
| `:175-182` `OnScreenLayerExitStart` | `yield break`，**没有退场动画** |
| `:126-144` `ChangeStartAnimationType` 等 | 空方法 + `TODO(original)` |
| `:184-192` `ExitModeManual/Auto` | 空方法 |

`BootArgBase` 是空基类（`:32-34`），每屏自定义派生类携带参数，例如 `ScreenLayerMusicScoreMaker.BootArg`（`.../Presenters/ScreenLayerMusicScoreMaker.cs:15-38`，25 个字段，其中 `CustomMusicScoreEntry` / `MusicScoreMakerData` / `FinishTransitionCallback` 是关键）。

**接动画的改法**：`ScreenInOutAnimBase`（`Sekai/ScreenInOutAnimBase.cs`）、`ScreenSlideInOut`（DOTween，0.2s，OutQuart）和接口 `IScreenInOutAnim` 都已存在，目前只有 `SubWindowSlideAnimation` 在用；把 `PlayStartAnimation/PlayExitAnimation` 改成 `StartCoroutine(anim.Play(...))` 即可，注意 `state` 必须停在 `Starting/Exiting` 直到回调 `StartAnimationDone/ExitAnimationDone`。

---

## 5. 资源层

### 5.1 职责划分

```text
业务代码
  ├─ Resources.Load（简单直接，工程里大量这么写）
  ├─ AssetManager（SingletonMonoBehaviour）        引用计数 + 依赖 + 两类后端
  │    ├─ ResourceLoader  → Resources.Load / LoadAsync
  │    └─ AssetBundleLoader → AssetBundleManager
  └─ AssetBundleManager（单例，管清单与 bundle 文件）
```

`AssetManager`（`Sekai/AssetManager.cs`）三个容器：`loadAssets`（正在加载）、`referenceCountTable`（引用计数）、`cacheMap`（已加载）。

### 5.2 `AssetBundleManager`

关键常量与流程（`Sekai/AssetBundleManager.cs`）：

| 项 | 值 | 行 |
|---|---|---|
| 清单路径 | `StreamingAssets/data/AssetBundleInfo.bytes` | 由 `CacheDirectoryName = "data"` 拼接，`:71` |
| 清单类型 | `AssetBundleInfo { version, bundles: Dictionary<string, AssetBundleElement> }` | `AssetBundleInfo.cs:9-13` |
| 单条 | `AssetBundleElement { bundleName, cacheFileName, cacheDirectoryName, hash, category, crc, fileSize, dependencies[], paths[], isBuiltin, isRelocate }` | `AssetBundleElement.cs:8-39` |
| 初始化 | 读清单 → 建 `categoryResourceMap` → `serverAssetBundleInfo = clientAssetBundleInfo`（**没有服务端**） | `:91-99, 630-663` |
| 加载 | 用 `cacheDirectoryName/cacheFileName` 拼路径；Windows/编辑器 `AssetBundle.LoadFromFile`；Android(`jar:`) `UnityWebRequest` | `:143-181, 492-497` |
| 编辑器分支 | 不读 AB 文件，用 `AssetDatabase.GetAllAssetBundleNames()` 现场造清单，`cacheFileName = bundleName` | `:501-508, 558-598` |
| 下载 | `StartCategoryDownload` 直接返回成功；`AssetBundleDownloadHandler` 全 `throw null` | `:399-405` |

**校验和**：`AssetBundleElement.crc/hash` 存在，但当前加载路径**没有校验**（直接用）。

### 5.3 清单里的「假压缩目录名」

构建时文件名不是可读名，而是：

```csharp
// Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs:169-184（摘要）
cacheFileName      = md5(bundleName)                  // 32 hex
cacheDirectoryName = md5(parentPath(bundleName)).前4位 // 4 hex
```

所以 `StreamingAssets/data` 下看到的是一堆哈希目录 —— 想知道谁是谁，只能查 `AssetBundleInfo.bytes`。

### 5.4 已知缺陷

- `AssetBundleStream` 读到位置 <128 时会**按位取反**（`Sekai/AssetBundleStream.cs:62-69`），但 `AssetBundleManager` 构造资源时 `Stream = null`（`:195`），这条路径实际未启用；
- `LoadAssetBundleFromStreamingAssetsUri` 用 `Thread.Sleep(1)` **在主线程忙等** UWR（`:445`、`:465`），会造成掉帧；
- `LruAssetCache*`（3 个文件）是 LRU 缓存骨架，方法体大量 `throw null`，别依赖。

---

## 6. 音频层

这是本工程最「不按常规」的一块：**没有用官方 CRI 运行时，而是纯 C# 重实现了一份同名兼容层**。

### 6.1 三层结构

```text
SoundManager（Sekai/SoundManager.cs，1315 行，业务 API）
    ↓ 用 CriWare.CriAtomExAcb / CriAtomExPlayer / CriAtomExPlayback
CriAtomShim（Assets/Scripts/Assembly-CSharp/CriWare/CriAtomShim.cs，932 行，兼容层）
    ↓ 用 AcbDecoder
AcbDecoder（Assets/Scripts/Assembly-CSharp/AcbDecoder/**，12 文件 2000 行，ACB/HCA → float PCM）
    ↓
Unity AudioClip + AudioSource（CriAtomAudioRuntime 内部）
```

**为什么不会被官方 CRI 抢走**：`Assets/CRIMW` 里的官方运行时（`CriMw.CriWare.Runtime` 程序集）确实会被编译，但 `Assembly-CSharp` 内部定义了**同名的 `CriWare.CriAtomEx*` 类型**；C# 的名字解析优先取当前程序集，所以 `SoundManager` 永远绑到 shim。官方运行时实际上不参与运行。

**ACF 是假的**：`CriAtomEx.RegisterAcf` 直接 `return true`，注释写明「直接解码 ACB/HCA，不需要 ACF」（`CriAtomShim.cs:52-56`）。所以 `Assets/StreamingAssets/ProjectSekai.acf` 只是摆设。

### 6.2 `SoundManager` 的 API 分组（按用途记）

| 分组 | 方法 | 说明 |
|---|---|---|
| 音量 | `SetupVolume(master, bgm, se, voice)` | 三路分轨 |
| 音效包 | `LoadSoundBundle(bundleName, isResident)` / `IsLoadedSoundBundle` | bundle 名形如 `live/sound/live_se/default`、`live/tap_se/<name>`、`sound/menu/menu_common` |
| cue 表 | `Load(cueSheetName, acbFileName)` / `ExistsCueName` / `GetCueInfos` | ACB 内多个 cue |
| 外部音频 | `RegisterExternalAudioClip(bundleName, cueName, clip)` | **曲包用这条**（`SoundManager.cs:204-239`），把解码好的 AudioClip 注册进伪 CRI |
| BGM（live） | `PrepareIngameBGM(cueName, startTime, cb, loop)` / `ResumePreparedPlaybackIngame` / `ResumeIngame(ms)` / `StopIngame` | 打歌主音轨 |
| SE | `PlaySE(cue)` / `PlaySEOneShot` / `PlayIngameSE` / `StopSE` | |
| 时钟 | `SetAudioSyncedUnityTimer(requestId)` / `TryGetAudioSyncedUnityTimer(out ms)` / `GetAudioSyncedUnityTimer()` | **live 的时间源** |
| 音量/音高 | `SetIngameBgmPitch` / `SetIngameBgmVolume` | 负流速/变速用 |
| 暂停 | `Pause` / `Resume` | |

### 6.3 ACB 文件从哪来

`GetAcbFileNameCandidates`（`SoundManager.cs:157-190`）会依次试：

1. 打进 AB 的 `.acb.bytes`（例如 `Assets/Sekai/assetbundle/resources/tutorial/sound/menu/menu_common/MenuCommon.acb.bytes`）
2. `StreamingAssets` 下的 `.acb`（`MenuCommon.acb`、`BGM_TITLE*.acb`、`SE_*.acb`、`TitleCall.acb`…）

`SoundBundleBuildData`（`Sekai/SoundBundleBuildData.cs:5-23`）描述「bundle → 若干 ACB 文件 + cueSheetName + 拆分份数」。

### 6.4 ACB/HCA 解码实现（AcbDecoder）

| 文件 | 职责 |
|---|---|
| `AcbFile.cs` | ACB 容器入口：解析 `@UTF` 表与 AWB 索引，`GetCueNameList()` 等 |
| `CriUtfTable.cs` | CRI 自定义 UTF 表（列式）解析 |
| `AwbParser.cs` | AWB 音轨索引 |
| `HcaDecoder.cs` | **HCA 解码器本体**（1050 行）：支持 v1.01–3.00、ciphType 56、loop、encoder delay，输出 float PCM |
| `BitReader.cs` / `BinaryUtil.cs` | 位/字节读取原语 |
| `UnityAudioClipExtensions.cs` | PCM → `AudioClip` |
| `WavWriter.cs` / `DecodeOptions.cs` / `AcbEntry.cs` / `AudioData.cs` | 导出与数据载体 |

> 这条链与仓库根目录的 `ffmpeg/` **完全无关**：ffmpeg 只用于 Windows 端录屏转码。

### 6.5 时钟实现（影响手感的关键点）

```csharp
// CriAtomShim.cs:754-779（摘要）
GetTimeMs(id)  = (long)(GetMonotonicPlaybackTimeSeconds(state) * 1000f)
```

`GetMonotonicPlaybackTimeSeconds` 用的是 **`Stopwatch`（单调墙钟）**，不是 `AudioSource.dspTime`；shim 里对放弃 dspTime 有明确注释（`:894-922` 附近）。

后果：

- 音频设备缓冲区/驱动延迟**不会**被 dspTime 校正；
- 暂停/恢复通过 `BaseTimeSeconds + 重置 Stopwatch` 补偿（`:781-845`），精度依赖帧边界；
- 想改判定手感，改这套时钟比改判定窗口更有效。

### 6.6 音频文件注册示例（曲包链路）

```text
CustomMusicScoreEntry.LoadAudioClipAsync()
  → UnityWebRequestMultimedia.GetAudioClip("file://" + path)   // ogg/mp3/wav
  → SoundManager.RegisterExternalAudioClip("custom_<id>", "custom_<id>", clip)
  → 进 live 时 BaseLiveController.ResolveMusicCueName() 拿到 "custom_<id>"
```

（`CustomMusicScoreEntry.cs:93-109,155-189`；`SoundManager.cs:204-239`）

---

## 7. 时间、判定窗口与帧率

### 7.1 三条「时间」

| 时间 | 来源 | 用途 |
|---|---|---|
| `currentMusicTimeMs` | `SoundManager.GetAudioSyncedUnityTimer()`（Stopwatch） | live 主时钟、编辑器预览时钟 |
| `currentAudioLatencyMusicTimeMs` | `max(timingAdjust + musicTime/1000, 0)`，`timingAdjust = Settings.TimingAdjustData * 0.016667`（`BaseLiveController.cs:46,76`） | 判定用（把用户延迟偏移叠进去） |
| `currentGameTime` | `Time.realtimeSinceStartupAsDouble`（`BaseLiveController.cs:208`，每帧刷新） | 触摸时刻回推、技能计时；`accumulateTimeApartFromPause` 另记「非暂停累计时长」（`:218`） |

`SoloLiveController.OnUpdate`（`:92-116`）每帧：

```csharp
UpdateMusicTime();
float fillerSec = BootData.MusicData.Music.fillerSec;
liveLogic.OnUpdate(currentAudioLatencyMusicTimeMs - fillerSec, currentGameTime);
if (result == 2 && IsNotesAllFinished) OnFinished();
if (IsAuto) liveLogic.OnAutoInput(); else liveLogic.OnInput();
LiveViewExt.OnUpdate(liveViews, currentAudioLatencyMusicTimeMs);
```

### 7.2 判定窗口

定义在 `LiveConfig.LoadMasterConfig()`（`Sekai/Live/LiveConfig.cs:186-209`），单位**帧**：

| 类型 | Perfect 前/后 | Great 前/后 | Good 前/后 | Bad 前/后 |
|---|---|---|---|---|
| Normal / Long | 2.5 / 2.5 | 5 / 5 | 6.5 / 6.5 | 7.5 / 7.5 |
| Flick | 2.5 / 2.5 | 6.5 / 7.5 | 7 / 8 | 7.5 / 8.5 |
| Long_End | 3.5 / 4 | 6.5 / 8 | 7.5 / 8.5 | 7.5 / 8.5 |
| Normal_Critical | 3.3 / 3.3 | 4.5 / 4.5 | 6.5 / 6.5 | 7.5 / 7.5 |
| Friction 系列 | **直接复制 Flick / Long 的窗口**（`TODO(original)`，`:200-208`） | | | |

转换：`CalcFrameToTime(frame) = frame / 60`（`:289-292`）。

**JustPerfect 是固定 1 帧**：`JustPerfectJudgeTime = 1f/60f`（`:439`），判定时用 `±JustPerfectJudgeTimeHalf` 比（`:379`）。所以：

- 60fps 下窗口 ±8.33 ms；
- 120/240fps 下**物理窗口不变**，只是采样更细 —— 判定不会变宽松，但「踩在窗口内」的采样精度提高；
- **帧间隔 = 判定的量化粒度**：`OnUpdate` 跑在 `LateUpdate`，一帧只能判一次。

### 7.3 高帧率实现（本仓库新增）

`CP/FramerateUtility.cs`：

```csharp
SupportedLimits = { 0(=跟随屏幕), 120, 144, 165, 185, 240 };
ApplyFrameRateLimit(limit):
    limit = SanitizeLimit(limit)            // 非法值回落 0
    QualitySettings.vSyncCount = 0          // 关垂直同步，否则 targetFrameRate 无效
    appliedRefresh = RequestDisplayRefreshRate(limit)   // 仅 Android 真机
    target = limit > 0 ? limit : GetFeasibleFramerate()
    if (limit > 0 && appliedRefresh > 0 && appliedRefresh + 0.5 < limit)
        target = round(appliedRefresh)      // 面板到不了就夹到面板值
    Application.targetFrameRate = target
```

Android 分支（`:95-190`，`#if UNITY_ANDROID && !UNITY_EDITOR`）：

1. `Build.VERSION.SDK_INT < 23` → 放弃；
2. 通过 `UnityPlayer.currentActivity` 拿 `Window`；
3. `Display.getSupportedModes()` 里**按当前物理分辨率筛选**（保证不改渲染尺寸），取「不超过 limit 的最高刷新率」；
4. `window.setAttributes(preferredDisplayModeId = 该 mode)`。

调用点（3 处）：

| 位置 | 时机 |
|---|---|
| `SekaiSingletonManager.cs:33` | 全局初始化（跟随屏幕） |
| `MusicScoreMakerEntryPoint.cs:36` | 主场景初始化（跟随屏幕） |
| `BaseLiveController.cs:61` | **每次进 live 按用户档位应用** |
| `ScreenLayerCustomMusicScoreManager.cs:713` | 设置面板改动后立即生效 |

存储：`LiveSettingData.FrameRateLimit`（`[Key("FrameRateLimit")]`，`:118-119`），默认 0；旧档迁移：`FrameRateLimit <= 0 && Use120FPS` → 120（`:293-297`）。

**触摸时刻的回推公式（值得单独看一眼）**：

```csharp
// Sekai/Core/Live/LiveLogic.cs:16
private const float TargetFrameTime = 1f / 60f;
// 使用处：:706（EnhancedTouch）、:802（PC 鼠标回落）
float musicTime = (float)((time - currentGameTime) * TargetFrameTime + currentFrameInfo.time);
```

- `time` 是 Input System 的 `touch.time`，**单位是秒**（`realtimeSinceStartup` 域）；
- `currentGameTime` 也是同域秒值（`BaseLiveController.cs:208`）；
- 因此 `(time - currentGameTime)` 是两个秒值之差，再乘 `1/60` 等于把偏移**缩小 60 倍**，实际效果≈「按当帧音乐时间判定」，输入时刻的亚帧信息基本被丢掉。

这看起来是原版遗留（原版 `currentGameTime` 很可能是帧计数，`× 1/60` 才成立）。**是否精确到这个程度取决于你是否需要亚帧判定**：如果要，这里应该改成 `currentFrameInfo.time + (time - currentGameTime)`（同域秒直接相加），并配合 `JustPerfectJudgeTime` 的窗口一起调。当前状态下高刷只提高了「采样次数」，没有提高「输入时刻精度」。

另外 `LiveConfig.JustPerfectJudgeTime = 1f/60f`（`:439`）也是按 60 帧写死的固定窗口。

---

## 8. Live（打歌）

### 8.1 类关系（容易误判的点）

```text
BaseController（MonoBehaviour，LateUpdate → OnUpdate）
└─ BaseLiveController（Sekai/Core/Live/BaseLiveController.cs，318 行）
   └─ SoloLiveController（Sekai/Core/Live/SoloLiveController.cs，598 行）  ← 唯一派生
```

**`Sekai/Live` 下的 54 个文件不是控制器**，而是模型（`MusicScore` / `NoteBase` 家族）、视图（`LiveFrontView` / `Background2DView` / `SkillView`）、配置（`LiveConfig` / `LiveBundleBuildData` / `LivePrefabDefine`）。

### 8.2 生命周期

```text
OnAwake（BaseLiveController.cs:48-57）
   backgroundLoadingPriority = High
   liveBundleBuildData = Resources.Load<LiveBundleBuildData>(LiveConfig.ConfigBundleNamePath)
   Settings = LiveSettingData.LoadFromStorage()
   LiveConfig.LoadMasterConfig() / LoadOptionData()
   liveOutUIController = new LiveOutUIController()

SoloLiveController.Awake → Setup()
   ApplyFrameRateLimit(...)
   SleepTimeout.NeverSleep
   背景 RT 尺寸（按当前屏幕宽高比，BaseLiveController.cs:105-121）
   音量、timingAdjust

LoadSound()（:123-142）
   live/sound/live_se/default、live/tap_se/<name>、曲目 bundle

LiveStart(waitTime=6f)（协程，:144-171）
   GC.Collect + (真机) GCMode.Disabled
   视情况结束转场（LiveTransitioner.SafeForceFinish）
   MusicReady() → SoundManager.PrepareIngameBGM(cue, startSec)
   OnMusicStart() → ResumePreparedPlaybackIngame
   wait（PlayStartEffect 开启时等 6s）
   OnRhythmGameStart() → liveLogic.RefreshInput()

每帧：LateUpdate → SoloLiveController.OnUpdate（§7.1）
结束：LiveLogic.OnUpdate 发现 IsNotesAllFinished → result=3 → OnFinished
     → SoloLiveController.OnFinished → PreExit → ResultAnim → Exit
失败：life <= 0 → Result = 2 → OnFailure
```

### 8.3 `LiveLogic` 每帧（`Sekai/Core/Live/LiveLogic.cs:235-262`）

```csharp
musicScore.Update(scoreInfoTime)          // 推进 MusicScoreInfo（bar/bpm/time/…）
currentFrameInfo = MusicScore.CurrentFrameInfo;
UpdateSeVolume();
InitializeUpdate();                       // 清 judgmentNoteList
UpdateEvent();                            // BPM/倍速/拍号/音量/技能事件逐个触发
UpdateSkill();
UpdateNote();                             // 每个 note.Excute(...) + 装饰音符自动判定
if (life <= 0) Result = 2;
```

`UpdateNote` 内部还包含 `ShouldTriggerDecorationJudgment` / `DecorationJudgment` / `AdvanceFinishedNotes` / `UpdateJudgmentNoteArray`（装饰音符、已完成音符推进），是本仓库新增逻辑集中区。

### 8.4 输入与判定

```text
OnInput()（:323-349）
  NativeInput.ActiveTouches（Input System 的 EnhancedTouch）
  → TouchExecution → CreateLiveTouch（把 touch 时刻换算成 musicTime）
  → UpdateLastTouch / UpdateTouchPositionCache
  → (编辑器/PC) ProcessMouseFallback
  → Judgment()
```

`Judgment()` → `AllocateInputJudgableEachNotes()` → 每个 note 的 `IsJudgment(ref touch, lane)`：

```csharp
// NormalNote.cs:53-68
State == Done → false
lane 不在 [JudgeLaneStart, JudgeLaneEnd] → false
offset = MusicScoreInfo.time - touch.musicTime
!LiveUtility.IsJudgmentTiming(帧类型, offset) → false
Normal 类要求 TouchPhase.Began；Long 结束要求 Ended/Canceled
```

然后 `NormalNote.Judgment()` 调 `LiveConfig.CalculateDefaultNoteResult` / `CalculateLongEndNoteResult`，得到 `(NoteResult, NoteResultDescription)`，写入 `JudgeInfo` 并把 `State = Done`（触发 `OnUnSpawnNote` 里的 5 个回调：判定/连击/分数/伤害/回收）。

`GetJudgeResultInternal(offset, frameType, isDirection)`（`LiveConfig.cs:370-410`）的判定顺序：
**JustPerfect（±1 帧）→ Perfect → Great → Good → Bad → Miss**；Flick 的 `isDirection=false` 分支在 Perfect 区间直接给 `Great + FlickMiss`。

### 8.5 Note 类型对照（编辑器 ↔ live）

编辑器 `MusicScoreNoteBase.NoteBaseType`（14 个，`MusicScoreNoteBase.cs:16-33`）与 live 的 14 个实现一一对应：

| NoteBaseType | live 类 | 备注 |
|---|---|---|
| Base / Normal | `NormalNote` | |
| Long | `LongNote` + `NormalNote`(child) | 长条由「头 + 尾」两个 note 组成，`parentNote` 关联 |
| Flick | `FlickNote` | 需要方向 |
| FrictionFlick | `FrictionFlickNote` | 摩擦类，判定窗口沿用 Flick |
| Connection | `ConnectionNote` | 与上一个 note 连线 |
| HiddenConnection | `HiddenConnectionNote` | |
| LongHoldCombo | `LongHoldCombo` | 长按连击体 |
| FrictionLong | `FrictionLongNote` | |
| FrictionHideLong | `FrictionHideLongNote` | |
| Guide | `GuideNote` | 引导（不判分） |
| Friction | `FrictionNote` | |
| FrictionHide | `FrictionHideNote` | |
| GuideEnd | `GuideEndNote` | |
| GuideHiddenConnection | `GuideHiddenConnectionNote` | |

> `guideColor` 字段只存在于 JSON（没有 `[Key]`），MessagePack 不走它。

### 8.6 计分 / 血量 / 评级

```csharp
// ScoreLogic.cs:60-70
totalCombo = LiveUtility.CalculateTotalComboCount(score)（为 0 时回退 TotalNoteCount）
life = liveBundleBuildData.Life（默认 1000）
BaseNoteScore = max(1, 1000000 / totalCombo)      // DefaultTotalScore = 1,000,000

// :146-168 倍率
JustPerfect / Auto = 1.0，Perfect = 0.9，Great = 0.7，Good = 0.5，Pass/其余 = 0
// 装饰音符 addScore = 0

// :183-201 评级
有官方谱面 scoreInfo → ScoreGaugeCalculator.GetScoreRank
否则 Fallback：S/A/B/C 阈值 = RankRate* × 1,000,000
```

伤害：`LiveConfig.Damages[NoteResult]` 查表扣血（`ScoreLogic.cs:170-181`）。

### 8.7 视图与对象池

`NotesViewManager`（`Sekai/Live/NotesViewManager.cs`）：

- 为 7 个 `NoteCategory` × Normal/Critical 建池：`CreateTypedPools(..., "NormalNotePool", "NormalNote", "NormalCriticalNotePool", "NormalCrtNote", noteShowRate)`；
- 池名/预制名来自 `LiveBundleBuildData` + `LivePrefabDefine`；
- 长条线、引导线、多押线是 `NoteLineView` / `PairNoteLineView`，材质里写 `_NoteShowRate`（`SetupNoteMask`）；
- 视图与模型分离：`NoteBase.CalcProgress()` 是**纯时间函数**（`NoteBase.cs:489-497`），不累加 `deltaTime` → 掉帧不会导致音符位置漂移。

### 8.8 暂停 / 恢复 / 结算 / 返回

- `OnPause`（`SoloLiveController.cs:126-141`）：`SoundManager.Pause()` + `LiveViewExt.Pause` + 暂停弹窗；视频生成模式下直接 return（禁用暂停）。
- 恢复走 `LiveViewExt.Countdown → CountdownView`（3-2-1 倒数）。
- 结算：`OnFinished → CallPreExit → PreExit → SetFinish → ResultAnim`，`LiveOutUIController` 负责结算 UI。
- 返回：`IsTestPlay` 时 `RequestScene(MusicScoreMaker)`（`SoloLiveController.cs:412-420`），否则回首页。

---

## 9. 谱面编辑器（MusicScoreMaker）

### 9.1 三层目录与职责

```text
Sekai/MusicScoreMaker/
├─ Common/          存档仓库、曲包存储、难度估算、播放历史  ← 与 UI 无关
├─ Ingame/          编辑器本体
│  ├─ Models/       MusicScoreMakerData / MusicScoreMakerModel / MusicScoreNoteBase /
│  │                MusicScoreEventData / QuantizeSettings …（20 个，纯数据）
│  ├─ Events/       157 个消息类型 + MusicScoreMakerEventDispatcher  ← 通信 + 撤销
│  ├─ Views/        57 个 MonoBehaviour 视图（音符预览、面板、对话框…）
│  ├─ Presenters/   MusicScoreMakerPresenter（8195 行，总控）+ ScreenLayerMusicScoreMaker + EntryPoint
│  ├─ Utilities/    坐标/tick 换算、吸附、设置、剪贴板、SUS 桥
│  ├─ Controllers/  StandaloneMusicScorePreviewController
│  └─ Input/        ToolInputHandler
└─ OutGame/         列表/搜索/创建/草稿/发布（每个子界面 = ScreenLayer+View+Presenter+Model 四件套）
```

### 9.2 数据模型

```csharp
// MusicScoreMaker/Ingame/Models/MusicScoreNoteBase.cs:13-77
[MessagePackObject(false)]
public class MusicScoreNoteBase {
    [Key(0)]  int id;
    [Key(1)]  long ticks;                 // 时间
    [Key(2)]  int laneStart; [Key(3)] int laneEnd;   // 可跨轨（长条）
    [Key(4)]  NoteCategory category;      // Normal/Long/Flick/Friction/…
    [Key(5)]  NoteType type;              // Default/Critical/…
    [Key(6)]  float speedRatio = 1f;      // 单键变速（本仓库新增）
    [Key(7)]  NoteLineType noteLineType;  // Straight/EaseIn/EaseOut
    [Key(8)]  NoteBaseType noteBaseType;  // 14 种形态
    [Key(9)]  int previousConnectionId; [Key(10)] int nextConnectionId;
    [Key(11)] NoteDirection direction;    // Flick 方向
    [Key(12)] bool isSkip; [Key(13)] bool isDecoration;
              string guideColor;          // 仅 JSON
}
```

`MusicScoreMakerData`（`MusicScoreMakerData.cs`，2525 行）是谱面根：`NoteList`、`MusicScoreEventDataList`、`MusicScoreTicksMax`、`MusicId`、`FullComboDataHash`、`EventArray`、`VersionCode = 1`，并提供 `MigrateToCurrentVersion()`。

**BPM/倍速/拍号/SE 音量不是字段**，是「打在 tick 上的事件」（`MusicScoreEventData { id, eventType, ticks, changeValue }`，类型只有 `BPM/HighSpeed/SeVolume/TimeSignature`）。

tick ↔ 秒的换算不靠公式，而靠**先把事件展开成 `MusicScoreInfo[]`（每小节一条：bar / barProgress / time / bpm / timeSignature / speedRatio）再做二分查找**：

| 方向 | 入口 |
|---|---|
| 事件列表 → `MusicScoreInfo[]` | `MusicScoreMakerUtility.ConvertMusicScoreInfo(List<MusicScoreEventData>)`（`:1123`） |
| bar + barProgress → tick | `MusicScoreMakerUtility.CalculateTicksFromBarAndProgress`（`:460-520`） |
| 秒 → tick | `MusicScoreMakerUtility.GetTicksFromTime`（`:1004` 调用处） |
| 编辑器模型 → live 谱面 | `MusicScoreMakerData.ToMusicScore(liveBundleBuildData, filterBeforeTicks, isMirror)`（`MusicScoreMakerData.cs:1603`） |

### 9.3 事件总线 + 撤销（同一个类）

`MusicScoreMakerEventDispatcher`（537 行）：

```csharp
Register<T>(Action<T>) / Publish<T>(evt)      // 按 Type 分发，见 :110-256
struct UndoableEvent { Action UndoAction, RedoAction;
                       long FocusTicksAfterUndo, FocusTicksAfterRedo; }
Stack<UndoableEvent> _undoStack, _redoStack;   // :55-57

PushUndoableActionAndDoAction(undo, redo, focusAfterUndo=-1, focusAfterRedo=-1, skipEditGuard=false)
   // :314-346  先执行 redo，再压栈，超 UndoStackLimit 裁栈
Undo() / Redo()  // :269-305：执行 action 并把焦点 tick 跳回去
SaveUndoRedoStack() / RestoreUndoRedoStack() / ClearSavedUndoRedoStack()
   // :410-451  试玩前快照，回来后还原（进入 live 会切场景，栈会丢）
```

其它：`EditGuard`（`Func<Action,bool>`，用来拦「正在播放/试玩时不可编辑」，`:81`）、自动存档计时器（`StartAutoSaveTimer` 等，`:357-408`，间隔选项 `{0,120,240,360,480,600}` 秒）。

### 9.4 交互与视图

- 可视区间裁剪：`NotesPreview.UpdateView(data, startTicks, endTicks)` 只实例化「当前可视 tick 窗口」内的音符（`Ingame/Views/NotesPreview.cs:55-60`），池在 `NotePreviewPool`；
- 吸附：`QuantizeSettings`（9 档量化）+ `MusicScoreMakerUtility.QuantizeTicks / QuantizeTicksInBar / SnapToBarStart`（`:723-819`）；
- 多选：`MusicScoreMakerData.SelectedNoteIdList / SelectedEventIdList / CopiedNoteList / SelectedTargetOperation`；
- 剪贴板：落盘到 `persistentDataPath/inappcache/ClipboardCacheListData.json`（`Utilities/ClipboardCacheManager.cs:11-13`）；
- 工具条/面板：`Views/ToolButton.cs`、`Views/MusicScoreMakerOptionUIBuilder.cs`（选项面板是**代码构建**的 UI，不是 prefab）。

### 9.5 SUS 导入

`Sekai/SUS/Converter.cs`（1168 行）：

```csharp
public MusicScore Convert(string musicScoreData, bool isNeedCombo = true, bool? overrideMirror = null)
```

- 解析粒度是「一行一条」：`#BBBxx:`（BPM 变化）、`#BBBdld:`（倍速）、`#BPMxx:`、`#REQUEST "ticks_per_beat"`，正则定义在 `:1032-1041`，使用点在 `:591-665`；
- 通道字符 → lane：`2..d → 0..11`、`f → 13`（fever）、`e → -2`，见 `GetNoteLine`（`:904-924`）；
- 音符码 → `NoteCategory/NoteBaseType` 的映射是几个 `switch (noteCode)`（`:763-850` 一带）；
- 输出 `MusicScore`（live 模型），再 `new MusicScoreMakerData(MusicScore)`（`MusicScoreMakerData.cs:300`）；
- **只有导入，没有导出**；
- 唯一调用点：`CustomMusicScoreManagerService.cs:284-291`（`isNeedCombo: false`）。

### 9.6 保存格式（重要）

`MusicScoreMakerRepository`（`Sekai/MusicScoreMaker/Common/MusicScoreMakerRepository.cs`）：

```csharp
SavePathBase = persistentDataPath + "/music_score_maker"     // :176
SaveToStorage(data, fileName)  → DeepCopyHelper.ToJsonString → File.WriteAllText
LoadFromStorage(fileName)      → JsonConvert.DeserializeObject → MigrateToCurrentVersion + InitializeIdCount
AutoSaveFilePrefix = "autosave_"                             // :19
GenerateAutoSaveFileName()     → autosave_yyyyMMdd_HHmmss_fff
```

序列化用 **Newtonsoft，且开了 `PreserveReferencesHandling.Objects` + `TypeNameHandling.Auto`**（`Utilities/DeepCopyHelper.cs:25-38`）：

- 存档 JSON 里会有 `$id` / `$ref` / `$type` —— **手改 JSON 很容易破**；
- 改字段名/类型时要同时改 `MigrateToCurrentVersion()`，否则老档读不回来；
- 这也是「同样内容两份文件字节数差很多」的原因。

> 注意：`MusicScoreNoteBase` 上有 `[MessagePackObject]`，那是原版网络协议遗留，**本地存档走的是 JSON**。

### 9.7 从编辑器进入 live（完整代码路径）

```csharp
// MusicScoreMakerPresenter.cs
OnTestPlay(OnTestPlayEvent)             :6944
  RecalculateInvalidPlacementsForTestPlay()
  HasBlockingInvalidPlacementsForTestPlay() → 有非法放置就跳转提示并 return
  Show2ButtonDialog<MusicScoreMakerTestPlayDialog>(…) → PlayTestLive(...)

PlayTestLiveCore(dialog, isFromFullComboCheck)   :6979-7070
  await screenLayer.IsSetupComplete
  MusicScoreMakerEventDispatcher.SaveUndoRedoStack()
  focusTicks = GetFocusTicksForTestPlay()
  SetFreeLiveBootData(...)              // Boot 组装，:7070-7128
     MusicScore musicScore = _model?.ToMusicScore(focusTicks);   // :7092
     //   → MusicScoreMakerData.ToMusicScore(...)  见 §9.2
     MusicData.MusicScore = musicScore
     IsTestPlay = true / IsUseCustomScore = true
     ReturnScreenType = MenuScreenType.MusicScoreMaker
     UserDataManager.FreeLiveBootData = …
  EntryPoint.PlayMode = PlayMode.SoloLive
  LiveTransitioner.SafeForceFinish(null)
  ScreenManager.PushUIScreen(MenuScreenType.LiveLoading)
     → ScreenLayerLiveLoading.OnFinishTransitionFadeOut()   :189-191
        → SceneManager.RequestScene(Core)
```

返回：`SoloLiveController` 判定 `IsTestPlay` 后 `RequestScene(MusicScoreMaker)`，`MusicScoreMakerEntryPoint.Start` 用 `MusicScoreMakerEntryPoint.BootData.bootData` 里保留的 `ScreenLayerMusicScoreMaker.BootArg`（含 `IsReturnFromTestPlay`、`LastSavedDataHash`、`CurrentMusicScoreScale`）重建编辑器，并 `RestoreUndoRedoStack()`。

### 9.8 OutGame 现状

`OutGame/` 下 61 个文件是「列表/搜索/创建/草稿/发布」的完整界面层，但依赖的原版 Service 大量是桩：

- `MusicScoreMakerPublishExecutor`：构造函数与 `ExecuteAsync` 都是 `throw null`（`:142-163`）；
- `MusicScoreDataDownloader`、`MusicScoreDataFactory`、`MusicScoreInfoModel` 里成片的 `TODO(original)`：`MasterDataManager`、`MusicScoreFactory`、`CustomMusicScorePublishedSearchByIdService` 等都不存在；
- 因此**本仓库的主入口不走 OutGame 的 Create 流程**，而是首页（`ScreenLayerCustomMusicScoreManager`）直接打开编辑器（`ScreenLayerCustomMusicScoreManager.cs:2086-2099`）。

---

## 10. 第三方曲包（CustomMusicScoreManager）

### 10.1 目录与包格式

```text
persistentDataPath/CustomMusicScores/
└─ <title>_<id>/
   ├─ manifest.json      CustomMusicScoreManifest
   ├─ score.json         编辑器谱面（MusicScoreMakerData 的 JSON）
   ├─ audio.ogg|mp3|wav  音频
   ├─ jacket.png         封面（导入时重采样到 740px 高）
   └─ video.<ext>        可选，曲包自带 MV
```

`CustomMusicScoreManifest`（`Sekai/MusicScoreMaker/Common/CustomMusicScoreManifest.cs:5-45`）关键字段：

| 字段 | 含义 |
|---|---|
| `formatVersion` | 目前 1 |
| `id` / `title` / `scoreTitle` | 曲包 id（缺省生成短 id）与标题 |
| `composer / lyricist / arranger / singer / collaborationLabel / description` | 元信息 |
| `audioFileName` | 默认 `audio.ogg` |
| `jacketFileName` | 默认 `jacket.png` |
| `scoreFileName` | 默认 `score.json` |
| `videoFileName` | 可选 |
| `fillerSec` | 音频前奏空白（**判定时要从音乐时间减掉**） |
| `secForMusicScoreMaker` / `previewStartTimeSec` | 编辑器时长 / 预览起点 |
| `musicDifficultyType` / `playLevel` | 难度与等级（缺省 `master`） |
| `userName` | 作者 |

`Normalize()` 负责补默认值与纠正负值。

### 10.2 导入 / 导出

`CustomMusicScoreManagerService`（`Assets/CustomMusicScoreManager/Runtime/Services/`）：

| 方法 | 行 | 行为 |
|---|---|---|
| `LoadItems()` | `:47` | 遍历 `CustomMusicScores` 子目录读 manifest |
| `ImportFolder(dir)` | `:136` | 校验扩展名白名单 → 复制 → 必要字段修正 → 生成 id |
| `ImportZip(zip)` | `:157` | 解压到临时目录后复用 `ImportFolder` |
| `ExportZip(entry, dest)` | `:181` | 打包到 `CustomMusicScores/Exports/<title>_<id>.zip` |
| `DeleteEntry(entry)` | `:119` | 有「必须在根目录内」的越界校验 |

`.sus` / `.txt` 谱面在导入时被转成 `score.json`（`:250-306`）。

### 10.3 MusicId 与音频注册

```csharp
// CustomMusicScoreEntry.cs:218-233
hash = FNV-1a(id.ToLowerInvariant())
value = hash & 0x7FFFFFFF
return value == 0 ? -1 : -value;        // 负数，避开官方曲 id
AudioCueName = "custom_" + id;
```

- 载入音频：`UnityWebRequestMultimedia.GetAudioClip("file://" + path, type)` → `SoundManager.RegisterExternalAudioClip(cue, cue, clip)`；
- live 侧：`BaseLiveController.ResolveMusicCueName()` 从 bootData 里取（`CustomMusicScorePath` / `AudioCueName`）。

### 10.4 其余能力

- 播放历史：`CustomMusicScoreStorage` 写 `PlayHistory.jsonl`（live 结束时 `CustomMusicScorePlayHistoryStorage.AppendLiveResult`，`SoloLiveController.cs:391`）；
- 备份/恢复：`Services/BackupService`（`persistentDataPath/backup_*`，含临时目录与回滚）；
- 改名：`Directory.Move` 到新文件夹（`:367-391`）。

---

## 11. 视频导出（录屏）

```text
live（VideoGeneration 模式，LiveLogic.DetectVideoGenerationMode）
  ├─ 抓帧：VideoGenerationService（按固定帧率渲染并读回）
  ├─ 录音：Services/AudioRecorder（Unity Microphone/回环）
  ├─ 编码：
  │    Windows → WindowsVideoEncoder → ffmpeg.exe（libx264，ffmpeg/ 目录）
  │    Android → AndroidVideoEncoder → MediaCodec + MediaMuxer（音频编码被跳过，见 `:492-494`）
  └─ 后处理：VideoPostProcessor（音视频合流、封面、时长）
输出：VideoSaveHelper → persistentDataPath/SavedVideos/
```

UI 侧：`Controllers/VideoGenerationController`、`VideoCompletionHandler`、`UI/VideoProcessingProgressDialog`；Android 需 `PermissionHelper` 申请存储/相册权限（`AndroidManifest.xml` 里也加了对应声明）。

**限制**：这条链只在 Windows/Android 上实现；Windows 强依赖仓库根 `ffmpeg/`（相对路径，发布时必须一起带上）。

---

## 12. 坑与陷阱清单

| # | 现象 | 原因 / 证据 | 建议 |
|---|---|---|---|
| 1 | 点按钮没反应 | 该 `MenuScreenType` 有 `ScreenLayerData` 但**没有 prefab** → `LogError("Not Found ScreenLayer;")`（`ScreenManager.cs:2327`）。230 条注册里只有 9 个有 prefab | 先查 `Assets/Resources/screen/prefabs/` 有没有同名 prefab |
| 2 | 页面切换是硬切 | `ScreenLayer.PlayStartAnimation/PlayExitAnimation` 空壳（`ScreenLayer.cs:158-161,194-202`） | 接 `ScreenInOutAnimBase` |
| 3 | 深路径连按返回跳页 | `LimitedStack(16)` 静默丢最老元素（`CP/LimitedStack.cs:31-38`） | 需要时改成环形/扩容并加日志 |
| 4 | 明明写了 `targetFrameRate` 但没到 120 | 忘了关 vSync / Android 没请求显示模式 | 用 `FramerateUtility.ApplyFrameRateLimit`，不要直接赋 `Application.targetFrameRate` |
| 5 | 高刷下手感没变好 | 触摸时刻回推 `(touch.time - currentGameTime) * 1/60` 量纲不一致（秒 × 1/60），亚帧信息被丢掉；`JustPerfectJudgeTime` 也按 60 帧写死（`LiveLogic.cs:16,706`、`LiveConfig.cs:439`） | 改成 `currentFrameInfo.time + (touch.time - currentGameTime)` 再一起调窗口，见 §7.3 |
| 6 | 音画有固定偏移 | 时钟是 `Stopwatch` 不是 `dspTime`（`CriAtomShim.cs:764-779`） | 用 `timingAdjust` 补偿，或改时钟源 |
| 7 | 手动改存档后打不开 | JSON 带 `$id/$ref/$type`（`DeepCopyHelper.cs:25-38`） | 用编辑器改，不要手改 |
| 8 | 老谱面读进来错乱 | 缺 `MigrateToCurrentVersion()` 的迁移分支 | 改 `MusicScoreMakerData` 字段时补迁移 |
| 9 | 曲包音频/封面不显示 | manifest 文件名与目录实际文件不匹配（`Normalize()` 只补默认值不校验存在） | 导入后校验 `HasAudioFileName/HasScoreFileName` |
| 10 | 试玩返回后撤销栈丢失 | 切场景会销毁 dispatcher；靠 `SaveUndoRedoStack/RestoreUndoRedoStack` 兜 | 自定义返回路径要保证 `BootArg` 带回来 |
| 11 | Android 上 AB 加载失败 | `StreamingAssets` 是 `jar:` URI，必须走 UWR（`AssetBundleManager.cs:492-497`） | 不要假设能用 `LoadFromFile` |
| 12 | 主线程卡顿 | `LoadAssetBundleFromStreamingAssetsUri` 里 `Thread.Sleep(1)` 忙等（`:445`、`:465`） | 真机上考虑改成协程/UWR |
| 13 | 打包后资源没变 | 忘了跑 AB 构建；编辑器走 `AssetDatabase` 所以「编辑器里正常」 | 菜单 `OpenSekai/AssetBundles/Build AssetBundles And Info` 或直接 Build（有钩子） |
| 14 | 直接 Play `Core.unity` 黑屏 | `Sekai.Core.EntryPoint.PlayMode` 默认非 `SoloLive` | 先设 `EntryPoint.PlayMode = PlayMode.SoloLive` 并准备 `UserDataManager.FreeLiveBootData` |
| 15 | Resources 路径大小写不一致 | 代码写 `"Screen/SceneEntry/..."`，磁盘是 `resources/screen/sceneentry/...` | Windows 无碍；跨平台构建前统一大小写 |
| 16 | 空方法调用无副作用 | `ScreenLayer.ChangeStartAnimationType`、`SoundUtility.SetupGlobalSeSettings`、`AssetBundleMetaManager.*` | 见总览 §9.3 桩清单 |
| 17 | `DeltaTimeK` 异常 | `targetFrameRate = -1` 时 `deltaK` 为负（`SekaiSingletonManager.cs:41-44`） | 不用它就罢，要用先 clamp |

---

## 13. 调试与排障

### 13.1 日志

- 文件日志：`UnityLogFileWriter` → `persistentDataPath/OpenSekaiLogs/`（`Sekai/UnityLogFileWriter.cs:10,28`）；
- `LogUtility`（`CP/LogUtility.cs`）是原版日志封装，`TODO(original)` 说明过滤表/文件回调和原版不一致；
- 关键链路都打了 `Debug.Log`：`FramerateUtility.ApplyFrameRateLimit` 会打印 `targetFrameRate / limit / display Hz / appliedMode / vSync`，**排查帧率问题先看这一行**。

### 13.2 常用排查入口（断点/日志埋点位置）

| 想确认 | 打在哪 |
|---|---|
| 场景怎么切的 | `SceneManager.RequestScene`（`:60`）、`ResolveLoadScene`（`:85`） |
| 页面为什么没出来 | `ScreenManager.InstantiateLayerScreen`（`:2310`）、`LoadScreenLayerPrefab`（`ScreenLayerData.cs:30`） |
| 资源从哪加载 | `AssetBundleManager.LoadAssetBundle`（`:143`）、编辑器分支（`:501`） |
| 音频 cue 有没有 | `SoundManager.Load` / `RegisterExternalAudioClip` / `GetCueInfos` |
| 判定是否正确 | `LiveConfig.GetJudgeResultInternal`（`:370`）、`NormalNote.Judgment`（`:70`） |
| 时钟是否跑偏 | `SoundManager.TryGetAudioSyncedUnityTimer`、`CriAtomShim.GetTimeMs`（`:754`） |
| 编辑器为什么不能编辑 | `MusicScoreMakerEventDispatcher.EditGuard`（`:81`）、`CanUndo/CanRedo`（`:89-97`） |
| 存档什么时候写的 | `MusicScoreMakerRepository.SaveToStorage`（`:67`）、`StartAutoSaveTimer`（`:362`） |

### 13.3 常用 grep

```bash
# 谁在推某个页面
grep -rn "PushUIScreen\|ChangeUIScreen" Assets --include=*.cs | grep MusicScoreMaker

# 谁在改某个设置
grep -rn "FrameRateLimit" Assets --include=*.cs

# 某个事件谁处理
grep -rn "class OnTestPlayEvent" Assets --include=*.cs
grep -rn "Register<OnTestPlayEvent>" Assets --include=*.cs

# 找某类音符的所有分支
grep -rn "Category == NoteCategory" Assets/Scripts/Assembly-CSharp/Sekai --include=*.cs
```

### 13.4 单独跑 live 的最小方法

```csharp
// 任意 MonoBehaviour 里
Sekai.Core.EntryPoint.PlayMode = Sekai.Core.PlayMode.SoloLive;
UserDataManager.FreeLiveBootData = new FreeLiveBootData { /* MusicData / 音量 / IsAuto … */ };
SceneManager.Instance.RequestScene(SceneManager.Scene.Core);
```

`FreeLiveBootData` 派生自 `LiveBootDataBase`，字段较多，最省事的办法是抄 `MusicScoreMakerPresenter.PlayTestLiveCore` 里 `SetFreeLiveBootData` 的组装代码（`MusicScoreMakerPresenter.cs:7070-7128`）。
