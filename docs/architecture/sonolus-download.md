# Sonolus 谱面下载接入调研

> 日期：2026-11-17。接口调研记录；后续实现已调整为并行原生加载，见 [NextRUSH+ 原生加载](native-next-rush.md)。
> 2026-11-18：游戏内 Sonolus 客户端（栏目、搜索、详情、社区、排行榜、下载、游玩）已实现，见 [服务器页面](server-screen.md)。
> 验证服务器：<https://untitledcharts.com/>。

## 结论

可以增加。公开谱面支持匿名获取列表、搜索、详情和资源，首版不需要账号系统。
下文的 ChartDocument 转换方案是最初调研建议，已被原生加载方案替代；接口实测记录仍适用。
核心工作是 **NextRUSH+ LevelData → ChartDocument 转换**，再复用现有曲包导入、曲库和打歌链路。
不能把下载的 JSON 改名为 `score.json`，也不能把所有 Sonolus 引擎都当作 PJSK 谱面。

建议先完成“粘贴谱面链接 → 下载兼容谱面 → 加入本地曲库”，随后接通首页的服务器入口，提供列表、搜索、分页和下载状态。
首版按引擎及实际数据声明兼容范围，特殊玩法检测后明确拒绝，不能静默丢失。

## 接口

下面路径拼接在用户输入的服务器根地址后；例如服务器为 `https://example.com/custom`，列表为 `https://example.com/custom/sonolus/levels/list`。
网站首页是 HTML，实际使用 `/sonolus/` 下的 JSON 接口。

| 用途 | GET 路径 | 返回内容 / 注意事项 |
| --- | --- | --- |
| 探测服务器 | `/sonolus/info` | `title`、按钮、配置；记录响应头 `Sonolus-Version` |
| 谱面主页、搜索表单 | `/sonolus/levels/info` | `sections`、`searches`，用于发现服务器支持的搜索选项 |
| 列表 | `/sonolus/levels/list?page=0` | `pageCount`、`items[]`；页码从 0 开始 |
| 快速搜索 | `/sonolus/levels/list?type=quick&keywords=hallway&page=0` | 关键词需 URL 编码 |
| 详情 | `/sonolus/levels/{name}` | `item` 才是 LevelItem；描述在详情外层 `description` |
| 下载 | `item.data.url`、`item.bgm.url`、`item.cover.url` | 跟随响应里的资源地址，支持跨域 CDN，不猜资源路径 |

通用客户端还应支持 `localization=zhs`、负数 `pageCount` 对应的 cursor 分页、401/404/429、超时和取消。
高级搜索按服务器提供的 `searches` 定义，不能把 UntitledCharts 的选项硬编码为整个协议。
实测该站提供 `advanced` 表单，包含 keywords、staff_pick、min_rating、max_rating、title_includes、author_includes 等字段。

