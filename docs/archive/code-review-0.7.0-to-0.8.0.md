# OpenSekai 0.7.0 至 0.8.0 代码审查记录

**审查范围**为 `94f040c..e4583de`。以 0.7.0 发布提交为基点，到 0.8.0 发布提交为止，共 58 个提交，涉及 1,282 个文件。三名审查者沿六组实际调用路径交叉核对实现、产品文档和测试。下文的修复提交均位于 `e4583de` 之后。随后开发的练习模式另行验证。

**0.8.0 发布基线**在 Linux 云计算机用 Unity 6000.6.7f1 运行 EditMode，共 1,885 项，通过 1,770 项，失败 93 项，跳过 22 项。93 项失败均经过曲包或素材安装路径，报错集中在 Linux 剩余空间查询。仅资源包存储和配置两个测试类就有 71 项失败。

| 模块 | 经专测确认的主要影响 | 修复提交 |
| --- | --- | --- |
| 个人资料、排行、成就、备份与结算 | 无法恢复的备份可放行清空，旧记录删除影响其他重试凭据，未拥有头像仍显示，时间格式影响排序，Android 拦截有效大备份 | `e8acdc2`、`bf2371e` |
| 段位 | 前台低帧率时，标称 50 秒的休息被拉长 | `1ecded7` |
| 资源包、皮肤与音频 | Linux 目标卷空间探测失败，素材包和曲包解压一起受阻 | `5dc1de1` |
| 谱面、导入、曲库与服务器 | 临时 HTTP 错误可清除已核验的 Ranked 资格 | `639ec65` |
| 判定、长条、计分与 Live 时间线 | 已核对手动判定、长条与分数路径，见第 5 节的用例证据 | 本轮未确认独立缺陷 |
| UI、编辑器导出、工具与构建 | 公开风格指南可能导出普通玩家截图，Linux 程序集校验可能漏报文件移动 | `f19029a`、`47dfec8` |

## 1. 个人资料、排行、成就、备份与结算

从结算写入历史、重建 Best 30、领取与佩戴奖励，一直追到单条删除、清空、备份合并及重启后的读取。重点核对了旧版无 ID 记录、相同成绩、带时区的导入记录和历史索引缓存。[档案存储实现](../../Assets/Game/Runtime/OpenSekai/App/Library/Storage)

- **无法恢复的备份也能放行清空。** 原先导出可接收空对象、未来版本记录或截断的 JSON 行，仍生成表示“已备份”的标记。恢复时这些记录会被拒绝。修复后导出与恢复使用同一套逐行校验。旧版备份标记需要重新导出一次，才能用于清空前的校验。单条删除保留无关的不可读行。`e8acdc2`。[备份与删除专测](../../Assets/Tests/EditMode/Packages/PlayerProfileStoreTests.cs)
- **删除单局时可能影响其他权益。** 删除无 ID 的旧记录会清除无关的重试成就凭据。删除最后一条支撑记录后，已经佩戴的奖励头像仍可能显示。修复后只撤销与该局相关的凭据，重新打开档案也只显示仍拥有的头像。`e8acdc2`。[成就专测](../../Assets/Tests/EditMode/Packages/AchievementTests.cs)
- **同分记录及历史时间线可能排错。** 带时区偏移或不同小数精度的时间字符串，按字面排序会颠倒真实时间。现在按实际时间排序，同一时刻按稳定记录 ID 确定顺序。`e8acdc2`。上述三组修复的联合专测共 15 项通过，含有效备份还原、重复 ID、旧无 ID 记录和重启后索引。[排行专测](../../Assets/Tests/EditMode/Packages/RankingHistoryTests.cs)
- **Android 文件选择器会拒绝可恢复的大备份。** Java 端曾限制 65 MiB，归档读取端允许 193 MiB。用 66 MiB 文档流复现，修复后可完整复制。C# 另有测试核对传给 Java 的参数，审查时也使用 Unity 编译参数完成了五个 C# 程序集的编译。通过的是 Java 桥接与归档预算专测，系统选择器仍缺 Android 真机验证。`bf2371e`。[桥接专测](../../Assets/Tests/EditMode/Packages/AndroidBackupPickerTests.cs)

