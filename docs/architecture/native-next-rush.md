# NextRUSH+ 原生谱面加载

> 更新于 2026-11-18。已实现并行加载、基础玩法、原始曲包导入、P0 兼容预检和 P1 有符号音乐时间映射。

预检记录 `Skill`、`FeverChance`、`FeverStart` 后跳过其业务逻辑。预检字段、固定版本和真实样本哈希见 [兼容基线](next-rush-compatibility.md)，后续阶段见 [NextRUSH+ 完整支持计划](../plans/next-rush-support-plan.md)。

## 结构

```text
现有 JSON / SUS → ChartFileReader → RuntimeChartConverter ──┐
                                                         ├→ RuntimeChartDefinition → PlaySession → Live
NextRUSH+ LevelData → NextRushChart → NextRushChartLoader ──┘
                              ↘ NextRushCompatibility 预检
```

NextRUSH+ 不经过 ChartDocument，不生成中间 score.json，也不反向转换成旧格式。
原始压缩谱面作为曲包文件保存，成绩哈希继续基于该文件的原始字节。
`RuntimeChartDefinition.Document` 在原生路径为 null，`NextRush` 保留解析后的原始实体；可玩标记、判定点数、最大 ticks 由对应加载器构建。
播放入口改用 `RuntimeChartLoader.Load`，再由 `PlaySession.FromChart` 接入共同运行时。旧的 `PlaySession.Create` 保留给现有调用与测试。

| 位置 | 职责 |
| --- | --- |
| `OpenSekai.Core/Charts/NextRushChart.cs` | 不依赖 Unity 的数据解析，保留实体名称、archetype 和 value/ref 字段；拒绝重复名称/字段、非有限数值 |
| `OpenSekai.Core/Charts/NextRushCompatibility.cs` | 按固定上游 play 输入分类，记录 revision、实体类型和数量、约定忽略项与预检阻塞原因 |
| `OpenSekai/Infrastructure/Charts/NextRushChartLoader.cs` | 读取 GZip/JSON；解释 native BPM、音符及连接，直接创建 MusicScore；检查兼容性 |
| `OpenSekai/Infrastructure/Charts/RuntimeChartLoader.cs` | 根据曲包格式标识分派，未知格式报错 |
| `OpenSekai/Charts/RuntimeChartDefinition.cs` | 两种来源共用的播放结果与统计 |
| `OpenSekai/Infrastructure/Packages/SongPackageManager.cs` | 原样替换 native 文件、保留格式标识，继续复用目录和 ZIP 导入/导出 |

路径均相对 `Assets/Game/Runtime/`。

## 曲包约定

原生曲包示例：

```text
manifest.json
level-data.json.gz
audio.mp3
jacket.png
sonolus-source.json   可选，下载工具保留服务器返回的详情与 engine 信息
```

manifest 示例：

```json
{
  "formatVersion": 1,
  "id": "example-native",
  "chartFormat": "next-rush",
  "title": "Example",
  "scoreTitle": "Example",
  "scoreFileName": "level-data.json.gz",
  "audioFileName": "audio.mp3",
  "jacketFileName": "jacket.png",
  "musicDifficultyType": "master",
  "playLevel": 20,
  "secForMusicScoreMaker": 0,
  "fillerSec": 0
}
```

`chartFormat` 为空时继续使用现有格式；也识别显式文件后缀 `.next-rush.json` / `.next-rush.json.gz`。
不能仅因为 JSON 包含 entities 就认作 NextRUSH+，其它 Sonolus 引擎也使用 LevelData。
曲包 ZIP 的文件名可以为 `.zip` 或 `.oskp`，继续通过曲库已有导入按钮安装。

`ReplaceScoreFile` 对显式 native 后缀校验解析后原样复制；替换回旧格式时会清除 chartFormat。
普通 `.json` 的旧读取行为保持不变，调用方不应将 native 数据交给 ChartFileReader。

## 当前兼容范围

