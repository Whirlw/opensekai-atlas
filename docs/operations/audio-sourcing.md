# 判定音与演出音的素材来源建议（CC0 / CC BY）

> 目的：在「不能带第三方权利素材」的前提下，给判定音（62 个 cue 里的 46 个）与演出/结算音（16 个）选出可用的来源，并说明各自要付出的合规动作。
> 适用读者：要做音频替换的人、发版前做合规检查的人。
> 前置条件：读过 [音频素材替换](audio-replacement.md)、[素材来源清单](asset-provenance.md)。
> 最后更新：2026-11-09

## 结论先说

判定音这类「几十毫秒的合成短音」**首选自制合成**，不用找库：

- 用 **jsfxr / sfxr / ChipTone / Bfxr**（都是 CC0 工具的产物归使用者）生成点击、短促打击、扫频
- 或用 DAW / Audacity 的噪声爆发 + 包络 + 滤波做
- 好处：0 授权风险、13 个 cue 之间的音色天然统一、体积可控、以后微调不用重新找素材

只有在需要「真实感」（比如木质敲击、玻璃、鼓皮）时才值得去库里找。下面按这个顺序给来源。

## 一、零风险路线：自己合成

| 目标 cue | 合成手法 | 时长 |
| --- | --- | --- |
| `se_live_tap` | 1–2 ms 噪声瞬态 + 3 kHz 带通 + 30 ms 指数衰减 | 0.10 s |
| `se_live_critical` / `*_critical` | 同上，基频抬高一个八度，加 0.5 ms 更硬的瞬态 | 0.12 s |
| `se_live_long` | 60 ms 起音的正弦/三角波，缓释 0.8 s，轻微颤音 | 0.9 s |
| `se_live_flick` | 200 ms 白噪扫频（4 kHz → 800 Hz）+ 高通 | 0.25 s |
| `se_live_connect` | 两个纯音上行三度（如 880 → 1108 Hz）+ 短混响 | 0.35 s |
| `se_live_trace` | 长条音的弱化版，音量 −6 dB | 0.5 s |
| `se_live_perfect` / `great` / `good` | 同一基音的三个亮度：perfect 最亮（高次谐波多），good 最闷 | 0.15 s |
| `se_live_clear` / `full_combo` / `all_perfect` | 上行琶音（3–5 音）+ 混响尾，长度按演出画面 3–13 s | 3–13 s |

工具：`jsfxr`（在线/离线）、`sfxr`（CC0）、`Bfxr`（CC0）、`ChipTone`（CC0）、Audacity + 自带生成器（GPL 程序，但导出音频无版权约束）。

## 二、可用的公开库（按授权从宽到严）

### CC0（无需署名，最省事）

| 来源 | 内容 | 备注 |
| --- | --- | --- |
| **Kenney** — *UI Audio*、*Interface Sounds*、*Impact Sounds*、*Digital Audio* | 点击、确认、错误、短打击、数码音 | 全站 CC0，打包下载，命名规整，最适合 UI/判定音 |
| **OpenGameArt** 的 CC0 筛选 | 各类 8-bit / 拟音包（如 SubspaceAudio 的 512 SFX 合集） | 每个条目页脚写明授权，**必须逐条确认是 CC0**，同站混有 CC BY-SA |
| **Freesound** 的 CC0 筛选（`License: Creative Commons 0`） | 单个拟音最丰富，玻璃/木头/鼓皮都有 | 需要登录下载；同一文件可能被重复上传，注意原始上传者 |
| **Wikimedia Commons** `Category:CC-Zero` | 少量拟音与乐器采样 | 质量参差，胜在授权写得清楚 |
| **Sonatina / VSCO2 CE 等 CC0 采样库** | 乐器单音，可做 `connect`、结算音的琶音 | 采样库授权多为 CC0，逐包确认 |

### CC BY（可用，但必须署名）

