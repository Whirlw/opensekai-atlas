# 第三方复核的逐条答辩与裁决

2026-11-20。第三方对 [多智能体代码审查报告](code-review-2026-11-20.md) 做了[逐条复核](code-review-2026-11-20-verification.md)，指出部分结论不一致。本文把被质疑的条目发回原模块审查员重新对账，逐条判定：**维持**、**修正**（原句部分字对部分字错，给替换句）、**撤回**（原判错误）、**待证**（静态代码不足以下结论）。裁决只以当前工作区源码为准——既不为原报告辩护，也不无条件接受第三方意见。本轮没有任何条目需要**升级**。

一句话结论：**第三方的算术与事实几乎全对。** 75 项模块争议中维持 16、修正 56、撤回 3，另有 41 条严重度下调（含 19 条 P1 降级）；横向模式里再撤回 2 条（`ColorFader`、`CustomButton`）。但同时有 5 处是第三方**判轻或判错**——它把"可达但未触发"当成"不存在"，把"可由代码顺序直接判定"当成"待证"——这些我方驳回。原报告自身还有 5 处数字与措辞错误需要修（见第五节）。

> **第三轮更新**：M11-7 已整行撤销，本表口径随之变为**维持 16、修正 55、撤回 4**（降级仍 41，其中原 P1 19 + 原 P2 22）。最终数见 [第三轮复核的再对账](code-review-2026-11-20-round3.md)，本文表格保留为第二轮时的快照。

## 一、裁决总览

| 模块 | 质疑条数 | 维持 | 修正 | 撤回 | 严重度下调 | 备注 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| M01 Core 领域层 | 6 | 2 | 4 | 0 | 2（P2→P3） | 无 P1 被质疑 |
| M02 App 服务层 | 6 | 2 | 4 | 0 | 3（2×P1→P2，1×P2→P3） | 横向模式 E 两例确系我方误读，已更正 |
| M03 曲库界面 | 5 | 0 | 5 | 0 | 4（2×P1→P2，2×P2→P3） | 含 1 处硬误报（死参数） |
| M04 首页与服务器 | 7 | 3 | 4 | 0 | 0 | 第 6/9 项第三方判轻了 |
| M05 演奏核心 | 12 | 2 | 9 | 1 | 7 | 唯一 P1 反而更硬；仅第 3、4 项维持 |
| M06 Live 视图 | 4 | 2 | 2 | 0 | 2 | 第 3 项第三方判软了（驳回） |
| M07 页面与控件 | 8 | 0 | 7 | 1 | 7 | 8 项里 0 项维持、7 项修正、1 项撤回 |
| M08 平台与资源 | 5 | 2 | 3 | 0 | 3 | 第 3/4 项维持 P1（驳回） |
| M09 数据与共享 | 6 | 0 | 5 | 1 | 4 | 第 5 项引用了说反的文档 |
| M10 音频动画渲染 | 6 | 1 | 5 | 0 | 5（1×P1→P2，4×P2→P3） | 原报告的模糊修法会制造新缺陷 |
| M11 测试套件 | 5 | 1 | 4 | 0 | 3 | 六类假象里撤一类 |
| M12 编辑器工具 | 5 | 1 | 4 | 0 | 1 | 第 5 项数字全换 |
| **合计（12 模块）** | **75** | **16** | **56** | **3** | **41** | 撤回 3 条 = M05-9、M07-7、M09-5；其中原 P1 降级 19 条。第三轮 M11-7 整行撤销后 → 修正 55、撤回 4 |
| R00 报告自身核算 | 5 | 4 | 1 | 0 | 0 | 此行口径不同：维持＝第三方对我方报告的指控成立 |

表里 M01–M12 的「维持」指原判站得住、「修正」指原句部分成立需替换表述、「撤回」指原判错误。R00 一行是另一层——它核的是报告自己的数字，所以「维持」表示**第三方对我的指控成立**（4 项），「修正」表示它的前提有误（1 项）。

除表内 3 条整条撤回外，第四节还有 2 条**横向模式级**的撤回（`ColorFader`、`CustomButton`），以及 3 处**子项级**撤回（M03 的「`keepScrollPosition` 从未使用」、M11 的「陈旧报告」与「输入适配零覆盖」、M12 的「永远通过」）。

## 二、M01–M05 逐条裁决

### M01 Core 领域层

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P2 两套 bar→tick 公式，非 4/4 谱面小节号错位 | 部分成立 | **修正** | P2 |
| 4 | P2 ActivationTime 退化，音符在判定线上凭空出现 | 未证实 | **修正** | P3 |
| 5 | P2 越界枚举直接抛异常、错误被吞成通用提示 | 部分成立 | **修正** | P3 |
| 6 | P2 开局 O(音符×连线) Find；SUS 每格 2–3 次分配 | 部分成立 | **修正** | P2 |
| 7 | P2 `filterBeforeTicks` 是死参数且方向相反 | 部分成立 | **维持** | P2 |
| 9 | P3 三项（反编译残留／下划线参数／迁移分支不可达） | 部分成立 | **维持** | P3 |

要点：

- **第 2 项**：真正错的是**标签**不是时间。进局时 `RuntimeChartConverter` 会删掉全部 `TimeSignature` 事件再强塞 4/4，`ticks / TICKS_PER_BAR` 打帧标签，两端共用同一套 4/4 归一化，所以音频时间不错位；非 4/4 谱面偏离真实小节的是暴露给模型的 `(bar, barProgress)`，且当前没有消费者读它。替换句：*两套 bar→tick 公式并存（TS 感知版只用于 importer），非 4/4 谱面的 (bar, progress) 随拍号累积偏离，而音频时间因 ticks 按拍计仍正确。*
- **第 4 项**：三条回退路径取的都是「更早」的一侧（首标记时间 / 标记起点 / 把坐标当时间），后果是**过早创建视图对象**，不是判定线上凭空出现；要构造出「晚于可见时刻」需要首个标记带大于 preempt 的正 skip，与「全反向变速谱」这个普遍表述不匹配。降 P3。
- **第 5 项**：字符串越界名在 `ChartJson.Read` 就抛 `JsonException`，数字越界值到转换阶段才由 `default` 分支抛 `ArgumentOutOfRangeException`（事件路径连参数都不带）。非法输入被拒绝是合理行为，剩下的只是 `PlayService` 的 `catch (Exception)` 只记 Message 并弹通用文案——诊断不可定位，降 P3。
- **第 6 项**：`ConvertNoteArray → ToNoteBase` 沿 `nextConnectionId` 走链，每步对整张 noteArray 做 `List.Find`，且这段在 `StartAsync` 的同步段里跑，维持 P2。SUS 侧改为「至少一次 `Substring`，带宽度列再 +1 次 `ToString`」——原「每格 2–3 次」是无采样的定量。
- **第 7 项维持**：生产入口 `PlaySession` 与 `RuntimeChartLoader` 都是三参调用，仓库里没有任何调用方传第五个实参；两条过滤方向一边 `GetEndTicks(note) >= 阈值`、一边 `ticks < 阈值`；更硬的一条是它写入的 `MusicScore.FilterBeforeTimeSec` 全仓只有赋值、没有读者。第三方「用途是否允许不同需定义」不能解释一个从未接线的参数。
- **第 9 项维持**：`OpenSekai.Core` 有 51 处手写 `[CompilerGenerated]`；`RuntimeChartConverter.Convert` 的公开参数是 `_document`/`_showPair`，而项目自己的 `03-naming.md` 写明对外接口不带下划线，所以这不是个人口味；迁移分支只剩负版本号可达（写入方恒为 1）。

