# 音频素材

这里保存游戏内置音效的源文件，按 `custom01`、`custom02`、`default` 和 `live_se_common` 分类。文件名就是游戏使用的 cue 名称。曲包中的歌曲音频由用户导入，不放在这里。

替换或新增 WAV 后，双击 `Import-All.bat` 检查并导入到 Unity 项目的 `Assets/StreamingAssets/sound/`。推荐使用 48000 Hz、16 位 PCM 的单声道或立体声 WAV（也支持 44100 Hz），并同步维护素材来源。导入方法、删除音效和试听检查详见 [音频素材替换](../docs/operations/audio-replacement.md)。

生成脚本和它用到的 CC0 录音位于 `Tools`（与 `ArtSource/Tools` 相同的管理方式）。运行 `Tools/render_se_pack.ps1` 会重新生成并覆盖四个分组里的全部 WAV，请先保存手工修改；素材来源、授权和各组设计见 `Tools/README.md`。

播放、加载和音量代码位于 `Assets/Game/Runtime/OpenSekai.Game/Audio/`；曲包音频代码位于 `Assets/Game/Runtime/OpenSekai.Game/Shared/Audio/`。
