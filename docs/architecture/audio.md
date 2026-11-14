# 音频

> 目的：说明游戏里的声音从哪来、时钟为什么用 `Stopwatch` 而不是 `AudioSettings.dspTime`，避免改判定时踩坑。
> 适用读者：开发者（改音频、改时间轴）。
> 前置条件：读过 [打歌运行时](live-runtime.md)。
> 最后更新：2026-11-11

## 概述

有两条音频链路：

1. **曲包音频**（用户导入的歌）：`PackageAudio` 用 `UnityWebRequestMultimedia` 读文件 → `AudioClip` →
   `SoundManager.RegisterExternalAudioClip` 注册成一个 cue，live 直接播。
2. **内置判定和演出音**：`SoundManager` 按 `Assets/StreamingAssets/sound/manifest.csv` 注册 42 个 WAV。
   本地 `CriAtomShim` 提供 cue 播放接口。

## 核心内容

### 曲包音频

```text
SongPackage.AudioPath
  → PackageAudio.LoadAudioClipAsync()      UnityWebRequestMultimedia.GetAudioClip(file://…)
  → Package.AudioLengthMs = clip.length*1000
  → SoundManager.RegisterExternalAudioClip(cueName, cueName, clip)
```

`PackageAudio.GetAudioSamples(count)` 会把整段采样读出来（`AudioClip.GetData`）再按窗口求峰值，
用于波形/预览；调用方必须注意这是**整段拷贝**，不要在每帧调用。

### 引擎音频与 shim

| 位置 | 说明 |
| --- | --- |
| `Assets/Game/Runtime/Platform/CriWare/CriAtomShim.cs` | 注册并播放 `AudioClip`，保留 ACB 加载分支 |
| `Assets/Game/Runtime/Platform/AcbDecoder/` | 由 shim 的 ACB 分支引用，仓库中没有 ACB 资源 |

### 时钟为什么不用 `dspTime`

`dspTime` 反映的是 DSP 缓冲推进，理论上更贴合实际出声时刻；但这套实现里音频是**先全解码再播放**，
播放起点由 `SoundManager` 控制，与 DSP 回调没有对齐保证。因此 `currentGameTime` 用
`Time.realtimeSinceStartupAsDouble`（秒），音乐时间由音频进度换算后传入 `LiveLogic`。

改动音频起点（`fillerSec`、`previewStartTimeSec`）会直接影响判定对齐，改完必须真机回归手感。

## 示例

判断「这首歌的音频多长」：

```csharp
// PackageAudio.RegisterAudioAsync 成功后
long ms = package.AudioLengthMs;   // 已按 clip.length * 1000 写入
```

## 常见问题与故障排查

- **开局卡在「正在加载音频...」**：文件不在 `AudioPath`、扩展名不被 `GetAudioClip` 支持，或文件损坏。
- **声音与谱面不对齐**：先确认 `fillerSec` 与 `timingAdjust` 两个来源是否被同时改动。
- **想换成 dspTime**：不要直接替换；先确认音频播放起点与 `SoundManager` 的调度方式。
- **内置音效缺失**：核对 WAV 文件及其 cue 在 `Assets/StreamingAssets/sound/manifest.csv` 中的对应条目。

## 相关文档

- [打歌运行时](live-runtime.md)
- [曲包与谱面数据](chart-package.md)
- [设置项说明](../reference/live-settings.md)
