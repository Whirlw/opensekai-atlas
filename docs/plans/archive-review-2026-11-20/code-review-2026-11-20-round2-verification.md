# 第三轮返回意见复核

2026-11-20，源码基准 `567116c3`。对照[本轮回函](code-review-2026-11-20-round2.md)、[上轮复核](code-review-2026-11-20-rebuttal-verification.md)和[75 行裁决明细](code-review-2026-11-20-rebuttal.md)。本轮核对源码、序列化资产、清单及 PNG 尺寸，未运行 Unity、Android 或会写回美术资产的编辑器工具。

## 逐模块裁决

| 模块 | 复核结论 |
|---|---|
| M01 | 第 2 项关于速度计算读者的更正可采纳。第 7 项仍须分开记录两个问题。第五参未从生产入口传入，输出字段也无生产读者。但传入阈值后，转换器本身会筛选音符和事件，前者保留链尾到达阈值的链，后者保留阈值前的事件。是否应改其中一个比较符取决于截取的预期用途。两份文档都没有给出能判定方向的需求或用例。flick 的 2.54 倍换算错误成立。另，上一轮 M01 明细是六行，flick 是表外补记。[转换器](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L135) · [事件筛选](../../../Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs#L242) · [flick](../../../Assets/Game/Runtime/OpenSekai.Core/Scoring/JudgeTuning.cs#L74) |
| M02 | 第 2 项可撤掉“读坏后用一条记录覆盖全表”。现存坏文件在发布前严格重读时抛出。细节上，文件缺失不会自动清除已有内存索引，严格读取抛出也会保留赋值前的缓存，不能一概称其为 null。第 3 项保留同步耗时风险，“只来自解析转换”仍过窄。本地谱在首个 await 前还可能同步读包信息、加载配置，并打开谱面计算 SHA-256。没有设备耗时可用于判定 ANR。[索引读取](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Sonolus/SonolusLevelInstaller.cs#L380) · [启动链](../../../Assets/Game/Runtime/OpenSekai/App/Playback/PlayService.cs#L128) · [文件哈希](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageHash.cs#L25) |
| M03 | 第 1 项撤掉固定 265 ms 正确，但本地走 Unity 音频请求及 GetContent，项目上层使用 CriAtom 无法证明 Unity 内部采样名称已过时。该数值只能作为注释记录的单一样本。第 2 项按同键在途任务去重成立，缓存淘汰、重试或换键后仍可能重新解码。第 3 项有首个 await 前的同步返回，可以采纳对“分离屏障”的纠正。[音频入口](../../../Assets/Game/Runtime/OpenSekai.Game/Shared/Audio/PackageAudio.cs#L111) · [详情去重](../../../Assets/Game/Runtime/OpenSekai/App/UI/JacketThumbnails.cs#L82) |
| M04 | 第 4、5、7、10 项沿用上轮。第 6 项每个排队中的不同下载按帧检查并发名额，补充成立。第 8 项面积阈值约 5.02 Mpx 成立，设备例子不能外推装机比例。第 9 项两条动画会争写，画面须实测。展开时长 0.46 秒，切库绑定到展开完成回调，取决于点击时刻；不能固定描述成“4.4 秒后直接跳进库”。[下载队列](../../../Assets/Game/Runtime/OpenSekai/App/UI/Server/RemoteCovers.cs#L122) · [尺寸阈值](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/BrandSprites.cs#L90) · [切库回调](../../../Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeScreen.cs#L295) |
| M05 | 第 1 项接受对方给出的绑定次序。对普通可抬手、非装饰的条，若合格隐藏点走同条 AddHoldCombo、与真尾等时并排在其后，最后一个等时候选会替代真尾，和绑定的 Child 不相等，抬手窗口会被拒绝。独立 tick 或假条走别的分支。静态链已明确，实际谱触发和最终玩家判定未执行验证。第 9 项对方进一步区分同指固定阈值与跨指可变阈值正确。第 3、7、8、11 项接受，零生命显示 CLEAR 可单独修，普通尾按样本时间结算，前方音符仍可能遍历事件表。[附点分支](../../../Assets/Game/Runtime/OpenSekai/Infrastructure/Charts/NextRushChartLoader.cs#L897) · [尾窗](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNoteTail.cs#L84) · [同指波次](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/JudgmentScheduler.cs#L267) |
| M06 | 第 1、10 项接受。第 3 项保留 P1，双方对当前缺陷一致。原验收要求指修复后取消旧触点，另一根手指应能独立按下并抬起，旧 ID 单独抬起不应触发。当前同 ID 抬手只有落在按钮内才会误触，按钮内外均会释放 ID。回函末尾列了首批 flick 和零生命，却没有给仍判 P1 的暂停键安排修复位置。[按钮](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveSpriteButton.cs#L112) |
| M07 | 第 2 项独立复数为 23/26 个 CustomButton、7/7 个 CustomToggle 启用间隔，30/33 成立，这是资产实例比例。InputManager GUID 在 prefab、scene 中零命中。第 4 项的旧弹窗确有 Close 后销毁宿主的路径，但“暂停→继续必走”不成立。曲库启动时直接 ShowLibraryPauseOverlay 并返回，恢复时调用 Destroy 也要有活动的旧弹窗才会 Close。第 7 项的一次性订阅撤回维持。返回键双消费、页面栈重入仍缺单项新定级。横向 F 两个动画赋值方法在 Assets 未检出调用。[暂停分流](../../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L284) · [销毁条件](../../../Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveOutUIController.cs#L31) |
| M08 | 第 3 项矩阵计算量约 22.12 亿 MAC，第 4 项成功 Play 扫描 3 次，守卫有外部监听器时 1 次、无外部时 3 次，接受。回函第二节把第 4 项结论误贴为 MAC 数字，第三节写对了。扫描次数与实际帧耗时应分别记录。 |
| M09 | 第 2 项多处屏蔽 Pass、第 3 项缓存也读 VideoPath 且目录被 File.Exists 挡住，均可沿用上轮裁决。 |
| M10 | 第 2 项撤回 0 与 1 排序等价正确。本 Feature 内入队顺序与整条相机渲染队列的排序是不同范围，“管线顺序正确”仅能按回函限定的局部理解。第 3、7、8、11、12 项沿用上轮收窄结论。 |
| M11 | 第 7 项撤销“四份手写”合理。补充理由有误：ChartJson 启用了对象引用保留，`$id` 属于序列化器识别的引用元数据。没有同名 C# 字段不能证明它被忽略。ChartNote 又有公开无参构造，不能把反序列化缺省字段归功于第 110 行有参构造的可选参数。未见证据说明这几份夹具因此产生错误判定。第 8 项超时等待器在 Assets 的 C# 中只有定义，撤掉永久 Loading 的生产后果正确；原明细还包含输入适配和渲染顺序，应写清 P3 指子项还是整行。[序列化设置](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/Serialization/ChartJson.cs#L13) · [构造函数](../../../Assets/Game/Runtime/OpenSekai.Core/Charts/ChartNote.cs#L109) |
| M12 | 第 2 项截图 busy 保留 P1，退出播放有清理路径。第 5 项复核 157 行，画稿均可读到 PNG 尺寸，目标缺 131。前 27 项全为整图且尺寸匹配，21 项目标存在、6 项缺失。第 28 个数据项是尺寸匹配、目标缺失的 LiveAtlas 裁片，进入无异常捕获的 Paste。前面成功的目标已被写回，后续批次中止，所以优先要验“应用前发现失效清单，或失败时能报告并处理剩余项”，仅删 isReadable 切换解决不了半批写入。以上是清单和代码推导，回函未附 Unity 执行日志。第 8 项应再缩小不可恢复窗口：删源后、重写引用或配置对应 importer 前中断，重跑可能漏掉已删项；两步完成后中断，不能仍断言该项引用或元数据永久丢失。[批处理](../../../Assets/Game/Editor/PaintKit.cs#L167) · [裁片写入](../../../Assets/Game/Editor/PaintKit.cs#L203) · [迁移顺序](../../../Assets/Game/Editor/AtlasSpriteMigration.cs#L117) |

## 统计与结案

重新读取旧答辩 75 行，得维持 16、修正 56、撤回 3，降级 41，其中原 P1 有 19。该数对应上轮明细。本轮把 M11-7 从“修正 P3”整行撤销后，同一批 75 项更新为 **维持 16、修正 55、撤回 4**。原 P1 降级仍为 19。若 M11-8 的 P3 指整行，新增一次 P2→P3，抵消 M11-7 撤销移出降级数，仍为 41；若只降超时子项且整行保留 P2，则为 40。保留历史表没有问题，但原报告勘误同时指向两份裁决，最终数应标明轮次和 M11-8 的范围。

可以按现有证据排期修复 flick、零生命标签、暂停键取消和截图会话。PaintKit 应将批次中止与多余重导入分别跟踪。NextRUSH 等时条尾、首页画面表现和 Android 最坏耗时还需各自的谱例、逐帧记录或设备测量。修复顺序属于回函建议，本次只做复核。