### M02 App 服务层

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 Sonolus 索引一条坏记录锁死全部安装 | 部分成立 | **修正** | P2 |
| 3 | P1 开局同步解析 + 重复 SHA-256 | 部分成立 | **修正** | P2 |
| 5 | P2 工作线程回调 progress | 成立 | **维持** | P2 |
| 6 | P2 SUS 转换结果被丢弃 | 部分成立 | **修正** | P2 |
| 7 | P2 BackupService 三缺陷 | 部分成立 | **维持** | P3 |
| 8 | P3 哈希失败空串致成绩静默丢弃 | 部分成立 | **修正** | P3 |

要点：

- **第 2 项**：「锁死全部安装」成立且静态可证——`PreparePublish` 先 `RecoverPublication()`，再严格 `ReadIndexFile()`，任一坏记录抛 `InvalidDataException` 并转成 `SonolusException`，全仓没有重置/备份/逐条容错入口。不成立的是另两半：`CommitPublication` 里的宽松 `LoadIndex + SaveIndex` 在正常调用链上永远走不到（同一次发布的严格重读先抛），而且那条分支只 `Remove` 一个键、不是「覆写成只剩一条」；「重复导入」也不成立（重导必然再失败）。损坏只能来自应用外（写入走 `AtomicFile`），故降 P2。
- **第 3 项**：「重复 SHA-256」只对一半：普通曲包每次开局无条件重算一次（缓存里本来已有 `ScoreKey`），而历史键走 `ScoreKeyOf` 缓存命中就不再算，只有缓存无键（库外曲包）时同一次调用才会算两遍。文案确实先回调，但同步段不让帧，Canvas 不重绘，效果上「要等同步工作结束才显示」成立。有加载遮罩、无 ANR，降 P2。
- **第 5 项维持**：解压整个包在 `Task.Run` 内执行，`SafeZipExtractor` 在拷贝循环里逐次回调，方法注释只写了「off-thread」没声明 progress 的线程。今天没有实害（唯一消费端用 `Volatile.Write`），但这是公开 API 的契约缺口且入口在线。
- **第 6 项**：SUS 分支在 `Task.Run` 里被故意跳过校验，主线程第 296 行单独转换却只丢弃返回值，所以「能解析出零可玩音符」的谱面会被装进曲库，直到开局才抱怨。但「任意非法谱面都能装」过宽：语法错误仍抛异常。改措辞、保 P2。
- **第 7 项维持但降 P3**：三条缺陷全部核实（同步 zip 读写、静态回调槽永不置空、备份失败后临时目录不删——恢复侧另有 `finally`，泄漏只在备份侧），全仓仍无调用方，按休眠代码处理。
- **第 8 项**：哨兵共用成立、静默丢弃成立，但曲库曲包的键优先取缓存，命中就不重算，「丢失」需要缓存无键且重算再次失败，属条件式，P3。

**横向模式 E 的两处更正在此确认成立**：`ServerRegistry.Changed` 是 `public event Action`，三处触发全部走 `private void RaiseChanged() => Changed?.Invoke();`，原报告把「三处调用 RaiseChanged」误读成三处直接 Invoke；`ServerScreen.PlayRequested` 是 `Action<PlayRequest>`，四个参数被结构体打包，正是 `04-events` 推荐写法。这两例应从 E 中撤下，替换为真实违规——`ServerScreen.BackRequested?.Invoke();`（无 `Raise` 包装）与 `ServerListPage` 只订阅并直接 `Rebuild` 的 `OnRegistryChanged`（不符合「订阅者类名_事件名」）。

### M03 曲库界面

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 1 | P1 打开曲库逐首主线程全量解码（约 265ms/首） | 部分成立 | **修正** | P2 |
| 2 | P1 搜索逐键全量重建 | 部分成立 | **修正** | P2 |
| 3 | P1 缩略图主线程解码，与类注释不符 | 部分成立 | **修正** | P2 |
| 5 | P2 响度 Stop/Start 可并发 | 部分成立 | **修正** | P3 |
| 9 | P3 死参数 + 死代码 | 一项误报一项成立 | **修正** | P3 |

要点：

- **第 1 项**：定量证据链断裂——「约 265ms/首」引自 `PackageAudio` 注释，而它引用的 `SoundManager.LoadFMODSound` 在全仓只存在于那条注释里（项目音频后端是 CRI Atom），扫描路径也不经 `RegisterPackageAudioClip`。「每首未测响度→一次主线程全量解码」与「隐藏后不停」仍成立（`OnDisable` 只 `SetVisible(false)`，不停扫描），降 P2 并去掉那个数字。
- **第 2 项**：「全量重建」名不副实：单元格虚拟化并复用池，只重绑可见区；「详情封面反复取消重开」也是错的——`LoadAsync` 按 key 去重、命中缓存即返回。成立的是：逐键 `SetSearch → Render`、整表重算、滚动复位、可见格重播入场动画、无防抖。
- **第 3 项**：「与类注释不符」是误读：注释只承诺 file reads / downscale / palette 在工作线程，这三步确实在 `RunOnThreadPool`，从未承诺解码/编码离线。真实问题是首次生成在主线程 `LoadImage` + 整图 `GetPixels32` + `EncodeToPNG`，属一次性开销，降 P2。
- **第 5 项**：「Stop 立即 Dispose 正在使用的 CTS 并置空 `_run`」逐字成立，是真实脆弱点；但唯一 `Stop → Start` 是 `PlayAsync`，中间隔着整段开局 `await`，旧趟在下一个样本前就退出，降 P3。
- **第 9 项**：前半句是**硬误报**——`keepScrollPosition` 在 `VirtualListView.cs:82` 参与分支，`SonolusBrowsePage.cs:437` 还显式传 `keepScrollPosition: before > 0`。后半句成立：`LibraryUi.CreateButton` 全仓无调用。保留 P3 只剩死代码部分。

### M04 首页与服务器界面

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 4 | P2 刷新后榜单/社区不重载，`_board` 索引错位 | 条件成立 | **维持** | P2 |
| 5 | P2 三个 UI 类各自 new client 并自管磁盘缓存 | 部分成立 | **修正** | P2 |
| 6 | P2 封面队列无上限、回收后仍下载并每帧自旋 | 部分成立 | **维持** | P2 |
| 7 | P2 只捕获 `SonolusException`，页面永久停在加载态 | 条件成立 | **修正** | P2 |
| 8 | P2 同步 `File.Exists`；7×1024² 常驻 + 28MB 瞬时 | 部分成立 | **修正** | P2 |
| 9 | P2 开屏尾段仍 `ApplyEnter` 导致卡片回弹 | 条件成立 | **维持** | P2 |
| 10 | P3 事件未走 Raise*（六处）；取消助手复制 6 份 | 风格成立（说是五处） | **修正** | P3 |

要点：

- **第 5 项**（我方修正）：`ServerScreen`、`ServerAddPanel`、`RemoteCovers`、`SonolusPreviewPlayer` 四处都 `new SonolusClient`（后两者每次下载/试听新建），但「自管磁盘缓存」只有 `RemoteCovers` 一家；安装与其余磁盘 I/O 都在 `Infrastructure/Sonolus`。原句把「四处构造」与「一家缓存」混成了「三个类各自管缓存」。
- **第 6 项维持**：`_pending` 表无容量上限属实，并发另有 `MAX_PARALLEL = 4` 但原文说的是「队列」不是并发；格子 `CancelCover` 只取消 `AttachExternalCancellation` 的等待，共享下载用 `_lifetime.Token` 继续跑完并落盘；`RemoteCovers` 只在 `ServerScreen.OnDestroy` 里 `Dispose`。三处均可静态证实，第三方的 `MAX_PARALLEL` 是补充不是反证。
- **第 7 项**（我方修正）：只 catch `SonolusException` 的收尾不是三处而是**六处**（BrowsePage 302/458、LevelPage 389/845/1010、ServerAddPanel 192），安装下载另有 `catch (Exception)`。后果要分档：社区/排行榜标签页 `ShowLoading` 之后没有非 `SonolusException` 的失败出口，且 `_commentsStarted` 让重启提前返回，确实永久停在「正在加载…」；浏览页与详情页顶栏有刷新图标，属卡到手动刷新。
- **第 8 项**（我方修正）：前半句成立（每次格子绑定都 `File.Exists`）；后半句的 1024/28MB 被当成无条件事实是错的——`TextureSize()` 按屏幕算，`needed > 560f` 才取 1024，多数手机取 512（约 7MiB）。
- **第 9 项维持**：`HomeSplash.Evaluate` 每帧无条件 `ApplyEnter(time - HOME_START)`，第 404 行从 3.9s 起把 home 设为可交互，而 `OpenLibrary`/`OpenServer` 只查 `_navigating`、不查 `_splash.IsPlaying`，于是 3.9–4.4s 这个窗口内起 `PlayExpand`，`ApplyEnter` 的 `if (_expansion != 0f) ApplyExpansion(0f)` 每帧把 amount 清回 0。第三方的「若」可以去掉。
- **第 10 项**（我方修正）：`StateChanged` 是 event（不是可写属性），直接 `?.Invoke` 的是**5 处**不是 6 处；但「取消助手复制 6 份」反而准确（含 `HomeScreen.CancelJacketLoad`）。P3。

