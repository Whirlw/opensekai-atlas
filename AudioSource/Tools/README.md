# 共享演出音生成工具

`make_se_pack.py` 使用 Python 标准库、`dsp.py` 和 `samples/` 中的 CC0 录音，生成倒计时、过场、Clear、Full Combo 和 All Perfect 五个 WAV（48000 Hz、16 位 PCM）。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AudioSource/Tools/render_se_pack.ps1
```

默认写入 `AudioSource/live_se_common/` 并覆盖同名文件。要先试听，可传入 `-OutDir LocalPacks/se-pack`，在该目录的 `live_se_common/` 下查看成品。双击 `AudioSource/Import-All.bat` 可导入正式音源目录中的共享演出音和手工制作的 `live_se_key` 打击音。

## 素材来源（全部 CC0 1.0）

`samples/` 里的文件保留 Freesound 原文件名。标注“已截短”的文件只保留生成器读取的开头部分（多留 1 s），采样数据逐字节不变，只删去了后半段和元数据块。loganzsound 素材包的原始说明见 `samples/46478__loganzsound__chimes-and-bells_readme_and_license.txt`。

| 文件编号 | 作品 | 作者 | URL | 用于 | 处理 |
| --- | --- | --- | --- | --- | --- |
| 876729 | hat-2 | cat-fox_alex | https://freesound.org/s/876729/ | 倒计时、三种结算 | 转单声道、44.1→48 kHz、裁剪、高通滤波 |
| 873723 | Chime Ball Pendant Sparkle Shakes Rapid 01 | loganzsound | https://freesound.org/s/873723/ | se_live_all_perfect、过场 | 截取短促的摇铃声、高通、淡出 |
| 874639 | Homemade Brass Wind Chimes Light Handling 02（已截短，前 9 s） | loganzsound | https://freesound.org/s/874639/ | SE_AREA_TRANSITION_SEKAI | 96→48 kHz，取 1.6 s，400 Hz–7 kHz 带通，指数衰减，混响 |
| 874685 | 36 Bar Chimes Sweep Ascend 01（已截短，前 6 s） | loganzsound | https://freesound.org/s/874685/ | se_live_clear、se_live_full_combo、过场 | 96→48 kHz，截取、高通、加宽、淡出、混响 |
| 874687 | 36 Bar Chimes Sweep Ascend Long Ring Out 01（已截短，前 9 s） | loganzsound | https://freesound.org/s/874687/ | se_live_all_perfect | 96→48 kHz，截取、高通、加宽、淡出、混响 |

逐 cue 的署名见 `docs/reference/audio-credits.md`。新增素材时把原文件放进 `samples/`，补上这张表和署名表，再运行 `Tools/audit-audio-provenance.ps1`。
