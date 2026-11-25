# 音效生成工具

`make_se_pack.py` 用 `samples/` 里的 CC0 录音和合成音生成三组历史源素材及现行共享演出音，共 43 个 WAV（48000 Hz、16 位 PCM）。只用 Python 标准库，结果是确定的：同样的输入每次生成相同的文件。`dsp.py` 是它用到的滤波、重采样、混响和循环处理函数。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AudioSource/Tools/render_se_pack.ps1
```

生成器会直接覆盖 `custom01`、`custom02`、`default`、`live_se_common` 里的成品，请先保存手工修改。当前打击音 `custom03` 由作者单独制作。生成后双击 `AudioSource/Import-All.bat` 导入。如果只想先听，可以传一个输出目录：`render_se_pack.ps1 -OutDir LocalPacks/se-pack`。

## 各组设计

- **custom01「Crystal Hat」**（A 大调）：判定音比第二套高 3 dB。perfect 和 critical 都是 hat 本身；flick = perfect + great + 一层较轻的扫频噪声；connect / trace 是摩擦音和剪刀声叠加。其余 critical 是对应普通版加一点混响。long 以雨天车站环境声为主体，叠加低 6 dB 的铃铛摇晃循环；long_critical 以 phase-90 循环为主体，叠加低 6 dB、带混响的 chime-ball 循环。
- **custom02「Glass Marimba」**（D 大调）：全部合成，不用采样。持续音是严格整周期的垫音和高音闪光点。
- **default**：custom01 降 3 dB，12 个 cue（没有 `se_live_trace_critical`）。
- **live_se_common**：原创琶音铃声、hat 和 loganzsound 的风铃素材，加混响；过场音没有铃声，长 2.2 s 自然衰减。

响度：判定音按开头 80 ms 的 RMS 对齐；long 循环整体比该套 perfect 低 15 dB（critical 低 13.5 dB）；connect / trace 比其他判定音低 6 dB。当前随包 WAV 播放路径不循环，所以循环素材做成 4–7 s，现阶段按住时能持续发声，将来支持循环后也能无缝衔接。

## 素材来源（全部 CC0 1.0）

`samples/` 里的文件保留 Freesound 原文件名。标注“已截短”的文件只保留生成器读取的开头部分（多留 1 s），采样数据逐字节不变，只删去了后半段和元数据块。loganzsound 素材包的原始说明见 `samples/46478__loganzsound__chimes-and-bells_readme_and_license.txt`。

| 文件编号 | 作品 | 作者 | URL | 用于 | 处理 |
| --- | --- | --- | --- | --- | --- |
| 876729 | hat-2 | cat-fox_alex | https://freesound.org/s/876729/ | custom01 判定音（除 connect / trace）、default、倒计时、结算 | 转单声道、44.1→48 kHz、裁剪、滤波、变调 |
| 873723 | Chime Ball Pendant Sparkle Shakes Rapid 01 | loganzsound | https://freesound.org/s/873723/ | custom01 long_critical 叠层、闪光颗粒、all_perfect、过场 | 高通、搁架、响度拉平、截成无缝循环 |
| 876185 | hh_s_s alveolar ejective fricative | rasputinero | https://freesound.org/s/876185/ | custom01 / default 的 connect、trace | 44.1→48 kHz、高通、裁剪，与剪刀声叠加 |
| 737698 | scissors one hit | ibaffette | https://freesound.org/s/737698/ | custom01 / default 的 connect、trace | 转单声道、44.1→48 kHz、高通、裁剪、加宽 |
| 766859 | AmbRain_EXT_RainyTrainStation loop | zusisking | https://freesound.org/s/766859/ | custom01 / default 的 se_live_long 主体 | 响度拉平、120 Hz 高通、截成 7.2 s 交叉淡化循环 |
| 466098 | Phase 90 loop w amp bypassed | humanoisemaker | https://freesound.org/s/466098/ | custom01 / default 的 se_live_long_critical 主体 | 按循环重采样 44.1→48 kHz、80 Hz 高通，整段 4.15 s 作循环 |
| 874637 | Decorative Bell Shakes Long（已截短，前 16 s） | loganzsound | https://freesound.org/s/874637/ | custom01 / default 的 se_live_long 叠层 | 96→48 kHz，取最平稳的 0.6 s 做循环并叠加错位副本 |
| 874639 | Homemade Brass Wind Chimes Light Handling 02（已截短，前 9 s） | loganzsound | https://freesound.org/s/874639/ | SE_AREA_TRANSITION_SEKAI | 96→48 kHz，取 1.6 s，400 Hz–7 kHz 带通，指数衰减，混响 |
| 874685 | 36 Bar Chimes Sweep Ascend 01（已截短，前 6 s） | loganzsound | https://freesound.org/s/874685/ | se_live_clear、se_live_full_combo、过场 | 96→48 kHz，截取、高通、加宽、淡出、混响 |
| 874687 | 36 Bar Chimes Sweep Ascend Long Ring Out 01（已截短，前 9 s） | loganzsound | https://freesound.org/s/874687/ | se_live_all_perfect | 96→48 kHz，截取、高通、加宽、淡出、混响 |

逐 cue 的署名见 `docs/reference/audio-credits.md`。新增素材时把原文件放进 `samples/`，补上这张表和署名表，再运行 `Tools/audit-audio-provenance.ps1`。
