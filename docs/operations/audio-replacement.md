# 音频外部音源包

> 目的：说清游戏里的判定音与演出音放在哪、怎么被加载、怎么新增或替换，以及交付时的规格与验收。
> 适用读者：做音频素材的人、改 `SoundManager` 的人、发版前做素材检查的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[音频需求表](audio-spec.md)。
> 最后更新：2026-11-12

## 现状

内置音效与曲包音频通过 WAV 注册，ACB 保留为兼容来源。

- 42 个 wav 放在 `Assets/StreamingAssets/sound/<bundle>/<cue>.wav`；
- `Assets/StreamingAssets/sound/manifest.csv` 列出每个 bundle 的 cue；
- 启动时 `SoundManager.LoadExternalSoundPack()` 由 `ExternalSoundPack` 读 manifest，把每组 WAV 注册为一份外部 cue sheet。同名 bundle 以 WAV 注册结果优先；
- 设备日志应出现四行 `[SoundManager] external pack: N cues for <bundle>`。

相关代码：

| 位置 | 作用 |
| --- | --- |
| `Assets/Game/Runtime/Platform/CriWare/SoundClipLoader.cs` | 用 `UnityWebRequestMultimedia` 读 wav（Android 上 StreamingAssets 在 apk 内，不能直接 `File.ReadAllBytes`） |
| `ExternalSoundPack.cs` | 加载内置 WAV 清单，注册曲包音频及其音乐 bundle 别名 |
| `AudioCatalog.cs` | cue 查找、bundle 注册和预解码名单。判定音优先使用 `live/tap_se/` + `LiveConfig.NoteSeName` |
| `AcbAssetSource.cs` | 兼容 AssetBundle、编辑器资源和 StreamingAssets 的 ACB 读取路径 |
| `VolumeService.cs` | 计算 BGM 和 SE 的播放音量。设置仍由 `ApplicationLocalSettings` 保存 |
| `Assets/Game/Runtime/Sekai/Live/LiveConfig.cs:143-171` | `GetNoteSeName(index)` → `custom01` / `custom02`，对应设置里的「音符音效 1 / 2」 |
| `Assets/Game/Runtime/Sekai/Core/Live/BaseLiveController.cs:126,141` | 进场时加载 `live/sound/live_se/default` 与 `live/tap_se/<皮肤>` |
| `Assets/Game/Runtime/Shared/Audio/PackageAudio.cs:33` | 曲包自带曲目音频，走同一注册入口（属于用户数据） |

## 42 个 cue 的分布

| 组 | 目录 | 文件数 | 用途 |
| --- | --- | ---: | --- |
| 判定音 样式 1 | `sound/live/tap_se/custom01/` | 13 | 六种音符的命中音（设置「音符音效 1」） |
| 判定音 样式 2 | `sound/live/tap_se/custom02/` | 13 | 同上（设置「音符音效 2」） |
| 判定音兜底 | `sound/live/sound/live_se/default/` | 12 | 皮肤缺文件时的后备 |
| 演出 / 结算 | `sound/live/sound/live_se/common/` | 4 | `SE_LIVE_COUNT_DOWN`、`se_live_clear`、`se_live_full_combo`、`se_live_all_perfect` |

逐 cue 的音色要求、时长与声道写在 [音频需求表](audio-spec.md) 第 4 节；逐条署名在 [音频署名](../reference/audio-credits.md)。

## 音频规格

| 项 | 要求 |
| --- | --- |
| 容器 | WAV（RIFF），未压缩 PCM |
| 采样率 | 44100 Hz（不允许 48000 / 22050，避免运行时重采样） |
| 位深 | 16-bit |
| 前导静音 | 判定音 ≤ 2 ms，其余 ≤ 5 ms |
| 峰值 | ≤ −1.0 dBFS，不得削波 |
| 尾部 | 自带 ≥ 5 ms 淡出，不许硬切 |
| 命名 | 与 cue 名完全一致，大小写也保持一致 |

## 新增或替换一个 cue

1. 按表把 wav 放到 `Assets/StreamingAssets/sound/<bundle>/<cue>.wav`（bundle 名用现有名字）。
2. 跑导入脚本校验并重写清单：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/import-audio-pack.ps1 -Apply
   ```

   脚本会检查 44100 / 16-bit PCM，并把文件登记进 `manifest.csv`。
3. 如果新增或删除了 cue，同步改 `AudioCatalog` 里的 `LiveTapPredecodeCueNames` / `LiveDefaultPredecodeCueNames` 两个硬编码数组，否则预解码不会覆盖到。
4. 在 `docs/reference/audio-credits.md` 增加或更新一行署名（来源只能是自制 / CC0 / CC BY）。
5. 跑一次 `Tools/audit-audio-provenance.ps1`，期望 `AUDIT OK`。

`Tools/import-audio-pack.ps1` 要求的交付结构：

```
audio-pack/
  custom01/            13 个 wav
  custom02/            13 个 wav
  default/             12 个 wav
  live_se_common/       4 个 wav
  credits.csv           file,source,author,licence,url,date,change
```

## 验收

1. **工程内自检**：`Tools/audit-audio-provenance.ps1` 输出 `AUDIT OK`，42 条署名无缺口。
2. **真机日志**：冷启动后出现四行 `[SoundManager] external pack: N cues for <bundle>`，数量与 manifest 一致。
3. **试听**：进曲库 → 开局倒计时 → 单点 / 长条 / 划键 / 摩擦 / 隐藏六种音符各打一串 → 设置里切「音符音效 2」再打一遍 → 结算（clear / full combo / all perfect）。
4. **对齐**：判定音不得有可闻延迟；若出现延迟，先查 wav 头部静音与解码路径，而不是改判定。
5. **体积**：新增音频后统计 APK 增量；wav 体积偏大时改 ogg（同样走 `UnityWebRequest`，`AudioType.OGGVORBIS`），并同步更新 manifest 与加载器。

## 相关文档

- [音频需求表](audio-spec.md)
- [音频来源建议](audio-sourcing.md)
- [音频署名](../reference/audio-credits.md)
- [素材来源清单](asset-provenance.md)
- [项目索引](../INDEX.md)
