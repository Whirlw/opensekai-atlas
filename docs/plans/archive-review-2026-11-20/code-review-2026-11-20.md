# OpenSekai 多智能体交叉代码审查报告

2026-11-20 对 `Assets/` 下全部 C# 源码做了一次**只读**静态审查。共 36 个独立审查智能体：项目按职责划分为 12 个模块，每个模块由 3 名互不相见的审查员分别以「正确性 / 架构与规范 / 性能与健壮性」为主视角独立通读，各自独立成稿，最后由人工汇总、去重、标注相互印证。

审查依据为项目自有的 [代码规范](../coding-standards/README.md) 与 [架构文档](../architecture/modules.md)，不是通用 C# 教条。**没有编译、没有运行、没有跑测试**，所有结论来自源码阅读，行号为审查时的快照，修复前请以当前文件为准。

- 覆盖：12 个模块，按模块表求和 709 个 `.cs` 文件条目（含测试 120 个），逐文件通读；单模块 4k–22k 行。
- 产出：按模块表逐行求和为 354 条（跨模块重复仍计入，真正去重后更少），其中 P0 0 条、P1 45 条、P2 约 190 条、P3 约 110 条。
- 交叉印证：14 处问题被 2–3 名不同视角的审查员各自独立发现（报告中标 `✔✔` / `✔✔✔`）。

> **勘误（2026-11-20，经五轮交叉复核后定稿）**：第三方对本文做了逐条复核。按**第三轮口径**，本文 75 项争议的最终分布是**维持 16、修正 55、撤回 4、降级 41**；41 行降级由**原 P1 19 行 + 原 P2 22 行**构成（M07-4 历经 P1→P2→P3 仍**只计一行**）；降级数按 **M11-8 整行计**，若只把超时子项算作降级则为 **40**。这个统计**只覆盖那 75 行**，不覆盖表外待办的级别。
>
> **已作废的具体措辞**（不再逐处改写，一律以末轮为准）：M10 关于「模糊读到上一帧残帧」的判断（现定为「配置死字段 + 枚举外裸 `1`」，画面另列待截帧）；M08 的「约 4.4e9 MAC / 每次 Play 2 次」（实际 22.12 亿 MAC / 成功路径 3 次）；M12 的「601 文件 / 约 1200 次重导入」（当前工作区实跑在数据项 28 中止、此前写回 21 张）；M03 的「约 265ms/首」（仅是一条注释样本）。
>
> 逐条答辩与最终裁决见：[第三方复核的逐条答辩与裁决](code-review-2026-11-20-rebuttal.md)（一）→ [二次复核的再对账](code-review-2026-11-20-round2.md)（二）→ [第三轮复核的再对账](code-review-2026-11-20-round3.md)（三，**含可执行的排期表**）→ [结案意见的再对账](code-review-2026-11-20-round4.md)（四）→ [第四轮回函复核的再对账](code-review-2026-11-20-round5.md)（五，**含 U7/U8 的验证口径**）。**以最后一轮为准**，本文保留为审查当时的原始记录。

## 怎么读

| 标记 | 含义 |
| --- | --- |
| `P0` | 崩溃、数据损坏、必现流程错误 |
| `P1` | 特定场景功能错误、明显资源/订阅泄漏、明显卡顿 |
| `P2` | 可维护性 / 性能 / 规范偏差，影响可控 |
| `P3` | 轻微 |
| `✔✔` / `✔✔✔` | 2 / 3 名不同视角的审查员独立报告了同一问题 |
| `[bug]` `[design]` `[perf]` `[serial]` `[event]` `[naming]` | 问题类别，取自审查员的 `category` |

## 结论摘要

整体不是"烂代码"，而是**三层结构断层**：

1. **纯逻辑层很健康**（`OpenSekai.Core`、`Shared/Live` 的判定与触摸数学、`Tests/Core`）。有边界处理、有注释、有针对性测试，M05 拿到全项目最高分 3.7。
2. **界面与表现层能用但"重"**：单类 1100+ 行、每帧闭包、逐实例材质、逐键全量重建。功能是对的，代价没算过。
3. **平台与工具层有相当一部分是"看起来存在的能力"**：`throw null` 空壳类、只有 TODO 的实现、被静默丢弃的参数、永远不触发的 Feature。这些不是性能问题，是**功能其实不存在**，而且文档与调用点都在假装它存在。

按严重度排序，最该先动的不是 P2 那一大堆命名/重复代码，而是四类"会真的伤到玩家或发布"的问题：

- **玩家数据可能不可恢复地丢**：设置文件原地截断写、替换媒体先删后写、Sonolus 下载索引一条坏记录锁死全部安装。
- **判定层有三处会稳定错判**：划键距离单位多除一次 2.54（门槛只有设计值的 1/2.54）、长条尾键按"最晚时间"重推导致有效抬手必 Miss、生命值耗尽的局被当成"完成"写进历史。
- **Android 上会 ANR**：AssetBundle 与 ACB 读取在主线程用 `while(!isDone)` / `Thread.Sleep` 空转等待。
- **发布流程会静默出坏包**：截图工具异常路径挂死 CLI 并遗留 Unity 进程、CLI 出包不复建 AssetBundle、录制权限剥离失败只打 warning。

---

## 一、先修这 12 项

按「影响 × 确信度」排序。每条都给证据位置与最小修复动作。

### 1. 长条尾判定会规律性吞掉有效抬手（M05，P1）

`LongNoteTail.TryGetTail` 不返回绑定的 `EndNote`，而是扫描 `NoteList` 取"时间最晚"的音符当尾键（汇总时已回读源码确认：`TryGetTail` 内部就是一个"取 `time` 最大候选"的循环）。当条末尾存在同时间的 combo 点、1 tick 条或排序不稳定时，重推出的尾与 `childNote` 绑定不一致，普通尾窗口永久关闭——玩家在窗口内抬手只能判 Miss。

