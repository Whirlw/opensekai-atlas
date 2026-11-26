# 音频素材署名与来源记录

> 目的：每一个随包分发的音频都要能追溯来源与授权。`Tools/audit-audio-provenance.ps1` 读这张表并与工程里的音频比对，缺行即失败。
> 适用读者：做音频替换的人、发版前做合规检查的人。
> 前置条件：读过 [音频素材替换](../operations/audio-replacement.md)、[音频来源建议](../operations/audio-sourcing.md)。
> 最后更新：2026-11-25

## 规则

- `自制` = 自己合成/录制，按本项目 MIT 分发，不需要署名。
- `CC0` = 无需署名，但仍记录来源，便于复核与将来替换。
- `CC BY` / `CC BY-SA` = **必须**在游戏内致谢处署名，并在本表写全：标题、作者、URL、授权版本、改动说明。
- 发布前本表不允许出现 `待定`。

live_se_key 的日期为原件登记日期。作者于 2026-11-25 主动将这 13 个 WAV 原件及其随包发行的 PCM16 版本以 [CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/) 公开。

## 清单

| bundle | cue | 来源 | 作者 | 授权 | URL | 取得日期 | 改动 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| live/tap_se/live_se_key | se_live_connect | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_connect_critical | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_critical | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_flick | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_flick_critical | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_good | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_great | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_long | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_long_critical | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_perfect | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_tap | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_trace | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/tap_se/live_se_key | se_live_trace_critical | AI 生成后由作者手动编辑 | OpenSekai 音效贡献者 | CC0-1.0 | — | 2026-11-25 | 按同名参考音效匹配响度，导出 PCM16 |
| live/sound/live_se/common | SE_LIVE_COUNT_DOWN | CC0 Freesound: hat-2 (876729) + 自制 | cat-fox_alex; OpenSekai procedural audio pack | CC0-1.0 | https://freesound.org/s/876729/ | 2026-11-15 | Single A5 bell beep over filtered hat; 0.22 s |
| live/sound/live_se/common | se_live_clear | CC0 Freesound: hat-2 (876729) + 36-bar sweep ascend 01 (874685) + 自制 | cat-fox_alex, loganzsound; OpenSekai procedural audio pack | CC0-1.0 | https://freesound.org/s/876729/ https://freesound.org/s/874685/ | 2026-11-15 | Original A-major bell arpeggio + excerpted chime sweep, stereo reverb; no melodic quotation |
| live/sound/live_se/common | se_live_full_combo | CC0 Freesound: hat-2 (876729) + 36-bar sweep ascend 01 (874685) + 自制 | cat-fox_alex, loganzsound; OpenSekai procedural audio pack | CC0-1.0 | https://freesound.org/s/876729/ https://freesound.org/s/874685/ | 2026-11-15 | Longer original A-major bell arpeggio and chord + excerpted chime sweep, stereo reverb; no melodic quotation |
| live/sound/live_se/common | se_live_all_perfect | CC0 Freesound: hat-2 (876729) + chime ball rapid shakes (873723) + 36-bar sweep ascend long ring out (874687) + 自制 | cat-fox_alex, loganzsound; OpenSekai procedural audio pack | CC0-1.0 | https://freesound.org/s/876729/ https://freesound.org/s/873723/ https://freesound.org/s/874687/ | 2026-11-15 | Two-octave original A-major arpeggio, bell chord, shimmer, shake sparkle and ring-out excerpt, stereo reverb; no melodic quotation |
| live/sound/live_se/common | SE_AREA_TRANSITION_SEKAI | CC0 Freesound: chime ball rapid shakes (873723) + 36-bar sweep ascend 01 (874685) + brass wind chimes light handling 02 (874639) + 自制 | loganzsound; OpenSekai procedural audio pack | CC0-1.0 | https://freesound.org/s/873723/ https://freesound.org/s/874685/ https://freesound.org/s/874639/ | 2026-11-15 | Soft transition: low-passed swept-noise whoosh, bar-chime glissando and brass wind-chime excerpt, faint shake sparkle, wet reverb; no bell; 0.95 s |