- 接受 Initialization revision 0、1、2，缺省为 1。当前使用项目自己的计分规则和默认初始生命。
- 保留 `Skill`、`FeverChance`、`FeverStart` 原始实体和忽略记录。它们不创建播放事件或判定点。引用方仍须满足所用字段的实体类型。
- 从第 0 拍开始的 BPM，支持变 BPM，4/4 拍。原生拍数用 double 计算时间，不先量化到 480 ticks；共同运行时的时间字段仍为 float。
- 第 0 拍前的音符沿用第 0 拍的 BPM 向前换算，保留负时间。
- Normal/Critical Tap、Flick、Trace、TraceFlick；方向支持普通、左、右。
- HeadTap 起点、Tick 中继、TailRelease/TailFlick/TailTrace/TailTraceFlick 尾部。Anchor 可作隐藏中继、无判定尾和 Guide 端点，允许同拍几何连接。Guide 可指向独立 Tap、Flick、Trace 和 TraceFlick，或无活动长条引用的 TailRelease、TailFlick、TailTrace、TailTraceFlick。独立的 TailRelease 使用松手判定窗口，Guide 本身不产生判定。
- 按实体字符串名称建立引用关系，实体顺序无关。Connector 的 `head/tail` 表示实际边，`segmentHead/segmentTail` 提供覆盖该边的样式区间，`activeHead/activeTail` 对应当前长条判定区间。继续读取早期加载器接受的连接表示。
- 同一条几何链可在连续可判定段和 Guide 段之间切换。HeadTap、Anchor 和 Tick 均可作为可判定段起点。相邻段共享交界的几何，头、尾和 Tick 各自只结算一次。
- Tick 可用作判定段起点或终点。端点 Tick 使用自身的轨道和接触判定，条身结束时仍可独立结算。Guide 可以借用其它链的非附着节点而保持原来的判定归属。跨越 `next/prev` 链边界的可判定连接可连成一个连续条身，前提是几何不分叉、活动区间覆盖整段。
- 按谱面创建 TransientHiddenTickNote，允许附在连接两端，也允许与可见 Tick 同拍。可见 Tick 和独立键可附着在同一条连接的相邻几何节点上，使用曲线计算判定轨道，以两端各自的变速轨迹计算纵向显示位置。每个原生判定点单独结算。
- Connector 可以经过附着分隔点，也可从附着端点起止。附着点从源路径继承轨道、宽度和变速轨迹；相邻子段使用源缓动的对应区间。几何路径允许附着点插在 `next/prev` 的两个节点之间，仍按 `activeHead/activeTail` 选择判定段。
- lane/size 转为 12 轨内的浮点轨道边界。Anchor 允许零宽。连接缓动 0–41 用于绘制、条身命中范围和附着位置，其中回弹及弹性曲线保留超调。普通和高亮长条支持带颜色变体，透明度固定为 1。8 种 Guide 颜色按样式端点透明度渐变。样式可引用跨链的区间，连接是否隐藏及是否穿判定线由样式起点决定。穿线时按两端的变速轨迹绘制。无判定尾的连接在尾节点过线后最多继续绘制 5 秒，真实尾键仍按本身规则结算。
- 连接按样式起点的 `segmentLayer` 排在舞台下方、舞台上方的两个层级，或音符键上方。同层 Guide 位于普通和高亮条上方。`segmentPresentation=1` 在该边的时间范围内铺满游戏镜头，透明度按谱面时间插值，并遵循样式起点变速组的隐藏设置。
- SimLine 通过本地同时押线逻辑重建，遵循设置开关。
- 变速组支持独立轨迹、跳距、隐藏、强制速度及 TIMESCALE / SCROLL 混用。适用于当前可编译的音符与线性长条。
- 原始文件最多 32 MiB；解压后最多 32 Mi 个字符；实体最多 100000，JSON 深度最多 32。

以下会明确报“NextRUSH+ 暂不支持”，不会静默丢弃：

- 非 4/4 拍及第 0 拍前的 BPM 变化。
- 分叉或合流的可判定连接、递归附着，以及使用尚未接入的音符族或 Damage 的判定区间。
- fake、damage、动态舞台、向下划键。
- HeadTrace/HeadFlick 等其它长条起点、TailTap、跨越不连续几何边的判定区间和未知实体/字段。

原生数据加载支持不等于复刻完整 NextRUSH+ 引擎。目前复用本项目判定、计分、音效和画面；后续玩法扩展应在原始数据保留的前提下逐项接入运行时。

## 分组变速

