# 曲包与谱面数据

> 目的：把「一个曲包目录」到「运行时音符列表」这条数据链路讲清楚，包括字段名与兼容逻辑。
> 适用读者：开发者（改谱面读取、改曲包格式、排查加载失败）。
> 前置条件：读过 [模块划分](modules.md)。
> 最后更新：2026-11-02

## 概述

一个曲包是一个目录，里面固定三件东西：`manifest.json`（元数据）、`score.json`（谱面）、音频文件；
封面可选。读取分四步：`SongPackageStorage` 发现目录 → `SongPackage` 解析路径 →
`ChartFileReader` 读 `score.json` 成 `ChartDocument` → `RuntimeChartConverter` 转成运行时音符与事件。

## 核心内容

### 目录与文件

```text
<CustomMusicScores>/<任意目录名>/
  manifest.json      SongPackageManifest
  score.json         ChartDocument（JSON）
  audio.ogg          音频（字段 audioFileName 可覆盖）
  jacket.png         封面（字段 jacketFileName 可覆盖，可选）
```

### `manifest.json` 主要字段

定义在 `Assets/Game/Runtime/Shared/Packages/SongPackageManifest.cs`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `formatVersion` | int | 当前 1 |
| `id` | string | 曲包 id，空则由 `SongPackageStorage.GenerateShortId()` 生成 |
| `title` / `scoreTitle` | string | 曲名；`scoreTitle` 空则取 `title` |
| `composer` / `lyricist` / `arranger` / `singer` / `collaborationLabel` / `description` | string | 元数据，空串兜底 |
| `audioFileName` | string | 默认 `audio.ogg` |
| `jacketFileName` | string | 默认 `jacket.png` |
| `scoreFileName` | string | 默认 `score.json` |
| `videoFileName` | string | 默认空串（播放器不读视频） |
| `fillerSec` | float | 开局前静默时长（秒） |
| `previewStartTimeSec` | float | 预览起点（秒） |
| `musicDifficultyType` | string | 默认 `master` |
| `playLevel` | int | 显示用的等级 |

`Normalize()` 负责给空值兜底，读出后应先调它再使用。

三个容易混的字段：

- `title`：曲名，显示在 live 的 MusicInfo 区域
- `scoreTitle`：谱面名，显示在谱面信息区（默认等于 `title`）
- `collaborationLabel`：联动彩带文案，留空则不显示彩带

### 存储布局

```text
Application.persistentDataPath/
  CustomMusicScores/
    <title>_<shortId>/          每个曲包一个目录（ID 由 SongPackageStorage.GenerateShortId 生成）
      manifest.json
      score.json
      audio.ogg / jacket.jpg
    PlayHistory.jsonl           打歌历史（追加写入，不在曲包目录内）
```

Windows 上 `persistentDataPath` 形如 `%USERPROFILE%\AppData\LocalLow\<公司名>\<产品名>\`。

### `score.json` 的结构

落盘类型是 `Assets/Game/Runtime/OpenSekai/Charts/ChartDocument.cs`，字段与运行时一一对应（不再有编辑器模型）：

| 类型 | 文件 | 说明 |
| --- | --- | --- |
| `ChartDocument` | `Charts/ChartDocument.cs`（41 行） | 顶层：`version`、`notes`、`events`、时间信息 |
| `ChartNote` | `Charts/ChartNote.cs`（79 行） | 音符：轨道、类型、时间、时长、判定宽度等 |
| `ChartEvent` | `Charts/ChartEvent.cs`（12 行） | 事件（BPM、流速等） |
| `ChartTimeline` | `Charts/ChartTimeline.cs`（366 行） | 时间轴换算 |

**旧类型名兼容**：`Assets/Game/Runtime/OpenSekai/Charts/Serialization/ChartJson.cs`（74 行）里注册了一个
`SerializationBinder`，把历史谱面 JSON 里的旧类型名（编辑器时代的 `MusicScoreData` 等）映射到
`ChartDocument` / `ChartNote` / `ChartEvent`。这段只做**类型名映射**，不引用任何已删除类型，所以可以长期保留。

### 读取链路

```text
SongPackageStorage.Enumerate()           发现 CustomMusicScores/* 下含 manifest.json 的目录
  → SongPackage(rootDirectory, manifest) 解析各文件绝对路径（ScorePath / AudioPath / JacketPath）
  → ChartFileReader.Read(SongPackage)    读 score.json → ChartDocument（内部走 ChartJson）
  → RuntimeChartConverter.Convert(...)   谱面 → 运行时音符/事件（含同时押线处理）
  → LiveLogic                           按时间推进判定
```

`ChartFileReader.ReadFile(path, musicId)` 按扩展名分派（`Assets/Game/Runtime/OpenSekai/Infrastructure/Charts/ChartFileReader.cs`）：

| 扩展名 | 处理 |
| --- | --- |
| `.json` | `ChartJson.Read` 直接反序列化成 `ChartDocument` |
| `.sus` / `.txt` | `SusRuntimeConverter` 转成运行时谱面，再 `RuntimeChartImporter.FromMusicScore` 转回 `ChartDocument`（不烘焙镜像、不生成 hold-combo 音符） |
| 其它 | 抛 `InvalidDataException("不支持的谱面格式：…")` |

无论读入什么格式，`ChartFileReader.Write` 只写 JSON，所以导入 SUS 后落盘会统一成 `score.json`。

### 写回

`ChartFileReader.Write(package, document)` 用同一套序列化写 `score.json`。曲库页目前没有「保存谱面」入口，
写入只用于导入时的格式归一与测试。

## 示例

最小可加载曲包（`manifest.json`）：

```json
{
  "formatVersion": 1,
  "id": "demo001",
  "title": "Demo Song",
  "scoreTitle": "Demo Song",
  "audioFileName": "audio.ogg",
  "scoreFileName": "score.json",
  "jacketFileName": "jacket.jpg",
  "fillerSec": 1.9,
  "musicDifficultyType": "master",
  "playLevel": 32
}
```

## 常见问题与故障排查

- **曲库不显示**：确认 `manifest.json` 在该目录第一层；`SongPackageStorage` 只扫描含该文件的子目录。
- **加载失败但曲包看起来正常**：`score.json` 解析异常或音符为空。`ChartJson` 的 binder 只认已知旧类型名，
  未知类型名会抛反序列化异常。
- **改了字段名后旧曲包读不出**：`AssetBundleInfo` 的字段名与 `.oskp` 的字段名都属于对外契约，见
  [ADR-0005](../decisions/0005-no-legacy-compat.md) 的例外说明。

## 相关文档

- [打歌运行时](live-runtime.md)
- [AssetBundle 与清单](asset-bundles.md)
- [打一首歌](../guides/play-a-chart.md)