### M05 演奏核心

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 1 | P1 长条尾键按「最晚时间」重推 | 部分成立 | **修正** | P2 |
| 2 | P1 `PauseLive` 不取消复活倒计时 | 部分成立 | **修正** | P2 |
| 3 | P1 生命耗尽被记为完成并写盘 | 部分成立 | **维持** | P1 |
| 4 | ✔✔✔ `OnApplicationPause` 用 `new` 隐藏基类虚方法 | 成立 | **维持** | P3 |
| 5 | P2 判定时钟双源 | 部分成立 | **修正** | P3 |
| 7 | P2 `Canceled` 手抬被吞、`TryFinish` 死路 | 部分成立 | **修正** | P3 |
| 8 | P2 普通尾用触摸样本时间（与文档不符） | 代码成立，缺陷待证 | **修正** | P3 |
| 9 | P2 条头不计入按下额度，可多判一个头 | 原因不成立 | **撤回** | 撤销 |
| 10 | P2 静默 `Done` 导致视图不回收 | 主要后果误报 | **修正** | P3 |
| 11 | P2 `NoteSpeedRatio` 全扫，唯一越打越慢的热路径 | 部分成立 | **修正** | P2 |
| 12 | P2 技能/事件链整条空转 | 部分成立 | **修正** | P3 |
| 13 | P3 `OnAwake` 死分支；`AdvanceAutoJudgment` 按值传游标 | 部分成立 | **修正** | P3 |

要点：

- **第 9 项撤回（唯一完全撤回）**：`Consumed` 里的 `is NormalNote or FlickNote` 只决定要不要 `RecordKeyPress`（留给抬手的快速点按限制），后面的 `RecordJudgedTouch`、`_usedFingerList.Add`、`_frameUsedFingers.Add`、`SilencedTouches.Add` 对所有已消费候选**无条件执行**——条头按下同样扣掉本帧额度。同指重复 `Began` 是否多判由「波次」决定（间隔 < 1/60 s 并入同波次共享额度），且已有用例断言跨波次的两次按下各判一个音。原判错在把那段类型判断当成了额度逻辑。
- **第 3 项维持 P1，且比原来更硬**：逻辑层生命 ≤ 0 只置 `LiveLogic.Result = 2` 并 `RaiseFailed`，控制器 `OnFailed` 只校正时钟，控制器自己的 `result` 仍是 0；终曲 `PreExit` 把它改写成 3，`TryAppendPlayHistory` 要求 `result == 3` 于是照写，`PublishPlayResultForLibrary` 因 `result == 3` 判 `Completed`，曲库面板对 `Completed` 显示 `CLEAR`。`result` 的四个赋值点里 1/2 都伴随 `_retiredByPlayer = true`，`Failed` 分支确实不可达。三方给的反而都是支持性事实。
- **第 1 项**：「规律性」与「1 tick 条」两处表述不成立——自动补点会跳过条自身链占用的网格位并一律 `SetSkip(true)`，隐藏/装饰/摩擦隐藏点已被 `IsTailCandidate` 排除，一 tick 条头尾时间差 2 ms 不构成平局。真实可达路径只剩「同时间的非排除候选排在终点之后」（NextRush 作者隐藏点走 `AddHoldCombo` 且不置 `Skip`，加上 `List.Sort` 对等时键不稳定），属作者谱面形状触发，降 P2。
- **第 2 项**：`PauseLive` 漏取消 `_resumeCoroutine` 属实，但两个后果断言都不成立——`ResumeCountDown` 期间 `OnResume` 已 `Hide` 暂停框；而 `PauseLive` 的唯一产品接线 `ConsecutiveAutoLiveView.Show` 在仓内零调用，按钮永远不可见。降 P2 的潜伏缺陷，一处接线即复现。
- **第 4 项**：隐藏虚方法属实，但基类这条链只翻一个全仓无人读取的 `_isPause`，`OnSystemPause` 也无重写，当前零运行时后果，按隐患记 P3。横向清单应把「隐藏虚方法」按「基类钩子是否承载状态」分级，而不是一律标最高确信度。
- **第 5 项**：两个时钟字段确实都存在且都 public 可变，Notes 读静态，但每帧由 `LiveClock.Sample` 一次写入同一值，中间没有第二个写点，找不到可分歧路径。结构冗余，P3。
- **第 7 项**：`Canceled` 被当成「系统取消≠有效抬手」是有意语义，且已有用例 `ASystemCancelLeavesAnOrdinaryTailToExpire` 显式要求取消后等超时判 Miss；普通尾实由 `JudgeTailAsRelease` 结算，`ReleaseTail.TryFinish` 因 `ReleasedInside` 恒 false 且被调用点跳过而成为死代码。不是断链，降 P3。
- **第 8 项**：事实成立（按抬手样本时间结算，与 `judging-design.md:317` 的「当前帧」冲突），但行为有回归用例锁定且更精确——`ABufferedReleaseIsGradedAtItsTouchTime` 明确要求晚到 60 ms 的帧仍按抬手样本给 JustPerfect。**该改的是文档，不是代码**，降 P3。
- **第 10 项**：绕过 setter 属实，但装饰单键与条身随后显式 `_presentation.Unspawn`；内部 combo 点不在 `ViewNoteList`、`OnSpawnNote` 也因父条列表不含它而提前返回，没有独立视图可漏。真正该记的是「退役靠调用点手工补偿」的约定风险，P3。
- **第 11 项**：机制成立（`Resolve` 每次从 `scoreInfos[0]` 起遍历、无游标、条子点也逐点调），但「唯一」「越打越慢」是绝对表述——播放头之后的音符提前返回，原生滚动走常量，且遍历的是 info 表不是音符表，未采样。保 P2。
- **第 12 项**：空转属实，但 `ChartDocument.Migrate` 在迁移时就把 `IsSkillOrFever` 事件整体滤除，技能数据根本进不了本局，属支持范围与文档声明问题，不是功能回归。P3。
- **第 13 项**：`OnAwake` 判空在「先 `AddComponent` 后 `Initialize`」下是死分支，仅「先初始化未激活对象」才会双启；`AdvanceList` 按值传游标属实，但持久游标由 `WalkNotes(ref)` 推进，按值重走是为重访条内点与尾，属有意设计。P3。

## 三、M06–M12 逐条裁决

### M06 Live 视图

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 1 | P1 封面色贴图永不释放，且与背景重复解码（✔✔） | 成立，有所有权边界 | **修正** | P2 |
| 3 | P1 `LiveSpriteButton` 不处理 `Canceled`，按钮整局失效 | 条件成立，「整局」取决于生命周期 | **维持** | P1 |
| 10 | P2 `OverlayMask` 可写序列化字段 + 死组件 + 材质不释放 | 部分成立，不能凭无 C# 调用宣布可删 | **修正** | P3 |
| 11 | P3 反编译状态机残留、`Reset()` 与 Unity 消息同名 | 成立（清理项） | **维持** | P3 |

