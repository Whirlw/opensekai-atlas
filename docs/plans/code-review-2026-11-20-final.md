# OpenSekai 代码审查最终方案

2026-11-20 起，对 `Assets/` 下全部 C# 源码做了一轮多智能体审查，随后是六轮交叉复核（36 个独立审查子智能体 → 第三方五轮复核 → 原答辩人六轮再对账）。**本文是唯一有效版本**，过程文档的全部结论已并入此处；若与过程文档冲突，以本文为准。过程文档已归档在 [`archive-review-2026-11-20/`](archive-review-2026-11-20/README.md)，只在追溯「某个判断为什么被改掉」时才需要读。

> 以下第一至九节记录实施前的基线。第一轮实施与实测见[第十节](#十实施与验收)，第三方复核见[第十一节](#十一执行情况复核)，复核后的修复与真机复测见[第十二节](#十二复核后的修复与验证)，最终验收结论见[第十三节](#十三二次修改复核)。审查阶段全部为静态核对：**没有运行 Unity、没有真机采样、没有执行任何破坏性工具**。因此文中每一处「需实测」都不是修辞——它们是这份方案目前无法自己闭合的部分。

## 一、结论

项目不是"烂代码"，而是**三层结构断层**：

1. **纯逻辑层健康**（`OpenSekai.Core`、`Shared/Live` 的判定数学、`Tests/Core`）：有边界处理、有注释、有针对性测试。
2. **界面与表现层能用但"重"**：单类 1100+ 行、每帧闭包、逐实例材质、逐键全量重建。功能对，代价没算过。
3. **平台与工具层有相当一部分是"看起来存在的能力"**：`throw null` 空壳共 **322 处**（Platform 110 / Data 89 / Assets 82 / UI 26 / Screens 15）、只有 TODO 的实现、被静默丢弃的参数、永不入队的 Feature。

最该先动的不是那一堆命名与重复代码，而是四类"会真的伤到玩家或发布"的问题——已全部落到下表的 P1。

## 二、最终统计（已冻结）

原始裁决的 75 项争议，经六轮复核后的最终分布：

| 指标 | 值 |
| --- | ---: |
| 维持 | 16 |
| 修正 | 55 |
| 撤回 | 4 |
| 降级 | **41**（原 P1 **19** + 原 P2 **22**） |

- 降级数按 **M11-8 整行计**；若只把超时子项算作降级则为 40。
- M07-4 历经 P1→P2→P3 仍**只计一行**。
- 这条统计**只覆盖那 75 行裁决，不代表待办数量**；排期完整性、模块状态、U7/U8 验证都不计入。

## 三、修复排期表（审查已结、修复待办）

> **覆盖**：首批可执行项 = 全部 P1 + 触发已明确的 P2。
> **状态**：各行**审查均已结**（U7/U8 为「触发待证」），**制表时修复状态一律未修**。审查状态与修复状态互不代表。

| 序 | 项 | 问题级别 | 验证任务优先级 | 触发/前提与修法 |
| ---: | --- | --- | --- | --- |
| 1 | M05/M06 暂停键不处理 `Canceled` | **P1** | — | 单文件；`LiveSpriteButton` 的闸门是「无在按」而非「同指」，一次系统取消会挡掉所有新按下，复位只有 3 处（`Setup`/`OnDisable`/同 ID `Ended`） |
| 2 | M01 flick 单位换算 | **P1** | — | 常量 0.032808 英寸/帧，100 dpi 应 **3.2808 px**、实算 **1.2917 px**，门槛 1.97 cm/s 而非 5 cm/s；修 `JudgeTuning.FlickDistanceInPixels` 与 `LiveUtility.ScreenDpiToInch` |
| 3 | M05 零生命被记为 `Completed`、曲库显示 `CLEAR` | **P1** | — | 只改结果标签与 kind 映射（`SoloLiveController:515` 把 `result` 0 改 3），**不动历史记录** |
| 4 | M12 截图会话「未保存场景」分支留下 busy | **P1** | — | 该分支在写完 `KEY_REQUEST`/GameView/`runInBackground` 之后才抛且不 `Cleanup`；改「先校验后写入 + `try/finally`」 |
| 5 | M09 设置原地写入 | **P1** | — | `LiveSettingsStore:50` 直接 `File.WriteAllText`；改临时文件 + 原子替换 |
| 6 | M02 替换媒体先删旧件 | **P1** | — | 先写 `.tmp`、成功后再替换，失败保留旧件 |
| 7 | M08a AB 依赖释放 | **P1** | — | 自建「父包 + 依赖包」，EditMode 断言 `GetAssetBundleReferenceCount` 归零；引用计数只增不减在 `AssetManager:152`、`:467` |
| 8 | M08b URI 同步空转 | **P1** | — | 任一普通 bundle 放进 StreamingAssets，在 `jar:`/WebGL 上复现 `while (!operation.isDone) Thread.Sleep(1)` |
| 9 | M08c HCA 越界 | P2 | — | 一段 v3.0、非次立体声、合计恰 128 频带的码流；读侧 `ScaleFactors[csCount - i]` 改 `csCount - i - 1`；验收要**比对合法样本的解码输出**，不能只验不抛异常 |
| 10 | M08d HCA 实际解码耗时（**问题级 P1**） | P1 | **P2 采样** | 真实曲目 + 设备；覆盖两条首次解码时机（包加载预载、`MusicReady`），另加「外部 clip / 缓存命中」对照用例断言 `Decode` 0 次 |
| 11 | M08e 每帧监听器扫描 | **P1** | — | 纯静态可修：守卫改缓存/事件驱动；另须验外部监听器启停时 fallback 接管与释放（`enabled=false`/`SetActive(false)` 不算外部——最易漏） |
| 12 | M03a 曲库首次进入的音频解码 | P2 | P2 采样 | ≥ 20 首未测响度，记每首最长单帧阻塞与整轮总时长；`GetContent` 非流式 |
| 13 | M03b 首次缩略图生成 | P2 | P2 采样 | 清 `temporaryCachePath` 后连续滚动，记每张生成耗时与掉帧 |
| 14 | M12 `PaintKit` 批次中止与多余重导入**分开跟踪** | P2 | — | 半批写入优先：应用前校验清单 + 逐行 `try/catch` + 输出失败清单；`isReadable` 翻转全程无用 |
| 15 | M02a 开局同步耗时 | P2 | 待真机采样 | 首个 `await` 前同步读包/设置/配置、解析转换并整份算一次会话哈希；「每次算一次」不算重复，重复只在缓存无键时 |
| 16 | M02b 组合根注入未覆盖开局 | P2 | — | `PlayService` 直接读 `Shared`；生产组合根当前也传 `Shared`，**仅替换组合根时暴露** |
| 17 | M02c 批量导入进度回调线程契约 | P2 | — | 仅传 `progress` 且进入解压循环时从 ThreadPool 回调；补线程声明或改投主循环 |
| 18 | M02d SUS 导入校验缺口 | P2 | — | 仅 `.sus`/`.txt` 且能解析出零可玩音符时被当作安装成功 |
| 19 | M02 坏索引阻断发布 | P2 | — | 严格重读处抛错即中止；**保持崩溃恢复时的删除保护** |
| 20 | M12 AB 构建失败须保住上一份有效输出 | **P1** | — | 与运行时释放依赖、`PaintKit` 半批写入各自验收；如延期须注明 |
| U7 | 返回键双消费 | **P1（触发待证）** | 玩家路径验证 | 判据：**同一次输入是否既进 `PlayCloseAnimation` 又让栈计数减一**；须排除 `Mysekai`、`ExecuteBackKeyProcess`、`BackUIScreenOverride`、切换忙状态。验收＝双退 vs 单效果 |
| U8 | 页面栈重入 | **P1（触发待证）** | 玩家路径验证 | **先发 `PushUIScreen`、结束前再调任一栈入口（含第二次 `Push`）**；先 `Change`/`Back` 属反例。直证＝Push 收尾会清掉别人刚取得的标志（并发且互相清空） |
| 21 | 文档校正 | — | — | 普通长条尾按抬手样本时间结算（与 `judging-design.md` 冲突）、`Skill/Fever` 支持范围、`throw null` 清单 |

## 四、各模块最终状态

| 模块 | 审后定性 | 剩余待修 |
| --- | --- | --- |
| M01 Core 领域层 | 分层干净、纯函数可测；风险在单位与公式双份实现 | #2 |
| M02 App 服务层 | 骨架清楚；问题在失败路径不原子与跨线程契约 | #6、#15–#19，另有 4 条 P3 只记录 |
| M03 曲库界面 | 结构最规范的 UI 模块；三条主线程重活 + 逐键全量重建 | #12、#13、导入同步解压（P2） |
| M04 首页与服务器 | 页面栈与取消令牌规范；泄漏与一次性建满 | P2 若干，#9 号待实测 |
| M05 演奏核心 | 全项目最高分；帧循环分层与长条解耦到位 | #1、#3 |
| M06 Live 视图 | 零分配意识明确；契约与实现脱节 | #1（同一按钮） |
| M07 页面框架与 UGUI | 骨架能用；生命周期收尾与返回键无单一出口 | U7、U8 |
| M08 平台与资源 | 空壳与主线程阻塞集中地 | #7–#11 |
| M09 数据与共享 | 判定与触摸纯逻辑最健康 | #5，另有路径读取 P2 与 P3 若干 |
| M10 音频/动画/渲染 | 分层意图清楚；「接口留着、实现没接上」 | 模糊截帧（P2，待截帧） |
| M11 测试套件 | 断言质量在水准之上；问题在「覆盖假象」 | 4 条 P3/P2 |
| M12 编辑器工具 | 有真防护；失败路径普遍不原子 | #4、#14、#20 |

## 五、已撤回与已降级的结论（这些说法不再成立）

写在这里是为了防止有人翻旧文档时把已推翻的结论当成事实：

| 曾经的结论 | 现在 |
| --- | --- |
| 条头按下不计入「一帧一个按下额度」 | **撤回**——`Consumed` 的类型判断只控制按键时刻记录，额度对所有候选无条件扣除 |
| `WhenCovered` 订阅累积 | **撤回**——一次性注册，调用前就置空 |
| `LiveContext` 读 `UserDataManager` 越界 | **撤回**——`modules.md` 明文规定的写槽-读槽边界 |
| 「四份手写测试夹具」 | **撤回**——JSON 单/多行不证明作者，字段差异由构造默认值补齐 |
| AB 超时「比原文更严重」 | 降回 P3——超时等待器全仓零调用 |
| `ColorFader.IsPlayFading` 卡住 / `CustomButton` 按压 tween 不取消 | **撤回**——`OnDisable` 都已有清理路径 |
| `ScreenLayerLiveLoading` 每个 boot 叠加一次订阅 | **撤回**——同上，一次性 |
| `keepScrollPosition` 是死参数 | **撤回**——它在分支里，且被另一处显式传入 |
| `PaintKit` 222 次 / 601 文件 / 1200 次重导入 | **作废**——当前工作区实跑到数据项 28 中止、此前写回 21 张 |
| 「`throw null` 五族都是 P1」 | 降 P3——零调用点、零挂载，属死代码清理 |
| flick 参数「方向不可判定」 | 收窄——两侧谓词差异是静态可证的，待定的只是翻哪一条 |
| `(RenderPassEvent)1` 与具名 `0` 等价 | **撤回**——URP 按整数比较、`SortStable`，两者是两个排序键；当前资产下位置相同，但那是资产条件不是等价 |
| 暂停键失效导致「整曲不可暂停」 | 收窄——只有**被按下的那个按钮**失灵，返回键/失焦/后台仍是暂停入口 |
| 首页「4.4 秒后跳进库」 | 作废——切库绑在 0.46 s 展开完成回调上，`HideHome` 关整个 canvas |
| 「损坏只能来自应用外」「有遮罩所以没 ANR」 | **撤回**——都是越界断言，改成「仓内未发现该路径」＋测量条件 |
| 「Push 期间 `IsUILayerWorking` 恒为 false」 | 收窄——事实只是 Push 不持有标志；更准的是它收尾会**清掉别人刚取得的标志** |

## 六、仍未闭合（需要运行时证据）

**专项测量 6 项**（依赖仓外样本 / 设备 / 帧抓取）：

1. M05：需一张把附点放在条尾拍的现场谱例（最小谱可验机制，真实谱才能证明影响面）。
2. M04：3.9–4.4 s 重叠窗口内的**逐帧委托顺序**，或覆盖窗口的录屏。试验单位＝「窗口内成功点击的一次开屏」，按点击时刻/设备/帧率分层；20 次零复现的 95% 上界约 13.9%，但确定性调度可能让各次完全相关，只能作弱证据。
3. M08：Android 最坏耗时（解码阻塞毫秒与峰值内存）。
4. M10：Frame Debugger 截帧，确认模糊输入的输入颜色与最终画面。
5. M12：`PaintKit` 的一次真实执行日志。
6. M03：曲库首次进入的主线程重活（#12、#13 的采样）。

**玩家路径验证 2 项**（只需仓内构造）：U7、U8——见上表。

## 七、作废的数字与措辞

- 「约 265 ms/首」——只是一条注释里的单个 3 MB Ogg 样本。
- 「约 4.4e9 MAC」「每次 `Play` 恒 2 次扫描」——实际 **22.12 亿 MAC**（乘、加分开计才是 44.24 亿次运算）、成功路径 **3 次**（`clips` 空 0 次、全 null 1 次）。
- 「601 个文件 / 约 1200 次重导入」——当前清单 157 行、数据项 28 中止、21 张写回。
- 「模糊读到上一帧残帧」——现定为「配置死字段 + 枚举外裸 `1`」，画面另列待截帧。
- 「约 500 个 `.cs`」——实际 Game 591 + Tests 120 = **711**。

## 八、这次审查学到的方法

1. **把「实现事实」「可达链」「后果」拆成三列**。绝大多数降级都源于把三者捆成一句断言：`Pass` 没被排除是真的，但 `HasJudgment` 先挡掉了；`VideoPath` 返回目录是真的，但消费者先 `File.Exists`；`WhenCovered` 没退订是真的，但它是一次性注册。
2. **给数字就要给复算方式**。说不清来源的定量，一律降级成「存在」表述。
3. **定 P1 之前先查全局兜底与可达性**：`Resources.UnloadUnusedAssets`、零调用点、`ScalePointerDown` 无调用、`throw null` 无人挂载——四处兜底都是复核时才补上的。
4. **「不可达」不等于「未触发」，也不等于「不存在」**。前者该降级（死代码），后者该保留（潜伏缺陷），第三种才是缺陷。
5. **改一处结论要连带检查多处引用**：本轮出现过两次「正文改了、表里还是旧数」和一次「跨文档序号照抄」（M03 的第 4 项 vs 第 5 项）。

## 九、附录

- 审查方式：12 个模块 × 3 个互不相见的子智能体（正确性 / 架构与规范 / 性能与健壮性），各模块 4k–22k 行，逐文件通读；每轮答辩由**原审查员**回炉，不另起新人。
- 术语：P0 崩溃/数据损坏 · P1 特定场景功能错误或明显泄漏/卡顿 · P2 可维护性/性能/规范偏差 · P3 轻微。
- 未覆盖：`Assets/Vendor/`、shader/HLSL、`.prefab`/`.unity`/`.asset` 的序列化值（仅顺带核对）、Android Java 与 `Tools/` 脚本。
- 局限：全部静态阅读。涉及并发时序、Unity 序列化取值、URP Pass 顺序的结论，都需要在编辑器或真机上确认。


## 十、实施与验收

### 已落地的改动

| 排期 | 实施结果 |
| --- | --- |
| 1–3 | 系统取消触摸会释放暂停按钮的占用。flick 的两处门槛共用英寸乘 DPI 的换算，100 dpi 的门槛为 3.2808 px。零生命的本局结果显示失败，历史记录写入逻辑保持原有行为。 |
| 4–6 | 截图会话先检查打开的场景，恢复 Game View 和后台运行设置。设置通过临时文件发布。替换音频、封面、视频和谱面先写入新内容，发布清单失败时恢复旧件。 |
| 7–9 | 父资源包记录拥有的依赖并在最后一次释放时归还，加载失败也归还已取得的引用。URI 平台异步读取随包目录并准备按哈希区分的本地文件。HCA v3 的高频重建改正一位索引，使用两份合法码流逐样比较全块非静音 PCM。 |
| 11、14 | 音频监听器使用缓存，场景加载时刷新，启停从缓存逐帧观察，曲库页面动态创建监听器及演奏视图就绪时立即交接，另以 0.5 秒间隔发现其他新组件（**该定时器已在第十二节删除，此句仅作历史记录**）。PaintKit 先校验整份清单，写入时逐项收集错误，取消可读标记来回切换。 |
| 16–20 | 开局使用组合根传入的曲包和设置。下载进度注明解压线程与发布线程。下载 ZIP 安装与替换谱面时都会拒绝可解析但零可玩音符的 SUS/TXT。Sonolus 普通发布能保留坏索引备份并挽救有效行，中断恢复继续严格读取索引。资源包构建在暂存目录完成，切换或刷新失败时恢复旧目录。 |
| U7、U8 | 返回键由页面管理器先交给最上层对话框，同一输入只产生一个效果。Push 开始时即占用页面切换状态，完成后才释放。 |
| 21 | 修订[普通尾评分时钟](../architecture/judging-design.md)、[各谱面格式的技能和 Fever 支持范围](../architecture/native-next-rush.md)，并补充 [322 处空实现的逐文件清单](../architecture/modules.md)。 |

### Android 采样（#8、#10、#12、#13、#15）

PLQ110，Android 16。测试使用独立应用 ID，采样完成后已卸载该测试包并确认原应用仍在。首次扫描使用 18 个设备曲包副本与 2 个项目测试曲包。清除测试副本的响度及缩略图缓存，进入曲库后连续滚动。下列是单台设备上观察到的耗时，响度与封面加载可同时占用一帧。

| 路径 | 实测 |
| --- | --- |
| 包内普通资源 | Android 从 APK 的 `jar:` 路径准备目录和文件后，普通资源包 `shader/live` 加载状态为 `Done`。独立 WebGL 样本通过 HTTP 同时发出两次准备请求，随后清空内存目录状态并重开。两次均读出同一普通包内的文本，浏览器只请求一次包体。[浏览器回执](../../TestResults/review-nov20-web-receipt.log) |
| 真实曲目首次 HCA 解码 | 没有原始曲目级 HCA。用完整的 DEINOS PHAINEIN 音频制成两份不同波形 ID 的 ACB 测试输入，48 kHz、双声道、178.522 秒。在独立测试版将两个新 cue 分别接入包预载与 `MusicReady`，使两条路径各自从冷缓存开始。预载首次解码 3735.0 ms，`MusicReady` 首次解码 3706.8 ms，各产生 68,552,552 字节 PCM。外部 clip 与命中缓存各成功准备一次播放，解码次数均为 0。 |
| 进程内存 | 预载前约 427 MiB PSS，预载结束后首个样点约 547 MiB，第二次首次解码及播放之后观测到约 734 MiB PSS、835 MiB RSS。包括 Unity、曲库、编码数据及已解码音频。采样间隔约 0.5 秒，数据只能给出观察到的峰值。 |
| 20 首冷响度扫描 | 第一首开始至末首结束约 13.03 秒。逐首耗时 178.3–2194.5 ms，中位数 550.5 ms。各首所跨最长帧间隔 176.5–2081.5 ms。重叠帧也包括启动与缩略图工作，单首的帧间隔可以长于它自身运行时间。 |
| 22 张冷缩略图 | 连续滚动中每张端到端 13.7–1982.2 ms，中位数 126.8 ms。15 张结束时所在的帧间隔超过 16.7 ms，最长观测间隔 2854.4 ms。同帧完成的多张图共享一帧，不能把它们累加成掉帧总数。 |
| 开局同步阶段 | 完整曲包 DEINOS PHAINEIN 首次 await 前 114.7 ms，复测 120.6 ms。两轮都进入了播放。 |

原始数据含逐首、逐图及首页逐帧记录。[冷曲库](../../TestResults/review-nov20-device/cold.log) · [HCA、开局及首页](../../TestResults/review-nov20-device/measure-final.log) · [进程内存](../../TestResults/review-nov20-device/memory-final.txt)。HCA 由 [VGAudio 2.2.1](https://github.com/Thealexbarney/VGAudio/releases/tag/v2.2.1) 离线编码。

<details>
<summary>逐首响度与逐张缩略图数据（毫秒）</summary>

| 曲包 ID | 响度扫描耗时 | 所跨最长帧间隔 |
| --- | ---: | ---: |
| `codexp23tail76ad6c33` | 178.3 | 1349.5 |
| `codexbaseline20261101tap` | 2194.5 | 2081.5 |
| `3zka7aj2dr3i` | 557.9 | 266.9 |
| `xn220h1yh2d9` | 470.6 | 316.2 |
| `8uj2pn68o5el` | 477.3 | 308.0 |
| `u3r2zt97irqx` | 413.5 | 266.5 |
| `amx9ivmfq9qy` | 279.0 | 176.5 |
| `r8yxmywwyzk3` | 779.1 | 583.0 |
| `ol3s9va6e8i9` | 588.0 | 388.1 |
| `qvfd1634upu4` | 779.9 | 374.9 |
| `3zqjqhxj3uks` | 466.0 | 274.3 |
| `fzzmizsha6lo` | 1256.3 | 573.7 |
| `l1sk2edi27uu` | 839.6 | 402.7 |
| `xdvgwx0rjliy` | 402.5 | 270.9 |
| `p1i19fw3hrxf` | 451.4 | 291.6 |
| `6kfhd9x2pryk` | 543.1 | 324.2 |
| `9g1s31yz2tp2` | 611.5 | 354.5 |
| `8c1788a7f4634e69989b979cb23c8c04` | 417.3 | 281.6 |
| `27ow4qgb6hqo` | 640.0 | 388.0 |
| `gbgtml3y0mxo` | 670.8 | 399.8 |

| 图像请求（按完成顺序） | 生成耗时 | 完成帧间隔 |
| --- | ---: | ---: |
| `server-43` | 1982.2 | 2854.4 |
| `restructure-p2-3-device-tai` | 194.7 | 199.2 |
| `Breach_of_Faith_gbgtml3y0mxo` | 152.3 | 199.2 |
| `DEINOS_PHAINEIN_27ow4qgb6hqo` | 150.6 | 199.2 |
| `Kamisama_8c1788a7f4634e69989b979cb23c8c04` | 137.7 | 199.2 |
| `LatencyTest_9g1s31yz2tp2` | 136.9 | 199.2 |
| `server-45` | 135.3 | 199.2 |
| `MARENYX_6kfhd9x2pryk` | 137.1 | 199.2 |
| `restructure-p2-3-device-tai` | 126.6 | 199.2 |
| `raputa_l1sk2edi27uu` | 128.7 | 199.2 |
| `server-42` | 127.0 | 199.2 |
| `Redshift_2nd_Ignition_p1i19fw3hrxf_2` | 112.7 | 199.2 |
| `baseline_ta` | 13.7 | 12.1 |
| `server-46` | 272.1 | 259.6 |
| `輪廻玲々_3zka7aj2dr3i` | 25.9 | 12.1 |
| `人生リセットボタン_Life_Reset_Button_u3r2zt97irqx` | 21.4 | 12.1 |
| `月葬_【APPEND_35】_8uj2pn68o5el` | 25.9 | 18.1 |
| `ULTIM@TE☆BE∀M!!_xdvgwx0rjliy` | 29.4 | 24.1 |
| `server-43` | 46.2 | 6.0 |
| `コネクト_amx9ivmfq9qy` | 31.9 | 6.0 |
| `server-44` | 28.7 | 6.2 |
| `神威_(Kamui)_xn220h1yh2d9` | 19.5 | 12.1 |

</details>

### 专项复核

- M04 在开屏第 203 帧（3.924 秒）向可交互的曲库卡片发送点击，第 277 帧（4.388 秒）隐藏首页，第 279 帧开屏结束回调执行，随后曲库保持显示。桌面端分别设为 30、60、120 fps，各在 3.92、4.05、4.20、4.35 秒向可交互的卡片发送两次独立开屏点击，共 24 次，全部进入曲库。按记录帧号，6 次先隐藏首页，0 次同帧，18 次先结束开屏。同帧记录不能区分帧内先后。设备和时序样本有限，未据此估算总体故障概率。[桌面逐次结果](../../TestResults/review-nov20-home-window.csv)
- M08e 重复开屏还暴露了曲库页面在场景加载后才添加监听器的交接空档。改为在创建时立即通知音频系统后，相同 24 次冷启动采样期间，双监听器警告由 1109 条降为 0 条。
- M05 检查 Kamisama、Hallway、Life Reset 与 Sbuga 的四份独立 LevelData，共 711 个隐藏 Tick 实体。核对附着目标与连接段声明的条尾后，未找到同拍条尾实例。[本地谱面扫描](../../TestResults/review-nov20-charts/local-scan.json)构造同拍谱时优先选择谱面绑定的尾键，并用抬手回放复核。额外的公开谱面目录在采样时返回 HTTP 403。真实曲目影响面仍待有对应结构的谱例。
- M10 用 Unity Frame Debugger 抓取当前渲染配置的 71 个事件。测试相机先复制屏幕颜色、再绘制 BlurUI、执行两次模糊绘制，最后输出到相机目标。黑底洋红标记在输入、模糊输出和最终画面的中心均为 RGB(255, 0, 255)。同一相对边缘位置的输入与最终画面是黑色，模糊输出为 RGB(99, 0, 99)。本次截帧覆盖曲库 UI 渲染器。[事件序列](../../TestResults/review-nov20-frame/events.txt) · [模糊输入](../../TestResults/review-nov20-frame/input.png) · [模糊输出](../../TestResults/review-nov20-frame/blur.png) · [最终画面](../../TestResults/review-nov20-frame/final.png)
- M12 已实际运行 PaintKit。现有清单在写入前报告了 131 条缺失目标纹理，整批写入 0 张。[执行日志](../../TestResults/review-nov20-paintkit.log)

### 最终回归

| 套件 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| [EditMode](../../TestResults/review-nov20-edit.xml) | 1510 | 0 | 21 |
| [PlayMode](../../TestResults/review-nov20-play.xml) | 32 | 0 | 0 |

跳过项包括 18 个显式性能观察用例和 3 个依赖额外样本或在线服务器的集成用例。32 项 PlayMode 结果含 31 项常规测试及 1 项覆盖 24 次冷启动的专项测量。PlayMode 已覆盖一次 Escape 只关闭对话框、Push 中再 Push/返回、外部监听器两种启停方式，以及模糊输入和最终颜色。EditMode 已验证真实父子资源包释放归零、发布失败保住旧目录、媒体写入回滚、未保存场景可再次请求截图、取消触摸后可再次点击暂停，以及同拍隐藏点和 HCA 边界。

## 十一、执行情况复核

> 本节保留第三方复核时的工作区状态。复核所列的第一轮源码和原始日志此后已分别提交为 `620a5d92`、`8bc89834`。复核后的处理见第十二节。

2026-11-20 对第十节的实施记录逐条核对了当前源码、测试与产物。**结论：23 项里 17 项已落实、2 项部分落实、1 项未落实、3 项属采样任务（已完成）。** 第十节的记录基本准确，但有 4 处需要修正或补记。

### 逐项核对

| 排期 | 第十节声明 | 复核 | 证据 / 缺口 |
| ---: | --- | :---: | --- |
| 1 | 取消触摸释放暂停键 | ✅ | `LiveSpriteButton:121` 把 `Canceled` 并入抬起分支、`:125` 只让 `Ended` 触发点击、`:130` 复位 ID；新增 `InputSystemTouchSourceTests:60` |
| 2 | flick 共用英寸×DPI | ✅ | `JudgeTuning:77-79` 抽 `InchesToPixels`、`LiveUtility:41` 复用；测试期望改为 3.2808 |
| 3 | 零生命显示失败 | ✅ *(路线不同)* | 不在 `result` 上改，而是 `SoloLiveController:574-575` 的 `kind = result == 3 && score.life > 0 ? Completed : Failed`。**`result` 仍是 3**，`result == 2` 的 Failure 演出分支仍不可达（实时演出走 LifeZero） |
| 4 | 截图先检查场景 | ✅ | `ScreenshotSession:74-91` 四条前置全部前移；`:129-130` `try{Finish}finally{Cleanup}` |
| 5 | 设置临时文件发布 | ✅ | `LiveSettingsStore:50` 改 `AtomicFile.WriteAllText` |
| 6 | 替换先写后删 | ✅ | `SongPackageManager:549/553/555` 暂存→`File.Replace`；旧件删除 `:542` 后移；`:561-565` manifest 失败回滚 |
| 7 | 依赖计数归还 | ✅ | `AssetManager:178` 记录 own 依赖、`:494-498` 递归释放；`AssetBundleLifetimeTests:58-65` 断言父子计数归零 |
| 8 | URI 异步读取 | ⚠️ **部分** | `AssetBundleManager:126-131` 已 `await SendWebRequest`、全仓无 `Thread.Sleep`；但**无 `CancellationToken`、未设 `request.timeout`、未查 `request.result`**——挂起即永不完成 |
| 9 | HCA 高频重建索引 | ✅ | `HcaDecoder:533` 改 `csCount - i - 1`、`:495` 有 128 上限守卫；`HcaBandTests:19-25` 用两份合法码流逐样比对全块 PCM |
| 10 | HCA 首次解码耗时 | ✅ 采样 | 3735.0 / 3706.8 ms，`decodes = 0`（`measure-final.log:141/143/145/151`）；插桩在 `TestResults/` 未入库 |
| 11 | 监听器改缓存 | ⚠️ **部分** | 每帧 `FindObjectsByType` 已消除（`CriAtomShim:1402-1405` 节流）；但**仍有 0.5 s 周期全场景扫描**（`Include` inactive），严格「只在变化时扫」未达 |
| 12 | 响度扫描 20 首 | ✅ 采样 | 13.03 s / 中位 550.5 ms（`cold.log`） |
| 13 | 缩略图 22 张 | ✅ 采样 | 端到端中位 126.8 ms（`cold.log`） |
| 14 | PaintKit 先校验后写 | ✅ | `:147` `f.Length < 12`、`:172-176` 写前预检、`:180-192` 逐行 `try/catch`、`:198-199` 失败清单、`isReadable` 往返整段删除、`:248` `AtomicFile`；缺口：预检与写入各解码一次 PNG |
| 15 | 开局同步 114.7 / 120.6 ms | ❌ **未修** | `PlayService:107-135` 仍在首个 `await`（`:142`）前同步读包/配置/解析/哈希；**全文无 `Task.Run`、无 `UniTask.Yield`**，`:105` 的进度文案后未让帧。第十节只把它列为采样，但排期表里它是 P2 修复项 |
| 16 | 组合根注入 | ✅ | `PlayService:23-24/52-53` 构造注入 `_packages`/`_settings`；`PlayServiceDependencyTests:46` 用 7 参构造 |
| 17 | 进度线程约定 | ✅ | `SongPackageManager:266-267` 注明「解压线程回调、发布线程通知」 |
| 18 | 拒绝零可玩音符 | ✅ *(换位)* | 不在调用点接返回值，而是 `ChartFileReader:34-35` 抛 `InvalidDataException`；缺口：导入路径无零音符专项用例 |
| 19 | 坏索引保护 | ✅ | `SonolusLevelInstaller:455-461` 严格重读、`:470-476` 删除前复核并改名、`:725` 非法记录即抛；`SonolusInstallIndexTests:163-179` |
| 20 | 构建暂存与回滚 | ✅ | `OpenSekaiAssetBundleBuildPipeline:88-111` 暂存、`:127-139` 备份/回滚、`:91-93` 清中断残留；`EditorPublicationTests:25-42` |
| U7 | 返回键单一分发 | ✅ | `ScreenManager.OnBackKey:541-542` 先交 `DialogBase.TryHandleModalBack()`；`DialogBase.Update:87-91` 仅在无管理器时自读 |
| U8 | Push 即占状态 | ✅ | `ScreenStackService:103-104` 先持标志再 `StartCoroutine`、`:385` 清自己的标志；`ForceBackUIScreen:144` 加忙碌守卫 |
| 21 | 文档校正 | ✅ | `judging-design:75/97/317`、`native-next-rush` 技能范围、`modules.md` 322 处清单 |

### 需要补记或修正的 4 处

1. **第 8 项不是"已落地"，是"部分落地"**：让出主线程做到位了，但排期要求的是「让出主线程**并带超时或取消**」，两者都缺。更要紧的是这次改动**带进一个新隐患**——`LibraryEntryPoint.cs:44-47` 把资源包准备失败降级成 warning 后继续，`_initialized` 留在 `false`，后续所有加载都报「does not contain bundle」；且只有这一个入口 `await` 了准备，`SekaiSingletonManager:37` 仍是同步 `Initialize`。
2. **第 11 项同理**：每帧扫描确实消除了，但改成了 0.5 s 周期全场景扫描。如果「只在变化时扫」是硬指标，这条还没到；如果 0.5 s 兜底是有意设计，应在记录里写明理由与代价。
3. **第 15 项要定性**：第十节把它归进"采样"，但排期表里它是 P2 修复项，代码没动。要么明确写成「接受现状：114.7/120.6 ms 可接受」，要么留在待办里。
4. **第 21 项漏了一处**：`modules.md:31` 的 `Live/View` 职责仍写着「特写与技能文字」，而本轮已确认 `CutinManager`/`SkillTextView` 是空实现——这条不在排期要求内，但和已写进 `modules.md` 的 322 处空实现清单自相矛盾。

### 两处记录与代码的细微出入

- 第 3 项的路线与排期给的两条都不同（既没接进 `result`，也没在 `PreExit` 区分 2/0），而是改了 kind 映射。**效果达标**，但「`result == 2` 的 Failure 演出」仍不可达——若产品预期是失败**演出**，而不只是结果面板文字，则还差一步。
- 第 18 项的修复点从调用点换到了 `ChartFileReader`——效果达标，但导入路径缺一条「零可玩音符」的专项用例（替换路径有，`SongPackageManagerTests:64-77`）。

### 验收质量

这一轮的实施不是"改完就算"：`AssetBundleLifetimeTests` 断言引用计数归零、`HcaBandTests` 逐样比对 PCM、`EditorPublicationTests` 模拟发布失败断言旧目录仍在、PaintKit 实跑报告 131 条缺失目标、Frame Debugger 抓 71 个事件确认黑底洋红标记、24 次冷启动探针把双监听器警告从 1109 条降到 0、EditMode 1510 通过 / PlayMode 32 通过。**方法上已经越过了"静态审查"能到达的边界。**

第十节引用的 13 份产物也逐个核过，全部存在（`cold.log` 6 KB、`measure-final.log` 15 KB、`memory-final.txt` 10 KB、`edit.xml` 1.29 MB、`play.xml` 50 KB、`events.txt` 11 KB、三张截帧 0.2–0.5 KB、`paintkit.log` 49 KB、`home-window.csv` 1.2 KB、`local-scan.json` 0.6 KB、Web 回执 1 KB）——不是口头声明。

### 一条必须提醒的事

这些改动**全部还在工作区未提交**（`ScreenshotSession.cs`、`PaintKit.cs`、`DialogBase.cs`、`ScreenManager.cs`、`ScreenStackService.cs`、`LiveSpriteButton.cs`、`JudgeTuning.cs`、`AssetManager.cs`、`AssetBundleManager.cs`、`HcaDecoder.cs`、`PlayService.cs`、`SongPackageManager.cs`、`OpenSekaiAssetBundleBuildPipeline.cs` 与 4 个 docs 文件均为 `M`）。`TestResults/` 下的采样数据与插桩同样未入库——下次清理测试产物时会被一并删掉。

## 十二、复核后的修复与验证

| 排期 | 处理 |
| --- | --- |
| 8 | 曲库和演奏场景都在资源目录准备完成后启动。单个 URI 请求有 30 秒时限，读取失败会报告具体资源。失败时停在可重试的提示，离开场景会取消该场景的等待。其他等待者可以继续使用同一次下载。浏览器实测覆盖首次请求返回 HTTP 503、再次请求成功、单个等待者取消、服务器挂起超时和缓存重开。[WebGL 回执](../../TestResults/review-followup-web-receipt.log) |
| 11 | 删除半秒定时器。音频运行时建立、场景载入、曲库创建监听器和演奏视图就绪时刷新缓存。运行中只检查缓存内组件的启用状态与对象存活情况。今后在场景载入后添加监听器的代码应在创建时调用 `SoundManager.NotifySceneAudioListenersChanged()`。 |
| 15 | 开局先显示读取提示并让出一帧，过场预制体通过 Unity 异步加载。在 Android 和桌面端，读包、谱面解析、转换和内容哈希在后台执行。转换用的 DPI、轨道判定宽度、长条连击间隔在主线程取值，音频注册、界面更新和开局发布也在主线程。单线程 WebGL 会先让出一帧，随后在主线程转换。 |
| 18 | 新增两个下载 ZIP 导入回归用例。它们分别使用仅含 BPM 的 `.sus` 和 `.txt`，检查导入报错、暂存目录清除和已有曲包保留。 |
| 21 | 修正模块职责表，将 `CutinManager` 和 `SkillTextView` 标为占位组件。 |

第 3 项按原排期验收零生命的结果文字和类型。演奏中耗尽生命继续走现有 LifeZero 演出。第 14 项保留两次 PNG 解码，预检逐张确认完整图像可以解码，再释放像素；写入时只持有当前图块和图集，避免把整批像素常驻内存。

### 开局真机复测

PLQ110、Android 16，使用原设备上的 DEINOS PHAINEIN 曲包副本（谱面 974,969 字节），每轮重启测试进程，保留曲库缓存。在曲库就绪后调用正式的自动开局入口。仅将读谱移到后台时，过场预制体的同步检查仍让入口运行了 56.67、56.00 毫秒才首次返回。改用异步加载预制体后得到下表。[中间版本第一轮](../../TestResults/review-followup-device-first.log) · [第二轮](../../TestResults/review-followup-device-second.log)

| 指标 | 最终版第一轮 | 最终版第二轮 |
| --- | ---: | ---: |
| 调用入口至发出读取进度通知 | 0.22 ms | 0.16 ms |
| 调用入口至首次让出主线程 | 0.75 ms | 0.71 ms |
| 调用入口至开始注册音频的总时间 | 277.12 ms | 295.44 ms |
| 上述期间记录到的主线程更新次数 | 45 | 45 |
| 上述期间最长采样间隔 | 15.19 ms | 26.65 ms |

两轮均从 APK 读出普通资源包并成功开局。第二轮截帧确认已进入自动演奏并产生分数。第十节的 114.7 / 120.6 ms 从旧版通过过场预制体检查后计时，覆盖曲包读取到音频注册前的同步工作。上表从调用入口计时，并将首次返回与跨帧的总等待分别记录。[最终版第一轮](../../TestResults/review-followup-device-final-1.log) · [最终版第二轮](../../TestResults/review-followup-device-final-2.log) · [真机演奏画面](../../TestResults/review-followup-device-play.png)

### 回归验证

常规 PlayMode 31 项全部通过，整轮双监听器警告为 0。监听器用例覆盖运行时添加后的同帧交接、组件启停、对象启停和组件销毁。另有两个曲库测试以前同时保留原页面和替换页面，这次在测试页接管时停用原页面。第十节 32 项中的额外一项是当时的 24 次冷启动采样。[场景测试结果](../../TestResults/review-followup-clean-play.xml) · [运行日志（已遮盖启动凭据）](../../TestResults/review-followup-clean-play.redacted.log)

EditMode 1516 项通过、21 项跳过、0 项失败。新增用例覆盖 URI 读取失败后重试与取消、下载空 SUS/TXT、读谱期间取消与离开曲库，以及从后台准备含长条和滑键的 SUS 后在主线程注册音频。代码规范检查覆盖 717 个 C# 文件，发现 0 项问题。[编辑模式结果](../../TestResults/review-followup-clean-edit.xml) · [规范检查](../../TestResults/review-followup-standards.log)

## 十三、二次修改复核

2026-11-20 对第十二节逐条核对源码与产物。**结论：第十一节提的 4 处全部闭合，#18 补的用例到位，23 个排期项至此全部落实**（其中 3 项本是采样任务，已完成）。

### 逐条

| 排期 | 第十二节声明 | 复核 | 证据 |
| ---: | --- | :---: | --- |
| 8 | 目录就绪后再启动、30 s 时限、报具体资源、可重试提示、按场景取消、共享下载不连带取消 | ✅ 6/6 | `LibraryEntryPoint:40` 与 `EntryPoint:19` 都 `await PackagedBundleStartup.WaitForReadyAsync`；`AssetBundleManager:147` 默认 `timeoutSeconds = 30`、`:151` `request.timeout` + `:152-153` `CancelAfterSlim` 双保险；`:164` `TimeoutException` 带 uri、`:167-168` 查 `request.result`；`PackagedBundleStartup:47-48` 弹「游戏资源加载失败 / 重新加载」并阻塞等 `_retry`；`:14` 用 `entry.destroyCancellationToken`；`:88/:97-102` 用单例 `AsyncLazy` 共享下载 |
| 11 | 删半秒定时器、四个刷新时机、逐帧只查缓存、提供 `NotifySceneAudioListenersChanged` | ✅ 4/4 | `CriAtomShim:1463-1483` 只剩 `OnEnable/OnDisable/OnSceneLoaded/LateUpdate/OnApplicationPause`，全文无 0.5 s 定时器；`:1474-1477 → :1399-1411` 逐帧只 `foreach _sceneListeners` 查 `isActiveAndEnabled`；`SoundManager:41-43` 提供静态通知入口，`LibraryView:115`、`LiveFrontView:141` 已调用 |
| 15 | 先提示并让一帧、预制体异步加载、四步后台化、主线程取值与注册 | ✅ 6/6 | `PlayService:102-103` 提示 + `await UniTask.DelayFrame(1)`；`:104` → `ScreenLayerData:158-163` `Resources.LoadAsync`；`:117-118` `await Task.Run(() => PrepareChart(...))`，读包/解析/转换/哈希全在委托内；`:113` 主线程预取 DPI、长条间隔；`:132-141` 音频与 `:162` 发布回到主线程（`PlayServiceDependencyTests:252-263` 断言同线程）；`:114-115` WebGL 分支直接在主线程转换 |
| 18 | 两个 ZIP 导入用例（`.sus`/`.txt`），查报错、暂存清除、曲包保留 | ✅ 2/2 | `ServerChartImportTests:182-203` 的 `[TestCase("sus")]`/`[TestCase("txt")]`；`:196-199` 断言 `InvalidDataException` 且消息含「没有可游玩的音符」、`:202` 扫 `.chart-import-*`、`:200` 断言原清单仍在 |
| 21 | 模块职责表标 `CutinManager`/`SkillTextView` 为占位组件 | ✅ | `modules.md:31` 已写「是特写和技能文字的占位组件」，与 `CutinManager`/`SkillTextView` 的空实现一致 |

### 证据链另核

第十二节引用的产物逐份核过，数据自洽：EditMode `total=1537 passed=1516 failed=0 skipped=21`、PlayMode `31 passed / 0 failed`、规范检查 `717 files, 0 findings.`；`play.log` 里含 "listener" 的只有 2 行 `AssetChangedListener` 耗时，**双监听器警告确为 0**；真机日志逐字段吻合（`readingNoticeMs=0.22 firstYieldMs=0.75 audioStageMs=277.12 framesBeforeAudio=45 maxIntervalMs=15.19`，末尾 `playback=Started`）；WebGL 回执里 `AssetBundleInfo.bytes` 先 503 再 200，`stall: response delayed for 5 seconds`，末行 `HTTP503=reported retry=ok cancelledWaiter=isolated stalledRequest=bounded warm=ok`。

### 6 处非阻塞残留（不改变"已落实"判定）

1. **#18 的"曲包保留"断言偏弱**：只断言清单文件存在，没像同文件 `InvalidDownloadLeavesExistingLibraryUntouched` 那样再断言 `LoadItems().Count == 1`，也没校验原 score/audio 仍在。
2. **#15 的预制体只是把首次 `Resources.Load` 移出首帧**，实例化仍在 `ScreenStackService:232/239` 同步执行——不过在音频之后、走预热缓存。
3. **#15 残留的主线程段**：`LiveBundleConfig.Load()` 是同步 `Resources.Load`（必须主线程）、设置快照首次读盘、`CalculateTotalComboCount` 的 O(n) 扫描、发布与 `PushLiveLoading`。
4. **WebGL 分支验不到**：`#if UNITY_WEBGL && !UNITY_EDITOR`，编辑器内跑不到该路径。
5. **#8 的重试提示文案是笼统的「游戏资源加载失败」**，具体 URI 只在 Console。
6. **一个测试交付形态**：#18 是「一个参数化方法出两条用例」，不是两个独立方法。

### 两处文档

- 第十节「另以 0.5 秒间隔发现其他新组件」已被第十二节删除，已在原句标注为历史记录，避免只读第十节的人误解。
- 本轮的改动仍未提交：除上一轮那 13 个源文件与 4 个 docs，又新增了 `PackagedBundleStartup.cs`（未跟踪）与 §十二 提到的若干测试。

### 二次复核后的补验与提交

针对残留第 1 条，空 `.sus` 和 `.txt` 下载分别被拒绝后，测试都从磁盘重新扫描曲库，断言仅剩原曲包，并逐字节比对其清单、谱面和音频。两条用例均通过。本次完整 EditMode 共 1537 项，1516 项通过、21 项跳过、0 项失败。PlayMode 和真机数据沿用第十二节的实测。[本次测试结果](../../TestResults/review-followup-final-import-edit.xml) · [运行日志（已遮盖启动凭据）](../../TestResults/review-followup-final-import-edit.redacted.log)

复核修复和测试已提交为 `8b43f6df`。16 份验证记录已提交为 `db65961a`，其中 5 份 Unity 日志各遮盖了一个启动凭据值，其余 11 份保留原始字节。WebGL 浏览器回执覆盖资源加载和重试；单线程读谱开局仍缺少浏览器端的直接验证。

## 十四、三次修改复核

2026-11-20 对第十三节的补验与提交逐项核对。**结论：残留第 1 条已闭合，两次提交属实且内容与声明一致；但有一处修正在工作区里没进提交。**

### 逐条

| 声明 | 复核 | 证据 |
| --- | :---: | --- |
| 两个用例都从磁盘重扫曲库、断言仅剩原曲包、逐字节比对清单/谱面/音频 | ✅ | `ServerChartImportTests:204` `manager.RescanItems()`、`:205` `Count == 1`、`:207` 同 `RootDirectory`、`:208-210` 分别 `ReadAllBytes` 比对 manifest/score/audio、`:211` `server-46` 不存在、`:212` 无 `.chart-import-*`；`:182-183` 两个 `[TestCase]`；`:203` 断言消息含「没有可游玩的音符」 |
| 完整 EditMode 1537 项、1516 通过、21 跳过、0 失败 | ✅ | `review-followup-final-import-edit.xml`：`total=1537 passed=1516 failed=0 skipped=21` |
| 修复与测试提交为 `8b43f6df` | ✅ | 提交存在，message「fix: finish asset startup and chart loading review follow-up」，**37 个文件**，含新文件 `PackagedBundleStartup.cs`（现已入库） |
| 16 份验证记录提交为 `db65961a` | ✅ | 提交含 **17 个文件** = 1 个 `.gitattributes` + **16 份记录**，message「test: preserve review follow-up device, browser, and regression records」 |
| 5 份日志各遮盖一个启动凭据值，其余 11 份保留原始字节 | ✅ | 5 个 `*.redacted.log` 各**恰好 1 行** `[REDACTED]`；抽查那份遮盖的正是 `-accessToken` 的值 |

### 一处未提交（需要处理）

**三个架构文档的修正在工作区里，没进任何提交**：

| 文件 | 未提交改动 | 最后一次提交 |
| --- | --- | --- |
| `docs/architecture/judging-design.md` | 14 行（7 改） | `d7055830` 11-19 20:34 |
| `docs/architecture/modules.md` | 16 行（+14/−2） | `a3810269` 11-17 16:17 |
| `docs/architecture/native-next-rush.md` | 2 行新增 | `7f894792` 11-20 08:18 |

第十二节 #21 的声明（「修正模块职责表，将 `CutinManager` 和 `SkillTextView` 标为占位组件」「修订普通尾评分时钟」「补充各谱面格式的技能和 Fever 支持范围」）在代码与文档内容上**都已做到**，但只存在于工作区——`git status` 里这三个文件仍是 `M`。这意味着一份干净检出看不到这些修正。

建议单独提交一次 `docs:`，把这三个文件带上；本方案文档（`code-review-2026-11-20-final.md` 与 `archive-review-2026-11-20/`）目前也未跟踪，可与之一并入库。

### 一条仍然成立的开放项

第十二节末句「单线程读谱开局仍缺少浏览器端的直接验证」与第十三节残留第 4 条一致：`#if UNITY_WEBGL && !UNITY_EDITOR` 分支在编辑器里跑不到，WebGL 回执覆盖的是资源加载与重试，不含读谱开局。

需要说明的是**浏览器端不是发行目标**：ADR-0006 定的是 Android 唯一发行平台（IL2CPP、仅 ARM64、minSdk 26、targetSdk 36），`Builds/` 下也只有 Android 产物。这里的 WebGL 是一个**为验证临时构建的测试脚手架**——`review-followup-web-build.log` 里 `Targeting platform: WebGL`，产物落在 `.utmp/review-nov20-web/WebOut/`（不进 `Builds/`），用 headless Chrome 配合本地 http server 跑；全仓只有 `PlayService.cs` 一处 `#if UNITY_WEBGL`，就是为它加的「单线程在主线程转换」分支。

因此这条的处置有两条路，二选一：

1. **保留该分支**：在现成的 `.utmp` 脚手架上跑一次读谱开局即可闭合，不必新建目标；
2. **不保留**：WebGL 既然不发行，删掉 `#if UNITY_WEBGL` 分支，这条随之关闭。

复核后已按第 2 项处理（2026-11-20）。`PlayService.StartAsync` 已删除 WebGL 专用的同步读谱分支，统一通过 `Task.Run` 准备谱面。删除后的代码与原 Android 和编辑器选用的路径逐行一致。第十三节残留第 4 条随之关闭。



