# 音频素材替换清单

> 目的：把「游戏里所有第三方音频」列清楚，并给出可执行的替换路线与验收标准。这是美术素材替换的最后一个大类。
> 适用读者：做音频素材的人、要做替换工具的人、发版前做素材合规检查的人。
> 前置条件：读过 [素材来源清单](asset-provenance.md)、[材质包导入](material-pack-import.md)。
> 最后更新：2026-11-09

## 路线 A 已实现（2026-11-09）

交付包放在 `StreamingAssets/sound/<bundle>/<cue>.wav`，启动时 `SoundManager.LoadExternalSoundPack()` 读 `StreamingAssets/sound/manifest.csv` 把每组 wav 注册成一份外部 cue sheet，**优先于 ACB**：同名 bundle 的 `_acbByBundleName` 会被覆盖，之后 `LoadSoundBundle` 因为 `ContainsKey` 直接返回，原 ACB 不再加载。

- 导入：`powershell -File Tools/import-audio-pack.ps1 -Apply`（校验 44100/16-bit PCM、写 manifest）
- 加载器：`Assets/Game/Runtime/Platform/CriWare/SoundClipLoader.cs`（`UnityWebRequestMultimedia`，Android 上 StreamingAssets 在 apk 内）
- 多 cue 注册：`CriAtomExAcb.CreateFromAudioClips(...)`
- 设备日志应出现四行：`[SoundManager] external pack: N cues for <bundle>`

## 范围修正（2026-11-09）

**当前版本只用得到「打歌判定音」与「演出/结算音」**，菜单 UI 音从未被任何界面播放（曲库页、HUD 都不调用 `PlaySE`）。因此：

- 菜单三份 cue sheet（188 + 86 + 23 = 297 个 cue、约 9.5 MB）**已从工程移除**，同时去掉 `SoundManager.EnsureDefaultSoundBundlesLoaded()` 与 `LibraryEntryPoint` 里的加载调用
- APK 从 **117.05 MB 降到 96.78 MB**（−20.3 MB；ACB 本身 9.35 MB，其余是打包时的重复与填充）
- 要换的只剩 **4 个 ACB、62 个 cue**：

| 文件 | 角色 | cue |
| --- | --- | ---: |
| `live/tap_se/custom01/custom01.acb` | 判定音 样式 1 | 14 |
| `live/tap_se/custom02/custom02.acb` | 判定音 样式 2 | 20 |
| `live/sound/live_se/default/se_live_default.acb` | 判定音兜底 | 12 |
| `live/sound/live_se/default/se_live_common.acb` | 演出 / 结算音 | 16 |

下面的「负载清单」保留作历史记录（菜单三份已不存在）；交付与验收只看上表这 4 个。若将来某个界面开始播 UI 音，需要把 `Assets/Game/AssetBundles/resources/tutorial/sound/menu/menu_common` 与两份 `StreamingAssets/MenuCommon*.acb` 找回，并恢复那两处加载调用。

## 概述

游戏音频全部走 **CRI ADX2（ACB/HCA）**，没有散装 wav/ogg 参与（唯一例外见下表最后一行的 `maimusicinfo.ogg`）。仓库自带只读解码器 `Assets/Game/Runtime/Platform/AcbDecoder/`，能枚举 cue、解码 HCA，所以**清单与试听可以在工程内完成**，不需要 CRI 工具。

现状：**7 个音频负载、约 11.0 MB、约 359 个 cue**，全部来自原作，必须替换。

## 负载清单