- **第 1 项降 P2，理由是「永不」说过了头**：`LiveMediaFiles` 每次都 `new Texture2D`，`Background2DView` 销毁自己那份而 `MusicInfoView` 没有，重复解码也成立；但离场走 `Core → TransitionBlank`，`SceneTransitionBlank` 会调 `Resources.UnloadUnusedAssets`，泄漏被场景切换兜住。方法论教训记在这里：**定 P1 之前先找全局回收兜底**。
- **第 3 项维持 P1（我方驳回第三方的软化）**：`NativeInput` 确实会产生 `Canceled`，而按钮只在 `Began`/`Ended` 分支动作；关键是第 114 行的闸门是 `if (_touchId == _disableId && …)`——判断的是「当前没有手指占位」，不是「同一根手指」。所以一次系统取消留下的陈旧 `_touchId` 会挡掉**任意手指**的后续按下，而复位只有 `OnDisable`；`FrontUIView.prefab` 里暂停键的 `SpriteRenderer` 与 `LiveSpriteButton` 挂在同一 `GameObject` 上，只有 `ShowSkip`/`HideSkip` 才会 `SetActive`，普通一首歌内不会复位。第三方把它软化成「可能」不成立。
- **第 10 项降 P3**：三条子断言全对（自建材质无 `OnDestroy`、setter 直写 `[SerializeField]` 字段、`throw` 与泄漏风险确实存在），但 GUID 在全部 `.prefab`/`.unity`/`.asset` 与 C# 侧零命中——组件永不实例化，风险不落地。这一条也回应了第三方的保留：**该用 GUID 全量检索定案，而不是挂成「须再核 prefab」**。
- **第 11 项维持 P3**：死枚举器的 `MoveNext()` 恒 `false` 且只挂在 `[IteratorStateMachine]` 上，真协程是 `CountdownCore`，所以只误导调试器；`Reset()` 是私有无参，编辑器会当 Unity 消息调，但数组未序列化恒为 `null` 提前返回，运行时无影响。

### M07 页面框架与 UGUI

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 每次点击全场景 Find，静态 0.2s 节流吞连点 | 部分成立 | **修正** | P2 |
| 3 | P1 `ScreenManager` 8 个 public 方法体是 `throw null` | 桩成立，应为 10 个 | **修正** | P3 |
| 4 | P1 `OnDisable` 取消关闭动画 → `OnClose` 永不触发 | 条件成立 | **修正** | P2 |
| 5 | P2 三处热路径开销 | 部分成立 | **修正** | P3 |
| 7 | P2 `ScreenLayerLiveLoading` 订阅累积 | 误报 | **撤回** | 撤销 |
| 8 | P2 子序号当按钮序号；长按用错 Root | 条件成立 | **修正** | P3 |
| 9 | P2 `GraphicButtonTapEffect.Awake` 写透明色 | 条件成立 | **修正** | P3 |
| 11 | P2 多处整体改写 UI 相机 `cullingMask` 且不还原 | 部分成立（改判对象） | **修正** | P3 |

- **第 7 项撤回**：`WhenCovered` 是一次性注册——`InvokeCovered` 先取走 `_onCovered` 再置空，`Hide`/`Release` 也清空，每次 boot 注册一次就被消费一次，不存在叠加（这条我自己也核过 `LibraryLoadingOverlay.cs:314`）。
- **第 2 项降 P2，但两面都成立**（这是我们**驳回第三方「部分成立」**的一条）：19/21 个 `CustomButton` 的序列化 `interval` 是 `Use`，而全仓没有 `InputManager` 实例，`FindFirstObjectByType` 必然返回 null，于是**静态共享的 0.2 秒节流是唯一路径**——不同按钮在 200 ms 内的连点必定被吞。它只是全局防抖，不到 P1，但不是「部分成立」。
- **第 3 项降 P3 并改数量**：`489-536` 共 **10 处** `throw null;`（含 `ShowBackground` 三重载），且这 10 个方法全仓零调用方——是未完成的 API 面，不是当前缺陷。
- **第 4 项降 P2**：机制与后果都实——取消 tween 就丢掉 `Destroy`，`OnClose` 只有 `FinishCloseWithoutDestroy` 一条路，`_dialogQueue` 与模糊都靠它释放；但仓内没有任何调用方在关闭动画期间禁用对话框（`Sleep` 路径先走 `FinishClose`），触发路径未证。
- **第 5 项降 P3，三段各自缩水**：每点击 Find 成立；`CustomTextMesh` 的 `LateUpdate` 先判 `IsTextChanged`/`IsRectSizeChanged`，`preferredWidth` 不是每帧；`AlphaMask` 的每帧 `new Vector3[4]` 与材质不 `Destroy` 成立，但组件全仓零引用。
- **第 8 项降 P3**：子序号显隐只在层内夹杂非按钮节点时错位，而三个 dialog prefab 的 `FooterButtons` 子节点就是纯按钮；长按借 `followEffect.Root` 是因为 `TouchLongTapEffect` 根本没有 `Root` 属性——那是唯一可用锚点，不是明确写错。
- **第 9 项降 P3**：只有 `refreshColorWhenAwake=false` 且外部未赋色时首帧才写透明；唯一挂该组件的 `ScreenLayerHeader.prefab` 里该值为 1，`SetDefaultColor` 也无调用者，现无实例受影响。
- **第 11 项降 P3 并改对象**：原报告点名的 `UIBlurLayerManager`、`CameraFOVUpdater` **完全不碰 `cullingMask`**；真正整体改写的是 `ScreenBlurController`（第 390 行），对象是它自有的 UI 相机，而场景里该相机 `m_Bits = 32` 本就只有 UI，改写结果与原值等价。点名的对象与「不还原的后果」两处都不成立。

### M08 平台层与资源

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 HCA 128 频带读 `ScaleFactors[128]` 越界崩溃 | 成立，触发条件比原文窄 | **修正** | P2 |
| 3 | P1 朴素 IMDCT + 整曲 `float[]` + 主线程同步 | 部分成立 | **维持** | P1 |
| 4 | P1 每帧与每次播放各扫 `AudioListener` 1–3 次 | 部分成立 | **维持** | P1 |
| 5 | P1 平台与资源目录 100+ 处 `throw null` 桩 | 成立，严重度依可达性 | **修正** | P3 |
| 6 | P1 下载 API 不下载却回调 `Success` | 部分成立 | **修正** | P3 |

- **第 2 项**：越界发生在**第 533 行**（原文与第三方都指向 532 的 `for` 行），读侧表达式是 `ch.ScaleFactors[csCount - i]`，`ScaleFactors` 长度就是 128（`:1053` 定义），`csCount == 128` 且 `i == 0` 时读下标 128；写侧 `SAMPLES_PER_SUBFRAME - 1 - i` 出界不了。前置是 v3.0 + 非次立体声 + `hfrGroupCount > 0` + 合计恰为 128。抛出的是 `IndexOutOfRangeException`，且 SE/BGM/预载入口都 `catch`，只有 `BuildTimelineSpan` 链路未捕获——后果是**中断这一局开唱**，不是进程崩溃。修法：读侧改 `csCount - i - 1`。降 P2。
- **第 3、4 项维持 P1（我方驳回第三方对严重度的削弱）**：第 3 项三个事实都成立——每子帧每声道一次 128×128 全乘、整曲单个 `float[]` 69 MB、在 `MusicReady` 协程里同步跑完；第三方「是否命中该解码器」是可达性问题，不是事实错误。第 4 项实测：**成功路径**（`clips` 非空且至少一个非 null）的 `Play` 为 3 次（`clips` 为 null/空数组在 `:750` 返回 0 次；非空但全为 null 在 `:766` 返回 1 次），守卫挂上后每帧有外部监听器 1 次、无外部 3 次，且每次 `FindObjectsByType` 都新建结果数组，正落在 `GCMode.Disabled` 的打歌窗口内。
  **（第三轮复算更正，原文数字偏小）**：矩阵部分应为 **22.12 亿 MAC**（135,000 块 × 16,384 乘加），把乘、加分开计才是 44.24 亿次运算——原文「4.4e9 MAC」**多算一倍**；扫描次数是一次 `Play` **3 次**、守卫每帧 **1 次或 3 次**——原文「1 次 / 2 次」各**少算一次**。
