# 音频外部音源包

> 目的：说清游戏里的判定音与演出音放在哪、怎么被加载、怎么新增或替换，以及交付时的规格与验收。
> 适用读者：做音频素材的人、改 `SoundManager` 的人、发版前做素材检查的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[音频需求表](audio-spec.md)。
> 最后更新：2026-11-12

## 现状

内置音效与曲包音频通过 WAV 注册，ACB 保留为兼容来源。

- 43 个 wav 放在 `Assets/StreamingAssets/sound/<bundle>/<cue>.wav`；
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

## 43 个 cue 的分布

| 组 | 目录 | 文件数 | 用途 |
| --- | --- | ---: | --- |
| 判定音 样式 1 | `sound/live/tap_se/custom01/` | 13 | 六种音符的命中音（设置「音符音效 1」） |
| 判定音 样式 2 | `sound/live/tap_se/custom02/` | 13 | 同上（设置「音符音效 2」） |
| 判定音兜底 | `sound/live/sound/live_se/default/` | 12 | 皮肤缺文件时的后备 |
| 演出 / 结算 | `sound/live/sound/live_se/common/` | 5 | 倒计时、场景切换与结算音效 |

逐 cue 的实际用途和创作建议见 [音频需求表](audio-spec.md)；逐条署名见 [音频署名](../reference/audio-credits.md)。

## 音频规格

导入工具接受 44100 或 48000 Hz、16 位 PCM、单声道或立体声 WAV。推荐新素材使用 48000 Hz；实际输出采样率随设备变化。文件名必须与 cue 一致。其他创作和试听建议见需求表。

## 新增或替换一个 cue

源素材集中在项目根目录的 `AudioSource/`，与 `ArtSource/` 一样可以直接浏览和修改。四个子目录分别对应上表四组音效；文件名必须与 cue 名完全一致。游戏运行时仍从 `Assets/StreamingAssets/sound/` 读取，曲包自带的音乐不属于这里。

1. 在 `AudioSource/<分组>/` 中替换或新增 WAV，保留正确的 cue 名称。
2. 双击 `AudioSource/Import-All.bat`。脚本会先检查全部分组、格式和已有 cue，全部通过后才复制文件并更新运行时清单。也可以先只检查：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/import-audio-pack.ps1
   ```

   要从其他位置导入，可加 `-Pack <目录> -Apply`。如果确实要删除已有 cue，移除源文件后加 `-AllowRemove -Apply`；旧的运行时 WAV 不会自动删除，但清单不再引用它。删除前应确认代码不再使用该 cue。
3. 新增或删除 cue 时，同步检查 `AudioCatalog` 中的预解码名单、`docs/reference/audio-cues.csv` 和 `docs/reference/audio-credits.md`。
4. 运行 `Tools/audit-audio-provenance.ps1`，确认来源记录完整，然后在 Unity 中刷新资源并试听。

```text
AudioSource/
  custom01/          音符音效 1
  custom02/          音符音效 2
  default/           判定音兜底
  live_se_common/    演出与结算
  Import-All.bat
```

## 验收

1. **工程内自检**：`Tools/audit-audio-provenance.ps1` 输出 `AUDIT OK`，43 条署名无缺口。
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
