# 音频

> 目的：说明游戏里的声音从哪来、时钟为什么用 `Stopwatch` 而不是 `AudioSettings.dspTime`，避免改判定时踩坑。
> 适用读者：开发者（改音频、改时间轴）。
> 前置条件：读过 [打歌运行时](live-runtime.md)。
> 最后更新：2026-11-02

## 概述

有两条音频链路：

1. **曲包音频**（用户导入的歌）：`PackageAudio` 用 `UnityWebRequestMultimedia` 读文件 → `AudioClip` →
   `SoundManager.RegisterExternalAudioClip` 注册成一个 cue，live 直接播。
2. **引擎内置音频**（SE、菜单音）：走 `CriWare` 命名空间，但真正跑的是本地实现 `CriAtomShim`，
   它把 `.acb`/`.hca` 解码成 `AudioClip`。

官方 CRI 插件资源仍在 `Assets/CRIMW`（28.5 MB / 203 文件），但同名类型被 shim 遮蔽，`.acf` 配置不参与运行。

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
| `Assets/Scripts/Assembly-CSharp/CriWare/CriAtomShim.cs` | 遮蔽官方 `CriAtom` 同名类型，提供最小可用实现 |
| `Assets/CRIMW/` | 官方 CRI 资源与 `libcri_ware_unity.so`（Android 包里 2.1 + 1.8 MB） |

`.acb` 通过 `AcbDecoder` 解出 `.hca`，再由 `HcaDecoder` **全量解码**成 PCM 填进 `AudioClip`。
这意味着：加载一首歌的音频时间与文件时长成正比，且内存里会有一份完整采样。

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
- **`.acb` 播放没声音**：确认走的是 shim 而不是官方 CRI 初始化路径（后者在本工程里没有初始化）。

## 相关文档

- [打歌运行时](live-runtime.md)
- [曲包与谱面数据](chart-package.md)
- [设置项说明](../reference/live-settings.md)