- **第 5 项降 P3，但计数要往上修**：实测 `Platform/` + `Assets/` 合计 **192 处**（`CP` 53、`LruAssetCache*` 66、`InputManager` 31、`UiEffect` 26、`AssetBundleDownloadHandler` 16），全工程 322 处——原文「100+」偏少，第三方给的 192 精确吻合。降级理由是不可达：五族无任何调用点，`InputManager` 的 GUID 未被任何 `.unity`/`.prefab` 引用，调用点还自带 `try/catch`。这从「P1 缺陷」变成「死代码清理项」。
- **第 6 项降 P3 并收窄**：只有 `StartCategoryDownload` 无条件回 `Progress = 1f, ResultCode = Success`；单包 `DownloadAssetBundle(Async)` 会按 `CheckNeedsDownload` 区分 `Error`/`Success`，原文断言过宽。更要紧的是这组下载 API 全仓零调用点（项目只有 StreamingAssets 加载路径）。

### M09 数据与共享纯逻辑

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 `JudgmentAggregator` 把 `Pass` 当真实判定，吞掉同帧真判定弹窗 | 算法成立，玩家路径待证 | **修正** | P3 |
| 3 | P1 `SongPackage.VideoPath` 返回目录，交给 `VideoPlayer` | 播放器故障误报 | **修正** | P3 |
| 4 | P2 路径属性每次读取都做文件系统探测，UI 绑定逐格触发 | 部分成立 | **修正** | P2 |
| 5 | P2 `LiveContext` 直接读全局 `UserDataManager`，与 `modules.md` 相反 | 部分成立 | **撤回** | 撤销 |
| 6 | P2 `LiveTrace` 热路径逐行 flush；`LatencyCapture` 常驻 31MB | 部分成立 | **修正** | P3 |
| 8 | P2 `Data/` 下的 MonoBehaviour UI 单元格 | 部分成立 | **修正** | P3 |

- **第 5 项撤回（第 2 条完全撤回）**：`modules.md` 第 48 行写的就是「由 `LiveContext.FromRetainedBoot()` 读出来交给控制器」——这是文档明文规定的**写槽-读槽边界**，不是违规；而且 `UserDataManager` 本身就在 Game 程序集内，不存在跨程序集逆向依赖。原句「与 `modules.md` 声明的边界相反」引用了恰好说反的文档，属于我方误读。
- **第 2、3 项都是「事实成立、后果被上游闸门截断」**：`IsRealResult` 确实没排除 `Pass`，但 `ResultPublication` 先有 `if (!point.HasJudgment) return;`，所有 `Pass` 产生者到不了 `JudgmentView`；`VideoPath` 对空文件名确实返回 `RootDirectory`，但唯一消费者 `TrySetupMovie` 先判 `File.Exists`，目录恒为 false（这条我自己也核过 `Background2DView.cs:306`）。两条都留作潜在回归风险，降 P3。
- **第 4 项维持 P2**：读取确实走 `GetFullPath + GetAttributes`，缺文件抛的异常被 `RejectReparsePoint` 吞掉不外泄；绑定只在 `HasJacket` 时读一次，滚动路径每格一次 `stat`。修法是把路径缓存进 `SongPackageItem`，不是砍掉重解析点防护。
- **第 6 项降 P3**：两条都只在设置页打开诊断时成立——关闭时 `IsOn` 只查缓存布尔，`Write` 不执行；31MB 是 `262144 × 120B` 的备用样本缓冲，`End` 后存回 `_spareSamples` 复用。属诊断模式固有代价，必须标开关条件。
- **第 8 项降 P3**：层级归属错误成立，两个捕获闭包也在；但全仓无 C# 调用者、prefab 也无 `Setup` 引用，闭包与回调失效没有可达路径，按死代码迁移或删除处理。

### M10 音频 / 动画 / 渲染

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 模糊截屏时机硬编码 `(RenderPassEvent)1`，输入依赖上一帧残帧 | 部分成立 | **修正** | P2 |
| 3 | P1 三个 Feature 子类漏调基类 `SetupRenderPass`，mask 与 Stencil 全失效 | 部分成立 | **修正** | P3 |
| 7 | P2 模糊用相机全分辨率 texel 采样，半径只有意图的一半 | 部分成立 | **修正** | P3 |
| 8 | P2 每 pass 每帧新建托管数组 + 未释放 `NativeArray`；UI pass 清共享深度 | 部分成立 | **修正** | P3 |
| 11 | P2 `SyncGameTime` 让 `PingPong` 退化 `Loop` | 部分成立 | **修正** | P3 |
| 12 | P3 `EffectPool.Spawn` 只轮转不查播放状态 | 条件成立 | **维持** | P3 |

- **第 2 项：事实成立，后果不成立，而且我给的修法是错的。** `(RenderPassEvent)1` 落在 URP 的 `[0, 50)` BeforeRendering 区块内，**与具名 `BeforeRendering`(0) 等价**，管线顺序并没有乱；`500` 属于 `_areaCameraSettings`，默认相机分支根本不等 copy 入队，把它代进来会让 copy 落在 `DrawBlurUI=300` 之后**覆盖 capture**、并与 `Blur=500` 同序——照原报告的修法改会制造新缺陷。真正的缺陷只是「配置字段死掉」。修法：用具名 `BeforeRendering`（或给 copy 一个 `PassSettings` 专用字段），并断言 `copy < 300 < 500 < 600`。
- **第 3 项：事实保留，后果撤回。** 三个子类确实只调 `EnqueuePasses`，而基类 `SetupRenderPass` 是 `private`，它们本就调不到；但它的入参是 `camera.cullingMask`（渲染时已按相机剔除过）与 `StencilState.defaultValue`，**没有 layerMask/stencil 配置可供"失效"**；唯一 active 的实例 `MusicItem_3.asset` 反序列化失败、`Passes` 为 `null`。所以是「扩展点没暴露成 protected」，不是 P1。
- **第 7 项改述**：用相机 texel 而非 capture texel 属实，但 shader 里 `stepUv = texel × _SamplingDistance`，把 `BlurSamplingDistance` 定义成**屏幕像素**并与 `CaptureResolutionScale` 解耦，本身是自洽的；「半径只有一半」只在把参数当成 capture 纹素时成立，源码没有这个约定。改为「采样单位与 capture 尺度不一致，建议显式用 capture 描述符或注明单位」，P3。
- **第 8 项改述**：两枚托管数组 + 两枚 `Allocator.Temp` 属实，但 `Temp` 帧末自动释放，**不是跨帧原生泄漏**，真实代价只是记录期的小额 GC；`ClearRenderTarget(DepthStencil)` 有条件执行，而唯一使用该 renderer 的 Library UI 相机之后没有可静态证明的附件读者，画面损坏未证。P3。
- **第 11 项**：空状态机（`MoveNext` 恒 `false`）与 `isReflesh` 属实；但 `PlayMotion` 用的仍是 `LoopType.Yoyo`，**不是**「退化 Loop」——`SyncGameTime` 的 `Mathf.Repeat(..., duration)` 只是把回程折进前半，造成同步瞬间的相位/方向跳变；且 `syncGameTime` 在所有资产里为 0、无代码赋值，当前不可达。P3。
- **第 12 项维持 P3**：`Spawn` 只按下标轮转、不查 `IsPlaying`，复用槽位时 `ParticleSystemController` 的 `Stop(true, StopEmittingAndClear)` 会截断上一枚特效，静态可证且调用链可达（`TapEffectView` Spawn 后直接 Play）。但触发条件是池饱和后轮回到该槽，原句「下一次 Spawn」不严谨，改为「池饱和轮回到仍在播放的槽位时被截断」。

