# 第三轮回函复核与结案意见

2026-11-20，源码基准 `567116c3`。对照[本轮回函](code-review-2026-11-20-round3.md)、[上一轮复核](code-review-2026-11-20-round2-verification.md)及[原始裁决明细](code-review-2026-11-20-rebuttal.md)。以下结论来自源码、当前安装的 URP 包、Git 跟踪状态、本地清单与 PNG 尺寸。未运行 Unity、设备测试或美术导入。

## 75 行统计可以结案

从原明细重新取数，再应用 M11-7 整行撤回、M11-8 整行降至 P3、M07-4 降至 P3，得到 **维持 16、修正 55、撤回 4**。按原始级别与最终级别比较并排除撤回项，**41 行降级，其中原 P1 为 19 行、原 P2 为 22 行**。M07-4 原判 P1，上轮已降 P2，本轮再降 P3，仍只计一行。这个统计仅覆盖原 75 行，不能用来证明表外待办的级别没有变化。[原明细](code-review-2026-11-20-rebuttal.md#L167)

原报告顶部勘误仍写“3 条撤回”，指向旧答辩和 round2。回函已给出最终口径，读者从原报告进入时仍会看到旧数。[原报告勘误](code-review-2026-11-20.md#L11)

## 逐模块处置

| 模块 | 复核意见 |
|---|---|
| M01 | 接受六行加一条表外 flick 的计数。音符和事件保留阈值的方向不同，先前复核已明确这一事实。改哪侧、是否需要改才是尚缺的用途约定。flick 在 100 dpi 下应约为 3.28 px，当前约为 1.29 px，继续按 P1 跟踪。[判定换算](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/JudgeTuning.cs#L74) |
| M02 | 接受坏索引抛错与已有内存缓存的区分，也接受把设置、配置及同步文件哈希计入开局耗时。坏索引可阻止发布，缺乏正常路径“读坏后以一条记录覆盖全表”的证据。单项降级不可扩展到设置和媒体数据丢失，见下表。 |
| M03 | 265 ms 保留为注释中的单一样本即可，休止争论不需要确定 Unity 私有函数的实现。详情内存容量为 4，同键在途任务会合并。只有内存未命中才需要再看磁盘，清空磁盘缓存本身不会令已在内存的详情重跑。磁盘缩略图成功解码可省去原图缩放；“多数”需要命中率证据。沿用第 3 项结论。[内存命中](../../../Assets/Game/Runtime/OpenSekai/App/UI/LibraryView.cs#L1042) · [磁盘读取](../../../Assets/Game/Runtime/OpenSekai/App/UI/JacketThumbnails.cs#L170) |
| M04 | 七项处置可接受。约 5.02 Mpx 是分辨率阈值；首页切库由点击后 0.46 秒展开动画的完成回调驱动，实际显示受帧调度影响。4.4 秒不能直接作为玩家看到的切库时刻。[导航回调](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeScreen.cs#L295) |
| M05 | 接受六项。普通条尾与同条合格隐藏点等时的静态链明确。有针对性的最小谱可验证加载到判定的行为；证明真实发行谱受影响还需要实际谱例。零生命结果标签、普通尾样本时间和同指按下阈值的处置可结案。 |
| M06 | 暂停按钮在 Canceled 后残留触点，补进首批合理。“整曲不可暂停”应限于屏幕按钮可能持续失灵。控制器逐帧读返回键，Playing 状态会调用 OnPause，进入后台或设备失焦也有暂停入口。这些旁路不消除触控缺陷。[返回键](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/BaseLiveController.cs#L467) · [失焦与后台](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L242) |
| M07 | 曲库开局走独立暂停面板，旧弹窗的关闭中断降至 P3 有依据。30/33 是相关资产实例中启用间隔的数量。返回键双消费与页面栈重入是另外两项，回函已明确写 P1，先前“没有单项级别”的问题到此有答案。但上轮答辩明说这两项已下调，这次恢复原级没有新增玩家路径或复现，两项也没进待办表。前者需同帧有可关闭弹窗和可退页面，后者需切换未完成时有第二个栈操作。应标成报告方保留 P1、实际触发待证。[旧排期](code-review-2026-11-20-rebuttal.md#L366) · [栈入口](../../../Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenStackService.cs#L91) |
| M08 | 接受矩阵部分约 22.12 亿 MAC、成功 Play 扫描 3 次、守卫有外部监听器时 1 次和无外部时 3 次。空输入和全空条目的提前返回也与实现一致。round2 的表格误贴现已修正。音频输入要求只适用于对应解码实验，见下表。[播放分支](../../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L747) |
| M09 | 接受 Pass 的多重屏蔽和 VideoPath 的补充。库缓存取时间戳时会跳过不存在的文件或目录值，未发现这条路径导致误认视频。[缓存时间戳](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongLibraryCache.cs#L239) |
| M10 | 0 和 1 落在同一事件区间，但回函给的“没有其它事件 0”不足以保证相同排序位置。举例：另一个 Feature 先入队事件 1，当前 copy 也为 1 时稳定排序会保留先后；若把 copy 改成 0，它就移到前面。当前 Feature 自己也入队了事件 0 的 setup。相对谁发生移动要同时核对事件 0、1 的并列项及入队顺序；实际图像影响仍待截帧。[入队顺序](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L102) · [事件赋值](../../../Assets/Game/Runtime/OpenSekai.Game/Rendering/SekaiLegacyDrawUIRendererFeature.cs#L147) |
| M11 | 接受两项。对象引用设置会消费 `$id`，公开无参构造和字段初始化解释了相关缺省值。撤销“四份手写夹具”有依据。第 8 项 P3 已明确指整行剩余测试缺口，生产线程阻塞另归 M08。[序列化器](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/Serialization/ChartJson.cs#L13) |
| M12 | 截图 busy 保留 P1。PaintKit 半批写入归第 4 项、isReadable 空转归第 5 项，接受。重新核验所有 157 幅画稿的尺寸，均过相应分支门槛。前 27 项是整图，21 个不同目标存在，6 个缺失；第 28 项是 35×42 的裁片，目标缺失，按正常执行到此会抛错，后面还有 129 项。默认清单位于被 Git 忽略的 LocalPacks，受跟踪的 ArtSource 清单路径和文件哈希均不同，工具也允许指定清单。因此这是当前工作区默认输入的静态结论，不能声称该提交在干净检出时必现，或 21 张已由本次复核实际写回。第 8 项补充成立：旧源都删完后重跑会因 entries 为空返回，所以即使引用及 importer 已处理，尚未完成的其它图集解绑与目标图集配置仍可能漏掉。若只删了部分源，剩余项仍能进入后续步骤，受影响范围应按中断点说明。[清单选择](../../../Assets/Game/Editor/PaintKit.cs#L25) · [裁片调用](../../../Assets/Game/Editor/PaintKit.cs#L203) · [迁移早退](../../../Assets/Game/Editor/AtlasSpriteMigration.cs#L100) · [图集收尾](../../../Assets/Game/Editor/AtlasSpriteMigration.cs#L141) |

## 用于排期前需要改的三处

| 回函待办 | 与已核证结论对齐的写法 |
|---|---|
| 第 4 行把设置、媒体和索引统一记为 M02、P2 | 拆成 M09 设置原地写入 P1、M02 替换媒体先删旧件 P1、M02 坏索引阻断发布 P2。前两项上一份答辩明确保留 P1，本轮没有提出降级依据。三者的失败后果和修复验收各不相同。[原级别](code-review-2026-11-20.md#L91) · [上轮裁决](code-review-2026-11-20-rebuttal.md#L365) |
| 第 5 行把仓外 ACB 列为整组前提 | 普通 AssetBundle 的依赖释放可用父包和依赖包验证，URI 同步等待可用相应平台的普通资源验证。这两项无需音频曲包。HCA 越界及实际解码耗时另列相应编码样本和设备条件。[依赖与 URI 等待](../../../Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleManager.cs#L291) |
| 合并待办漏掉已有处置 | 回函保留 P1 的两项 M07 应各有处置。上轮仍保留的“AB 构建失败须保住上一份有效输出”也未列出；如延期，注明延期即可。它与运行时释放 AB 依赖、以及 PaintKit 半批写入各有独立的验收条件。[上轮顺序](code-review-2026-11-20-round2.md#L145) · [构建输出清理](../../../Assets/Game/Editor/OpenSekaiAssetBundleBuildPipeline.cs#L306) |
