# 音频外部音源包

> 目的：说清游戏里的判定音与演出音放在哪、怎么被加载、怎么新增或替换，以及交付时的规格与验收。
> 适用读者：做音频素材的人、改 `SoundManager` 的人、发版前做素材检查的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[音频需求表](audio-spec.md)。
> 最后更新：2026-11-25

## 随包内容

| 源目录 | 运行时 bundle | cue 数 | 用途 |
| --- | --- | ---: | --- |
| `AudioSource/live_se_key` | `live/tap_se/live_se_key` | 13 | 默认打击音，也是外部资源包缺少某个打击音时的回退 |
| `AudioSource/live_se_common` | `live/sound/live_se/common` | 5 | 倒计时、场景切换与结算 |

`SoundManager.LoadExternalSoundPack()` 读取 `Assets/StreamingAssets/sound/manifest.csv`，按 bundle 注册 WAV。正式打歌、资源包演示和试听使用同一个内置打击音包。

## 导入

1. 按 cue 名将 WAV 放入对应的源目录。支持 44100 或 48000 Hz、单声道或立体声 PCM16，打击音原件也支持 float32。
2. 将 FFmpeg 加入 PATH，双击 `AudioSource/Import-All.bat`。也可以运行以下命令指定程序路径。省略 `-Apply` 可只渲染和校验成品。

   ```powershell
   ./Tools/import-audio-pack.ps1 -Apply -Ffmpeg 'C:/tools/ffmpeg.exe'
   ```

3. 脚本以 EBU R128 积分响度测量每个打击音。短于 400 ms 的片段仅在测量时补静音至一个完整分析窗。目标值取自本地 `LocalPacks/upstream-audio/custom01` 的同名 WAV，已写在脚本中。每个文件只施加恒定增益，再导出 PCM16，复测要求误差不超过 0.1 LU、真峰值低于 0 dBTP。共享演出音原样复制。
4. 新增或移除 cue 时，同步维护 `docs/reference/audio-cues.csv`、`docs/reference/audio-credits.md` 及导入脚本中的目标值。删除清单条目需要 `-AllowRemove`，同时清理对应的运行时 WAV 和 `.meta`。
5. 运行 `Tools/audit-audio-provenance.ps1` 核对逐条记录。在游戏中试听单点、长条、划键、摩擦，以及倒数和结算音效。

逐 cue 用途见 [音频需求表](audio-spec.md)，素材来源见 [音频署名](../reference/audio-credits.md)。