| # | 路径 | 角色 | cue 数 | 大小 | 主要 cue |
| --- | --- | ---: | ---: | ---: | --- |
| 1 | `Assets/Game/AssetBundles/resources/startapp/live/tap_se/custom01/custom01.acb.bytes` | 打歌判定音 **样式 1**（音符音效 1） | 14 | 263.6 KB | `se_live_tap`、`se_live_long`、`se_live_flick`、`se_live_connect`、`se_live_trace`、`se_live_perfect/great/good` 及 `*_critical` |
| 2 | `.../live/tap_se/custom02/custom02.acb.bytes` | 打歌判定音 **样式 2**（音符音效 2） | 20 | 288.3 KB | 同上（原版此处有重复 cue） |
| 3 | `.../live/sound/live_se/default/se_live_default.acb.bytes` | 判定音兜底（未选皮肤或皮肤缺失时） | 12 | 203.0 KB | 与 #1 同名 |
| 4 | `.../live/sound/live_se/default/se_live_common.acb.bytes` | 打歌演出与结算演出音 | 16 | 659.4 KB | `se_live_clear`、`se_live_full_combo`、`se_live_all_perfect`、`se_live_end_*`、`se_live_cutin_*`、`SE_LIVE_FEVER`、`SE_LIVE_SKILL`、`SE_LIVE_COUNT_DOWN` |
| 5 | `Assets/Game/AssetBundles/resources/tutorial/sound/menu/menu_common/MenuCommon.acb.bytes` | 菜单/UI 通用音（**打包内，权威版**） | 188 | 6234.1 KB | `SE_UI_SCROLL`、`SE_DECIDE1`、`SE_CANCEL`、`SE_RESULT_*`、`SE_AREA_*`、`SE_PLA_*`、`SE_MUSIC_*` 等 |
| 6 | `Assets/StreamingAssets/MenuCommon.acb` | 同上，StreamingAssets 备份（AssetRipper 补充，cue 较少） | 86 | 2843.9 KB | 与 #5 子集 |
| 7 | `Assets/StreamingAssets/MenuCommon_Built_in.acb` | 原版启动即加载的 UI 音 | 23 | 499.3 KB | `SE_CANCEL`、`SE_DECIDE1`、`SE_TITLE_LOGO`、`SE_TITLE_ENTER`、`SE_UI_DIALOG_OPEN`、`SE_UI_SWIPE` |
| – | `Assets/Graphics/Resources/maimusicinfo.ogg` | 菜单预览用短音频（非 ACB） | 1 | 130.2 KB | – |

加载点（改代码时要一起看）：

| 位置 | 作用 |
| --- | --- |
| `Assets/Game/Runtime/Sekai/SoundManager.cs:27-83` | `MenuCommon*` 常量、`LIVE_DEFAULT_SOUND_BUNDLE_NAME`、`LIVE_TAP_SE_*`、**硬编码的预解码 cue 列表**（13 条打歌 cue + 20 条默认 cue） |
| `SoundManager.cs:548` | 判定音 bundle = `live/tap_se/` + `LiveConfig.NoteSeName` |
| `SoundManager.cs:204` | `RegisterExternalAudioClip(bundleName, cueName, AudioClip)` —— 现有「绕开 ACB」的入口 |
| `Assets/Game/Runtime/Sekai/Live/LiveConfig.cs:143-171` | `GetNoteSeName(index)` → `custom01` / `custom02`，对应设置里的「音符音效 1 / 2」 |
| `Assets/Game/Runtime/Sekai/Core/Live/BaseLiveController.cs:126,141` | 进场时 `LoadSoundBundle("live/sound/live_se/default")` 与 `("live/tap_se/" + NoteSeName)` |
| `Assets/Game/Runtime/Shared/Audio/PackageAudio.cs:33` | 谱面自带曲目音频走 `RegisterExternalAudioClip`（**这部分是用户曲包，不属于要替换的素材**） |

## 音频规格（实测）

| 类别 | 编解码 | 采样率 | 声道 | 典型时长 |
| --- | --- | --- | --- | --- |
| `se_live_tap` | HCA | 44100 | 1（单声道） | 0.30 s |
| `se_live_long` | HCA | 44100 | 2 | 5.81 s |
| `se_live_flick` | HCA | 44100 | 2 | 1.82 s |
| `se_live_clear` | HCA | 44100 | 2 | 9.68 s |
| `se_live_all_perfect` | HCA | 44100 | 2 | 13.00 s |

替换素材按 **44100 Hz / 16-bit** 交付即可，不必跟随原时长，但要遵守下面几条听感约束：

- **打歌判定音**：起音不许有静音前导（判定与发声要对齐），建议 ≤ 0.12 s 起音、总长 ≤ 0.5 s，峰值 −1 dBFS，避免刺耳的高频毛刺（会与 165 Hz 高刷下的密集击打叠成噪音）。
- **长条 / 划键**：0.8–2 s，尾音自然衰减，不要留硬切。
- **结算演出音**：与画面时长匹配即可；`se_live_all_perfect` 这类允许 10 s 以上。
- **UI 音**：≤ 0.3 s，音量比打歌音低 6–9 dB，避免菜单里频繁点击疲劳。

## 替换路线

### 路线 A（推荐）：散装 wav + 运行时注册

仓库已有 `RegisterExternalAudioClip`，不需要 CRI Atom Craft。做法：

1. 新素材按 `StreamingAssets/sound/<bundle>/<cue>.wav` 落盘，`<bundle>` 用现有 bundle 名（如 `live/tap_se/custom01`）。
2. `SoundManager.LoadSoundBundle` 在 ACB 加载失败时，扫描上述目录，把每个 wav 用 `RegisterExternalAudioClip(bundleName, cueName, clip)` 注册进去。
3. `UnityWebRequestMultimedia.GetAudioClip(..., AudioType.WAV)` 加载，注意 Android 上 `StreamingAssets` 不能直接用 `File.ReadAllBytes`（要走 `UnityWebRequest`，`PackageAudio` 已有同类实现可抄）。
4. 好处：素材任意 DAW 产出、可增量替换、体积可控（wav 会变大，必要时改 ogg + `AudioType.OGGVORBIS`）。