协议来源：[列表接口](https://wiki.sonolus.com/custom-server-specs/endpoints/get-sonolus-type-list)、[详情接口](https://wiki.sonolus.com/custom-server-specs/endpoints/get-sonolus-type-item-name)、[页码](https://wiki.sonolus.com/custom-server-specs/query-parameters/page)。

### LevelItem 与资源

LevelItem 包含 `name`、`rating`、`title`、`artists`、`author`、`engine` 和资源定位信息。
资源使用 SRL：`{ "hash": "...", "url": "..." }`；hash 是可选 SHA-1，应在存在时校验原始资源字节，并用于下载缓存。
以 `/` 开头的 URL 按 Sonolus 规范相对**服务器地址（含路径前缀）**拼接，而不是直接套标准 URI 的域名根路径解析。

`data` 是 GZip 压缩的 JSON，顶层为 `bgmOffset` 和 `entities`；每个实体包含 archetype、可选 name 和 data 字段数组。
数组里的数据有数值 `value` 和字符串引用 `ref`，不能强制都解析为浮点数。
音乐和封面可能没有扩展名，应根据文件头和实际格式选择本地文件名；本次样本为 MP3（ID3）和 PNG。
游戏使用自身的玩法、皮肤和音效，不需要下载执行 `engine.playData`、ROM 或整套 Sonolus 资源包。

来源：[LevelItem](https://wiki.sonolus.com/custom-server-specs/misc/level-item)、[SRL](https://wiki.sonolus.com/custom-server-specs/misc/srl)、[LevelData](https://wiki.sonolus.com/level-specs/resources/level-data)。

## 实测记录

2026-11-17 17:24–17:25 UTC，无 Cookie、无 Sonolus-Session，所测接口均返回 200。
`Sonolus-Version: 1.1.5`；列表当时为 1557 页，第一页 10 项；第 1 页返回不同谱面；搜索 hallway 返回 3 项。
这些数量是当时快照，不应写死。

| 项目 | 普通样本 | Gimmicks 样本 |
| --- | --- | --- |
| 曲名 | You're Walking Down The Hallway | 人生リセットボタン / Life Reset Button |
| name | `UnCh-81fb32f7724a4a1eb92ddbbe045082c9` | `UnCh-690d4ea6de0443399b9d8f4be42f815b` |
| 等级 | 35 | 31 |
| 引擎 | `NextRUSH_P`，version 13，描述 v2.19.1 | 同左 |
| 实体数 | 995 | 8576 |
| bgmOffset | 0 | 1.271 秒 |
| 压缩谱面大小 | 10,234 字节 | 85,325 字节 |
| 下载验证 | data / bgm / cover 全部 SHA-1 一致 | data SHA-1 一致；未重复下载媒体 |

普通样本音频 2,513,116 字节，封面 319,426 字节。
谱面 data SHA-1 分别为 `8c6d45d2973cd858bb6661d4c6971ec9402c2754`、`abee22bc7bcc892da5cfe734f1110b060d8fd891`。
详情与样本可能被作者更新或删除，复测时以最新响应为准。

普通样本包含 NormalTapNote、CriticalTapNote、NormalHeadTapNote、NormalTailReleaseNote、Connector、SimLine、TransientHiddenTickNote 等。
第二张额外出现 3 个 timescale group、136 个 timescale change、4997 个 AnchorNote，以及 trace、flick、彩色 guide 和非默认透明度。
这说明“能下载该站谱面”与“能完整播放该站所有谱面”是两项验收标准。

下载文件和详细 report 留在临时目录，未添加音乐、封面或原谱到仓库：

```text
C:\Users\31089\AppData\Local\Temp\OpenSekai-Sonolus-20261117\
C:\Users\31089\AppData\Local\Temp\OpenSekai-Sonolus-20261117-gimmick\
```

### 复测工具

新增 `Tools/probe-sonolus.ps1`，使用 PowerShell 自带网络和 GZip 能力，无额外依赖：

```powershell
# 抽取当前第一页第一张谱面，验证搜索/分页/详情、下载谱面并统计实体
./Tools/probe-sonolus.ps1

# 同时下载音乐和封面并校验 SHA-1
./Tools/probe-sonolus.ps1 -IncludeMedia

# 指定样本，便于比较结果
./Tools/probe-sonolus.ps1 -LevelName UnCh-690d4ea6de0443399b9d8f4be42f815b
```

默认输出到随机临时目录；`-OutputDirectory` 可指定输出位置。
工具保存 `details.json`、`levels-info.json`、压缩和解压后的 LevelData、含实体示例的 `report.json`。
它是调研探针，不是曲包安装器，也未实现生产下载所需的大小限制、重试或断点续传。

## 项目接入位置

| 现有位置 | 接入方式 |
| --- | --- |
| `Assets/Game/Runtime/OpenSekai/App/UI/Home/HomeView.cs` | 服务器卡片已接通服务器页面（[服务器页面](server-screen.md)） |
| `Assets/Game/Runtime/OpenSekai/Infrastructure/Charts/ChartFileReader.cs` | 当前 JSON 仅接受 ChartDocument，SUS/TXT 走 SusRuntimeConverter；Sonolus 输入应显式分流 |
| `Assets/Game/Runtime/OpenSekai.Core/Charts/ChartDocument.cs` | 转换后的统一本地格式；NoteList、MusicScoreEventDataList、连接 ID |
| `Assets/Game/Runtime/OpenSekai/Charts/ChartTimeline.cs` | 每拍 480 ticks，负责 BPM 到时间换算 |
| `Assets/Game/Runtime/OpenSekai/Infrastructure/Packages/SongPackageManager.cs` | 暂存目录生成有效本地包后调用 ImportFolder，复用复制、importedAtUtc、缓存登记 |
| `Assets/Game/Runtime/OpenSekai.Core/Packaging/SongPackageManifest.cs` | 曲名、作者、等级、媒体路径；来源信息需扩展契约或保存额外 sidecar |
| `Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs` | 复用运行时转换及长条 combo 生成，不能重复导入服务端生成的 combo tick |

建议分成三部分：

1. `SonolusServerClient`（App / Infrastructure）：服务器地址和分享链接解析、列表/搜索/详情、资源下载；UnityWebRequest 提供异步、取消、进度和文件写入。
2. `NextRushLevelConverter`（Core）：仅解析数据并生成 ChartDocument 与兼容性诊断，不依赖网络或 Unity 对象。
3. `SonolusImportService`（App）：选择对应转换器，校验资源，写 manifest/score/媒体，最后通过 SongPackageManager 加入曲库。

转换器按 engine.name 和确认过的 schema/revision 选择；engine.version 不是足以区分语义的唯一版本号。
服务端未来可能切换引擎，同一引擎也可能增加字段。未知关键玩法要报告，不可直接套用旧映射。

### 转换要点与边界

- **时间**：以 `#BEAT` 生成 ticks（round(beat × 480)），`#BPM_CHANGE` 生成 BPM 事件；保留负拍、变拍/变 BPM 等边界检查。
- **轨道**：NextRUSH 使用 lane / size 的中心与半宽语义，本项目落盘为整数 laneStart / laneEnd。标准 12 轨可按边界转换，但要验证端点的包含关系；分数轨、超范围轨道不应直接截断。应先写合成样本验证左右边界、奇偶宽度和镜像。
- **长条**：先按实体 name 建引用表，再解析 next、activeHead、attachHead/attachTail 和 Connector 的 segmentHead/segmentTail，生成内部连接 ID；校验缺失引用、循环和时间顺序。ref 是实体名字，不是数组下标。
- **隐藏 tick**：TransientHiddenTickNote 是服务端数据中的生成判定点。本地运行时也生成 long-hold combo，须选择单一生成来源并核对最终 combo，不能直接当普通音符全部保留。
- **同时押线**：SimLine 可由本地已有逻辑重新生成。
- **配置实体**：Initialization 中可含 revision 和玩法配置，需要读取并检查；不应笼统丢弃全部非音符实体。
- **offset**：第二样本的非零 bgmOffset 必须保留。当前 fillerSec 被 Normalize 限制为非负，且会影响播放时钟；不能未经验证直接复制。实施时先确认 Sonolus offset 的符号和时钟定义，用正负 offset 合成样本核对首音、末音、预览与结算跨度，必要时新增有符号偏移字段。
- **变速**：本项目已有全局 HighSpeed 和单音符 speedRatio，但不足以等价表达所有分组曲线变速、skip、停止和倒流。首版只放行确认等价的数据，其余报告为暂不兼容。
- **特殊玩法**：动态舞台、damage、独立 release、特殊 easing、fake/guide、颜色和透明度等按实际实现逐项判断；已有 Guide/Friction 类型不等于语义已与 NextRUSH 对齐。

引擎字段和连接行为参考其 [note 定义](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/master/sekai/play/note.py)、[connector 定义](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/master/sekai/play/connector.py)、[timescale 逻辑](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/master/sekai/lib/timescale.py)。以上是待实现的映射方案，尚未通过实际播放验证。

### 曲包与下载事务

下载到随机暂存目录，媒体完成校验并通过转换后才登记曲包；取消、失败或不兼容时不留下曲库条目。
本地文件名固定生成，不直接使用服务器的 title/name 作为路径。
为 JSON、解压大小、实体数量、媒体大小设上限，流式写文件，避免手机内存峰值。
对有 hash 的资源校验；无 hash 的资源仍需做格式校验，不能以为 SHA-1 提供来源认证。

manifest 可映射 title/scoreTitle ← title，userName ← author，composer ← artists（聚合署名，无法拆分作词/演唱），playLevel ← rating；难度类别没有通用标准字段，不应只凭等级推断。
建议保留 `sonolus-source.json`：服务器地址、level name、engine name/version、资源 hash、导入器版本和原始详情。
用规范化服务器地址 + level name 做“已下载”标记，用资源 hash 判断更新；不要替换现有本地 id 或成绩哈希。
预览资源可选，首版可继续使用已下载完整音乐的本地预览。

来源链接首版至少支持服务器根地址、`https://host[/prefix]/levels/{name}` 分享链接和 `sonolus://host[/prefix]/levels/{name}`，统一解析到服务器和 level name；第三方 open.sonolus.com 包装链接另行解析，不应当作资源服务器。

## 实施顺序与验收

1. 先实现转换器和兼容性检查。用自建最小数据覆盖 tap/critical/flick/trace、线性和曲线长条、BPM、非零 offset；与现有 SUS 导入结果比时间、轨道、连接和 combo。普通样本用于联调，不把第三方曲目加入测试仓库。
2. 接通单谱面链接下载入库：普通样本正确落盘、出现在曲库、播放与音乐同步，重启后仍可离线打歌；不兼容样本必须给出具体原因。
3. 再接服务器列表和搜索页面：加载下一页、详情、取消下载、失败重试、“已下载 / 可更新”状态；最后在 Android 真机验收 MP3 播放、生命周期恢复和内存。

网络层测试应使用固定响应和本地合成资源，覆盖 URL 前缀、cursor、404/401/429、坏 hash、损坏 GZip、取消和中途失败；线上服务器仅作手动集成验证。

可参考服务器维护者的 [chart-downloader](https://github.com/UntitledCharts/chart-downloader) 来核对下载流程。该仓库标为 GPL-3.0，当前项目为 MIT；本次只研究协议，未复制其实现。引擎 LICENSE.txt 为 MIT；如后续复用代码需保留对应声明。

此次变更仅增加调研文档和可复测的探针；没有修改游戏运行时，也没有宣称已支持 Sonolus 谱面播放。