| 来源 | 内容 | 合规动作 |
| --- | --- | --- |
| **Freesound** 的 `License: Attribution` | 高质量拟音 | 记录「标题 / 作者 / 来源 URL / 授权版本」，写进 `docs/reference/audio-credits.md` 与游戏内「致谢」 |
| **OpenGameArt** 的 CC BY 条目 | 成套音效 | 同上；注意有的要求「改编作品也 CC BY」 |
| **Incompetech / Kevin MacLeod**（音乐，不适用短音） | BGM | 同上 |

### 不建议（不是 CC0/CC BY，或有再分发限制）

| 来源 | 问题 |
| --- | --- |
| **Sonniss GDC Game Audio Bundle** | 「免版税」但授权禁止把素材本身再分发；嵌进游戏通常允许，条文与 CC 不同，需要单独存档条款 |
| **Pixabay / Mixkit** | 自有许可证，**禁止把内容原样再分发**（游戏内嵌通常允许），不是 CC |
| **ZapSplat 免费区** | 需注册、有署名要求且禁止再分发 |
| **YouTube 音频库** | 条款随视频平台，不适合游戏内嵌 |

## 三、无论走哪条路线都要做的事

1. **每个入库音频一行记录**，落在 `docs/reference/audio-credits.md`：

   ```
   cue                 | 文件                      | 来源 | 作者 | 授权 | URL | 取得日期 | 改动
   se_live_tap         | custom01/se_live_tap.wav  | 自制 | —    | 本项目 | —   | 2026-11-09 | 合成
   se_live_flick       | custom01/se_live_flick.wav| Freesound | @xxx | CC0 | https://... | 2026-11-09 | 裁剪 0.25s + 降 3dB
   ```

2. **CC BY 的作品必须能被用户看到署名**：在「设置」或关于页放一段致谢文本（本项目已有 `docs` 与仓库 README，可在游戏内加一个只读面板）。只写在仓库里通常被认为不够。
3. **保留原始下载件**：放到 `LocalPacks/audio-sources/`（不进 git），只把处理后的成品入库，便于将来复核授权链条。
4. **合规自检**：建议加一条 EditMode 测试或 `Tools/audit-audio-provenance.ps1`——遍历 `StreamingAssets/sound/**` 与 4 个 ACB 的 cue，断言每个都在 `audio-credits.md` 里有记录；缺一条就失败。

## 四、按 cue 的取材建议（如果去库里找）

| cue | 找什么 | 处理 |
| --- | --- | --- |
| `se_live_tap` | 短促木质/塑料敲击、UI 点击 | 砍到 ≤0.15 s，去尾噪，峰值归一 −1 dBFS |
| `se_live_critical`、`*_critical` | 同族但更亮、更硬的点击 | 与 tap 同源，高通 +6 dB 或升调 |
| `se_live_long` | 持续的柔和嗡鸣/弦乐泛音 | 循环点对齐，尾部 200 ms 淡出 |
| `se_live_flick` | whoosh / 纸张划过 / 快速挥动 | 反转（swell→cut）更像「划出去」 |
| `se_live_connect` | 铃、玻璃、双音提示 | 加 0.2 s 短混响 |
| `se_live_perfect / great / good` | 同一素材三个亮度 | 用同一个源，改低通截止（8k / 4k / 1.5k Hz）保证是一家人 |
| `se_live_clear / full_combo / all_perfect / end_*` | 上行琶音、镲片、欢呼类 | 与判定音同调性（同一基音或五度关系），避免结算时突兀 |

## 五、推荐的最小方案

1. 判定音 **13 + 13（两套皮肤）+ 12（兜底）**：自制合成，一套为主，第二套通过换波形/升调区分，兜底直接用第一套的弱化版
2. 演出/结算 **16 个**：用 CC0 的乐器采样（Sonatina 之类）自己做琶音，或直接用 Kenney 的短提示音叠加
3. 全程零 CC BY → **不需要游戏内致谢**，只需要 `audio-credits.md` 里写清「自制」，合规成本最低

## 当前状态

- 本文件只是选型建议，**尚未引入任何新音频**
- 要换的 4 个 ACB / 62 个 cue 见 [音频素材替换](audio-replacement.md)
- 到货后的导入方式见同文档「路线 A」：散装 wav + `RegisterExternalAudioClip`（该分支尚未实现）