加载器按 `first` 和 `next` 编译各组，校验事件归属、顺序、循环及孤立事件。同拍事件保留链接顺序，最后一个事件决定该拍之后的速度和隐藏状态，跳距逐项累加。跳距用事件所在拍的 BPM 换算为秒。`editorName`、`editorHidden`、`editorLane` 作为编辑器元数据保留。

TIMESCALE 积分速度并累加跳距。SCROLL 使用当前瞬时速度与含跳距的时间宽度。混合模式在模式边界组合正向和反向距离，按组缓存每帧采样，使用预编译索引查找事件与组合区间。正的 `forceNoteSpeed` 取代该组的玩家显示速度，隐藏事件只影响画面。

画面按补偿后的谱面时间求轨迹，音符与基础长条的端点使用各自所属组，按原有判定时钟处理输入与成绩。显示调度在可能进入画面前激活，出画后可重入。纯 TIMESCALE 和单向 SCROLL 使用预编译的入场界限，其他 SCROLL 组合使用保守的提前激活，复杂谱面的成本仍需实测。

事件速度与 BPM 换算后的跳距各限于绝对值 10⁹。强制速度大于 12 和数值溢出的混合变速链会在加载时拒绝。变速缓动接受上游无超调的曲线和阶跃，距离采用 double 解析积分。 [固定上游轨迹实现](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/timescale.py)

## 音乐偏移与三秒规则

以秒为单位，`音频时间 = 谱面时间 + bgmOffset`。音频文件第 0 秒对应谱面时间 `−bgmOffset`。正偏移让第 0 拍晚于文件开头，负偏移让第 0 拍早于文件开头。固定上游的 SUS 分析器将 `WAVEOFFSET` 取反并交给 USC 转换器，后者把该值写入 `bgmOffset`。 [SUS 分析器](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/js/src/sus/analyze.ts) · [USC 转换器](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/js/src/usc/convert.ts)

用原始音频交叉核对。Kamisama 首个音符在第 2 拍、119.5 BPM，换算到音频约 0.493 秒，解码后起音约 0.495 秒。Life Reset Button 首个音符在第 0.0625 拍、200 BPM，换算到音频约 1.290 秒，解码后起音约 1.280 秒。起音以 5 ms 帧估算。

运行时保留音符的谱面时间。Live 统一用文件时间启动、暂停、恢复、重开和执行跳过。判定用 `音频时间 − bgmOffset − L − J`，画面用 `音频时间 − bgmOffset − L + V`，自动播放的 `J` 为 0。原生偏移以 double 保留，曲包原有 `fillerSec` 仍原样存储，并供 JSON / SUS 路径使用。原生谱面时间和偏移各限于 ±1,000,000 秒。

- **开局**以曲目信息页退场为起点。音乐可听起点与首个音符中较早的时刻落在 3 秒后。若音符入场需要更久，则按所需入场时间提前开局。
- **结尾**以音乐可听终点与最后一个音符中较晚的时刻为准。若音符延续到音频文件结束之后，时钟继续推进。
- **结算**在上述结尾后 3 秒启动结算演出，随后沿用演出与转场时长。片尾跳过仍可按原有条件提前触发演出。

## 下载验证与使用

开发者可先下载一张谱面生成原生曲包：

```powershell
./Tools/download-sonolus-chart.ps1 -LevelUrl 'https://untitledcharts.com/levels/UnCh-81fb32f7724a4a1eb92ddbbe045082c9'
```

也支持 `sonolus://host[/prefix]/levels/{name}` 链接和服务器路径前缀。
工具调用 `probe-sonolus.ps1` 获取详情、下载谱面/音频/封面并验证 SHA-1，识别媒体文件头，保留谱面原始压缩字节和来源详情，再打包为 `.oskp`。
仅接受该站当前确认的 `NextRUSH_P` 引擎名称；该检查不代替播放时的实际功能检查。
下载完成不代表所有玩法均可播放，不兼容的数据仍可保存，播放时会给出原因。

默认输出到随机临时目录；也可指定 `-OutputPath 'E:/Downloads/chart.oskp'`，已有文件不会覆盖。
将输出曲包通过曲库“导入”按钮导入，即可离线加载。
工具的 master 难度是显示默认值；Sonolus 的通用 LevelItem 没有项目难度类别的直接对应字段。

游戏内可直接浏览 Sonolus 服务器并下载入库，详见 [服务器页面](server-screen.md)和[服务器谱面下载](../guides/server-chart-downloads.md)。命令行工具保留给开发验证，网络部分沿用调研探针的超时与校验。