### 路线 B：重做 ACB

用 CRI Atom Craft 生成同名 cue 的 ACB 覆盖现有文件。优点是零代码改动；缺点是工具链与授权成本，且**cue 名与顺序必须完全一致**（`SoundManager` 里预解码列表是硬编码的），不建议。

## 交付清单（按路线 A）

需要作者产出 **55 个 wav**（打歌类）+ **188 个菜单类**（可选，优先前 20 个高频 UI 音）：

| 组 | 目录 | 文件数 | 说明 |
| --- | --- | ---: | --- |
| 判定音 样式 1 | `sound/live/tap_se/custom01/` | 13 | `se_live_tap`、`se_live_long`、`se_live_long_critical`、`se_live_flick`、`se_live_flick_critical`、`se_live_connect`、`se_live_connect_critical`、`se_live_trace`、`se_live_trace_critical`、`se_live_critical`、`se_live_perfect`、`se_live_great`、`se_live_good` |
| 判定音 样式 2 | `sound/live/tap_se/custom02/` | 13 | 同上（可与样式 1 不同音色，便于玩家区分） |
| 判定音兜底 | `sound/live/sound/live_se/default/` | 12 | 与样式 1 同集合（少 `se_live_trace_critical` 之一，按 ACB 实际 cue 为准） |
| 演出/结算 | 同上目录 | 8 | `se_live_clear`、`se_live_full_combo`、`se_live_all_perfect`、`se_live_finish`、`se_live_end_clear`、`se_live_end_perfect`、`se_live_end_all_perfect`、`se_live_end_finish` |
| 菜单/UI | `sound/menu/menu_common/` | 优先 20 | `SE_UI_SCROLL`、`SE_DECIDE1`、`SE_CANCEL`、`SE_TITLE_LOGO`、`SE_TITLE_ENTER`、`SE_UI_DIALOG_OPEN`、`SE_UI_SWIPE`、`SE_COUNT_UP`、`SE_COUNT_DOWN`、`SE_RESULT_*` 等 |

**命名必须与 cue 名完全一致**（大小写不敏感，但建议照抄）。新增/删除 cue 要同步改 `SoundManager` 里 `LiveTapPredecodeCueNames` / `LiveDefaultPredecodeCueNames` 两个硬编码数组，否则预解码不会覆盖到。

## 验收

1. **工程内自检**（可写成 EditMode 测试）：对每个 ACB 用 `AcbDecoder.AcbFile.Load` 枚举 cue，断言「要替换的 cue 都有对应 wav」；再断言 `SoundManager` 的预解码列表 ⊆ cue 集合。
2. **真机试听清单**：进曲库（UI 音）→ 开局倒计时 → 单点/长条/划键/摩擦/隐藏 六种音符各打一串（判定音样式 1）→ 设置里切「音符音效 2」再打一遍 → 结算（clear / full combo / all perfect 三种演出音）。
3. **对齐检查**：判定音不得有可闻延迟；若出现延迟，先查 wav 头部静音与 `AudioType` 解码路径，而不是改判定。
4. **体积**：替换后统计 APK 增量；wav 全量约 +8–12 MB，若不可接受改 ogg（同样走 `UnityWebRequest`，`AudioType.OGGVORBIS`）。

## 机器可读清单

全部 cue 的逐条清单在 [`docs/reference/audio-cues.csv`](../../docs/reference/audio-cues.csv)（360 行，字段 `bundle,index,cue`），由工程内的 `AcbDecoder` 直接枚举生成，ACB 若被替换后需要重新生成：

```csharp
// unity_eval，按同名逻辑遍历各 acb 输出 bundle,index,cue
var acb = AcbDecoder.AcbFile.Load(path);
for (int i = 0; i < acb.Entries.Count; i++)
    sb.AppendLine(bundle + "," + i + ",\"" + acb.Entries[i].Name + "\"");
```

注意：ACB 里存在**同名的重复 cue**（`custom02`、`MenuCommon_Built_in` 都有），替换时必须按 `index` 对齐，不能只按名字覆盖。
## 当前状态

- 清单：已完成（本文件）
- 替换：**未开始**，7 个负载仍是原作音频
- 代码：`RegisterExternalAudioClip` 已存在，但 `LoadSoundBundle` 还没有「ACB 缺失时回落散装 wav」的分支，走路线 A 需要补这一段
