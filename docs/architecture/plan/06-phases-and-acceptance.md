# 方案：阶段、清单、验收与风险

> 目的：记录分阶段实施、待改清单、验收标准与风险控制。
> 适用读者：开发者。
> 前置条件：先读 [方案索引](../player-lite-plan.md)。
> 最后更新：2026-11-02（仅按章节拆分为独立文件，正文未改）

> 这是设计稿的一个分册。全文索引与「现状对照」见 [方案索引](../player-lite-plan.md)。

## 11. 分阶段实施与退出条件

建议由一名熟悉 Unity 的开发者按以下顺序实施。工作量是代码阅读后的粗估，尚未包含真实设备性能基线；有条件时在第一阶段结束后重新估算。

| 阶段 | 主要交付 | 进入下一阶段前的条件 | 估计工作日 |
|---|---|---|---:|
| P0 基线与样本 | 可运行基线、Windows／Android 构建、代表谱、依赖和资源入口清单、性能记录 | 直玩、自动、暂停、重开、结束与返回有可重复结果 | 2–3 |
| P1 谱面能力抽离 | 独立旧格式读取、时间轴、转换与包模型；首页直玩改走统一服务 | 不启动编辑器即可加载旧 JSON／SUS，关键转换与基线一致 | 4–6 |
| P2 应用入口替换 | Library 场景、独立设置、导航、返回状态；删除试玩与编辑器恢复分支 | 从全新启动到退出 Live 全程无编辑器 UI／Presenter 依赖 | 3–4 |
| P3 会话与结果 | 明确的结束原因、重开／暂停清理、结果快照、成绩兼容、资源所有权 | 取消／错误／重开不串局，手动成绩只提交一次 | 3–5 |
| P4 实体删除与构建瘦身 | 删除编辑器独占代码资源，收窄注册与 AB，隔离录屏，分批移除依赖 | 轻量 Windows／Android Player 能从干净输出构建并完成闭环 | 2–4 |
| P5 性能与发布验收 | 输入与渲染热路径优化、内存回归、依赖守卫、更新文档与报告 | 达成测试矩阵和性能门槛，产出新旧对比 | 3–5 |

**核心范围合计约 17–27 个工作日。** 输入时间修复建议作为独立变更纳入 P5 单独验收；若还要求更换音频时钟后端、替换大型 UI 框架或将所有模块彻底分程序集，应另留排期，不能把它们隐藏在上述估算里。

首个可演示里程碑是 P1＋P2：原有曲包可以通过新曲库直接进入 Live 并返回。此时尚不能声称资源和依赖已彻底轻量化。

每个阶段保持可构建，设置独立版本控制检查点。新入口稳定前保留旧入口用于对照；完成验证后删除旧入口，而不是长期在轻量发行中保留两套应用。

## 12. 需要修改的代码与资源清单

| 现有位置／类 | 建议动作 |
|---|---|
| `ScreenLayerCustomMusicScoreManager` | 提取开局服务和设置；保留曲库职责；删除 OpenEditor、编辑器恢复、默认录屏入口 |
| `CustomMusicScoreManagerService` | 保留包管理；SUS／JSON 转换下沉到 Charts；替换文件先验证再提交 |
| `MusicScoreMaker/Common/CustomMusicScoreEntry` | 拆成包信息、谱面读取与音频资源句柄；去掉编辑器模型返回类型 |
| `CustomMusicScoreManifest`、`CustomMusicScoreStorage` | 移到独立曲包模块，保持格式与存储路径兼容 |
| `CustomMusicScorePlayHistoryStorage` | 移到成绩存储；消费 PlayResult；兼容旧行与坏行隔离 |
| `MusicScoreMakerData` | 提取存储 DTO、迁移与播放转换；删除选中／编辑／预览逻辑 |
| `MusicScoreMakerUtility` | 提取纯时间与转换算法；不整体搬迁编辑器 UI 和总线依赖 |
| `MusicScoreNoteBase`、`MusicScoreEventData` 等 | 整理旧格式映射，保留实际使用的数值与字段语义 |
| `DeepCopyHelper` | 原编辑器用途移除；读取兼容由专用读取器承担；局内不做 JSON 深拷贝 |
| `SUS/Converter` | 接统一导入入口，验证长按生成与镜像语义 |
| `MusicScoreMakerEntryPoint`、`MusicScoreMaker.unity` | 必要初始化迁入新入口；解绑后删除旧入口与场景 |
| `SceneManager`、`SceneTransitionBlank`、`EditorBuildSettings` | 配置 Library／Core／过渡映射与构建顺序 |
| `SoloLiveController`、`BaseLiveController` | 删除编辑器分支；接会话、结果、统一退出与资源清理 |
| `LiveOutUIController` 与暂停 prefab | 移除试玩／发布弹窗，保留普通暂停和确认，接结果面板 |
| `LiveLogic` | 首轮保留判定与音符执行；移出录屏类型检测；清理输入分配；时间修复单独验证 |
| `LiveViewFactory` | 仅允许实际支持的单人视图，移除无效模式；必要 prefab 缺失应报清晰加载错误 |
| `Background2DView`、`MusicInfoView`、加载背景 | 用包信息契约替换编辑器 Common 引用；背景按选项加载 |
| `SoundManager`、`CriAtomShim` | 先保留后端；补充外部 clip 的生命周期与时钟接口 |
| `LiveSettingData`、`FramerateUtility` | 保留有效设置与高刷策略；避免返库时重置；工具选项退出 UI |
| `ScreenManager`、页面注册资产、枚举 | 收窄到真正可达页面，处理字符串加载和序列化数值兼容 |
| `OpenSekaiAssetBundleBuildPipeline` | 加入轻量构建范围，重建清单并排除旧输出残留 |
| 视频 Controllers／Services／Models | 移至可选模块；轻量构建不引用其类型、编码器与专用权限 |
| `MusicScoreMaker/Ingame` 的 Views／Events／Presenters／Input 与 OutGame | 完成抽离后整体移除独占实现；按依赖扫描处理外围引用 |

