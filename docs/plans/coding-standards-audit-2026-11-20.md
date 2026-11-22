# OpenSekai 代码规范审查与整改计划

2026-11-20 审查 `dev` 的 `7f894792`。优先处理渲染资源的序列化缺失。曲库场景使用的 UI Renderer 和默认场景 Renderer 都能追踪到受影响的资源。随后处理重复订阅、导入后重复刷新、动画清理和可写缓存，再分模块收敛命名与格式。

审查依据为 [项目 C# 代码规范](../coding-standards/README.md)。主范围包含 `Assets/Game` 下的 App、Core、Game 和 Editor，以及 `Assets/Tests`，共 708 个受版本控制的 C# 文件。命名与特性采用语法扫描，事件、状态和生命周期按代表性调用链复核，Unity 序列化问题核对了实际资源和场景引用。第三方库、Android Java 和独立构建或素材脚本在本轮范围之外。

仓库结构检查 `./Tools/verify-structure.ps1 -CheckAssemblies -RequireNoEmptyDirectories` 通过。以下运行影响依据源码和资源推导，Unity 复现、自动化回归和 Android 验收待实施阶段执行。

## 优先整改

### P1 渲染设置未从现有资源载入

`SekaiLegacyDrawUIRendererFeature` 的 `BlurLayerMask` 和 `UILayerMask` 已有序列化资源值 64 和 32，当前私有后备字段缺少 `[SerializeField]`。曲库场景使用的 UI Renderer 引用了该资源。进入模糊分支时，两处绘制 Pass 会收到零图层掩码。同类型中的 Pass 顺序字段也需核对。默认场景 Renderer 上还有两处同类问题，`ShaderTagTypes` 保持空数组，使 `CreatePasses` 提前返回，描边的 `fovCurve` 则使用默认常数曲线。对应第 05 篇 §3.4。 [图层掩码字段](../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L51) · [遮罩资源](../../Assets/Graphics/Rendering/ScreenLayerUI_0.asset#L16) · [曲库场景](../../Assets/Game/Scenes/Library.unity#L1042) · [渲染管线配置](../../Assets/Graphics/Rendering/UniversalRenderPipelineAsset.asset#L19) · [Shader 标签字段](../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawObjectsRendererFeature.cs#L50) · [Shader 标签资源](../../Assets/Graphics/Rendering/Eyelash_1.asset#L22) · [描边字段](../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiCharacterOutlinePass.cs#L44) · [描边资源](../../Assets/Graphics/Rendering/SekaiCharacterOutlineFeature_3.asset#L21)

恢复相关字段的 Unity 序列化。改名时用 `[FormerlySerializedAs]` 保留已提交资源的键。验收时直接加载现有资源，核对掩码、Shader 标签、Pass 顺序和描边曲线，再检查曲库弹窗模糊及 Live 场景的渲染。

### P2 弹窗标签重复初始化会累积处理器

`UIPartsDialogTabGroup.SetupTab` 用两个独立的 lambda 退订和订阅。退订不能移除上次初始化时添加的委托，重复调用 `Setup` 后，一次选择可以触发多次处理。发布端的 `OnSelectedIndexChanged` 还是可以被外部整体替换的 `Action<int>` 属性。按第 04 篇改用具名处理器配对订阅和退订，并将广播收敛到事件。验收时在同一弹窗实例上连续初始化并切换标签，确认每次切换只触发一次处理。 [订阅端](../../Assets/Game/Runtime/OpenSekai.Game/UI/UIPartsDialogTabGroup.cs#L47) · [发布端](../../Assets/Game/Runtime/OpenSekai.Game/UI/CustomIndexToggleGroup.cs#L76) · [设置弹窗入口](../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogSetting.cs#L63)

### P2 服务器详情隐藏后仍运行加载动画

详情请求返回前打开评论，再切回简介，评论容器会停用，但其无限呼吸动画仍在全局调度器上运行。详情请求成功后只启动当前标签的后续加载，隐藏的评论动画可能一直运行到重新打开或销毁页面。详情请求失败时，重试入口只出现在简介页。按第 11 篇把动画的启停与页面及标签可见性绑定，并核对当前标签能显示其前置请求的失败状态。验收覆盖请求未完成时切换标签、请求成功与失败、离开和重开页面。 [动画创建](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/ServerWidgets.cs#L321) · [详情请求](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs#L345) · [标签切换](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs#L714)

### P2 Android 网页导入后重复刷新曲库

首页直接订阅 `ServerChartDownloads.Imported`。服务器页也会订阅同一事件，再通过 `LibraryChanged` 转给首页的同一处理器。服务器页存在时，一次成功导入会重复刷新曲库和首页统计。按第 07、08 篇收敛同一导入事件的传递路径，保留原生 Sonolus 下载完成后的通知语义。验收覆盖 Android 网页导入、原生 Sonolus 导入和返回曲库。 [首页直接订阅](../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeScreen.cs#L220) · [服务器转发](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/ServerScreen.cs#L393) · [首页接收转发](../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeScreen.cs#L468) · [刷新处理](../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeScreen.cs#L502)

### P2 游玩历史暴露可写缓存

`LoadSummaries` 和 `ReloadSummaries` 返回缓存字典，摘要的 `Best` 也引用带公开 setter 的记录。调用方可绕过 `Recorded` 篡改缓存，下次追加时这些修改可能进入磁盘快照。按第 10 篇让查询和事件返回只读查询结果及不可变摘要，保留旧 JSON 键。验收覆盖查询、追加、快照重载及旧历史文件。 [公开查询](../../Assets/Game/Runtime/OpenSekai/App/Library/Storage/CustomMusicScorePlayHistoryStorage.cs#L83) · [摘要与记录](../../Assets/Game/Runtime/OpenSekai/App/Library/Storage/CustomMusicScorePlayHistoryStorage.cs#L269) · [快照写入](../../Assets/Game/Runtime/OpenSekai/App/Library/Storage/PlayHistorySummaryCache.cs#L58) · [现有历史测试](../../Assets/Tests/EditMode/Packages/PlayHistoryRecordTests.cs)

### P2 转场淡入淡出缺少生命周期清理

`ColorFader` 通过自写协程逐帧插值，组件停用或销毁时没有集中清理。自身 GameObject 停用并中断协程后，协程末尾不会复位 `IsPlayFading` 读取的状态。按第 11 篇让创建动画的组件负责取消，保留现有的延迟、回调和状态语义。`ScreenLayerLiveLoading` 的白场插值可在同一批复核。验收覆盖正常完成、播放途中停用、重复播放和场景切换。 [转场动画](../../Assets/Game/Runtime/OpenSekai.Game/Screens/ColorFader.cs#L939) · [取消与插值](../../Assets/Game/Runtime/OpenSekai.Game/Screens/ColorFader.cs#L972) · [转场调用方](../../Assets/Game/Runtime/OpenSekai.Game/Screens/TransitionService.cs#L120) · [LiveLoading 白场](../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenLayerLiveLoading.cs#L107)

## 模块整改候选

| 模块 | 发现与整改方向 |
| --- | --- |
| Live 状态 | `LiveLogic.result` 公开可写，可以绕过 `Result` 的通知。正常完成分支也独立写字段和发事件。统一状态写入口，并按第 04、10 篇收敛事件声明、触发与订阅。 [公开字段](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveLogic.cs#L124) · [结束分支](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveLogic.cs#L273) |
| Live 动画 | 暂停和跳过按钮的缩放绕过 `LiveMotion` 的固定步长截图调度器。结算背景淡入和特效回调各计 0.2 秒，分别使用游戏时间和独立时间，时间缩放改变时可能错位。按第 11 篇统一调度器，把阶段效果和完成回调放在同一条时间线。 [按压缩放](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveSpriteButton.cs#L153) · [结算动画](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveResultView.cs#L71) · [调度器](../../Assets/Game/Runtime/OpenSekai.Game/Live/LiveMotion.cs#L13) |
| Live 背景 | `Background2DView` 同时处理背景呈现、Android 视频缓存和同步文件复制。它与曲目信息页的封面读取错误处理也已分叉。按第 07 篇将平台文件职责移出视图，明确读取失败时的回退规则。 [视频缓存](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/Background2DView.cs#L620) · [背景封面](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/Background2DView.cs#L774) · [曲目信息封面](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/MusicInfoView.cs#L801) |
| App 事件 | `ServerRegistry.Changed` 在三处直接触发，订阅端用 `Rebuild` 直接注册。`PlayRequested` 还使用四个位置参数。按第 04 篇收敛 `RaiseChanged` 和具名订阅者，多参数事件优先考虑结构体。 [触发](../../Assets/Game/Runtime/OpenSekai/App/Server/ServerRegistry.cs#L134) · [订阅](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/ServerListPage.cs#L55) · [多参数事件](../../Assets/Game/Runtime/OpenSekai/App/UI/Server/ServerScreen.cs#L49) |
| 导入与存储 | NextRUSH 的 `Builder` 同时处理时间换算、音符构建、连接图和音效区间，可以沿既有阶段提取独立逻辑。另有三处重复维护文件名保留字符规则，可共享字符判断并保留调用点各自的空白和回退语义。对应第 07 篇 §§5.1–5.2。 [NextRUSH 构建](../../Assets/Game/Runtime/OpenSekai/Infrastructure/Charts/NextRushChartLoader.cs#L141) · [曲库导出](../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryPresenter.cs#L368) · [曲包管理](../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L935) · [曲包存储](../../Assets/Game/Runtime/OpenSekai.Game/Shared/Packages/SongPackageStorage.cs#L129) |
| 页面与音频 | `ScreenLayerData` 的配置字段公开可改，`SoundBundleBuildData.AcbFiles` 可整体替换或修改原数组。`DialogBase` 的三个关闭出口重复维护相同的状态更新和回调顺序。按第 05、07、10 篇收紧配置写权限，收敛弹窗收尾流程。 [页面配置](../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenLayerData.cs#L7) · [音频配置](../../Assets/Game/Runtime/OpenSekai.Game/Audio/SoundBundleBuildData.cs#L19) · [弹窗关闭](../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L399) |
| Core 设置 | `LiveSettingData` 把旧 JSON 键 `_noteShowRate` 暴露成公共可写属性，视图直接读取它并重复做反向换算。按第 03、10 篇将消费者收敛到 `NoteShowRate`，单独保留序列化键。 [设置模型](../../Assets/Game/Runtime/OpenSekai.Core/Settings/LiveSettingData.cs#L120) · [视图读取](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/NoteShowRateView.cs#L28) |
| Editor 截图 | `ScreenshotDriver` 通过字符串反射读写 Live 控制器的内部字段，并复制状态枚举数值。字段改名后，问题会到截图运行时才暴露。按第 08 篇利用现有 Editor 程序集访问权限提供类型化入口。 [截图驱动](../../Assets/Game/Editor/OpenSekai/Screenshots/ScreenshotDriver.cs#L39) · [程序集访问权限](../../Assets/Game/Runtime/OpenSekai.Game/AssemblyInfo.cs#L7) |

## 命名与格式基线

常量按 `ALL_CAPS`、普通私有字段按 `_camelCase`、显式标注 `[SerializeField]` 或 `[SerializeReference]` 的字段按 `camelCase` 统计。下表按标识符计数，常量包含局部常量。Unity 隐式序列化的公开字段和 JSON 属性按具体用例复核。 [命名规则](../coding-standards/03-naming.md) · [特性与序列化](../coding-standards/05-attributes-serialization.md)

| 范围 | C# 文件 | 常量命名偏差 | 普通私有字段命名偏差 | 显式序列化字段命名偏差 |
| --- | ---: | ---: | ---: | ---: |
| App | 96 | 53 | 50 | 0 |
| Core | 46 | 40 | 5 | 0 |
| Game | 424 | 86 | 209 | 355 |
| Editor | 23 | 77 | 39 | 0 |
| Tests | 119 | 113 | 30 | 0 |
| **合计** | **708** | **369** | **333** | **355** |

另有 31 行声明特性与成员同行，以及 1 行堆叠多个特性。根目录缺少第 06 篇要求的 `.editorconfig`，现有结构检查未覆盖上述命名和格式规则。 [同行示例](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/OverlayMask.cs#L12) · [堆叠示例](../../Assets/Tests/EditMode/Live/StallReplayTests.cs#L383) · [团队样式配置](../coding-standards/06-regions-editorconfig.md#四团队协作配置editorconfig)

## 实施顺序与验收

1. 先恢复渲染配置序列化，直接用现有资源验证旧键迁移与加载结果，再核对曲库模糊和 Live 画面。
2. 分别处理事件订阅、服务器与曲库刷新、历史缓存和动画清理。用对应的调用链做回归，其中网页导入以 Android 实机结果验收。
3. 加入根目录 `.editorconfig`，把声明命名和特性换行纳入检查。历史偏差按模块处理，Unity 资源字段和 JSON 键的改名随兼容性验证一起提交。

## 整改与验收结果

- **渲染资源**　Unity 已直接加载现有 Renderer 资源，核对了模糊层与 UI 层掩码 64、32、绘制顺序、Eyelash 标签及描边曲线。曲库设置弹窗使用半透明遮罩，另用曲库 UI 相机和测试图形检查了旧版模糊渲染通道。已复核曲库、设置弹窗和 Live 12.5 秒处的截图。
- **事件、状态与动画**　弹窗重复初始化后，每次切换只通知一次。服务器详情加载动画随标签和页面可见性启停，失败时可在当前标签重试。网页导入与原生 Sonolus 导入各保留一条刷新路径。转场与 Live 动画统一清理，游玩历史查询返回只读缓存和不可变记录，旧 JSON 可正常重载。
- **模块整改**　已收敛 Live 结果写入、服务器事件、页面和音频配置、弹窗关闭流程；提取媒体文件处理、NextRUSH 时间换算及共享文件名校验；设置页保留旧 JSON 键，截图工具改用类型化接口。
- **规范检查**　根目录已加入 `.editorconfig`，CI 已接入声明命名和特性换行检查。本地扫描覆盖 711 个 C# 文件，发现 0 项。程序集边界、目录结构和差异格式检查均通过。
- **Unity 回归**　EditMode 共 1,521 项，1,500 项通过，21 项跳过，0 项失败。PlayMode 全部 28 项通过，包含对模糊纹理中心、边缘过渡和背景像素的检查。
- **Android**　在 Android 16 的 PLQ110 上覆盖安装验收包，从内置 WebView 下载《Luminous Entities Lost Heart》，返回游戏后自动导入成功。曲包数由 17 增至 18，新目录 `server-45` 含清单、谱面、音频和封面，下载临时目录已清空。已在导入后的曲库刷新回调加入独立日志，供复测计数。验收包位于 `Builds/standards-audit-20261120/OpenSekai.apk`。
- **Android 刷新计数复测**　在同一手机的内置 WebView 下载《愛♡スクリ～ム！》，返回游戏后新增 `server-46`，下载临时目录为空。此次应用进程在 11:11:06 记录了 1 次导入后的曲库刷新和 1 次导入成功，重新进入曲库后仍为 19 首。两次下载的曲包数依次为 17、18、19。
