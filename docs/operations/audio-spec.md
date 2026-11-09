# 音频素材需求表（对外交付用）

> 目的：这是一份**自包含**的需求书，交给外部制作方（人或另一个 AI）按表产出音频，交付后可直接替换游戏里的原始音频，不再需要二次加工。
> 适用读者：接了这份活的人/AI、以及验收的人。
> 前置条件：无。技术规格与命名在本文内自足；工程背景只作参考。
> 最后更新：2026-11-09

## 1. 项目与要替换什么

这是一个开源的下落式音游（Project Sekai 风格，四键 + 判定线）。游戏里的打歌音效与结算演出音目前仍是原作的，必须全部替换。

当前版本**只用到两类音频**，菜单/UI 音已从工程移除，不在本次范围：

| 类别 | 作用 | 文件数 |
| --- | --- | ---: |
| 打歌判定音（两套皮肤 + 一套兜底） | 六种音符命中时发声，玩家可在设置里切换音色 | 38 |
| 打歌演出 / 结算音 | 开局倒计时提示音、结算画面的 clear / full combo / all perfect 演出 | 4 |

**合计 42 个 wav 文件**。

## 2. 全局技术规格（硬性）

| 项 | 要求 |
| --- | --- |
| 容器 | WAV（RIFF），未压缩 PCM |
| 采样率 | **44100 Hz**（不允许 48000/22050，避免运行时重采样） |
| 位深 | 16-bit |
| 声道 | 按第 4 节每个 cue 指定（判定音多数是单声道或窄立体声） |
| 峰值 | ≤ **−1.0 dBFS**，不得削波 |
| 前导静音 | **≤ 5 ms**（判定音必须 ≤ 2 ms，否则与判定错位） |
| 尾部 | 必须自带淡出（≥ 5 ms），不许硬切出爆音 |
| DC 偏移 | < 0.5% |
| 文件命名 | 与 cue 名**完全一致**，全小写（除已经大写的 `SE_` 开头项），扩展名 `.wav` |
| 时长 | 见第 4 节；判定音**超时即不合格**（拖尾会糊掉连打） |

响度参考：判定音以 **−14 LUFS 短时**为目标（短促素材用响度计读 integrated 会不稳，可用 RMS 近似：−18 dBFS RMS ±2 dB）；结算音 **−16 LUFS integrated**。

## 3. 游戏内的判定与音效触发（决定音色取向）

六种音符各有普通与 critical（critical = 高分音符，音色应更亮/更"贵"，但不能刺耳）：

| 音符 | 玩家动作 | 发声次数 |
| --- | --- | --- |
| Tap | 点一下 | 1 次 |
| Long（长条） | 按住，中途每半拍有保持检查点 | 头 1 次 + 尾 1 次 |
| Flick（划键） | 按住后向左右划 | 1 次（划出去时） |
| Friction（摩擦） | 按住沿长条滑 | 走 `trace` 音 |
| Connect | 长条中的连接点 | 每点 1 次 |
| Hidden | 无判定，不发声 | — |

判定等级：`perfect` / `great` / `good`（`bad` 与 `miss` **不发声**）。等级音必须**同源**，只改亮度，玩家才能听出"这一下打得好不好"而不用看判定字。

## 4. 逐 cue 需求表

### 4.1 判定音 — 样式 1（`custom01`，13 个）

输出目录：`audio-pack/custom01/`

| cue | 触发 | 音色方向 | 时长 | 声道 |
| --- | --- | --- | --- | --- |
| `se_live_tap` | 普通单点命中 | 干脆的木质/玻璃点击，短促无尾 | 0.08–0.15 s | mono |
| `se_live_critical` | critical 单点命中 | 同 tap 家族，更亮更硬（高通 +6 dB 或升八度） | 0.10–0.18 s | mono |
| `se_live_perfect` | PERFECT 判定 | tap 的"高亮版"，带一点金属泛音 | 0.12–0.20 s | mono |
| `se_live_great` | GREAT 判定 | 同源，低通 ~4 kHz，稍闷 | 0.12–0.20 s | mono |
| `se_live_good` | GOOD 判定 | 同源，低通 ~1.5 kHz，更闷更轻 | 0.12–0.20 s | mono |
| `se_live_long` | 长条**起始**按住 | 起音清晰的柔和"嗡"，可延续 | 0.30–0.60 s | stereo |
| `se_live_long_critical` | critical 长条起始 | 同 long，亮度提高 | 0.30–0.60 s | stereo |
| `se_live_flick` | 划键成功 | 短 whoosh（噪声扫频 4k→800 Hz） | 0.15–0.30 s | stereo |
| `se_live_flick_critical` | critical 划键 | 同 flick，加高频"叮" | 0.15–0.30 s | stereo |
| `se_live_connect` | 连接点 | 铃/玻璃双音（上行三度），短混响 | 0.25–0.40 s | stereo |
| `se_live_connect_critical` | critical 连接点 | 同上，加一个八度 | 0.25–0.40 s | stereo |
| `se_live_trace` | 摩擦音符移动 | 极轻的持续摩擦/气流声，**必须不抢主音** | 0.30–0.50 s | stereo |
| `se_live_trace_critical` | critical 摩擦 | 同 trace，略亮 | 0.30–0.50 s | stereo |

### 4.2 判定音 — 样式 2（`custom02`，13 个）

输出目录：`audio-pack/custom02/`，**cue 名与 4.1 完全相同**。

设计要求：与样式 1 必须**可听辨不同**（玩家在设置里切换要能听出差异），但同属"星空/电子"气质。推荐做法：

- 换波形与音高：样式 1 偏"木质点击 + 玻璃"，样式 2 偏"合成器 pluck + 金属短鸣"
- 保持时长、声道、响度规格不变（不许靠变长/变响来区分）
- `perfect/great/good` 仍然只差亮度，不要换音高