此表是主要修改点，不是“按表直接删文件”的脚本。最终删除名单由代码、GUID、运行时加载与构建依赖报告共同确定。

## 13. 验证与验收标准

### 13.1 谱面回归样本

至少覆盖以下样本；保留当前转换器输出作为对照，重构后的正确性不只通过新旧代码相互调用来证明：

| 类别 | 样本与检查 |
|---|---|
| 基础判定 | Tap、Critical、Flick 左／右／上，判定窗口内外边界 |
| 长条 | 同轨／跨轨、头尾、中间连接、隐藏点、长按连击点、尾 Flick |
| Friction／Guide | 可计分与不计分形态、Guide 颜色、断点连接 |
| 表现扩展 | 装饰音符、单键变速、零／负速度、同时押线 |
| 时间事件 | 多 BPM、同 tick 事件、HighSpeed、SE 音量、filler、尾部事件、拍号兼容基线 |
| 旧数据 | 缺省字段、旧版本、实际含 `$type/$id/$ref` 的 JSON、旧设置与历史 |
| SUS | 常规谱、变 BPM、长条、隐藏／引导及长按连击生成 |
| 非法输入 | 坏 JSON、未知版本、连接循环／缺失、无效数值、无音频、零计分谱、损坏 ZIP |

转换对比包括音符类型、数量、连接关系、时间、方向、速度、事件、总 Combo；生成的临时对象 ID 不要求字面相同，但引用关系必须一致。除等价对比外，使用能手工核算时间和连击数的小样本，防止复制旧缺陷却没有明确记录。

### 13.2 流程矩阵

- 全新安装、已有曲库、空曲库、损坏单个曲包都能进入曲库。
- 手动与自动各完成一首，退出后恢复原选择；自动成绩不污染手动记录。
- 加载连点、加载取消、加载失败后重试，无重复音乐与重复 Live。
- Playing／Paused／Result 中重开与退出，输入与音频状态正确。
- 连续暂停恢复、连续重开、连续切歌，结果不重复保存。
- Android 切后台、锁屏与返回、实际支持的触摸多指；Windows 鼠标回落和返回键。
- 实际支持的高帧率档位，实测帧时间与判定时间不随帧率错误缩放。
- 封面缺失时回落；可选 MV 缺失／关闭时仍可游玩；开启时单独验证。
- Windows Player 与 Android Player 都验证实际包资源，不能仅在 Unity Editor 中验收。

第一版保留当前输入覆盖，不将新增完整键盘打歌模式作为隐含需求。

### 13.3 性能门槛

以下为建议验收目标，**不是已经测得的收益**。P0 固定设备、曲包、画质、分辨率和构建方式后填写基线；无法支持某高刷档的设备不承诺强行达标。

| 指标 | 采样方法 | 建议门槛 |
|---|---|---|
| 核心局内分配 | 预热后 60 秒密集谱，分别观察输入、判定、HUD／音符循环 | 核心持续循环目标 0 B／帧；剩余一次性分配有清单 |
| 帧时间 | 同设备同谱，记录主线程／渲染线程及 p50／p95／p99 | 不回退；支持 120 FPS 的验收机，稳定区间 p95 以 8.33 ms 为目标；240 FPS 为 4.17 ms |
| 内存 | 曲库→开局→返回，连续 20 轮，等待正常回收与允许缓存稳定 | 无随局数持续上升的 AudioClip、Texture、RT、Note 实例或持有链 |
| 加载 | 冷／热启动与开局分开统计 | 热开局不重复全盘扫描／转换；相同条件下不慢于基线 |
| 音频与时间 | 长曲与多次暂停恢复，记录音轨位置对时钟误差 | 重构不得增加漂移；时间修复单独报告误差分布 |
| 构建体积 | Windows Player／附件、APK、StreamingAssets、仓库分别统计 | 轻量发行不含编辑器独占资源与录屏工具链；降幅以构建报告为准 |

菜单、加载、打歌分别统计，避免用菜单平均 FPS 掩盖打歌卡顿。包体积先报告绝对大小和组成，再报告百分比，不预设“减少 50%”一类未经测量的数字。