## 验证

`Assets/Tests/EditMode/Charts/NextRushChartLoaderTests.cs` 覆盖：原生来源不依赖 ChartDocument、分数轨道、变 BPM、命名长条连接、判定点数量、自动播放、同时押线、坏引用/循环、未知功能拒绝、GZip、失败替换保护和 ZIP 导入导出字节保留。

2026-11-17 实测样本 `UnCh-81fb32f7724a4a1eb92ddbbe045082c9`：995 个实体，848 个判定点，120 Hz 自动播放可完成。完整 EditMode 回归第一轮为 992 项：974 通过、18 跳过、0 失败；后续专项测试最终为 29 项全部通过，包含真实曲包下载、导入后的字节保留与自动播放。
真实媒体与原谱未加入仓库。重复真实样本集成测试时：

```powershell
$env:OPENSEKAI_SONOLUS_SAMPLE_DIR = '<probe-sonolus.ps1 的输出目录>'
$env:OPENSEKAI_SONOLUS_PACKAGE = '<download-sonolus-chart.ps1 生成的 .oskp>'
unity test . --mode EditMode --filter NextRush --output TestResults/next-rush.xml --non-interactive
```

第二个变量可选；提供时还验证真实曲包经现有安装链路后媒体存在、谱面字节不变和原生播放完成。
没有提供第一个变量时，线上样本测试会跳过，其余合成测试不访问网络。

Android 真机画面与音乐同步尚待验收。

2026-11-18，P0 完整 EditMode 回归为 1001 通过、18 跳过、0 失败。其中 NextRUSH+ 专项 39 项全部通过，包含 Hallway 原始 LevelData 的 848 判定点和自动播放回归。

P1 完整 EditMode 回归为 1010 通过、18 跳过、0 失败。其中 NextRUSH+ 专项 46 项全部通过，覆盖 ±0.51 秒、±1 秒、零偏移、负拍与变 BPM、合成音频点击及三种玩家补偿。负时间划键用例验证提前划键松手和尾部即时判定。

Windows 的曲库 PlayMode 回归 5 项全部通过。原生用例使用 +0.51 秒偏移和一段 4 秒音频，验证开局预滚、预滚和音乐播放中暂停恢复、局内重开、音频文件结束后的最后一个音符，以及末音符 3 秒后触发结算演出。旧 JSON 用例在同一轮从曲库完成整首自动播放并返回结算。

P2 在 Windows 的隔离 Unity 项目运行完整 EditMode 回归，1047 通过、19 跳过、0 失败。NextRUSH+ 专项 55 项全部通过，包含 Hallway 的 848 判定点及自动播放、双组独立轨迹、隐藏时手动判定、停止与倒流重入、同拍跳距和混合变速。

P2 使用本地原始谱面单独验证变速编译。Kamisama 的 2 组 7 事件正确读入，核对第 203、204 和 207 拍的速度边界。Life Reset Button 的 3 组 136 事件正确读入。Android 未连设备，未采集真机同步与性能数据。

P3 已用原始 Kamisama 曲包通过安装、原始谱面字节校验和 120 Hz 自动播放。1265 个实体中有 51 个 Anchor 和 154 个隐藏 Tick，共 885 个判定点。全量 EditMode 回归 1048 通过、19 跳过、0 失败。Hallway 原谱单独复验，848 个判定点及自动播放通过。Windows 曲库 PlayMode 5 项通过，合成谱覆盖曲线 Anchor 尾、Guide、暂停恢复、重开和结算。用户已确认 M1 验收。

普通长条头到达判定线后固定在线上，可见性跟随当前连接片段，尾节点到线时退场。合成谱回归覆盖经过 Anchor 继续显示、变速组隐藏与恢复、尾部退场。全量 EditMode 回归 1053 通过、19 跳过、0 失败，包含原始 Kamisama 曲包。修复后的原谱画面待人工复核。

P3 后续全量 EditMode 回归 1058 通过、19 跳过、0 失败。合成谱覆盖可见 Tick 的曲线位置和双组变速、阶跃两侧的命中位置、Guide 末端的独立手动输入以及跨链样式透明度。原始 Kamisama 曲包再次通过导入和自动播放，仍为 885 个判定点。