### 4.3 判定音 — 兜底（`default`，12 个）

输出目录：`audio-pack/default/`。

cue 集合 = 4.1 去掉 `se_live_trace_critical`（共 12 个）。用途：玩家选的皮肤缺文件时的后备。

推荐做法：直接用**样式 1 的 −3 dB 弱化版**，不要另做一套。

### 4.4 演出 / 结算（`live_se_common`，4 个必做）

输出目录：`audio-pack/live_se_common/`

| cue | 触发 | 音色方向 | 时长 | 声道 |
| --- | --- | --- | --- | --- |
| `SE_LIVE_COUNT_DOWN` | 开局倒计时提示 | 短促的单音"嘀"，可连响 3 次 | ≤ 0.20 s | mono |
| `se_live_clear` | 结算：CLEAR | 中性上扬，2–3 音琶音 | 1.5–3.0 s | stereo |
| `se_live_full_combo` | 结算：FULL COMBO | 比 clear 更亮，加铃/镲 | 2.0–4.0 s | stereo |
| `se_live_all_perfect` | 结算：ALL PERFECT | 最华丽：上行 4–5 音 + 混响尾，允许更长 | 3.0–6.0 s | stereo |

三者的**调性必须一致**（同一基音或五度关系），因为它们在同一画面位置依次出现，玩家会直接对比。

### 4.5 可选（本次不是必需，做了更好）

`live_se_common` 里还有 12 个 cue 目前没有任何触发点（多人/Fever/技能系统尚未实现），**可以不做**：`SE_LIVE_CUTIN_A`、`SE_LIVE_CUTIN_COMBO`、`SE_LIVE_FEVER`、`SE_LIVE_FEVER_CHANCE`、`SE_LIVE_PRAISE_CUTIN`、`SE_LIVE_SKILL`、`se_live_cutin_left`、`se_live_cutin_right`、`se_live_finish`、`se_praise`、`SE_UI_MATCH_TEAM_01`、`SE_UI_MATCH_TEAM_02`。

## 5. 反向需求（明确不要）

1. **不要**使用任何无授权采样：来源只能是「自己合成/录制」或 **CC0**；若用 CC BY，必须在交付的 `credits.csv` 写全标题/作者/URL/授权版本，否则我方无法发布。
2. **不要**人声、歌词、可识别的旋律引用（避免与任何音乐作品冲突）。
3. **不要**超过第 4 节标注的时长（判定音拖尾会糊掉密集连打，是这类项目最常见的失败）。
4. **不要**用 48 kHz 或 mp3/ogg 交付（我方按 44.1 kHz WAV 直读）。
5. **不要**靠"更响"来区分 critical（玩家会疲劳）；用亮度/泛音。
6. **不要**给判定音加长混响（连打时混响会堆积成噪音）。

## 6. 客观自检（交付前请自己跑一遍）

用 `ffmpeg` / `sox` 抽查，下面命令对每个文件都成立才算合格：

```bash
# 时长（秒）与采样率
ffprobe -v error -show_entries stream=sample_rate,channels -show_entries format=duration -of default=nw=1 file.wav

# 峰值（必须 ≤ -1 dBFS）
ffmpeg -v error -i file.wav -af volumedetect -f null - 2>&1 | grep max_volume

# 前导静音（判定音 ≤ 2ms）
ffmpeg -v error -i file.wav -af silencedetect=noise=-50dB:d=0.005 -f null - 2>&1 | grep silence_start

# 是否有真峰值削波
sox file.wav -n stat 2>&1 | grep -E "Maximum amplitude|Pk lev dB"
```

## 7. 交付结构

```
audio-pack/
  custom01/            13 个 wav（4.1）
  custom02/            13 个 wav（4.2）
  default/             12 个 wav（4.3）
  live_se_common/       4 个 wav（4.4）
  credits.csv          每行一个文件：file,source,author,licence,url,date,change
  selfcheck.md         你跑第 6 节命令的结果粘贴（每个文件的峰值/时长/前导静音）
```

`credits.csv` 的 `source` 只能是 `自制` / `CC0` / `CC BY`；`CC0` 以下也请填来源链接（方便将来复核）。

## 8. 我方如何验收

1. 文件清单与命名逐条比对（42 个，缺一即退回）
2. 自动化检查：采样率/位深/时长/峰值/前导静音（同第 6 节，我方用脚本批量跑）
3. 入库后真机试听，按这份清单逐项确认：
   - 单点连打 16 分音符 3 秒，判定音不糊、不叠加爆音
   - 长条按住 → 尾判，两段音色衔接自然
   - 划键、摩擦、连接点各自可辨
   - `perfect/great/good` 三档能盲听区分
   - 设置里切「音符音效 1 / 2」，两套能盲听区分
   - 结算三种演出音与画面节奏对得上
4. 合规：`credits.csv` 覆盖全部 42 个文件，且没有 `待定`

## 9. 参考：这些音在工程里的位置（仅供理解，不需要你改）

| 逻辑 bundle | 工程路径（替换后由我方导入） |
| --- | --- |
| `custom01` | `Assets/Game/AssetBundles/resources/startapp/live/tap_se/custom01/` |
| `custom02` | `Assets/Game/AssetBundles/resources/startapp/live/tap_se/custom02/` |
| `default` | `Assets/Game/AssetBundles/resources/startapp/live/sound/live_se/default/` |
| `live_se_common` | 同上目录（`se_live_common`） |

我方会把你交付的 wav 放到 `StreamingAssets/sound/<bundle>/<cue>.wav`，由运行时 `RegisterExternalAudioClip` 注册，不再需要 CRI ACB 格式。因此**文件名就是接口**，不要自行加前缀或改大小写。