## 2. 段位

沿 `DanChallengeService` 检查分享定义、全曲预检与固定副本、首曲交接、继承生命、曲间休息、暂停重试、退出及写回历史。首曲交接前保存场次符合设计，进程中断后可以回溯到相应曲序。[段位实现](../../Assets/Game/Runtime/OpenSekai/App/Playback/DanChallengeService.cs) · [段位设计与落盘时机](../plans/player-profile-and-results.md)

**前台低帧率会延长休息。** 旧逻辑每帧最多扣 0.1 秒。以每秒 4 帧运行，经过 200 帧即 50 秒后，倒计时仍剩约 30 秒。`1ecded7` 改为计算实际前台时长，并在切换后台和回来时更新计时基点。两个专测分别覆盖前台低帧率与后台暂停。[段位计时专测](../../Assets/Tests/EditMode/Packages/DanChallengeTests.cs)

## 3. 资源包、皮肤与音频

核对素材包安装、配置迁移、内置与外部组选择、音频裁片、循环和单次播放、纹理绑定，以及开局失败时释放已分配的资源。涵盖音符、轨道、判定、HUD、背景和结算特效。[素材包存储](../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/MaterialPackStore.cs) · [打歌素材准备](../../Assets/Game/Runtime/OpenSekai/App/Playback/MaterialAudioPreparation.cs)

**Linux 提取前空间检查选错文件系统。** Unity Mono 对根路径调用 `DriveInfo` 时可能抛出异常。即使能查询，根目录也可能和实际暂存目录位于不同挂载点。`5dc1de1` 改为查询解压目标目录所在文件系统。使用 Unity 随附的 Mono 跑过 3 项专测，包括常规暂存目录、单独挂载的 `/dev/shm` 与不存在的目录。独立挂载用例将结果和系统 `df` 实测值比较。这个修复也覆盖第 4 组的曲包解压入口。[实现](../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SafeZipExtractor.cs) · [挂载点专测](../../Assets/Tests/EditMode/Packages/SafeZipDestinationSpaceTests.cs)

**4:3 窗口下的背景回归测试采用了固定视角。** 云机实际运行默认背景素材时，三个像素颜色断言均通过，但按固定 42° 计算的世界坐标偏差为 0.28613。相机在 4:3 会自适应垂直视角。`d1ff69a` 让测试按 1080 高参考画布检查最终投影位置和尺寸，原有颜色、损坏回退、重试和资源释放断言照常执行。[背景测试](../../Assets/Tests/PlayMode/MaterialPackBackgroundTests.cs)

## 4. 谱面、导入、曲库与服务器

沿曲包安装、来源识别、预览重定向、哈希比对、排行榜缓存更新及曲库展示检查。对重复刷新和暂时不可用的服务器，核对了之前已确认资格的处理。[Ranked 核验实现](../../Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/ChartRankLookup.cs)

**临时预览服务错误可覆盖已确认的 Ranked。** 用 429 和 503 响应复现，另测了错误响应中夹带 Location 的情况。`639ec65` 保留原有资格并提示后续重试，同时校验能否从响应提取真实重定向。7 项专测通过，包括正常重定向、断网、200 和 304 响应。[回归测试](../../Assets/Tests/EditMode/Packages/ChartRankPreviewTests.cs)

## 5. 判定、长条、计分与 Live 时间线

对照判定规则，检查按下、抬手、持续接触的处理顺序，长条中间点与尾键的归属，原生谱连接关系、装饰点排除、计分分母、扣血及减分显示。发布基线中 `JudgeWindowTests` 9 项、`FlickTailJudgeTests` 15 项、`HoldQuotaTests` 3 项、`LongHoldPipelineTests` 69 项、`ScoreLogicTests` 29 项全部通过。本组抽查未定位独立的新缺陷。实际设备的触摸延迟和音画同步仍需真机测量。[判定规则](../reference/judging.md) · [长条测试](../../Assets/Tests/EditMode/Live/LongHoldPipelineTests.cs) · [计分测试](../../Assets/Tests/EditMode/Live/ScoreLogicTests.cs)