### M11 测试套件

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 1 | P2 覆盖假象六类 | 六项分别判断 | **修正** | P2 |
| 3 | P2 用例向真实日志目录写并按前缀删文件 | 部分成立 | **修正** | P3 |
| 5 | P2 `LibrarySmokeTests` 四点问题 | 部分成立 | **维持** | P3 |
| 7 | P2 四份手写夹具字段集合不一致 | 待证 | **修正** | P3 |
| 8 | P2 三类零覆盖 | 部分误报 | **修正** | P2 |

- **第 1 项六小项逐条裁定**：①（视图寿命用例从不 `OnView`）、③（手拼 reset 漏 `timeline`/`event`/`TapCount`/`presentation`）、④（`Assume` 转 `Inconclusive`）、⑥（`LiveHarness` 构造改 4 个静态量 + `AddDevice`，抛错则 `Dispose` 不执行）**维持**；② 收窄为「该用例不读生产判定表」，不能说「其他判定用例也拦不住」；⑤ **撤回**——`Run()` 末尾同步 `WriteAllLines` 之后测试才读，且断言 `offenders` 为空，不存在陈旧报告假绿（这条我自己也核过 `MissingScriptAuditTests.cs:20-28`）。
- **第 3 项误报**：清理不是按文件名前缀，而是 `GetFiles(...).Except(existing)` 再按首行 `chart={GUID marker}` 匹配，玩家自己的日志不含这个随机标记。成立的部分只有「写进真实 `persistentDataPath/logs`、中断会留残件」。
- **第 5 项维持 P3**：四点读码皆实（三处 `GetField(NonPublic)`、非 0/1 索引被归 0、一发现 `LibraryScreen` 即断言 `IsReady`、全文件无 `[Timeout]`）。第三方补的「有 15 秒自实现 deadline」只降低挂死风险，不推翻「没有 NUnit 超时兜底」。
- **第 7 项**：「4 份手写副本」不可证——`Tools/fixtures` 的 7 份 `score.json` 里只有 `head-settlement`、`hold-overlap-regression` 是多行手写，其余是单行导出副本。但字段差异静态可证：手写两份顶层 5 键、`note` 11–13 键，导出副本 8 键、`note` 19 键，缺项走 `ChartNote` 构造默认值。改为「≥2 份手写夹具，字段集合与导出副本不同」。
- **第 8 项**：输入适配**撤回**（`InputSystemTouchSourceTests` 已覆盖按压时间）；Pass 顺序降为「只验序列化 event 号，无逐相机入队验证」；AB 超时零覆盖**成立且比原文更严重**——`WaitForLoadedAssetBundle(timeoutSec)` 超时只是退出等待循环，**不把状态置为 `Error`**，于是 `_assetbundleLoadState` 停在 `Loading`，后续每次 `LoadResource` 都走到 `!= Done` 分支返回 `null`。调用方既拿不到超时信号，也永远等不到恢复。

### M12 编辑器工具

| # | 原判 | 第三方 | 裁决 | 最终 |
| --- | --- | --- | --- | --- |
| 2 | P1 `ScreenshotSession.Start` 先写全局状态再校验，抛错后永久 busy | 部分成立 | **修正** | P1 |
| 3 | P1 `TestRunnerBridge` 在测试开始前返回，CLI 拿不到报告与退出码 | 部分成立 | **修正** | P3 |
| 5 | P2 `PaintKit` 每个文件 isReadable 往返重导入（601 文件 / 约 1200 次） | 部分成立，称是估算 | **修正** | P2 |
| 6 | P2 两处用进程 CWD 解析路径（后者因此「永远通过」） | 部分成立 | **修正** | P2 |
| 8 | P2 `AtlasSpriteMigration` 中断后无法重跑 | 部分成立，称取决于中断点 | **维持** | P2 |

- **第 2 项维持 P1 但收窄**：前置条件里 **busy、编译错误、场景不符三条确实在写状态之前**检查；只有「有未保存改动的场景」这条在写入 `KEY_REQUEST`、改 GameView 索引、开 `runInBackground` 之后才抛，而 `Finish` 只写 `status.json`、不调 `Cleanup`——所以会话内 `IsBusy` 恒真、设置不还原，只能等重编译触发的静态构造器 `delayCall` 或重启。第三方把「先 Finish 再抛」当成已回收，是误读。
- **第 3 项降 P3**：`Run` 在 `_api.Execute` 之后立即 `return true`，报告靠 `RunFinished` 回调写，确实没有退出协议；但**仓内没有任何 `-executeMethod` 调用者**——CI 走 `.github/workflows/editmode-tests.yml` 的 game-ci，本地走 `Tools/run-editmode-tests.ps1` 的 `unity test` 并检查 `$LASTEXITCODE`。所以它不是现行的批处理缺陷，只是类注释仍在宣称一个没接线的 CLI 入口。降 P3。
- **第 5 项：数字换掉，机制保留**。重导入次数是**确定值不是估算**——每处理 1 行就把该行纹理的 `isReadable` 置真、`SaveAndReimport`，收尾再复位一次（`Export` 398/417，`Apply` 225 + 270），而像素一律取自 `LoadImage(File.ReadAllBytes(...))`，`isReadable` 全程无用。按行不按纹理：`LocalPacks/paint-kit/manifest.csv` 157 行 / 33 张纹理，其中 130 个 rect 行只指向 6 张纹理，一张 2048² ASTC 图集被 111 行各翻一遍（约 222 次）；现存 24 个 `.meta` 全是 `isReadable: 0`。原报告「601 个文件、约 1200 次」在当前树里无法复算（三个清单共 209 行、AtlasSprites 已迁出 150 张），删掉。
- **第 6 项撤回「永远通过」**：`MissingScriptAudit` 写 TSV，由 `MissingScriptAuditTests` 断言「报告存在且除表头外为空」，CI 用的是 game-ci 全量 EditMode——这是**真门禁**（这条我自己也核过 `MissingScriptAuditTests.cs:20-28`）。留下的只有 CWD 依赖：`MissingScriptAudit` 与 `GeneratedAssetSaveGuard` 的 `File.Exists` 走进程 CWD，而 `testing.md` 明确要求 CWD 必须是项目根，所以是条件性空转，不是无条件通过。原句「guard 的清单读取』也说错了——它读的是 `Application.dataPath`。
- **第 8 项维持 P2（驳回「取决于中断点」）**：顺序是 写最小 `.meta` → 删源 `.asset` → `Refresh` → 配 importer，而 `entries` 只从现存 `.asset` 反推。删完再跑直接空集早退；在循环内中断则第 129 行的 `type: 2 → 3` 改写没执行，已换条目的引用停在 `null` 且永不再被收进 `guids`。**没有任何中断点能靠重跑补齐**，这是代码顺序直接可判定的结论，不需要复现。

## 四、七个横向模式的对账

第三方给每个模式都做了评述。逐条对下来，模式本身都成立，但**归因与严重度需要改**——尤其"空实现"要按可达性分三档，否则会把死代码当缺陷。

### A. 空壳 API——成立，但要按可达性分档

第三方说「均有事实依据」，我们实测把规模钉死了：`Platform/` + `Assets/` 合计 **192 处** `throw null;`，全工程 **322 处**，分布见第五节表格。原报告的「100+」偏少，第三方的 192 精确。

真正的争议是严重度。按可达性分三档后，只有第三档才是缺陷：

