# NextRUSH+ 原生谱面加载

> 更新：2026-11-17。第一阶段已实现：并行加载、基础玩法、原始曲包导入与下载验证工具。

后续范围与阶段见 [NextRUSH+ 完整支持计划](../plans/next-rush-support-plan.md)。按用户约定，后续将直接忽略技能和 Fever 实体；当前代码尚未接入该忽略规则。

## 结构

```text
现有 JSON / SUS → ChartFileReader → RuntimeChartConverter ──┐
                                                         ├→ RuntimeChartDefinition → PlaySession → Live
NextRUSH+ LevelData → NextRushChart → NextRushChartLoader ──┘
```

NextRUSH+ 不经过 ChartDocument，不生成中间 score.json，也不反向转换成旧格式。
原始压缩谱面作为曲包文件保存，成绩哈希继续基于该文件的原始字节。
`RuntimeChartDefinition.Document` 在原生路径为 null，`NextRush` 保留解析后的原始实体；可玩标记、判定点数、最大 ticks 由对应加载器构建。
播放入口改用 `RuntimeChartLoader.Load`，再由 `PlaySession.FromChart` 接入共同运行时。旧的 `PlaySession.Create` 保留给现有调用与测试。

| 位置 | 职责 |
| --- | --- |
| `OpenSekai.Core/Charts/NextRushChart.cs` | 不依赖 Unity 的数据解析，保留实体名称、archetype 和 value/ref 字段；拒绝重复名称/字段、非有限数值 |
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

## 第一阶段兼容范围

- Initialization revision 2，默认初始生命配置。
- 从第 0 拍开始的 BPM，支持变 BPM，4/4 拍。原生拍数用 double 计算时间，不先量化到 480 ticks；共同运行时的时间字段仍为 float。
- Normal/Critical Tap、Flick、Trace、TraceFlick；方向支持普通、左、右。
- HeadTap 起点、Tick 中继、TailRelease/TailFlick/TailTrace/TailTraceFlick 尾部，线性普通/高亮长条。
- 按实体字符串名称建立引用关系，顺序无关；校验孤立节点、循环、共享节点、缺失引用、时间逆序和 Connector 片段。
- 按服务端 TransientHiddenTickNote 创建隐藏判定点，只创建一次；该路径不运行旧转换器的自动 hold-combo 生成。
- lane/size 转为浮点轨道边界，保留一轨宽及以上的分数位置；范围限制在当前 12 轨内。隐藏点沿线性片段按时间插值。
- SimLine 通过本地同时押线逻辑重建，遵循设置开关。
- 原始文件最多 32 MiB；解压后最多 32 Mi 个字符；实体最多 100000，JSON 深度最多 32。

以下会明确报“NextRUSH+ 暂不支持”，不会静默丢弃：

- **非零 bgmOffset**：保留在原始数据中，但当前播放器只有旧 filler 时钟。其符号与有符号时钟接入需要独立验证；当前零偏移谱面可播放。
- 分组变速事件、skip、停止、倒流、非 4/4 拍及负拍。
- Anchor/guide 装饰连接、fake、damage、动态舞台、颜色覆盖、透明度、非线性 easing、向下划键。
- HeadTrace/HeadFlick 等其它长条起点、TailTap、自定义判定区间和未知实体/字段。

原生数据加载支持不等于复刻完整 NextRUSH+ 引擎。目前复用本项目判定、计分、音效和画面；后续玩法扩展应在原始数据保留的前提下逐项接入运行时。

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

游戏内已可直接浏览 Sonolus 服务器并下载入库（同样的曲包结构），见 [服务器页面](server-screen.md)。这个命令行工具仍保留给开发验证，网络部分沿用调研探针的超时与校验。

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

Android 真机画面与音乐同步尚未验收；非零偏移和特殊玩法不在本阶段支持范围内。