### 13.4 结构验收

完成时必须同时满足：

1. 玩家可从 Library 完成导入、手动／自动游玩、暂停、重开、结果与返库闭环。
2. 发行场景、页面注册、prefab 与 AB 中没有编辑器独占入口或缺失脚本。
3. App 和 Live 不引用编辑器 Presenter／Views／Events／编辑状态模型；旧名称只允许出现在受控兼容映射、迁移资料或测试样本中。
4. 默认轻量构建不引用视频生成具体类型，也不依赖 ffmpeg 才能启动或完成一局。
5. 保留的运行路径不触达反编译空壳；不要求以“清零整个仓库所有 throw null”作为本轮目标。
6. 旧曲包、设置、历史可读取；原始编辑器草稿和用户录屏未被自动删除。
7. 两个平台的干净构建、回归与性能报告齐备，文档主线更新为新流程。

## 14. 主要风险与控制

| 风险 | 控制方式 |
|---|---|
| 旧 JSON 因类型改名失效 | 实际旧文件样本、明确类型映射、保留引用恢复、读取失败不写回 |
| 长条／Guide／负流速表现变化 | 先抽算法，比较结构与时间，再做表现回归；不同时换判定和渲染 |
| 引擎引用在移动文件时断裂 | 保留 `.meta`；逐批迁移；扫描 Missing Script 与 GUID |
| 删掉编辑器后仍把资源打进包 | Resources／AB／字符串加载一起审计；干净构建与包内容检查 |
| 异步开局晚到、重开复用已判定音符 | 取消令牌与局 ID；不可变定义和可变运行状态分离 |
| 全局单例、音频或 RT 越用越多 | 明确资源所有者；20 轮生命周期与内存快照检查 |
| 时钟切换导致 Android 手感回退 | 本轮先保留后端；输入映射、音频后端、窗口变更拆开验收 |
| 为“分层漂亮”导致重写过大 | 允许短期适配器；先让闭环独立，后删空壳、立程序集边界 |

最终交付应包括可玩轻量构建、已移除编辑器的源码、兼容读取与回归样本、构建裁剪规则、性能对比，以及更新后的运行／开发文档。

## 附：本方案核对的主要来源

文档来源：`docs/项目总览（已过期）.md`、`docs/实现细节（已过期）.md`、`docs/美术资产（已过期）.md`、`README.md`。

代码锚点均为本次阅读时的位置，后续以方法名为准：

| 文件 | 核对位置 |
|---|---|
| `Assets/CustomMusicScoreManager/Runtime/UI/ScreenLayerCustomMusicScoreManager.cs` | `PlaySelectedAsync:2112`、`CreateDirectPlayBootData:2178`、`OpenEditor:2071`、视频启动数据 `:3170` |
| `Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Common/CustomMusicScoreEntry.cs` | `LoadScore:63`、`RegisterAudioAsync:93` |
| `Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Models/MusicScoreMakerData.cs` | `ToMusicScore:1603` 及其音符转换链 |
| `Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Utilities/MusicScoreMakerUtility.cs` | 编辑器事件／视图依赖及时间换算 |
| `Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Utilities/DeepCopyHelper.cs` | JSON 设置 `:33` |
| `Assets/CustomMusicScoreManager/Runtime/Services/CustomMusicScoreManagerService.cs` | `ReplaceScoreFile` 中 SUS→编辑器模型的转换与文件替换 |
| `Assets/Scripts/Assembly-CSharp/Sekai/Core/Live/SoloLiveController.cs` | 初始化 `:26`、成绩写入 `:381`、退出 `:395`、结果演出 `:526` |
| `Assets/Scripts/Assembly-CSharp/Sekai/Core/Live/BaseLiveController.cs` | GC `:152`、返回策略 `:278`、清理 `:326` |
| `Assets/Scripts/Assembly-CSharp/Sekai/Core/Live/LiveLogic.cs` | 输入数组 `:323`、触摸／鼠标时间换算 `:706/:802` |
| `Assets/Scripts/Assembly-CSharp/CriWare/CriAtomShim.cs` | Stopwatch 时钟 `:896` |
| `Assets/Scripts/Assembly-CSharp/Sekai/LiveOutUIController.cs` | 普通暂停与编辑器试玩弹窗的混合实现 |
| `Assets/Scripts/Assembly-CSharp/Sekai/Live/LiveViewFactory.cs` | 按模式加载 UI、背景与缺失视图回落 |
| `Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Common/CustomMusicScorePlayHistoryStorage.cs` | JSONL 追加、最佳成绩、旧记录字段 |
| `Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs` | 所有标签枚举 `:34` 与 StreamingAssets 输出 |
| `ProjectSettings/EditorBuildSettings.asset`、`Packages/manifest.json` | 场景顺序与当前依赖 |

---


## 相关文档

- [方案索引](../player-lite-plan.md)
- [架构总览](../README.md)
- [重构日志](../../changelog/2026-11-refactor-log.md)