| 档 | 判据 | 例 | 结论 |
| --- | --- | --- | --- |
| 一 | 零调用点、零挂载 | `LruAssetCache*` 66 处、`CP/UnityWebRequestClient` 41 处、`InputManager` 31 处 GUID 未被任何 `.unity`/`.prefab` 引用 | 死代码清理项，P3（原判 P1 过重） |
| 二 | 在线调用但被上层降级 | `SkillData`/`CutinData`（`ChartDocument.Migrate` 先滤掉 Skill/Fever）、下载 API（`CheckNeedsDownload` 区分 Error/Success） | 兼容壳 / 范围问题，P2–P3 |
| 三 | 在线调用、无降级 | `SoundUtility.SetupGlobalSeSettings`（两个启动入口当真调）、`ShaderPreloader`（`EntryPoint` 门禁恒真）、`TapEffectView.PlayHaptic`（振动开关整条空转） | 真缺陷，保持原级 |

### B. 失败路径不原子——成立，且三处同源

第三方认可"写入顺序属实"，同时指出我们漏算了已有兜底（Sonolus 的事务恢复、常规导入的 `finally`）。这一点接受。

M12 的三条其实是同一个病：**写全局状态后抛错**（`ScreenshotSession` 写 BUSY/GameView/`runInBackground` 后才校验未保存场景）、**删源后改写引用**（`AtlasSpriteMigration` 写最小 `.meta` → 删源 → 之后才配 importer）、**异步入口无退出协议**（`TestRunnerBridge` 在 `Execute` 后立即 `return true`）。加上 M02 的"先删旧文件再写新文件"与"索引严格读让安装永久失败"，修法是同一套：**先校验后写入 + `try/finally` 收尾 + 先写临时物再原子替换**。

### C. 主线程同步 I/O——成立，但要区分玩家侧与工具侧

第三方指出"响度按帧预算让出、日志依开关、编辑器迁移不会造成 Android 玩家 ANR"——都对，原报告把两类混在一起了。分开之后：

- **玩家侧（保持原级）**：AB/ACB 的 `while (!isDone)` 与 `Thread.Sleep(1)`、开局同步解析谱面、Android 2DMV 同步复制、曲库逐首 `GetContent` 全量解码。
- **工具侧（不算 ANR）**：`PaintKit` 的逐行重导入、`AtlasSpriteMigration` 的批量写、`FontAssetRebuilder` 的全工程文本改写。
- **数字作废**：M03 的「265ms/首」引自一段注释，而它引用的方法在全仓不存在，已删。

### D. 热路径——成立，但"每帧"要能被常量或调用点钉住

第三方说"18 次等次数须绑定具体动画或控件规模"。M04 那一处恰好可以绑定：`HomeCards` 的 `COLUMNS = 3`、`ROWS = 6`，`Tick` 里对内层 strip 逐行 `GetChild`，就是 18 次/帧。

三处保持原级：`NoteSpeedRatio.Resolve` 每次从 `scoreInfos[0]` 起遍历（M05 第 11 项，修正了"唯一/越打越慢"的绝对表述）、`CriAtom` 每帧 1–2 次全场景 `FindObjectsByType`（M08 第 4 项，维持 P1）、`LiveViewExt` 每帧闭包（M06 第 2 项，P1→P2）。

反过来有一处原报告说过头了：`CustomTextMesh` 的 `LateUpdate` 先比对文本与尺寸再决定刷新，`AlphaMask` 的 `Vector3[4]` 只在更新路径分配——都不是"每帧固定成本"。

### E. 事件规范——成立，但举证换了两处

第三方指出两例与源码相反，**核实后成立，已从原报告撤下**：

- `ServerRegistry.Changed` 是 `public event Action`，三处触发全部走 `private void RaiseChanged() => Changed?.Invoke();`。
- `ServerScreen.PlayRequested` 是 `Action<PlayRequest>`，四个参数被结构体打包，正是 `04-events` 推荐写法。

第三方说 `ScreenManager` 的实际属性名是 `OnChangeScreenSize`，**也对**（第 285 行）。但它仍然是个可赋值的 `Action<T>` 属性而不是 `event`，所以「可被外部整体替换」这一点不因改名而消失。

替换后的真实举证：`ServerScreen.BackRequested?.Invoke();`（无 `Raise` 包装，而同文件 `LibraryChanged` 走 `RaiseLibraryChanged`）、`ServerListPage` 的 `OnRegistryChanged`（订阅者未按 `类名_事件名` 命名）、`NoteBase` 的判定回调仍是可写 `Action` 属性（M05 第 6 项，维持为接口风险）。

### F. 生命周期——成立，但我方**撤回两条**

第三方对这两条的纠正，读码后必须接受：

- **`ColorFader`**：`OnDisable() => Stop()`，而 `Stop()` 把 `_state` 置 `State.None`，`IsPlayFading => _state == State.Fading` 立刻变 false。原报告「中断协程后 `IsPlayFading` 卡住」**撤回**；真正残留的是 `onFinish` 回调被丢弃。
- **`CustomButton`**：`OnDisable` 在 Press 态会调 `OnFinishedControlSelectable()`，该路径调 `KillPressTween()`（`_pressMotion.TryCancel()`）。原报告「按压 `MotionHandle` 不在禁用时取消」**撤回**；更要紧的是 `_pressMotion` 的唯一赋值点 `ScalePointerDown` 全仓零调用，这条路径本身不可达。

保留的部分：`DialogBase` 的关闭收尾仍靠动画完成路径（禁用会取消动画 → `OnClose` 不触发），是具体缺陷；`new` 隐藏基类虚方法两处属实（`SoloLiveController.OnApplicationPause`、`AssetManager.OnDestroy`），但前者只翻一个无人读取的 `_isPause`、零运行时后果，应记 P3 隐患而不是缺陷；M06 的纹理"永不释放"被 `TransitionBlank` 的 `Resources.UnloadUnusedAssets` 兜住，降 P2。

### G. 测试覆盖——成立，但六类里要撤一类、收窄三类

M11 的结论：①③④⑥维持；②收窄为「该用例不读生产判定表」；⑤「陈旧报告」**撤回**（`Run()` 同步写盘后才读，且断言 `offenders` 为空）；「按文件名前缀删玩家日志」**误报**（实际是 `Except(existing)` + 首行 GUID 标记）；输入适配零覆盖**撤回**（`InputSystemTouchSourceTests` 已覆盖）；「四份手写夹具」不可证，改为「≥2 份手写夹具，字段集合与导出副本不同」；Pass 顺序降为「只验序列化 event 号，无入队顺序验证」。唯一变严重的是 AB 超时——它不是"零覆盖"那么简单，超时后状态停在 `Loading`，调用方永远拿不到失败信号。

## 五、报告自身的核算（R00）

第三方最后质疑的是这份报告自己的数字。复算结论：**它的算术全对，但它对我方"说了什么"有一处误读。**

| # | 第三方指控 | 复算结果 |
| --- | --- | --- |
| 1 | 表内逐行相加为 354 条、45 条 P1、709 个文件条目 | **全部正确**。合并后 28+28+24+27+29+30+34+28+28+28+36+34 = 354；P1 1+2+3+2+3+3+9+10+3+3+0+6 = 45；709 确为「文件/行」列的文件数相加 |
| 2 | M02 表内 P1=2，正文却有 3 条 P1 | **成立**。正文三条（替换媒体先删后写、Sonolus 索引锁死、开局同步解析）各自独立成条。表内 P1 是**去重后的组数**，正文只列主要项——已在原报告表头加注这个口径，不改数字 |
| 3 | 合计 711 个 `.cs`，其中 Assets/Tests 120 | **成立**。实测 Game 591 + Tests 120 = 711（Assets 全量 761，Vendor 50 不在范围）。表内 709 与 711 差 2，恰为两个未归口的 `AssemblyInfo.cs`（`Runtime/OpenSekai` 与 `Runtime/OpenSekai.Game` 根目录） |
| 4 | M07 标「全项目最低 2.3」与同表 M08 2.0 矛盾 | **成立**，且**不止一处**：同段「P1 密度最高（9 条）」也被 M08（10 条、每文件 0.19 vs 0.085）反证。正确排序：M05 3.7 > M01/M03 3.3 > M02/M04/M06/M09/M10/M11/M12 3.0 > M07 2.3 > M08 2.0 |
| 5 | 模块正文的「完整读完 N 个文件」与真实文件数矛盾 | **不成立（前提有误）**。原文没有任何模块级「完整读完 N 个文件」的表述；那句话出自各审查员的覆盖率自述，没有进报告正文。抽查真实数 M05=90、M07=106、M08=53，与表内完全一致 |