- [LongNoteTail.cs:54](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNoteTail.cs#L54)　`TryGetTail` 的时间推导
- 同处还叠加一次性能问题：`ExpireTail` 每帧对每条活跃条触发 2–3 次全表扫描（P2）。
- 修：直接返回绑定的 `EndNote`，仅在它不合格时才回退扫描；并把尾键候选在 `SortNoteList` 时缓存一次。

### 2. 暂停后 3 秒会被复活倒计时自动解除（M05，P1）

`PauseLive` 没有取消 `_resumeCoroutine`。在连续自动演奏的复活倒计时期间暂停，面板还开着、音乐与判定却自己重新开始。

- [SoloLiveController.cs:343](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L343)
- 同文件另一条：生命值耗尽的一局被归为"完成"发布并写入 `PlayHistory`，`PlayResultKind.Failed` 分支不可达，[SoloLiveController.cs:515](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L515)。
- 修：进入 Pause 的清理抽成一个私有方法，两个入口共用。

### 3. 划键判定门槛只有设计值的 1/2.54（M01，P1）

`flick` 距离由英寸换算像素时又除了一次 2.54，实际门槛约 **1.97 cm/s**，而不是 [judging.md](../reference/judging.md) 明文写的 5 cm/s。慢拖也会被判成划键。

- [JudgeTuning.cs:72](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/JudgeTuning.cs#L72)　`screenDpi / 2.54f * FlickDistanceInches`
- `FlickDistanceInches` 的语义是**英寸/帧**：`5/2.54/60 = 0.0328`，正好等于 5 cm/s（0.0328 in/frame × 60 = 1.97 in/s = 5.0 cm/s）。换算像素只需 `dpi × 0.0328`；写成 `dpi / 2.54 × 0.0328` 等于把常量当成了厘米/帧。
- **同一个测试文件里躺着互相矛盾的两条断言**，这条 bug 因此一直被"锁"着：`JudgeTuningTests.cs:43` 期望 `FlickDistanceInPixels(100) == 1.2919`（把 /2.54 写进了期望值），而 `JudgeTuningTests.cs:52` 又断言同一个常量是 5 cm/s。两者不可能同时为真。
- 同一公式还复制在 [LiveUtility.cs:39](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveUtility.cs#L39) 的 `ScreenDpiToInch`，被谱面加载用于每个划键音符，所以修的时候两条路径要一起改。
- 相关：`ChartTimeline` 内并存两套 bar→tick 公式，非 4/4 谱面导入后小节号与进度整体错位（[ChartTimeline.cs:313](../../../Assets/Game/Runtime/OpenSekai/Charts/ChartTimeline.cs#L313)）。
- 修：改成 `screenDpi * FlickDistanceInches`，把 `FlickDistanceInPixels` 的期望值改成 `3.28`，并同步 `ScreenDpiToInch` 的命名与公式。别让常量名、公式、测试期望三者各说一套。

### 4. AssetBundle 依赖引用计数只增不减，bundle 永不卸载（M08，P1，✔✔）

`LoadAssetBundle` 对父包与每个依赖各 `+1`，`DisposeCore` 只对传入的包 `-1`，从不减依赖。每次「加载父包 → 卸载父包」（每局 Live 都会发生）都会让 shader/effect 依赖的计数只涨不落，`UnloadAssetBundle` 变成空操作。

- [AssetManager.cs:152](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L152)、[AssetManager.cs:454](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L454)
- 修：加载时记录父子关系，释放时对依赖对称递减；加一条断言"卸载后引用计数全为 0"的回归测试。

### 5. AssetBundle 与 ACB 读取在主线程空转等待（M08，P1，✔✔）

Android 上 `streamingAssetsPath` 是 `jar:` URI，两条读取路径都会进阻塞分支：

- [AssetBundleManager.cs:443](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleManager.cs#L443)　`while (!operation.isDone) { Thread.Sleep(1); }`
- [CriAtomShim.cs:484](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L484)　`while (!operation.isDone) { }`（无 yield、无超时、无取消）

UnityWebRequest 的完成由主线程循环泵驱动，在主线程空转等它，最坏情况是永久挂死。这是进歌/换场景时长时间白屏与 ANR 的直接来源。

- 同类问题在别处也存在：开局路径在 UI 线程同步解析谱面并对同一文件重复做 SHA-256（[PlayService.cs:119](../../../Assets/Game/Runtime/OpenSekai/App/Playback/PlayService.cs#L119)）；曲库打开时逐首在主线程全量解码音频测响度（[LibraryLoudnessScanner.cs:120](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoudnessScanner.cs#L120)）；Android 首次播放 2DMV 同步复制整段视频（[LiveMediaFiles.cs:33](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveMediaFiles.cs#L33)）。
- 修：统一改成 `yield return request.SendWebRequest()` / `UniTask`，带超时与 `CancellationToken`。

### 6. 设置与用户文件可能不可恢复地丢（M09 + M02，P1，✔✔）

- 设置落盘是**原地截断写**（`File.WriteAllText` 直写正式文件），写入中途被杀即留下截断 JSON；`Load` 捕获异常后返回全新默认对象，**玩家全部设置与延迟校准静默清零**。[LiveSettingsStore.cs:45](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Settings/LiveSettingsStore.cs#L45)、[ApplicationLocalSettings.cs:95](../../../Assets/Game/Runtime/OpenSekai.Game/Data/ApplicationLocalSettings.cs#L95)
- 替换谱面/音频/封面是**先删旧文件再写新文件**，新文件编码或写入失败时旧资产已不可恢复，manifest 仍指向它。[SongPackageManager.cs:574](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L574)
- Sonolus 下载索引走**严格解析**：`sonolus-downloads.json` 只要有一条坏记录，此后所有安装都在 `PreparePublish` 第一步永久失败；另一条吞异常的分支还会把整份索引覆写成只剩一条，已装谱面变 Missing 并重复导入。[SonolusLevelInstaller.cs:380](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Sonolus/SonolusLevelInstaller.cs#L380)
- 修：全项目已有 `AtomicFile`（temp + `File.Replace`）范式，把它提到 `OpenSekai.Game` 供 Shared 复用；索引改为逐条容错 + 读失败时重命名备份，禁止在未成功读取的前提下覆写。

### 7. 返回键没有单一消费者，一次 Esc 既关框又退页（M07，P1，✔✔✔）

`DialogBase.Update` 与 `ScreenManager.Update` 各自读一次返回键：一次 Esc 既关闭对话框又弹掉底下页面；且 `BackKeyChecker` 算出的 raycast 结果被丢弃。同时 `allowCloseExternal = false` 只挡住关闭按钮与遮罩点击，**挡不住返回键**，1 按钮对话框会连带触发 OK 回调。

- [DialogBase.cs:443](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L443)、[ScreenManager.cs:539](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenManager.cs#L539)、[DialogBase.cs:245](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L245)
- 修：返回键收敛到单一分发点（先问对话框队列），三个关闭出口共用一个带门禁的 `CanClose()`。

### 8. 页面栈并发保护只覆盖一半入口（M07，P1，✔✔）

`PushUIScreen` 从不写 `_changeUILayerCoroutine`，重入守卫与 `IsUILayerWorking` 在推送期间形同虚设，页面切换可并发并互相清空标志；`WillExitBehaviourStatus.Cancel` 只在 Push 里被尊重，`Pop`/`Change` 仍会把页面换掉（且 `Pop` 先出栈后播动画，取消时栈条目已丢）。

- [ScreenStackService.cs:91](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L91)、[ScreenStackService.cs:386](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L386)、[ScreenStackService.cs:557](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L557)
- 同处还有两处**无超时等待**，启动失败会把整栈永久锁死。
- 修：`ExitScreen` 返回结果枚举，三条路径统一回滚；等待加超时与失败出口。

### 9. 对话框关闭动画被 `OnDisable` 取消后 `OnClose` 永不触发（M07，P1）

关闭动画期间对象被禁用，`OnClose` 不执行：对话框留在队列里、屏幕模糊不释放，下次弹框排队行为异常。

- [DialogBase.cs:255](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L255)
- 修：把"关闭收尾"从动画回调里解耦，`OnDisable` 时同步走 `FinishClose`，保证 `OnClose` 恰好发一次。

### 10. UI 模糊的截屏时机被写死成未定义枚举值（M10，P1，✔✔）

`_copyScreenToCapturePass.renderPassEvent = (RenderPassEvent)1;` —— 序列化配置的 `EventCopyScreenToCapture = 500`（资产里实测就是 500）从未被读取。`1` 不是任何具名事件，模糊的输入是本帧尚未写入的相机颜色附件，结果依赖上一帧残留内容。

- [SekaiLegacyDrawUIRendererFeature.cs:150](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L150)
- 叠加三条：模糊开关是**进程级可变静态状态**，唯一正常复位在 `await` 之后，对象在帧末前被销毁则永久为真（[SekaiUIEffectSettings.cs:9](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiUIEffectSettings.cs#L9)）；4 张全屏 RT 只在 feature 销毁时释放；`RequireDisposeUIBlurTexHandle` 从不赋值，teardown 时也漏释放。
- 修：读序列化事件点，并断言 `copy < blur < drawUI`；模糊开关改为引用计数 + `try/finally` 复位。

### 11. 打歌热路径持续分配，而打歌期间 GC 被关闭（M05 + M06，P2，✔✔）

`BaseLiveController` 在打歌期间设 `GarbageCollector.Mode.Disabled`，但帧循环仍在分配：`LiveViewExt` 每帧/每音符经闭包转发、`LiveInputRouter` 每帧构造两个委托、`PendingTails` 走 `IReadOnlyList` 装箱、`UpdateNotes` 里线性 `Contains`。M05 与 M06 两名不同视角的审查员**独立**指到了同一根因。

- [LiveViewExt.cs:89](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveViewExt.cs#L89)、[LiveInputRouter.cs:85](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveInputRouter.cs#L85)、[LiveNoteFrame.cs:100](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L100)
- 同一模块还有一条资源问题：`BaseNoteView` 对每个池化音符的每个 `Renderer` 调 `renderer.material` 克隆材质且从不释放，破坏合批并按音符数膨胀内存（[BaseNoteView.cs:159](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/BaseNoteView.cs#L159)）。
- 修：热路径改显式 `for` + 缓存委托；材质改 `MaterialPropertyBlock`。

### 12. 发布工具在失败路径上不收敛（M12，P1，✔✔）

- **截图驱动无 try/finally**：`Run()` 只有正常路径才恢复 `AudioListener.volume`（运行中被静音）与 `Time.captureDeltaTime`（定步长 0.1s）、写 `report.json`、回调 `_onDone`。首个 `WaitForLibrary` 超时（120s）即让整个 run 静默死掉，CLI 只能等 900s 超时并遗留一个占着工程的 Unity 进程。[ScreenshotDriver.cs:61](../../../Assets/Game/Editor/OpenSekai/Screenshots/ScreenshotDriver.cs#L61)（L1 与 L3 独立确认）
- **AssetBundle 构建先递归删 `StreamingAssets/data` 再构建**，失败即无 catalog 也无回滚，且 `BuildPlayerWithAssetBundles` 忽略其 `false` 返回值继续出包。[OpenSekaiAssetBundleBuildPipeline.cs:90](../../../Assets/Game/Editor/OpenSekaiAssetBundleBuildPipeline.cs#L90)
- **CLI 出包绕过 AB 步骤**：菜单 Build 会先建 bundle，`-executeMethod CliBuild.PerformBuild` 直接 `BuildPipeline.BuildPlayer`。干净检出上必然失败（`StreamingAssets/data` 被 gitignore），而换过构建目标的旧 bundle 又能通过新鲜度与打包条数两项校验，安静进包。[CliBuild.cs:51](../../../Assets/Game/Editor/CliBuild.cs#L51)（L1 与 L3 独立确认）
- **权限剥离失败只打 warning**：`RecordingBuildRules` 把所有异常降级成 `LogWarning`，剥离失效时发布包会带着存储/相册权限出去。[RecordingBuildRules.cs:116](../../../Assets/Game/Editor/OpenSekai/RecordingBuildRules.cs#L116)
- 修：构建/截图一律「先写临时、成功再替换」，并把所有"校验型"步骤改成失败即 `throw BuildFailedException`。

---

## 二、七个横向模式

单条问题好修，模式不修就会反复长出来。这 7 个模式跨越 3 个以上模块，独立审查员在不同文件里各自撞到。

### A. "接口留着、实现没接上"：文档与调用点都在假装能力存在

这是最普遍、也最容易被误判为"已完成"的问题形态。

| 位置 | 表现 |
| --- | --- |
| `Assets/.../Assets/LruAssetCache*.cs` | LRU 缓存整族是反编译桩，构造与取值即 NRE，而 `modules.md` 仍把它写作既有能力 |
| `Platform/CP/UnityWebRequestClient.cs`、`InputManager.cs`、`UiEffect/*` | 100+ 处 `throw null`，HTTP 客户端、输入管理、混合色组件全部不可执行 |
| `Data/SkillData.cs`、`CutinData.cs`、`MasterInGameCutInCharactersModel.cs` | 所有成员 `throw null`；消费方用 `try/catch` 兜底，掩盖了技能/切镜功能实际不可用 |
| `Live/View/CutinManager.cs`、`CutinView.cs`、`SkillTextView.cs` | 整层空实现，`LiveFrontView` 每帧仍按它存在来转发调用 |
| `Live/View/TapEffectView.cs:507` | `PlayHaptic` 是空方法，振动设置项整条链路空转 |
| `Audio/SoundUtility.cs:5` | `SetupGlobalSeSettings` 只有一行 TODO，两个启动入口当真调用它 |
| `Rendering/ShaderPreloader.cs` | 无任何预载逻辑，`EntryPoint` 的"等预载完成"条件恒真 |
| `Rendering/SekaiMusicItemSettings.cs` | 注册表无任何实现者，对应 Renderer Feature 永不入队 |
| `Screens/ScreenManager.cs:489` | 8 个 public 方法体是 `throw null` |
| `Audio/SoundManager.cs` | `PlaySEOneShot` 丢 priority、`ResumeIngame` 丢 musicTimeMs、`ResumeIngameSe` 空方法 |

**建议**：一次性做一次"死 API 清查"，规则是——没有被调用的实现就删（YAGNI），被调用的就补实现，两者都不是就改成 `NotSupportedException` 并标注 `Obsolete`。禁止再留 `throw null`。

### B. 失败路径不原子、不收敛、不发声

正常路径写得比失败路径细致得多：

- 先删后写（`SongPackageManager` 替换媒体、`OpenSekaiAssetBundleBuildPipeline` 清 StreamingAssets、`AtlasSpriteMigration` 删源资产）。
- 捕获后静默降级为 warning（`ReleaseSigningBuildStep` keystore 缺失、`RecordingBuildRules` 权限剥离、`BuildStampBuildStep` git 超时）。
- 只捕获已知异常类型（`SonolusBrowsePage` 只 catch `SonolusException`，未预期异常让页面永久停在加载态）。
- 校验型工具永远成功退出（`MissingScriptAudit` 查出问题也不设非零退出码，CI 门禁形同虚设）。
- 备份/导入的中转目录无人清理（`BackupService` 失败留下的临时导出目录、被 kill 残留的 `.chart-import-*`）。

**建议**：建立两条硬约定——(1) 任何"破坏性写入"必须先写临时物、成功后再原子替换；(2) 任何"校验型"步骤失败必须让构建/流程失败，而不是 warning。

### C. 主线程同步 I/O 与自旋等待

`while (!op.isDone)`、`Thread.Sleep(1)`、`ReadToEnd()` 后才发现超时、`File.Exists` 逐格探测、整段视频 `File.Copy`。Android 上这些直接表现为白屏与 ANR。

涉及：M02（开局解析、SHA-256、曲包复制）、M03（响度扫描、导入解压、缩略图解码）、M06（2DMV 复制）、M08（AB/ACB 读取）、M09（`LiveTrace` 逐行 flush）、M12（`PaintKit` 每文件 `SaveAndReimport`）。

**建议**：把"主线程不做超过一帧预算的 I/O"写进规范，并在 code review 清单里加一条"是否在 Update/按钮回调里碰磁盘"。

### D. 热路径分配，而唯一不开 GC 的窗口正是热路径

打歌期间 `GCMode.Disabled`，却被每帧闭包、装箱枚举器、逐实例材质、`FindObjectsByType` 新数组填满。M05 与 M06 独立撞到同一根因，说明这是结构性的而非偶发。

另一半是"每帧全扫"：`NoteSpeedRatio.Resolve` 从首条 info 扫到当前音符（越打越慢，M05/M09）、`CriAtomAudioListenerGuard.LateUpdate` 每帧全场景找 `AudioListener`（M08）、`LiveSpriteButton` 之外的 `CustomSelectableDefine` 每次点击 `FindFirstObjectByType`（M07）、`HomeCards` 每帧 18 次 `Transform.GetChild`（M04）。

### E. 事件与可见性规范系统性偏差

`04-events` 的三条硬规则（`event` 而非可写 `Action` 属性、触发必须走 `Raise*`、订阅者命名 `类名_事件名`）在 6 个模块里成片违反：`NoteBase` 的判定回调可被外部整体替换、`ScreenManager.ScreenSizeChanged` / `DialogBase.OnClosed` / `LibraryView.BackRequested` 都是可赋值属性、`ServerRegistry.Changed` 三处直接触发、`PlayRequested` 用四个位置参数。

更严重的一种：`[SerializeField]` 打在 `Action` 字段上（`CustomButton` 等，M07 ✔✔）——Unity 无法序列化委托，这些字段永远不会被持久化，`FormerlySerializedAs` 也完全无效。

### F. 生命周期收尾不对称

四种反复出现的形态：

1. **`new` 隐藏基类虚方法**：`SoloLiveController.OnApplicationPause`（M05 ✔✔✔）、`AssetManager.OnDestroy`（M08 ✔✔✔）。基类清理永不执行。
2. **靠动画回调收尾**：动画被打断即状态不收敛（`DialogBase` 的 `OnClose`、`ColorFader` 的 `IsPlayFading`、`CustomButton` 的按压 `MotionHandle`）。
3. **静态全局状态无复位**：`SekaiUIEffectSettings.Blur`、`UserDataManager`、`LiveSettingsStore._shared`、`LiveConfig` 的判定表与模式。域重载/场景切换后残留。
4. **资源所有权不清**：`MusicInfoView` 自建封面色贴图从不 `Destroy`（M06 ✔✔，每次进歌泄漏一张全尺寸贴图，还与 `Background2DView` 重复解码同一文件）、`AlphaMask` 运行时材质不销毁、`OverlayMask` 关遮罩时把材质置 null 丢失原材质。

### G. 测试的"覆盖假象"

M11 的 36 条问题里没有一条是"测试写错导致假红"，全是**测试看起来覆盖了、实际没覆盖**：

- `AutoplayCompletionTests` 视图寿命用例从不调 `OnView`，尾键退役路径完全没跑。
- `JudgeFrameTableTests` 只验 `frame/60` 换算，真实判定窗口表被改动不会被任何用例拦住。
- `ResultPublicationTests` 手工拼装 reset 序列，绕开生产 `LiveSessionState.Reset`，漏两步也发现不了。
- `Assume.That` 把前置条件不满足的 6 个 flick 窗口用例静默降级为 `Inconclusive`。
- `MissingScriptAuditTests` 可能读到上次运行的陈旧报告而假绿。
- `LiveHarness` 在构造函数里改 4 个进程级静态量并 `AddDevice`，构造抛异常时 `Dispose` 不执行，污染后续所有用例。
- 零覆盖的高风险项：AssetBundle 加载超时/重试/下载（而该超时分支会静默成功）、`OpenSekai.Game/Platform` 的输入适配、`Rendering` 的 Pass 顺序。

**建议**：把 `Assume` 是否可接受、以及"断言必须落在被测行为上"写进 `operations/testing.md`；给 `LiveHarness` 加 `IDisposable` 作用域对象。

---

## 三、分模块结果

评分是该模块 3 名审查员独立给出的 1–5 分均值（5=优秀）。"合并后问题数"是按文件+行邻近度去重的启发式结果，仅作规模参考；**"P1"列是去重后的组数，与下文正文列出的 P1 条目不必一一对应**（正文只列主要项，M02/M07/M08 三格因此无法从正文逐条复现）。

| 模块 | 范围 | 文件/行 | 评分 | 合并后 | P1 |
| --- | --- | --- | --- | --- | --- |
| M01 Core 领域层 | `OpenSekai.Core/`、`OpenSekai/Charts/`、`OpenSekai/Live/` | 51 / 4.8k | **3.3** | 28 | 1 |
| M02 App 服务层 | `OpenSekai/App/{AppContext,Bootstrap,Diagnostics,Library,Playback,Server}`、`Infrastructure/`、`CustomMusicScoreManager/` | 38 / 7.4k | **3.0** | 28 | 2 |
| M03 曲库界面 | `OpenSekai/App/UI/*.cs` | 32 / 7.3k | **3.3** | 24 | 3 |
| M04 首页与服务器界面 | `OpenSekai/App/UI/{Home,Server}/` | 21 / 7.3k | **3.0** | 27 | 2 |
| M05 Live 演奏核心 | `Live/{Runtime,Notes}/`、`LiveMotion.cs` | 90 / 12.5k | **3.7** | 29 | 3 |
| M06 Live 视图 | `Live/View/` | 43 / 7.9k | **3.0** | 30 | 3 |
| M07 页面框架与 UGUI | `Screens/`、`UI/` | 106 / 11.9k | **2.3** | 34 | 9 |
| M08 平台层与资源 | `Platform/`、`Assets/` | 53 / 8.7k | **2.0** | 28 | 10 |
| M09 数据与共享 | `Data/`、`Shared/`、`Extensions/` | 80 / 5.4k | **3.0** | 28 | 3 |
| M10 音频/动画/渲染 | `Audio/`、`Animation/`、`Rendering/` | 52 / 4.2k | **3.0** | 28 | 3 |
| M11 测试套件 | `Assets/Tests/` | 120 / 22k | **3.0** | 36 | 0 |
| M12 编辑器工具 | `Assets/Game/Editor/` | 23 / 2.5k | **3.0** | 34 | 6 |

### M01 Core 领域层（3.3）

三名审查员均判定"分层干净、纯函数可测、无生命周期问题"，最大风险是**单位与公式的双份实现**。

- `P1` 划键距离单位多除 2.54（见"先修 3"）。
- `P2` `ChartTimeline` 内 bar→ticks 两套公式并存，非 4/4 谱面小节号错位。
- `P2` `ScoreGauge` 的"无 Master 阈值"策略在 `Create` 与 `GetRank` 之间分叉，Game 侧被迫复刻兜底；`Create(1)` 让整条槽恒为满格。
- `P2` NextRUSH 全反向变速谱 `ActivationTime` 退化，音符在判定线上凭空出现。
- `P2` 谱面 JSON 越界枚举值直接抛异常，整首谱变为不可玩，且错误被吞成通用提示。
- `P2` 开局主线程组装存在 O(音符数×连接数) 的 `List.Find`；SUS 每格 2–3 次字符串分配。
- `P2` `filterBeforeTicks` 是死参数，且事件与音符的过滤方向相反（一旦接上就会错位）。
- `P2/✔✔` `RuntimeNoteGeometry` 对 skip 连接点缺邻点直接裸 `throw`，整张谱加载失败。
- `P3` `[CompilerGenerated]` 残留、公共 API 参数用 `_` 前缀、`ChartDocument.Migrate` 的迁移分支永不可达。

### M02 App 服务层（3.0）

骨架清楚（组合根 + 四端口 + 原子写 + zip-slip 防护），问题集中在**失败路径与跨线程契约**。

- `P1` 替换媒体先删后写（见"先修 6"）。
- `P1` Sonolus 索引一条坏记录锁死全部安装（同上）。
- `P1` 开局路径 UI 线程同步解析 + 重复 SHA-256 + 进度文案要等同步工作结束才显示。
- `P2` 组合根注入的 `Packages` 不进开局路径，`PlayService` 直接读 `SongPackageManager.Shared`——注入实现只影响列表、不影响实际开局。
- `P2` `ImportDownloadedZipAsync` 在工作线程回调 `Action<float> progress`，签名未标注线程约定。
- `P2` SUS 导入时转换结果被丢弃，非法谱面会被当作安装成功。
- `P2` `BackupService`（228 行）全仓无调用方，却带着主线程解包、静态回调槽永不清空、临时目录泄漏三个真实缺陷。
- `P3` `SongPackageHash` 哈希失败返回空串，与"没有谱面"共用同一哨兵，导致该局成绩被静默丢弃。
- `✔✔✔` `PerformanceHud.OnGUI` 每次调用 `new GUIStyle` 并拼字符串，污染它自己要测的帧时间。`✔✔✔` `SonolusComment` 越界时间戳抛非 `SonolusException`，违反文件头"解析永不抛"的自述契约。

### M03 曲库界面（3.3）

全项目结构最规范的 UI 模块（Presenter/View 分层、虚拟列表回收、LRU 缩略图缓存、`CancellationTokenSource` 生命周期都写对了），但**三条主线程重活 + 每次输入全量重建**。

- `P1` 打开曲库即对每首未测响度的曲子做一次阻塞式全量音频解码（约 265ms/首，主线程），且页面隐藏后扫描不停。
- `P1` 搜索框每敲一个字符就 `SetSearch → Render` 全量重建：滚动归位、整屏单元重播入场动画、详情封面加载被反复取消重开。
- `P1` 缩略图首次生成在主线程解码整图 + PNG 编码，与类注释"读盘与缩放都在工作线程"不符。
- `P2` 导入 `.oskp` 在主线程同步解压，无进度无取消。
- `P2` `LibraryLoudnessScanner` 的 `Stop()` 立即 `Dispose` 正在使用的 CTS 并置空 `_run`，随后 `Start()` 可让新旧两趟扫描并发。
- `P2` 每次改排序/分组都 `PlayerPrefs.Save()` 同步落盘。
- `P2` `LibraryView` 1129 行同时管相机/Canvas/布局/异步资源/返回键/三套动画（`✔✔`）。
- `P3` `LibraryDropdown.Close` 硬编码写回 `Control` 色，导入按钮打开过一次菜单后底色被永久改掉。
- `P3` `VirtualListView.SetItemCount` 的 `keepScrollPosition` 参数从未被使用；`LibraryUi.CreateButton` 死代码。

### M04 首页与服务器界面（3.0）

- `P1` 高级搜索面板根节点建在 `ModalLayer` 上，浏览页返回时只 `Dispose` 动画不销毁 —— **每进一次服务器泄漏一整棵 UI 树**，并保活整页对象图。
- `P1` `SonolusBrowsePage.BuildSection` 一次性实例化所有 section 的全部格子，无回收无虚拟化。
- `P2` `SonolusLevelPage` 1117 行承载六条异步生命周期（详情/封面/下载/试听/评论/榜单），取消管理只能靠 `OnDispose` 手工维护。
- `P2` 详情页刷新后排行榜/社区两页不重载，`_board` 是索引，榜单列表变化时标题与表格内容错位。
- `P2` `App/UI/Server` 三个 UI 类各自 `new SonolusClient` 并自管磁盘缓存，I/O 未落到 Infrastructure。
- `P2` 封面下载队列无上限，格子回收/页面关闭后仍在下载并每帧自旋。
- `P2` 三个网络入口只捕获 `SonolusException`，未预期异常让页面永久停在加载态。
- `P2` `RemoteCovers` 每次绑定时同步 `File.Exists`；品牌纹理 7×1024² 未压缩常驻 + 一次 28MB 瞬时分配。
- `P2` `HomeView` 开屏时间线尾段仍每帧 `ApplyEnter`，把已开始的转场 `expansion` 归零，卡片回弹。
- `✔✔` 事件未走 `Raise*`（`SonolusPreviewPlayer.StateChanged` 六处直接 `Invoke`）；`cancel+dispose+置空` 助手在模块内复制 6 份。

### M05 Live 演奏核心（3.7）

全项目评分最高。帧循环分层（`OnUpdate`/`OnInput`/`OnView` 三段、三条判定通道、结果发布单入口）与长条头/尾/内部点解耦都到位，绝大多数不变量有注释与回归用例支撑。

- `P1` 长条尾键按"最晚时间"重推（见"先修 1"）。
- `P1` `PauseLive` 不取消复活倒计时（见"先修 2"）。
- `P1` 生命值耗尽被记为"完成"并写盘（同上）。
- `✔✔✔` `OnApplicationPause` 用 `private new` 隐藏基类 `protected virtual`，基类暂停钩子永不执行；同文件 `OnApplicationFocus` 却显式调了 `base`。
- `P2` 判定时钟双源：`LiveLogic.currentFrameInfo`（public 可变字段）与静态 `MusicScore.CurrentFrameInfo`，Notes 判定直接读后者。
- `P2` `NoteBase` 的判定/计分回调是可被外部整体替换、可置 null 的 `Action` 属性，漏绑只在运行时表现为"有判定没分"。
- `P2` 抬起通道只接受 `Ended`，`Canceled` 的手抬被吞；`TailFrame.ReleasedInside` 恒为 false 使 `ReleaseTail.TryFinish` 成为死路。
- `P2` 普通长条尾用触摸样本时间评分，而非当前帧音乐时间（与判定文档明文不符）。
- `P2` 按下的长条头不计入"一根手指一帧一个按下额度"，同帧重复 `Began` 样本可多判一个头。
- `P2` `SetStateUnnotice(Done)` 绕过 `State` setter，`RemoveView` 不执行，视图不回收。
- `P2` `CalcTimeOffset → NoteSpeedRatio.Resolve` 每帧对每个可见音符与条子点从 info 表首条全扫到该位置（唯一"越打越慢"的热路径）。
- `P2` `SkillLogic` 的技能/事件链整条空转，多个 API 无调用方。
- `P3` `OnAwake` 里 `_context` 判空是死分支，一旦成立会双重 `BeginLive`；`AdvanceAutoJudgment` 声称推进游标实际按值传入。

### M06 Live 视图与表现层（3.0）

零分配意识明确（网格与数组复用、对象池、动画显式取消），但**契约与实现脱节**。

- `P1` `MusicInfoView` 自建封面色贴图永不释放，且与 `Background2DView` 重复解码同一文件（`✔✔`）。
- `P1` `LiveViewExt` 每帧/每音符经闭包转发（见"先修 11"）。
- `P1` `LiveSpriteButton` 不处理 `TouchPhase.Canceled`，触摸取消后暂停/跳过按钮整局失效。
- `P2` `CutinManager`/`CutinView`/`SkillTextView` 整层空实现，`modules.md` 承诺的"特写与技能文字"实际不存在。
- `P2` `TapEffectView.PlayHaptic` 空方法，振动设置完全无效。
- `P2` `BaseNoteView` 逐 `Renderer` 克隆材质且从不释放。
- `P2` `Android` 2DMV 缓存目录只增不减，首次拷贝同步阻塞主线程。
- `P2` `NotePool` 溢出时在打歌中 `Instantiate` 且只增不减。
- `P2` `LiveViewFactory` 绕过 `ResourceCatalog` 硬编码 `Resources` 路径。
- `P2` `OverlayMask` 用 public setter 暴露 `[SerializeField]` 字段，且是全仓零引用的死组件、自建材质无释放路径。
- `P3` `CountdownView` 残留反编译状态机类，`Reset()` 与 Unity 消息同名。
- `P3` 6 个视图文件使用 `Sekai.Core.Live` 命名空间（Game 程序集内），模糊了 App→Game→Core 的层次。

### M07 页面框架与 UGUI（2.3，全项目第二低；最低是 M08 的 2.0）

骨架能用，但**生命周期收尾与返回键消费没有单一出口**，P1 共 9 条，是各模块中第二多（M08 为 10 条）。

- 见"先修 7 / 8 / 9"三条。
- `P1` 每次 UI 点击做一次全场景 `FindFirstObjectByType<InputManager>`，且回落用的 0.2s 节流是**静态共享**的，会吞掉相邻按钮连点。
- `P1` `ScreenManager` 等 8 个 public 方法体是 `throw null`（`✔✔`）。
- `P1` `OnDisable` 取消关闭动画 → `OnClose` 永不触发，对话框队列与屏幕模糊永久不释放。
- `P2` `CustomSelectableDefine`、`CustomTextMesh`（每帧 `preferredWidth`）、`AlphaMask`（每帧分配 `Vector3[4]` + 材质不销毁）三处热路径开销。
- `P2` `TransitionService` 用 `default(DisplayLayerType)` 当"全部层"哨兵，与 `Layer_BG = 0` 冲突。
- `P2` `ScreenLayerLiveLoading` 订阅 `LiveEntryCurtain.WhenCovered` 后从不退订，每次 boot 叠加一次。
- `P2` `UIPartsDialogButtonGroup` 把子物体序号当按钮序号做显隐；`TouchEffect.StartLongTapEffect` 用错 Root（长按特效挂到跟随特效锚点上）。
- `P2` `GraphicButtonTapEffect.Awake` 用未初始化的 `_defaultColor` 重置颜色，按钮图标在首次交互前全透明。
- `P2` `UIAtlasImageLoader.LoadAsync` 同步加载后返回 `(bool, null)`，丢掉元素且忽略 `CancellationToken`。
- `P2` `UIBlurLayerManager`/`CameraFOVUpdater` 等把 UI 相机 `cullingMask` 整体改写且不还原。

### M08 平台层与资源体系（2.0）

- 见"先修 4 / 5"两条（引用计数、主线程空转）。
- `P1` HCA 128 频带解码读 `ScaleFactors[128]` 越界崩溃（[HcaDecoder.cs:532](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/HcaDecoder.cs#L532)）。
- `P1` HCA 解码用 128×128 朴素 IMDCT 矩阵、整曲展开为 `float[]`，全部在主线程同步完成（3 分钟立体声约 2×10⁹ 次浮点运算、约 70MB）。
- `P1` `CriAtom` 每帧与每次音效播放各做 1–3 次 `FindObjectsByType<AudioListener>` 全场景扫描。
- `P1` `Assets/LruAssetCache*` 整族反编译桩；`Platform/CP` 下 HTTP 客户端、`InputManager`、`UiEffect` 混合色组件同为 `throw null` 桩（共 100+ 处）。
- `P1` 下载 API 不下载任何文件却回调 `Success` 与进度 1.0。
- `✔✔✔` `AssetManager` 用 `new` 隐藏基类 `virtual OnDestroy`，单例收尾被跳过。
- `✔✔` `CriUtfTable` 越界检查只覆盖首字节，截断 ACB 抛 `ArgumentOutOfRangeException` 而非 `AcbException`。
- `P2` 清单模型用 lowerCamelCase 公有可变属性；`LogUtility` 用公共静态委托字段而非 `event`；`AssetBundleLoader.GetFullAssetName` 每次加载都重拉全部资产名。
- `P3` `LoadAcbFile` 静默丢弃 `awbPath`，ACB+AWB 分离的音源无法加载。

### M09 数据与共享纯逻辑（3.0）

`Shared/Live` 的判定与触摸纯逻辑是全模块最健康的部分（无每帧分配、struct 传递避免装箱、NaN 与边界都有处理且有测试）。

- `P1` 设置非原子写（见"先修 6"）。
- `P1` `JudgmentAggregator` 把 `NoteResult.Pass` 当作真实判定，同帧内会吞掉真判定的弹窗。
- `P1` `SongPackage.VideoPath` 在 manifest 无 `videoFileName` 时返回曲包目录本身，被当成视频地址交给 `VideoPlayer`。
- `P2` `SongPackage` 的路径属性每次读取都做 `Path.GetFullPath + File.GetAttributes`（缺失文件走异常路径），而虚拟列表每次绑定单元格都读一次。
- `P2` `LiveContext`（Game 侧）直接读全局 `UserDataManager`，与 `modules.md` 声明的边界相反。
- `P2` `LiveTrace` 在判定热路径逐行 `Debug.Log` + `AutoFlush` 文件写；`LatencyCapture` 采集后常驻约 31MB 托管缓冲。
- `P2` `Data/` 层多个类所有成员 `throw null`，`LiveEventData` 构造函数丢弃 `skills`/`cutIns` 入参。
- `P2` `MaterialExchangeHeaderCeilItem` 是放在 `Data/` 下的 MonoBehaviour UI 单元格（层级归属错误 + 每行闭包 + 异步回调无销毁保护）。
- `P3` `Data/` 下十余个枚举成员用全小写/下划线；`MasterMusic` 用 public 字段承载序列化数据。
- `✔✔` 公共 API 参数用 `_` 前缀（`SusRuntimeConverter`、`LiveSettingsStore`）。

### M10 音频 / 动画 / 渲染（3.0）

- `P1` `SetupVolume` 丢弃 voice 形参，语音音量设置完全失效（`✔✔`）。
- `P1` UI 模糊截屏时机硬编码（见"先修 10"，`✔✔`）。
- `P1` 三个 Renderer Feature 子类重写 `AddRenderPasses` 时漏调基类 `SetupRenderPass`，`layerMask` 恒为 `~0`，layer mask 与 Stencil 配置全部失效。
- `P2` `SekaiUIEffectSettings.Blur` 无人拥有的全局可变状态，被多实例每帧覆写（`UIBlurController` 在 `Update` 里无条件写，`UIBlurLayerManager.Disable` 直接置 false）。
- `P2` `SekaiMusicItemSettings` 注册表无实现者，对应 Feature 永不入队。
- `P2` 6 个 renderer feature 资产把设置序列化在 `m_Settings` 下，与 C# 的 `settings` + `FormerlySerializedAs("_settings")` 对不上，设置被静默丢弃。
- `P2` `SekaiUIBlurPass` 用相机全分辨率 texel size 采样 0.5 尺度 capture，模糊半径只有配置意图的一半。
- `P2` `SekaiDrawObjectsPass`/`SekaiDrawUIPass` 每相机每 pass 每帧新建 2 个托管数组 + 2 个未释放的 `NativeArray`；`SekaiDrawUIPass` 还会清空共享的深度/模板附件。
- `P2` `ExternalSoundPack.Load` 在异步加载开始前就置位，失败后永久不重试。
- `P2` `SoundUtility.SetupGlobalSeSettings`、`ShaderPreloader` 均为空壳但被启动流程当真调用。
- `P3` `TweenBase` 残留反编译生成的空协程类并挂着失效的 `IteratorStateMachine`；`SyncGameTime` 让 `PingPong` 丢掉回程退化 `Loop`；`isReflesh` 字段无任何读写点。
- `P3` `EffectPool.Spawn` 只做轮转不检查目标是否仍在播放，特效会被下一次 `Spawn` 截断。

### M11 测试套件（3.0）

没有永真断言、没有被注释掉的断言，断言质量整体在水准之上。问题集中在**测试自身的可信度与被测边界**（见"横向模式 G"）。

- 覆盖假象 6 类（视图寿命、判定窗口表、retry reset、`Assume` 降级、陈旧报告、`LiveHarness` 全局状态）。
- 13 个文件用进程工作目录拼 `Tools/fixtures` 路径，隐藏外部依赖。
- 两个 EditMode 用例写 `Application.persistentDataPath/logs` 并按文件名前缀删真实文件。
- `GeneratedAssetSaveGuardTests` 直接把字形烘进受跟踪的字体资产并 `SaveAssets`，唯一保护是被测的 guard 本身。
- `PlayMode/LibrarySmokeTests` 靠反射读私有字段；全局音符特效包按名字反推索引还原，其它取值被静默改成 0；`LibrarySceneBootsAndShowsThePage` 拿到 `LibraryScreen` 的同一帧就断言 `IsReady`，且全文件无 `[Timeout]`。
- `NextRushChartLoaderTests` 单个 5145 行 fixture 混装 loader、判定循环与像素渲染。
- `live` 测试夹具存在 4 份手写副本，已出现字段集合不一致。
- 零覆盖：AssetBundle 超时/下载/重试、`Platform` 输入适配、`Rendering` Pass 顺序。

### M12 编辑器工具（3.0）

注释与防御意识很强（`Editor`-only asmdef、bundle 新鲜度检查、打包条数校验、生成资源守卫），但失败路径普遍不原子。

- 见"先修 12"四条。
- `P1` `ScreenshotSession.Start` 先写全局状态（`SessionState`、`runInBackground`、GameView 索引）再校验前置条件，抛错后永久卡在 busy 且设置不还原。
- `P1` `TestRunnerBridge` 的自动化入口在测试真正开始前就返回，批处理模式下拿不到报告也拿不到退出码。
- `P2` `PaintKit.ApplyPainted` 用 `f.Length < 8` 做守卫却访问 `f[11]`，且 `rect` 行 `Paste` 不在 `try/catch` 内 —— 整批会中途抛错并留下部分写入与 `isReadable` 污染。
- `P2` `PaintKit` 对每个文件做 `isReadable` 往返 + `SaveAndReimport`，而像素其实来自 `LoadImage` 的字节（601 个文件即约 1200 次无谓重导入）。
- `P2` `GeneratedAssetSaveGuard` 与 `MissingScriptAudit` 用进程 CWD 解析项目相对路径，工作目录不同就静默跳过（后者因此永远"通过"）。
- `P2` `FontAssetRebuilder` 绕过 `AssetDatabase` 直接文本重写全工程资产后调 `SaveAssets()`，非原子且可能与内存版本互相覆盖。
- `P2` `AtlasSpriteMigration` 破坏性一步式迁移：先写手写 `.meta` 再删源资产，导入设置配置在后，中断后无法重跑。
- `P2` `BuildStampBuildStep` 的 5 秒超时无效：`ReadToEnd()` 先无限阻塞，`WaitForExit(5000)` 结果被忽略且不 `Kill`。
- `P2` `PrefabFieldAudit` 不认 `[FormerlySerializedAs]`，把已迁移的重命名报成 stale。
- `P3` 同一模块内两套命名空间并存（`Sekai.EditorTools` / `OpenSekai.EditorTools`）。

---

## 四、建议的修复批次

不要按模块修，按风险修。

**批次 1：判定与存档（数据正确性，改动小、收益确定）**
M05 长条尾键 / 暂停协程 / 失败归类；M01 flick 单位 + bar→ticks 单源；M09 `NoteResult.Pass`；M09 + M02 原子写与"先写后换"。

**批次 2：Android 卡顿与 ANR（体验兜底）**
M08 两条主线程空转 + 引用计数 + HCA 越界；M02 开局线程化；M03 响度扫描与缩略图；M06 2DMV 复制；M10 逐帧数组与 RT 释放。

**批次 3：页面与热路径（结构性债务）**
M07 返回键单出口 + 页面栈重入 + 对话框收尾；M05/M06 闭包与材质；M07 `FindFirstObjectByType`；M04 泄漏的搜索面板与一次性建满的格子。

**批次 4：发布链路（防止出坏包）**
M12 截图工具 try/finally、AB 构建先建后换、CLI 与菜单同源、权限剥离失败即失败；顺手把 `MissingScriptAudit` 改成会红的门禁。

**批次 5：死 API 清查（一次性、可机械执行）**
`throw null` 全集、只有 TODO 的实现、被静默丢弃的参数、永不入队的 Feature。这一步不动逻辑，只删或补，风险最低但最容易被跳过——它决定了下一轮审查还能不能相信代码表面。

**批次 6：规范整改（可分批、可工具化）**
`04-events` 三规则（`event` / `Raise*` / 命名）、`[SerializeField]` 打在委托上、`03-naming` 的 `_` 前缀与枚举大小写、`public 可变字段`。这批与既有的 [代码规范审查与整改计划](coding-standards-audit-2026-11-20.md) 可合并执行。

---

## 五、方法与局限

**方法**：项目按目录职责划分为 12 个模块（与 [modules.md](../architecture/modules.md) 的分层一致）。每个模块由 3 个独立智能体分别审查，互不可见，各自以「正确性 / 架构与规范 / 性能与健壮性」为主视角完整通读该模块全部 `.cs`，要求每条问题附真实行号与原码摘录，禁止无证据推断。三份结果由汇总者按「文件 + 行邻近 + 标题词元相似度」做启发式合并，合并结果中记录被几名审查员独立命中。

**这份报告的可信度来自哪里**：同一问题被不同视角、不同起点的审查员各自撞到时（14 处），基本可以当作事实；只被一人报告且标 `confidence: medium/low` 的条目，建议先自行复现再动手。

**汇总时做的复核**：对排名最前的 3 条 P1 回读了源码。`FlickDistanceInPixels` 的单位问题不仅成立，还能在同一个测试文件里找到两条互相矛盾的断言（见"先修 3"）；`TryGetTail` 的"取最晚候选"逻辑与报告描述一致；`PauseLive` 确认没有取消倒计时的动作。其余条目未逐条回读，保留审查员给出的 `confidence` 标注。

**局限**：

- 全部为静态阅读，**未经编译与运行验证**。涉及运行时行为的结论（尤其是并发时序、Unity 序列化实际取值、URP Pass 顺序）需要在编辑器或真机上确认。
- 合并去重是启发式的，同一问题的不同表述可能仍被算作两条，也可能把相邻但不同的问题合到一起——`alternateTitles` 保留了这个不确定性。
- 未覆盖：`Assets/Vendor/`（第三方）、shader/HLSL、`.prefab`/`.unity`/`.asset` 的实际序列化值（仅在审查员顺带核对资产时涉及）、Android Java 与构建脚本（`Tools/`）。
- 行号是审查时快照，修复前请以当前文件为准。
