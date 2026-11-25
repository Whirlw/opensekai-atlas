# 音频素材

游戏随包分发两组 Live 音效。`custom03` 是唯一的内置打击音，包含作者用 AI 生成后手动编辑的 13 个 WAV。`live_se_common` 包含倒计时、过场和结算使用的 5 个共享音效。逐条来源见 [音频署名](../docs/reference/audio-credits.md)。

双击 `Import-All.bat` 导入。请将 FFmpeg 加入 PATH，或运行 `Tools/import-audio-pack.ps1 -Apply -Ffmpeg <可执行文件路径>`。工具从手工编辑的原件生成 PCM16 成品，采样率、声道和时长沿用原件。每个打击音的目标响度已按 `LocalPacks/upstream-audio/custom01` 的同名 WAV 测定并写入脚本。导入只需源文件和这些数值。详见 [音频素材替换](../docs/operations/audio-replacement.md)。

`custom01`、`custom02` 和 `default` 是留存的历史源素材。`Tools/render_se_pack.ps1` 可重新生成这三组及共享演出音。