## 6. UI、编辑器导出、工具与构建

检查截图请求解析、区间与帧索引、动画导出、异常退出时的状态恢复、风格指南导出来源、资源构建清单，以及编辑器程序集与源码的匹配检查。

- **公开风格指南可误用普通玩家截图。** 重用输出目录时，旧的示例库文件可能与后一次普通截图并存。`f19029a` 让截图报告记录是否使用示例曲库和各帧哈希。导出前验证所需帧，并将经校验的文件流直接交给渲染。旧报告缺少哈希，需要从示例工作区重新生成截图。PowerShell 专测通过，覆盖普通截图覆盖旧样本、像素被替换、完整样本成功、缺帧时阻止整批导出。[来源校验](../../Tools/style-guide-frames.ps1) · [专测](../../Tools/verify-style-guide-source.ps1)
- **Linux 程序集校验漏报仅大小写变化的源码移动。** 用 Unity 自带 .NET 8.0.318 SDK 编译真实 DLL/PDB，再把 `Note.cs` 改名为 `note.cs`，原版校验仍通过。`47dfec8` 修复后同时报出新增及移除路径。该专测模拟编辑器返回的已加载模块 ID，实际执行完整磁盘和 PDB 校验。[校验器](../../Tools/verify-editor-scripts.ps1) · [专测](../../Tools/tests/verify-editor-script-paths.ps1)
- **截图区间命令实测。** 在 Linux 使用 PowerShell 7.6.3 调用 `capture-screens.ps1`。混排单帧和两段 120 fps 区间，生成 402 帧请求。60 fps 的两种动画生成 1,392 帧请求。零长度、倒序、三段式区间、非数字、Infinity、241 fps 均在启动编辑器前被拒绝。这里核验的是生成的请求与预计帧数，实际 PNG 渲染、Windows 下的 JPEG 编码及 Windows PowerShell 5.1 仍待对应平台验证。[命令入口](../../Tools/capture-screens.ps1) · [截图序列测试](../../Assets/Tests/EditMode/Resources/ScreenshotSequenceTests.cs)

## 验证边界与全量结果

运行环境为 x86-64 Linux 云计算机。程序集、挂载点、文件流及脚本命令采用本机实测。三人审查覆盖关键调用路径和可复现边界。本次没有逐帧目检全部二进制美术资源，也未进行 Android 系统文件选择器真机交互、Windows 与 macOS 运行、不同手机上的触摸或听感测量。

| Unity 6000.6.7f1 验证 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| 全量 EditMode | 1,993 | 0 | 22 |
| 全量 PlayMode | 75 | 0 | 7 |

EditMode 验证提交 `eccc9b2`，PlayMode 验证提交 `fb62f99`。两个提交之间只调整了一个 PlayMode 手势用例。该用例在完整场景中通过屏幕射线命中控件，派发拖动与点击事件。另对 Unity Input System 的模拟鼠标做过独立验证。PlayMode 的 7 个跳过项需要本地上游素材包。

发布基线的 93 个失败用例按完整用例名逐项比对，修复后全部通过。EditMode 的 22 个跳过项包括 18 个需显式运行的压力测量、3 个需外部样本或服务器的可选集成用例，以及 1 个 Windows 专用用例。

## 练习模式的验收路径

从主界面向左滑，点击左侧的「练习模式」，选择谱面并填写起止秒数。输入 `0.5x, 2s, 0.7x, 2s`，会用两种速度各播放同一片段一次，每次播放后休息两秒。保存模板后，可对另一首歌或另一个片段复用。练习中可以立即改速，暂停后可以拖动。拖出片段进入自由练习，点击「重练」恢复原模板。练习退出会返回曲库，正式成绩和排行保持原值。

已测范围包含曲前负时间、整段越界、歌曲尾部、音频代理层切换、休息中拖动、末帧按键、校准偏移和不写结算。支持输入 0.001 到 3072 倍，背景音乐随速变调。Linux 云机通过 Unity 无声输出设备运行，音轨位置和 DSP 截止时刻有断言，手机扬声器和耳机听感仍需真机验证。
