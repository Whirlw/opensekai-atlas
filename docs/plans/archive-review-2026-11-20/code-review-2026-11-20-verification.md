# OpenSekai 代码审查逐条复核 · 2026-11-20

对照工作区代码，基准提交 `567116c3`。按[原报告](code-review-2026-11-20.md)顺序列出结果。成立表示源码足以确定该行为；部分成立给出触发条件或反证；待证表示仍缺少能把观察和后果连起来的证据。本次未运行 Unity、构建或 Android 真机测试。原文没有随附约 340 条明细、独立审查原稿或性能采样，以下覆盖公开列出的条目。

## 优先清单

| 原编号 | 结论 | 证据、触发条件与处理判断 |
|---|---|---|
| 1 长条尾 | 部分成立 | 扫描以 >= 取最后一个同时间候选，随后要求它与 Child 相同。AddHoldCombo 只追加列表，候选又没有排除 LongHoldCombo，所以在绑定普通尾后追加同时间可判 combo，确有选错对象的路径。隐藏连接点和 skip 候选已排除，常规自动补点还会避开终点并设置 Skip；一 tick 的正时长本身不会导致平局。扫描次数取决于活动状态。修复要保留隐藏尾不可判、内部点不可提前结尾两条约束。[LongNoteTail:37](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNoteTail.cs#L37) [LongNote.Chart:114](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.Chart.cs#L114) [TailBehaviourTests:244](../../../Assets/Tests/EditMode/Live/TailBehaviourTests.cs#L244) |
| 2 暂停、零生命 | 部分成立 | 常规 OnPause 会停止恢复协程，连续自动入口 PauseLive 不会，且 IsPlayableState 包含倒计时，协程三秒后直接恢复。该自动视图 Setup 先 Hide，PauseLive 自身也不展示暂停框；未找到仓内产品代码主动调用该视图 Show，“开着面板自行恢复”仍缺正常入口复现。零生命打到终点走 Completed，符合历史写入条件时会入库，同时有 LifeZero 演出及历史 life_zero 字段，而曲库结果面板会将普通的 Completed 显示为 CLEAR。玩家指南把耗尽称为失败，应先明确完成原因、通关标记和历史收录三者的约定。[SoloLiveController:265](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L265) [SoloLiveController:343](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L343) [SoloLiveController:515](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L515) [LibraryResultPanel:419](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryResultPanel.cs#L419) [CustomMusicScorePlayHistoryStorage:212](../../../Assets/Game/Runtime/OpenSekai/App/Library/Storage/CustomMusicScorePlayHistoryStorage.cs#L212) [ConsecutiveAutoLiveView:40](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/ConsecutiveAutoLiveView.cs#L40) |
| 3 flick、小节 | 成立，范围有别 | 100 DPI 下 5 cm/s 对应每参考帧 3.281 px，当前两条公式约 1.292 px。测试都能通过，冲突在物理含义。原判定文档自身也同时列了目标和错误换算。改动覆盖使用该距离的输入分支；非 4/4 是独立问题，见 M01。[JudgeTuning:72](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/JudgeTuning.cs#L72) [LiveUtility:39](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveUtility.cs#L39) [JudgeTuningTests:43](../../../Assets/Tests/Core/JudgeTuningTests.cs#L43) |
| 4 依赖计数 | 成立 | 父包缓存命中提前返回，首次装载或父包卸载后重载才递归加依赖。普通释放只减指定包，失败分支也只退父包。强制清理另有入口。共享依赖的回归应验证“最后一个持有者释放后归零”。[AssetManager:152](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L152) [AssetManager:293](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L293) [AssetManager:454](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L454) |
| 5 Android 等待 | 阻塞成立，ANR 待证 | AB 的 URI 读取 Sleep 轮询，ACB 的 URI 读取空转，均无本地截止时间。普通文件和其他加载分支不经过该循环。主线程命中就占住该线程；特定 Unity 版本是否永久等待、线上白屏归因和持续时间需线程栈或真机采样。响度分析已有分帧预算，开局重复哈希有缓存条件。[AssetBundleManager:443](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleManager.cs#L443) [CriAtomShim:467](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L467) [LoudnessMeter:117](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Audio/LoudnessMeter.cs#L117) |
| 6 设置、替换、索引 | 前两项成立，索引部分成立 | 两个设置存储直接写正式文件，解析失败有 warning 并回默认。媒体先删槽位后编码或复制，普通谱面虽先验证，也在删除旧件后写正式文件。Sonolus 的坏记录会阻止后续发布，读取失败也能让查询显示未安装；正常发布先严格重读才进 CommitPublication，未找到报告所指“读坏即覆写为一条”的正常调用链。恢复用索引决定可删除哪个目录，不能直接把坏索引当空表继续。[LiveSettingsStore:50](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Settings/LiveSettingsStore.cs#L50) [ApplicationLocalSettings:106](../../../Assets/Game/Runtime/OpenSekai.Game/Data/ApplicationLocalSettings.cs#L106) [SongPackageManager:434](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L434) [SongPackageManager:574](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L574) [SonolusLevelInstaller:380](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Sonolus/SonolusLevelInstaller.cs#L380) [SonolusLevelInstaller:454](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Sonolus/SonolusLevelInstaller.cs#L454) |
| 7 返回键 | 成立，有门槛 | 两个 Update 同读同帧输入，ScreenManager 忽略射线结果并可回退页面。实际是否双退取决于当前处理器、栈和对话框状态。外部关闭开关未覆盖硬件返回路径，单按钮实现能触发确认。[DialogBase:443](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L443) [ScreenManager:539](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenManager.cs#L539) [Common1ButtonDialog:149](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/Common1ButtonDialog.cs#L149) |
| 8 页面栈 | 成立 | Push 检查忙标志却没像 Change/Pop 那样持有它，结束还会清标志。ExitScreen 的 yield break 不向调用方返回 Cancel，Pop 已先改栈，Change 可继续。等待退出或启动状态没有截止条件。[ScreenStackService:91](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L91) [ScreenStackService:308](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L308) [ScreenStackService:386](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L386) [ScreenStackService:514](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L514) |
| 9 对话框关闭 | 条件成立 | 关闭完成通知由动画完成路径驱动，禁用会取消动画。在仍由服务持有、且未走整体清场的对话框上，收尾可能遗漏。不能从这里推出模糊必定永久存在。[DialogBase:255](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L255) [DialogBase:527](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DialogBase.cs#L527) |
| 10 模糊 | 部分成立 | 复制事件硬编码为 1，资产字段值 500 未采用。早排序可以是有效的自定义排序，基础相机或叠加相机已有的颜色也影响输入；需要帧调试器才能定性为残帧。四张 RT 依描述符缩放和复用，其中三张显式释放，UIBlurTexHandle 的释放开关没有赋真。多处写全局开关属实，“永久为真”未证。[SekaiLegacyDrawUIRendererFeature:147](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L147) [SekaiUIBuffer:60](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiUIBuffer.cs#L60) [资产](../../../Assets/Graphics/Rendering/ScreenLayerUI_0.asset#L29) |
| 11 热路径 | 成立，开销未量化 | 游戏阶段禁 GC，有捕获参数的视图委托、实例方法委托、经接口遍历 PendingTails 及线性 Contains。Contains 增加查找成本，本身不必分配。renderer.material 会产生实例材质，成本随已创建的池对象数增长，重复借同一对象不会每次都再克隆。[BaseLiveController:229](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/BaseLiveController.cs#L229) [LiveViewExt:89](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveViewExt.cs#L89) [LiveInputRouter:85](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveInputRouter.cs#L85) [LiveNoteFrame:100](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L100) [BaseNoteView:159](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/BaseNoteView.cs#L159) |
| 12 发布 | 分项成立 | ① 截图迭代器异常可能跳过音量恢复、报告和回调，900 秒是外层默认等待，残留进程取决于启动和退出方式。② 构建在正式目录清空后才产出新包，真正构建失败会抛异常；菜单忽略的是前置检查返回的 false。③ CLI 不建 AB，但缺 catalog 或过时会由预处理明确失败；新鲜度与条数不识别错误目标。④ 权限后处理失败确实只记 warning，最终权限需检查合并后的发行 manifest。[ScreenshotDriver:61](../../../Assets/Game/Editor/OpenSekai/Screenshots/ScreenshotDriver.cs#L61) [OpenSekaiAssetBundleBuildPipeline:90](../../../Assets/Game/Editor/OpenSekaiAssetBundleBuildPipeline.cs#L90) [CliBuild:51](../../../Assets/Game/Editor/CliBuild.cs#L51) [AssetBundlePreBuildStep:37](../../../Assets/Game/Editor/AssetBundlePreBuildStep.cs#L37) [RecordingBuildRules:116](../../../Assets/Game/Editor/OpenSekai/RecordingBuildRules.cs#L116) |

## 模块明细

编号按各模块原文项目符号排列；重复提出的事项沿用优先清单结论。

### M01 领域与谱面

1. **成立**。flick 单位见优先项 3。
2. **部分成立**。含拍号的 bar → tick 与固定每小节 tick 的路径并存；后者用于事件排序、BPM 表和部分逆算。错位取决于谱面格式、拍号变化和调用的转换方向。[ChartTimeline:67](../../../Assets/Game/Runtime/OpenSekai/Charts/ChartTimeline.cs#L67) [ChartTimeline:313](../../../Assets/Game/Runtime/OpenSekai/Charts/ChartTimeline.cs#L313)
3. **成立**。Create 对缺省阈值生成默认槽，GetRank(null) 直接 D，全零阈值又可直接 S。Create(1) 向下取整使 S 阈值为 0，零分进度就是 1。[ScoreGauge:34](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/ScoreGauge.cs#L34)
4. **未证实**。ActivationTime 对负斜率、前缀最大值及带 scroll 标记的情况有不同分支，保守提前激活也可能更早创建对象。“全反向便在判定线上突然出现”缺目标段与逐帧坐标反例。[NextRushScrollTimeline:323](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/NextRushScrollTimeline.cs#L323)
5. **部分成立**。未知 noteBaseType 等值可在转换阶段抛出；不同枚举的分支和错误呈现并不相同。拒绝不支持的谱面值合理，可改进的是定位到音符和字段的提示。[RuntimeChartConverter:602](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L602) [ChartJson:24](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/Serialization/ChartJson.cs#L24)
6. **部分成立**。连线查找确实逐次 Find，最坏随连线数乘以总音符数增长。SUS 字符串处理的分配数依 token、解析分支和编译实现而异，固定“每格 2–3 次”缺采样。[RuntimeChartConverter:507](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L507)
7. **部分成立**。参数进入实际算法，正常入口没有传非空值；音符保留终点在阈值及以后的链，事件保留阈值前的数据。两者用途是否允许不同需定义截取语义。[RuntimeChartConverter:135](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L135) [RuntimeChartConverter:242](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L242)
8. **成立**。无父级、不在列表或缺左右有效邻点均有明确异常，转换路径未在该点降级。受影响的是不满足几何约束的谱面。[RuntimeNoteGeometry:14](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeNoteGeometry.cs#L14) [RuntimeChartConverter:357](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L357)
9. **部分成立**。生成标记及下划线参数属于风格；CURRENT_VERSION=1 时正版本号不能进入排序迁移，负值形式上仍可进入。清理 EventArray 的迁移动作始终执行。[ChartDocument:33](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/ChartDocument.cs#L33) [RuntimeChartConverter:23](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L23)

### M02 服务与存储

1. **成立**。媒体替换见优先项 6。
2. **部分成立**。索引可阻断发布；覆写链缺证，见优先项 6。
3. **部分成立**。读取提示先回调，同步转换可能阻止 UI 及时绘制。普通包计算会话 SHA；第二个历史键优先取缓存，仅未命中时重算。临时包直接使用已有哈希。[PlayService:99](../../../Assets/Game/Runtime/OpenSekai/App/Playback/PlayService.cs#L99) [SongPackageManager:101](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L101)
4. **成立**。服务直接查 Shared 库和历史键，替换组合根的 Packages 无法覆盖这两步。[PlayService:102](../../../Assets/Game/Runtime/OpenSekai/App/Playback/PlayService.cs#L102)
5. **成立**。下载导入在后台执行的阶段会回调 progress；当前 UI 消费端用 Volatile.Write，潜在问题在其他调用方的线程预期。[SongPackageManager:266](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L266) [ServerChartDownloads:125](../../../Assets/Game/Runtime/OpenSekai/App/UI/ServerChartDownloads.cs#L125)
6. **部分成立**。SUS 转换返回对象被丢弃，异常仍会中断安装。能返回空谱的输入缺少与开局同等的可玩性检查，不能推成任意语法错误均能安装。[SongPackageManager:290](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs#L290) [ChartFileReader:24](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Charts/ChartFileReader.cs#L24)
7. **部分成立**。备份有同步解压、导出成功后才删临时目录以及静态状态，恢复路径另有 finally。未找到正常产品调用，属于休眠代码风险。[BackupService:82](../../../Assets/Game/Runtime/OpenSekai/CustomMusicScoreManager/Services/BackupService.cs#L82) [BackupService:117](../../../Assets/Game/Runtime/OpenSekai/CustomMusicScoreManager/Services/BackupService.cs#L117)
8. **部分成立**。I/O 错误返回空串并记 warning；最终历史键为空会跳过写入，缓存命中时仍可有有效的键。[SongPackageHash:17](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageHash.cs#L17) [CustomMusicScorePlayHistoryStorage:42](../../../Assets/Game/Runtime/OpenSekai/App/Library/Storage/CustomMusicScorePlayHistoryStorage.cs#L42)
9. **两项成立**。HUD 的 OnGUI 建样式并格式化文字，仅在该诊断界面生效。评论时间戳只检查正数，超过 DateTimeOffset 范围可抛标准异常。[PerformanceHud](../../../Assets/Game/Runtime/OpenSekai/App/Diagnostics/PerformanceHud.cs) [SonolusModels:546](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Sonolus/SonolusModels.cs#L546)

### M03 曲库

1. **部分成立**。隐藏页面只通知视图，未测曲目继续逐首处理。MeasureAsync 每 4096 帧取样、默认约 3 ms 主动让帧；加载阶段默认 stream=false，PackageAudio 的注释明确记载 3 MB Ogg 在 GetContent 构建 FMOD 音频时主线程约 265 ms，支持首次加载卡顿的风险，但这个样本不能当作每首歌的固定耗时。[PackageAudio:89](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Audio/PackageAudio.cs#L89)[LibraryScreen:86](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryScreen.cs#L86) [LibraryLoudnessScanner:120](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoudnessScanner.cs#L120) [LoudnessMeter:117](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Audio/LoudnessMeter.cs#L117)
2. **部分成立**。SetSearch 每次 Render，SetItemCount 默认复位滚动并重播可见格动画；列表使用虚拟单元格。详情工作量还取决于当前选中项。[LibraryPresenter:110](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryPresenter.cs#L110) [LibraryView:219](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryView.cs#L219) [VirtualListView:79](../../../Assets/Game/Runtime/OpenSekai/App/UI/VirtualListView.cs#L79)
3. **部分成立**。首次生成的 LoadImage 和 EncodeToPNG 在主线程，读文件、缩放和调色分析已有后台步骤；原文引用的“读盘与缩放”与实现相容。[JacketThumbnails:173](../../../Assets/Game/Runtime/OpenSekai/App/UI/JacketThumbnails.cs#L173) [JacketThumbnails:215](../../../Assets/Game/Runtime/OpenSekai/App/UI/JacketThumbnails.cs#L215)
4. **成立**。手选包走同步 ImportZip，下载导入另有异步入口。[LibraryPresenter:427](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryPresenter.cs#L427)
5. **部分成立**。Stop 取消并释放后随即允许新任务开始，旧异步任务可尚未退出；循环检查取消，不能据此认定两个完整扫描长期并跑。[LibraryLoudnessScanner:47](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoudnessScanner.cs#L47)
6. **成立**。修改排序和分组会同步保存偏好，频率取决于用户操作。[LibraryPresenter:89](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryPresenter.cs#L89)
7. **成立（维护性）**。LibraryView 同时管理列表、布局、可见性及转场。行数不能单独证明功能故障。[LibraryView:219](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryView.cs#L219)
8. **条件成立**。Close 回写 Control，若入口原来用强调色，关闭后会失去它，直到重新设色。[LibraryDropdown:120](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryDropdown.cs#L120)
9. **一项误报，一项成立**。keepScrollPosition 在第 82 行参与分支；未找到 LibraryUi.CreateButton 的调用。[VirtualListView:79](../../../Assets/Game/Runtime/OpenSekai/App/UI/VirtualListView.cs#L79) [LibraryUi:277](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryUi.cs#L277)

### M04 首页与服务器

1. **成立**。高级搜索根挂到 screen.ModalLayer，退出浏览页会 Dispose 行和动画，但没有销毁该根。服务器宿主长驻时重复进入可积累树，宿主销毁限定其寿命。[SonolusSearchPanel:40](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusSearchPanel.cs#L40) [SonolusSearchPanel:156](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusSearchPanel.cs#L156) [SonolusBrowsePage:222](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusBrowsePage.cs#L222)
2. **成立**。BuildSection 按返回内容逐格实例化，开销随该批 sections 的实际规模变化。[SonolusBrowsePage:612](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusBrowsePage.cs#L612)
3. **成立（维护性）**。详情、封面、试听、下载、社区、榜单都由同页协调。[SonolusLevelPage](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs)
4. **条件成立**。刷新详情和刷新已经加载的标签页是不同操作；按索引留存榜单选择，服务端重新排序时要核对显示内容。[SonolusLevelPage:78](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs#L78)
5. **部分成立**。ServerScreen、ServerAddPanel、RemoteCovers、SonolusPreviewPlayer 均构造客户端。缓存逻辑存在于部分 UI 类，把它归于三个类各自维护磁盘缓存不准确。[RemoteCovers:130](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/RemoteCovers.cs#L130) [ServerScreen:581](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/ServerScreen.cs#L581)
6. **部分成立**。待处理唯一 URL 数没有上限，等待者逐帧 Yield，实际同时下载受 MAX_PARALLEL 限制。取消单格等待不取消共享下载；Dispose 取消整个下载器。[RemoteCovers:69](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/RemoteCovers.cs#L69) [RemoteCovers:114](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/RemoteCovers.cs#L114)
7. **条件成立**。浏览、列表和详情的部分请求只处理取消与 SonolusException，意外异常可能错过恢复状态；下载等入口另有通用 catch。[SonolusBrowsePage:299](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusBrowsePage.cs#L299) [SonolusLevelPage:389](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs#L389) [SonolusLevelPage:655](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusLevelPage.cs#L655)
8. **部分成立**。绑定检查磁盘，品牌纹理使用 RGBA32；分辨率按显示尺寸选 512 或 1024，像素生成有后台任务，Apply 后释放可读副本。28 MiB 是 7 张 1024² 基础像素之和，不能当作各机型一次实测分配。[RemoteCovers:45](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/RemoteCovers.cs#L45) [BrandSprites:61](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/BrandSprites.cs#L61) [BrandSprites:93](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/BrandSprites.cs#L93)
9. **条件成立**。独立 splash 时间线在尾段继续 ApplyEnter 并允许交互；若此时开始展开，两个写入可冲突。HomeView 自己的 Run 会取消上一次转场。[HomeSplash:403](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeSplash.cs#L403) [HomeView:140](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeView.cs#L140) [HomeView:284](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeView.cs#L284)
10. **风格成立**。状态通知有五处直接 Invoke（原文写六处），取消、释放、清引用模式重复。不同任务的取消时机和所有权仍需分别核对。[SonolusPreviewPlayer](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusPreviewPlayer.cs) [SonolusBrowsePage:783](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/SonolusBrowsePage.cs#L783)

### M05 演奏核心

1. **部分成立**。同时间可判候选有歧义，见优先项 1。
2. **部分成立**。两个暂停入口行为不同，见优先项 2。
3. **部分成立**。完成原因与生命结果分开计算；逻辑层生命归零时置 Result=2 并发 Failed，控制器 OnFailed 只校时，终曲发布时又用其自身的 result。曲库页面确实可把零生命显示为 CLEAR；历史本身能保留 life_zero。历史是否排除这一局属于产品规则。[SoloLiveController:573](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L573)
4. **成立**。派生 OnApplicationPause 隐藏基类并自行处理，未调用 SetSystemPause；焦点通知仍走 base，不能推成所有系统暂停均失效。[SoloLiveController:242](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L242) [BaseController:40](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/BaseController.cs#L40)
5. **部分成立**。当前帧暴露在实例和静态状态上，允许不同步；需要具体写入顺序才能断定某次判定用错时间。[LiveLogic](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveLogic.cs) [MusicScore](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/MusicScore.cs)
6. **成立（接口风险）**。判定回调可被替换或清空，调用方需要完成装配；缺少现行开局漏绑实例。[NoteBase](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/NoteBase.cs)
7. **部分成立**。JudgeLifts 只给 Ended 结算；路由和持有维护识别 Canceled，取消接触与有效抬手语义不同，已有用例显式要求系统取消后等尾键超时 Miss。[取消回归](../../../Assets/Tests/EditMode/Live/TailReleaseLaneTests.cs#L111)ReleaseTail.TryFinish 的该路径未承担正常抬手，实际由 JudgeTailAsRelease 结算。[LiveHoldCoordinator:159](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveHoldCoordinator.cs#L159) [LiveInputRouter:130](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveInputRouter.cs#L130) [ReleaseTail:23](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/Tails/ReleaseTail.cs#L23)
8. **代码成立，缺陷待证**。普通抬手明确按样本音乐时间结算，过窗帧仍能接收窗内输入。架构文档确实写当前帧，现有回归却明确要求晚到 60 ms 的帧仍按抬手样本给 JustPerfect。文档已与实现及用例分歧，应优先校正文档。[判定设计](../../architecture/judging-design.md#L317) [抬手回归](../../../Assets/Tests/EditMode/Live/TailReleaseLaneTests.cs#L86)[LiveHoldCoordinator:192](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveHoldCoordinator.cs#L192)
9. **报告给出的原因不成立**。Consumed 的类型判断只限制近期按键记录，随后对所有已消费候选都记录手指并扣除该轮额度。跨输入波次会重置本轮列表，若要主张重复 Began 多判，应按波次验证，不能归因于条头类型。[JudgmentScheduler:588](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/JudgmentScheduler.cs#L588) [LongNoteHeadNote:12](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNoteHeadNote.cs#L12)
10. **主要后果误报**。静默 Done 确实绕过 setter，装饰单键和条身随后显式 Unspawn。内部 combo 循环另行设置 Done，须先证明该点持有独立视图才能报漏回收。[LiveNoteFrame:445](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L445) [LiveNoteFrame:490](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L490) [LiveNoteFrame:503](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L503)
11. **部分成立**。Resolve 从首事件遍历，后段的查询可以更贵，但有提前返回，原生滚动也有自己的时轴；“唯一越打越慢”无对比采样。[NoteSpeedRatio:28](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Live/NoteSpeedRatio.cs#L28) [NextRushScrollTimeline](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/NextRushScrollTimeline.cs)
12. **部分成立**。技能链有残留代码，当前 ChartDocument 迁移还会滤除 Skill/Fever。应列为模式支持范围及文档问题，不能视作已有功能回归。[SkillLogic](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SkillLogic.cs) [ChartDocument:33](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/ChartDocument.cs#L33)
13. **部分成立**。标准 AddComponent 后 Initialize 的顺序只启动一次；先初始化未激活对象再 Awake 才有重复风险。AdvanceList 按值遍历属实，持久游标由 note session 推进，以便跨帧重访长条。[SoloLiveController:25](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L25) [LiveTimeline:52](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveTimeline.cs#L52)

### M06 演奏表现

1. **成立，有所有权边界**。自定义封面经两次 LoadCustomJacketTexture 创建，背景销毁自己的副本，MusicInfoView 未见对等释放。Resources 或共享封面须另行处理。[MusicInfoView:805](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/MusicInfoView.cs#L805) [Background2DView:689](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/Background2DView.cs#L689) [Background2DView:804](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/Background2DView.cs#L804)
2. **成立**。捕获参数的闭包见优先项 11，P1 所需的帧耗时证据欠缺。
3. **条件成立**。按钮仅在 Ended 释放触点，Canceled 后新 Began 可能被旧 ID 拦住。禁用会重置 ID，“整局”取决于按钮生命周期。[LiveSpriteButton:110](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveSpriteButton.cs#L110) [LiveSpriteButton:160](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveSpriteButton.cs#L160)
4. **能力缺口成立**。特写和技能文字有可调用的空接口；架构文档列其职责，不能单凭目录说明认定它们已交付。[CutinManager](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/CutinManager.cs) [CutinView](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/CutinView.cs) [SkillTextView](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/SkillTextView.cs)
5. **成立**。PlayHaptic 只检查设置，没有触发设备振动。[TapEffectView:507](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/TapEffectView.cs#L507)
6. **成立**。池化视图持有逐 Renderer 实例材质，见优先项 11。[BaseNoteView:159](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/BaseNoteView.cs#L159)
7. **成立**。Android 缓存未命中或文件变化时同步复制；文件匹配时跳过。未见按容量或年龄清理此缓存的逻辑。[LiveMediaFiles:19](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveMediaFiles.cs#L19)
8. **成立，影响待测**。池不够会 Instantiate，池随峰值增长。同一宿主内保留对象是池的用途，是否导致卡顿要看扩容时机。[NotePool:51](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/NotePool.cs#L51)
9. **成立（约定）**。工厂含直接资源路径，有目录入口可以统一管理。[LiveViewFactory](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveViewFactory.cs)
10. **部分成立**。组件有运行时材质、可写属性和直接清空 graphic.material 的逻辑。仅凭没有 C# 调用不能宣布 MonoBehaviour 可删，仍须核 prefab、场景与动态挂载。[OverlayMask:70](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/OverlayMask.cs#L70) [OverlayMask:150](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/OverlayMask.cs#L150)
11. **成立（清理项）**。有残留枚举器，Reset 同时作为已有流程方法及 Unity 约定名。没有运行错误证据。[CountdownView:64](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/CountdownView.cs#L64) [CountdownView:88](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/CountdownView.cs#L88)
12. **成立（命名）**。六个视图文件使用 Sekai.Core.Live，实际依赖边界由 asmdef 决定。[LiveViewBase](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveViewBase.cs) [SoloSkillTextViewGroup](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/SoloSkillTextViewGroup.cs)

### M07 页面与控件

1. **见优先项 7–9**。双读返回、切屏重入和中断关闭各自有不同前提。
2. **部分成立**。启用间隔检查的控件会查 InputManager，None 提前返回。后备 0.2 秒节流在控件间共享，确实可能挡住不同按钮。[CustomSelectableDefine:105](../../../Assets/Game/Runtime/OpenSekai.Game/UI/CustomSelectableDefine.cs#L105)
3. **桩成立，数量须按成员统计**。ScreenManager 可搜到 10 行 throw null，逐个核对为 10 个 public 方法（含重载），原文少计两项；P1 还需可达调用。[ScreenManager:489](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenManager.cs#L489)
4. **条件成立**。关闭动画取消漏回调，重复见优先项 9。
5. **部分成立**。AlphaMask 的更新路径有 Vector3[4] 和材质所有权问题。CustomTextMesh 的 LateUpdate 检查文本与尺寸再决定刷新；把 preferredWidth 算为所有文本每帧固定成本过度。点击查找见第 2 项。[AlphaMask:141](../../../Assets/Game/Runtime/OpenSekai.Game/UI/AlphaMask.cs#L141) [CustomTextMesh:293](../../../Assets/Game/Runtime/OpenSekai.Game/UI/CustomTextMesh.cs#L293)
6. **成立**。全层哨兵用 default，显式传 Layer_BG=0 会走全层分支。[TransitionService](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/TransitionService.cs) [DisplayLayerType:5](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/DisplayLayerType.cs#L5)
7. **误报**。WhenCovered 注册一次性回调，InvokeCovered 在调用前取走并清空，释放时还会 Drop curtain。正常反复 boot 不会积累该订阅。[LibraryLoadingOverlay:295](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoadingOverlay.cs#L295) [LibraryLoadingOverlay:311](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryLoadingOverlay.cs#L311)
8. **条件成立**。按钮显隐依赖子层级顺序，只有层级夹杂其他节点时产生错位；长按启动定位使用的根与随后启动的长按对象有别，视觉后果取决于 prefab 结构。[UIPartsDialogButtonGroup](../../../Assets/Game/Runtime/OpenSekai.Game/UI/UIPartsDialogButtonGroup.cs) [TouchEffect:59](../../../Assets/Game/Runtime/OpenSekai.Game/UI/TouchEffect.cs#L59)
9. **条件成立**。Awake 仅在 refreshColorWhenAwake 为真时读调色板，随后无条件 ResetInteraction。关闭该选项且外部未赋默认色时会写透明色。[GraphicButtonTapEffect:55](../../../Assets/Game/Runtime/OpenSekai.Game/UI/GraphicButtonTapEffect.cs#L55)
10. **成立**。LoadAsync 同步 Load，返回的元素固定 null，没有检查取消令牌。[UIAtlasImageLoader:75](../../../Assets/Game/Runtime/OpenSekai.Game/UI/UIAtlasImageLoader.cs#L75)
11. **部分成立**。能定位到 ScreenBlurController 和 ScreenCameraStack 整体写 cullingMask。是否需要恢复取决于相机所有权，共用相机才有互相覆盖的问题。[ScreenBlurController:384](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenBlurController.cs#L384) [ScreenCameraStack:57](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenCameraStack.cs#L57)

### M08 平台与资源

1. **见优先项 4、5**。依赖计数与主线程等待成立，设备影响未测。
2. **成立，触发条件比原文窄**。新版 HCA、非次立体声通道且 extraCount>0 时，合计 csCount=128 可通过检查。第一次迭代会读长度 128 数组的下标 128；同为 128 频带但 extraCount=0 不进循环。[HcaDecoder:483](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/HcaDecoder.cs#L483) [HcaDecoder:532](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/HcaDecoder.cs#L532) [HcaDecoder:1053](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/HcaDecoder.cs#L1053)
3. **部分成立**。直接 IMDCT 和整段交错 PCM 分配可造成较高成本。按 48 kHz、180 秒、双声道、float32 算，输出约 69 MB；实际运算、阻塞和是否走该解码器均依音源而定。[HcaDecoder](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/HcaDecoder.cs) [CriAtomShim:124](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L124)
4. **部分成立**。守卫 LateUpdate 及播放器辅助路径会找监听器并产生结果数组。每次播放精确调用次数随分支变化。[CriAtomShim:1452](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L1452) [CriAtomShim:1531](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L1531)
5. **成立，严重度依可达性**。资源和平台目录合计 192 行 throw null，CP 子目录为 53 行。未实施的兼容 API 不能一概列为当前 P1。[InputManager](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/InputManager.cs) [UnityWebRequestClient](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CP/UnityWebRequestClient.cs)
6. **部分成立**。StartCategoryDownload 无传输就回进度 1 和成功。单包 DownloadAssetBundle(Async) 却区分 CheckNeedsDownload 的 Error/Success，本地资源加载也会检查结果。[AssetBundleManager:404](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleManager.cs#L404) [AssetManager:352](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L352)
7. **成立**。AssetManager.OnDestroy 只 ForceDisposeAll，隐藏父类清理。其影响是单例生命周期与基类契约脱节。[AssetManager:312](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs#L312) [SingletonMonoBehaviour:108](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CP/SingletonMonoBehaviour.cs#L108)
8. **成立**。行字段读取检查起始字节，多字节字段仍可能超出剩余长度。具体抛出的标准异常取决于读函数，截断输入未统一成 AcbException。[CriUtfTable](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/AcbDecoder/CriUtfTable.cs)
9. **成立，三类影响不同**。清单属性名可能是持久化契约，LogUtility 静态委托可整体替换，GetFullAssetName 的整表读取值得按加载频率衡量。[LogUtility](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CP/LogUtility.cs) [AssetBundleLoader](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleLoader.cs)
10. **成立于该 API**。LoadAcbFile 忽略 awbPath，分离式资源不能靠该参数提供波形；现有内嵌和外部 WAV 路径另行工作。[CriAtomShim:124](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L124)

### M09 数据与共享逻辑

1. **成立**。设置直写见优先项 6。
2. **算法成立，玩家路径待证**。Pass=2 会压过 Bad/Good/Great/Perfect，但压不过 Miss。若先收到 Pass 再收到 Perfect，后者可被抑制。许多 Pass 产生者 HasJudgment=false 或静默退场；还需给出实际送达 JudgmentView 的样本才能定 P1。[NoteResult](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/NoteResult.cs) [JudgmentAggregator:31](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Live/JudgmentAggregator.cs#L31) [JudgmentView:91](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/JudgmentView.cs#L91) [LiveFrontView:323](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveFrontView.cs#L323)
3. **播放器故障误报**。缺文件名确实返回 RootDirectory；Background2DView 先检查 File.Exists，目录在这里会提前返回，不会赋给 VideoPlayer.url。[SongPackage:56](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Packages/SongPackage.cs#L56) [Background2DView:305](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/Background2DView.cs#L305)
4. **部分成立**。非空文件名触发路径和属性检查，缺文件会走异常路径；空名直接返回目录。虚拟列表绑定可重复查询，优化时还要保留重解析点防护。[SongPackage:56](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Packages/SongPackage.cs#L56) [SongPackageStorage:140](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Packages/SongPackageStorage.cs#L140)
5. **部分成立**。LiveContext 的组装入口读取旧单例，随后把依赖交给本局。modules.md 限定的是 App 内的读点；这里属于 Game，未见所称的程序集逆向依赖。[LiveContext:53](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveContext.cs#L53)
6. **部分成立**。启用追踪会逐行记日志并刷新，采集启用后可保留 262144 个样本的备用缓冲。31 MB 是依布局估算，普通未启用诊断的打歌不承担全部成本。[LiveTrace](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Diagnostics/LiveTrace.cs) [LatencyCapture:76](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Diagnostics/LatencyCapture.cs#L76)
7. **成立（兼容层）**。SkillData 等有 throw null，LiveEventData 构造与传入的技能、特写数据不对称。与技能范围一并判断优先级。[SkillData](../../../Assets/Game/Runtime/OpenSekai.Game/Data/SkillData.cs) [CutinData](../../../Assets/Game/Runtime/OpenSekai.Game/Data/CutinData.cs) [LiveEventData](../../../Assets/Game/Runtime/OpenSekai.Game/Data/LiveEventData.cs)
8. **部分成立**。Data 目录中确有 UI MonoBehaviour 和回调绑定。回调能否在实际卸载后访问失效对象还需核调用者、销毁时机。[MaterialExchangeHeaderCeilItem](../../../Assets/Game/Runtime/OpenSekai.Game/Data/MaterialExchangeHeaderCeilItem.cs)
9. **成立（风格）**。枚举大小写和 MasterMusic 的公有字段可直接观察；调整序列化名称前需要考虑现有资产。[MasterMusic](../../../Assets/Game/Runtime/OpenSekai.Game/Data/MasterMusic.cs)
10. **成立（风格）**。公共参数使用下划线前缀。[SusRuntimeConverter](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Conversion/SusRuntimeConverter.cs) [LiveSettingsStore:19](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Settings/LiveSettingsStore.cs#L19)

### M10 音频、动画与渲染

1. **成立于音量入口**。设置页传入 voice，SetupVolume 只向音量模型传 master、bgm、se。是否有玩家可听见的独立语音内容需另核。[SoundManager:47](../../../Assets/Game/Runtime/OpenSekai.Game/Audio/SoundManager.cs#L47) [LibrarySettingsPanel:363](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibrarySettingsPanel.cs#L363)
2. **部分成立**。见优先项 10。500 属于 areaCameraSettings，当前 UI 相机路径的 DrawBlurUI=300、Blur=500、DrawUI=600。直接代入 500 会把复制移到 DrawBlurUI 后，且与 Blur 同序，不能机械套用修法。[RendererSettingsTests:22](../../../Assets/Tests/EditMode/Graphics/RendererSettingsTests.cs#L22) [SekaiLegacyDrawUIRendererFeature:147](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L147)
3. **部分成立**。三个派生 Feature 入队前跳过基类的逐相机 Setup；基类传的是 camera.cullingMask 与默认 stencil。报告声称“所有自定义 stencil 配置失效”缺少对应配置，额外绘制还受相机已有剔除结果限制。[SekaiDrawObjectsRendererFeature:117](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawObjectsRendererFeature.cs#L117) [SekaiMusicItemRendererFeature](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiMusicItemRendererFeature.cs) [SekaiOpaqueReflectionRendererFeature](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiOpaqueReflectionRendererFeature.cs) [SekaiTransparentReflectionRendererFeature](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiTransparentReflectionRendererFeature.cs)
4. **成立**。多个控制器写同一静态 Blur，重叠实例可能互相覆盖；具体是否错误随启动与关闭顺序而定。[SekaiUIEffectSettings](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiUIEffectSettings.cs) [UIBlurController](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/UIBlurController.cs) [UIBlurLayerManager](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/UIBlurLayerManager.cs)
5. **当前仓内成立**。注册表本身有实现，未找到实际注册的音乐物件；空表时 Feature 不入队。[SekaiMusicItemSettings](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiMusicItemSettings.cs) [SekaiMusicItemRendererFeature](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiMusicItemRendererFeature.cs)
6. **字段不匹配成立**。MusicItem 两项、OpaqueReflection 两项、TransparentReflection 两项资产使用 m_Settings，基类仅声明 settings 和旧名 _settings；默认空 ShaderTagTypes 会使 CreatePasses 返回 null。六项的实际反序列化结果及启用场景尚未在编辑器核验。[SekaiDrawObjectsRendererFeature:57](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawObjectsRendererFeature.cs#L57) [资产示例](../../../Assets/Graphics/Rendering/MusicItem_3.asset#L16)
7. **部分成立**。采样偏移用相机尺寸，capture 可缩放。0.5 尺度对应半个源纹素或一个屏幕像素，是否偏离设计取决于采样距离的约定单位。[SekaiUIBlurPass:59](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiUIBlurPass.cs#L59) [SekaiUIBuffer:25](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiUIBuffer.cs#L25)
8. **部分成立**。建 renderer list 时确有两组托管数组及 Allocator.Temp 数组；Temp 不能计为跨帧原生泄漏。UI pass 有条件清深度与模板，需核同附件后续读者才能认定画面损坏。[SekaiDrawObjectsPass:94](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawObjectsPass.cs#L94) [SekaiDrawUIPass:75](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawUIPass.cs#L75) [SekaiDrawUIPass:98](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiDrawUIPass.cs#L98)
9. **成立**。首次调用先设置 requested，失败后本对象没有重试；加载尚未完成时再次调用也会立刻通知 onDone。[ExternalSoundPack:22](../../../Assets/Game/Runtime/OpenSekai.Game/Audio/ExternalSoundPack.cs#L22)
10. **成立**。两个启动入口调用空的全局 SE 设置；ShaderPreloader 仅存状态，无真正预载工作。[SoundUtility](../../../Assets/Game/Runtime/OpenSekai.Game/Audio/SoundUtility.cs) [ShaderPreloader](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/ShaderPreloader.cs)
11. **部分成立**。有反编译状态机和空闲字段；SyncGameTime 把回程时间折进首半周期，导致同步瞬间相位不对。实际创建 motion 仍选 Yoyo，不能说整个播放退化成 Loop。[TweenBase:462](../../../Assets/Game/Runtime/OpenSekai.Game/Animation/TweenBase.cs#L462) [TweenBase:473](../../../Assets/Game/Runtime/OpenSekai.Game/Animation/TweenBase.cs#L473)
12. **条件成立**。轮转池没有保护仍在播的对象，新 Spawn 在同一槽尚未播完时会打断效果。[EffectPool](../../../Assets/Game/Runtime/OpenSekai.Game/Animation/EffectPool.cs)

### M11 测试

1. **六项分别判断**。① AutoplayCompletion 的该循环只 OnUpdate 和 OnAutoInput，且没有退场记录时跳过，尾部视图路径覆盖不足。② JudgeFrameTable 的窗口宽度主要由硬编码帧数换算，不能据此断言全套其他判定测试也拦不住改动。③ ResultPublication 手动 Reset 的用例不能替代生产重试流程的集成测试。④ ScrollingFlick 的 Assume 失败确会转 Inconclusive；四组边界参数及其他调用受影响。⑤ “读上次缺脚本报告”误报，Run 同步 WriteAllLines 后测试才读，抛错会令用例失败。⑥ LiveHarness 构造期间改全局状态并加设备，构造中途抛错时普通 Dispose 无法保障回滚。[AutoplayCompletionTests:129](../../../Assets/Tests/EditMode/Live/AutoplayCompletionTests.cs#L129) [JudgeFrameTableTests:29](../../../Assets/Tests/EditMode/Live/JudgeFrameTableTests.cs#L29) [ResultPublicationTests:421](../../../Assets/Tests/EditMode/Live/ResultPublicationTests.cs#L421) [PressCandidateRuleTests:35](../../../Assets/Tests/EditMode/Live/PressCandidateRuleTests.cs#L35) [MissingScriptAudit:69](../../../Assets/Game/Editor/MissingScriptAudit.cs#L69) [MissingScriptAuditTests:20](../../../Assets/Tests/EditMode/Structure/MissingScriptAuditTests.cs#L20) [LiveHarness:33](../../../Assets/Tests/EditMode/Live/LiveHarness.cs#L33)
2. **依赖成立**。多处直接用相对 Tools/fixtures，包括 File.ReadAllText 与 GetFullPath；另有以 Application.dataPath 为锚的测试。仓内提交的 fixture 是测试输入，风险是依赖特定 CWD 的那一组。[PlayServiceDependencyTests:22](../../../Assets/Tests/EditMode/App/PlayServiceDependencyTests.cs#L22) [AutoplayCompletionTests:26](../../../Assets/Tests/EditMode/Live/AutoplayCompletionTests.cs#L26)
3. **部分成立**。确有测试向真实持久日志目录写数据，但清理检查唯一 chart 标记；LatencyCaptureTests 还排除预先存在的文件。“仅按文件名前缀删除玩家日志”误报。[LatencyCaptureTests:54](../../../Assets/Tests/EditMode/Live/LatencyCaptureTests.cs#L54) [StallReplayTests:256](../../../Assets/Tests/EditMode/Live/StallReplayTests.cs#L256)
4. **成立**。测试对受跟踪字体执行 TryAddCharacters、SetDirty、SaveAssets，finally 丢弃内存改动。若被测保存保护失效，哈希断言会失败，却不会恢复已写的文件。[GeneratedAssetSaveGuardTests:65](../../../Assets/Tests/EditMode/Resources/GeneratedAssetSaveGuardTests.cs#L65)
5. **部分成立**。反射读私有字段、将特效包恢复为 0/1、发现组件即断言 IsReady 均属实。全文件未用 NUnit Timeout，但多处有 15 秒 deadline，需区分各等待点。[LibrarySmokeTests:148](../../../Assets/Tests/PlayMode/LibrarySmokeTests.cs#L148) [LibrarySmokeTests:232](../../../Assets/Tests/PlayMode/LibrarySmokeTests.cs#L232)
6. **成立（维护性）**。同一大型 fixture 跨加载、判定和显示行为，影响定位及维护，不能由行数推断断言失效。[NextRushChartLoaderTests](../../../Assets/Tests/EditMode/Charts/NextRushChartLoaderTests.cs)
7. **待证**。确有多个局部测试装配和共享 LiveHarness；报告未列“四份”的文件、对应字段及影响的断言，尚无法确认这四份应当等价。[LiveHarness](../../../Assets/Tests/EditMode/Live/LiveHarness.cs)
8. **部分误报**。InputSystemTouchSourceTests 直接读取 NativeInput.ActiveTouches；RendererSettingsTests 覆盖序列化 pass 顺序，未验证逐相机实际入队顺序。未定位到 AB 超时、下载、重试的对应端到端测试。[InputSystemTouchSourceTests:28](../../../Assets/Tests/EditMode/Live/InputSystemTouchSourceTests.cs#L28) [RendererSettingsTests:22](../../../Assets/Tests/EditMode/Graphics/RendererSettingsTests.cs#L22)

### M12 编辑器工具

1. **四项见优先项 12**。外层脚本会在 900 秒默认超时后抛出；自行启动编辑器的分支没有在该处终止子进程。附着到用户已有编辑器时要保留其所有权。[截图工具](../../../Tools/capture-screens.ps1#L211)
2. **部分成立**。已 busy、编译错误和运行在错误场景均在写状态前检查；未保存场景先 Finish 再抛，拒进 Play Mode 也有回收。Apply 分辨率、打开场景等非预期异常在写状态后仍有窗口，不能说普通前置条件失败都永久 busy。[ScreenshotSession:70](../../../Assets/Game/Editor/OpenSekai/Screenshots/ScreenshotSession.cs#L70) [ScreenshotSession:105](../../../Assets/Game/Editor/OpenSekai/Screenshots/ScreenshotSession.cs#L105)
3. **部分成立**。桥接器异步 Execute，由 RunFinished 保存文件，适用于常驻编辑器 EditMode。把它作为批处理退出门禁缺退出协议；项目文档已列 PlayMode 域重载丢回调及相应测试入口。[TestRunnerBridge:68](../../../Assets/Game/Editor/TestRunnerBridge.cs#L68) [TestRunnerBridge:88](../../../Assets/Game/Editor/TestRunnerBridge.cs#L88) [测试说明](../../operations/testing.md)
4. **成立**。长度 8–11 的行通过检查后访问 f[11]，即使不考虑图片导入也会越界。Paste 中途异常可能留下已写文件及修改过的 importer。[PaintKit:145](../../../Assets/Game/Editor/PaintKit.cs#L145) [PaintKit:212](../../../Assets/Game/Editor/PaintKit.cs#L212)
5. **部分成立**。部分路径设置可读性并重导入，同时又从文件字节加载临时 Texture2D。是否往返两次取决于初始可读性及分支；1200 次是特定假设的估算。[PaintKit:219](../../../Assets/Game/Editor/PaintKit.cs#L219) [PaintKit:394](../../../Assets/Game/Editor/PaintKit.cs#L394)
6. **部分成立**。相对资产路径用于 File API，非项目 CWD 有误跳过风险。MissingScriptAudit 生成 TSV，另有测试断言无条目；“永远通过”与该调用链不符。[GeneratedAssetSaveGuard](../../../Assets/Game/Editor/GeneratedAssetSaveGuard.cs) [MissingScriptAudit:32](../../../Assets/Game/Editor/MissingScriptAudit.cs#L32) [MissingScriptAuditTests:20](../../../Assets/Tests/EditMode/Structure/MissingScriptAuditTests.cs#L20)
7. **成立，有范围**。FontAssetRebuilder 对扫描到且匹配旧引用的文本直接 WriteAllText，随后 SaveAssets。批量操作没有事务回滚；是否真被内存旧值覆盖需复现。[FontAssetRebuilder:102](../../../Assets/Game/Editor/FontAssetRebuilder.cs#L102)
8. **部分成立**。迁移分多个步骤写 meta、删除、刷新和配置 importer，中断可能留下部分完成状态。能否重试取决于具体中断点，不能笼统称无法重跑。[AtlasSpriteMigration:120](../../../Assets/Game/Editor/AtlasSpriteMigration.cs#L120)
9. **成立**。先同步 ReadToEnd 再 WaitForExit(5000)，前者已经可能无限等待，返回值也没有驱动终止。[BuildStampBuildStep:53](../../../Assets/Game/Editor/BuildStampBuildStep.cs#L53)
10. **成立**。字段审计用反射收集当前序列化字段名，不能识别 FormerlySerializedAs 的合法旧键，可能产生误报。[PrefabFieldAudit:219](../../../Assets/Game/Editor/PrefabFieldAudit.cs#L219)
11. **成立（风格）**。编辑器工具存在两套 namespace；名称影响 executeMethod 等外部入口，统一需同时核调用者。[TestRunnerBridge](../../../Assets/Game/Editor/TestRunnerBridge.cs) [CliBuild](../../../Assets/Game/Editor/CliBuild.cs)

## 横向模式与修复批次

| 原模式 | 复核结论 |
|---|---|
| A 空实现 | LRU、CP、技能数据、切镜视图、振动、全局 SE、shader 预载、音乐物件注册调用、页面管理桩均有事实依据。SoundManager 的 priority、恢复时间和 SE 恢复接口须按实际后端能力描述。不可用 API、有上层降级的兼容入口和未实现玩法的优先级有别。 |
| B 失败收尾 | 媒体、AB 和图集迁移的写入顺序属实。缺 keystore 和权限后处理有 warning，构建戳超时另见 M12。网络异常处理见 M04。备份及突发中断可留下临时物，常规导入有 finally，事务性 Sonolus 发布还会恢复。MissingScriptAudit 的门禁在 EditMode 测试。 |
| C 主线程 I/O | 同步请求等待、视频复制、开局解析、缩略图编码和工具重导入应分别处理。响度按帧预算让出；日志依开关；编辑器迁移不会造成 Android 玩家 ANR。 |
| D 热路径 | 见优先项 11。速度表扫描、监听器查找、HomeCards 更新条带时的 GetChild 均有实际成本，材质创建是对象初始化成本。18 次等次数须绑定具体动画或控件规模。 |
| E 事件 | 广播的 Raise 命名与若干可写委托偏离项目规范。ServerRegistry.Changed 已经集中经 RaiseChanged 触发，ServerScreen.PlayRequested 只传一个 PlayRequest；原文两例与源码相反。ScreenManager 的实际属性名是 OnChangeScreenSize。Action 的 SerializeField 不产生委托持久化；单一所有者的回调槽与多订阅者事件要分别判断。 |
| F 生命周期 | 两个 new 隐藏虚方法属实。Dialog 关闭是具体缺陷；ColorFader.OnDisable → Stop 会取消动画并设 State.None，不能用它证明 IsPlayFading 卡住。CustomButton 已有禁用及取消路径，需具体残留场景。全局持久设置跨场景常驻本身不足以证明污染。封面、副本材质和遮罩的所有权见 M06、M07。 |
| G 测试 | 见 M11 第 1、8 项。窗口常量、真实重试和实际绘制顺序各有不同缺口；缺脚本陈旧报告及输入适配零覆盖应撤回。 |

**批次 1** 可优先处理 flick、设置与媒体写入、同时间尾点。生命结果需分别定义曲库文案和历史收录；Sonolus 需保持崩溃恢复时的删除保护。**批次 2** 可先处理 AB 等待和依赖所有权、HCA 下标，再用真机采样排其余性能项。**批次 3** 保留返回键、切屏取消、搜索面板释放，热路径先量分配频率。**批次 4** 优先保证 AB 构建失败保留上一份有效输出，并区分截图工具自启和附着的编辑器。**批次 5** 清桩前查场景、prefab、反射及外部工具引用，删除或补实现都可能改变行为。**批次 6** 可整理命名与事件，持久化字段和公共入口需要兼容性判断。

## 报告本身的核算

原模块表逐行相加为 **354 条、45 条 P1、709 个文件条目**。M02 表内写 2、正文却有 3 条标 P1。M07 写“全项目最低 2.3”，同表 M08 为 2.0。当前 Assets/Game 与 Assets/Tests 合计 711 个 C# 文件，其中 Tests 为 120；这项计数不能证明每个文件都经过审阅。独立审查人数、14 处交叉印证、各级百分比及未展示的去重条目没有逐项原稿可核。优先级应依据可达路径、触发条件和玩家影响重新确定。