由此在原报告里修掉的硬错误（已改）：

1. 「约 500 个 `.cs` 文件」→ 709 个条目（真实 711，差两个 `AssemblyInfo.cs`）。
2. 「约 340 条、P1 约 40 条」→ 354 条、P1 45 条（与表内一致）。
3. M07 标题「（2.3，全项目最低）」→「（2.3，全项目第二低；最低是 M08 的 2.0）」。
4. M07 正文「P1 密度最高（9 条）」→「P1 共 9 条，是各模块中第二多（M08 为 10 条）」。
5. 表头补一句：「P1 列是去重后的组数，与正文列出的 P1 条目不必一一对应」。

顺带补一个第三方没算的硬数据。全仓 `throw null;` 实测 **322 处**，集中在五个目录：

| 目录 | 处数 | 主要文件 |
| --- | ---: | --- |
| `Platform/` | 110 | `CP/UnityWebRequestClient.cs` 41、`InputManager.cs` 31、`UiEffect/GradientColor.cs` 18、`CP/HttpResult.cs` 12、`UiEffect/BlendColor.cs` 8 |
| `Data/` | 89 | `SkillData.cs` 25、`CutinData.cs` 18、`MasterInGameCutInCharactersModel.cs` 13、`CutinCardData.cs` 8 |
| `Assets/` | 82 | `LruAssetCache.cs` 39、`LruAssetCacheHandle.cs` 26、`AssetBundleDownloadHandler.cs` 16 |
| `UI/` | 26 | `UIPartsCharacterIcon.cs` 26（整类所有成员都抛） |
| `Screens/` | 15 | `ScreenManager.cs` 10 |

第三方给的两个数字都对得上：它说「资源与平台目录合计 192 行」——`Platform 110 + Assets 82 = 192`，分毫不差；它说 `ScreenManager` 是 10 个 public 方法（原报告写 8 个），实测 `throw null;` 正是 10 处。横向模式 A 因此有了确切的规模，不再只是"一片空壳"的定性描述。

## 六、对原报告的处置

原报告**不回写**裁决结果，保留为审查当时的原始记录，只在顶部加勘误指引，并修掉上节那 5 处硬错误。理由是：把结论就地改掉会让"当时发现了什么、被推翻了几条"这段过程消失，而这恰恰是这轮交叉审查最有用的信息。

下一步建议按受影响程度排序，而不是按原报告的批次：

1. **先修 4 条仍然成立的 P1**：生命归零被记为完成（M05）、AssetBundle 依赖引用计数只增不减（M08）、主线程空转等待（M08）、设置与媒体写入不可恢复（M09/M02）。
2. **再修 3 条被确认、只是严重度下调的**：长条尾键重推、返回键双消费与页面栈重入、UI 模糊截屏时机（修法要按 M10 的复核意见，不能直接代入 500）。
3. **然后清掉 2 条已被撤回/降为文档问题**：条头按下额度的多判（撤回）、普通尾按样本时间评分（改为校正 `judging-design.md`，不动代码）。
4. **空壳清查单独立项**：322 处 `throw null;` 按目录分批删或补，先做 `Data/`（89 处）与 `Assets/LruAssetCache*`（65 处），因为它们既不可达也没有上层降级。

## 七、这轮交叉审查暴露的方法问题

裁决之外，值得单独记一笔的是**双方各自的系统性偏差**——它们会重复出现。

### 我方（原审查）的三类失误

1. **把「实现事实」和「后果可达性」捆成一条断言。** 绝大多数降级都源于此：`Pass` 没被排除是真的，但 `HasJudgment` 先挡掉了；`VideoPath` 返回目录是真的，但消费者先 `File.Exists`；`WhenCovered` 没有退订是真的，但它是一次性注册。**写法应拆成两列：事实 / 可达链 / 后果**，任一列不成立就整条降级。
2. **定量数字常取自注释或单一样本，而不是可复算。** 已作废或改写的：「约 265ms/首」（引自一段注释，被引的方法全仓不存在）、「约 1200 次重导入」（实测应为 2 × 处理行数，按行不按纹理）、「601 个文件」、「约 500 个 .cs」（真实 711）、「每格 2–3 次分配」、「18 次 GetChild」（这条反而对，因为可以绑到 `COLUMNS×ROWS`）。**规则：给数字就要给复算方式，给不出就用"存在"表述。**
3. **定 P1 之前没查全局兜底与可达性。** 四处兜底是这轮才补上的：`TransitionBlank → Resources.UnloadUnusedAssets`（M06 纹理）、`OnDisable → Stop → State.None`（ColorFader）、`OnEnable → ResetScale` 与 `ScalePointerDown` 零调用（CustomButton）、`throw null` 五族零调用点零挂载（M08）。

### 第三方（复核）的三类偏差

1. **把可由代码顺序直接判定的确定性事实，降级为"视条件而定"。** 重导入次数（看 `isReadable` 与调用顺序即可算）、`AtlasSpriteMigration` 重跑不可能（`entries` 只从现存 `.asset` 反推）、`WhenCovered` 一次性（`_onCovered = null` 写在调用前）——都不需要复现。
2. **遇到「须核 prefab/场景」就判「部分成立」。** `OverlayMask` 的 GUID 在全部 `.prefab`/`.unity`/`.asset` 零命中，全量检索即可定案；`CustomButton` 的 `_pressMotion` 唯一赋值点零调用同理。这类软化应该驳回。
3. **个别引用偏移**：`CriAtomShim:124` 被两个不同条目重复引用（那里其实是 `LoadAcbFile` 壳）；HCA 越界真正所在的行（533）反而没被点到。

### 它做对的地方

事实层面它几乎全对——56 条修正里的绝大多数是它先指出的；`throw null` 的 192 与 `ScreenManager` 的 10 个方法，两个计数都精确。它对我们**严重度的削弱**在该接受的四处都该接受（M03 两条 P1、M08 第 5/6 项、M07 第 3 项、M09 第 2/3 项）。

真正需要驳回的只有一类：**它把"可达但未触发"和"不可达"混为一谈，又把"可由代码判定"当成"待证"**。这两种偏差方向相反，前者让我们该降没降，后者让我们该定没定。

## 八、仍未闭合的点

以下 6 项静态代码给不出结论，需要运行时证据，本轮不下裁决：

| 项 | 需要什么 |
| --- | --- |
| M08 第 3 项 HCA 主线程解码 | 仓内零 `.acb`、无 `HcaDecoder` 测试；需要一份真实音源测单曲解码耗时与峰值内存 |
| M08 第 2 项越界触发 | 需要 v3.0 + 非次立体声 + 合计恰 128 频带的样本，确认是否真有这种 ACB |
| M05 第 1 项尾键重推 | 需要一个「同时间可判候选排在终点之后」的谱面，确认普通尾窗口是否真的关死 |
| M10 第 2 项模糊截帧 | 需要 Frame Debugger 看叠加相机在 `(RenderPassEvent)1` 时颜色附件里到底是什么 |
| M07 第 4 项关闭动画被禁用 | 需要一次「关闭动画播放中把对话框 GameObject 禁用」的复现，确认 `OnClose` 是否丢失 |
| M04 第 9 项开屏转场冲突 | 需要在 3.9–4.4 s 内点卡片，确认 `expansion` 是否被清回 0 |


