# 打歌资源包 · 音效样包与首轮清册

## 在编辑器里试听

在 Unity 菜单选择 **OpenSekai → Dev → Material Packs → Enable audio ZIP...**，在打开的目录里选 `material-audio.zip`，再从曲库开一局。这个命令也接受自制音效 ZIP，会按清单顺序启用其中所有组并替换当前音效选择。样包的空击和普通 PERFECT 是短促的高音，按住普通长条会听到低音循环。松手、暂停重试、返回曲库后分别检查低音是否及时停止。选择同一菜单下的 **Restore built-in audio** 后再开一局，即可听回原有音效。两个命令都只在局外生效。

样包的两段声音由正弦波合成。短音为 1.6 kHz 衰减音，长音为 300 Hz。长音文件含 5760 个单声道采样帧，循环区间为第 480 至 5280 帧，右端不含。摩擦音单次触发使用整段，持续触发使用 4800 帧循环。两组还可分别启用。[下载样包](../../Tools/fixtures/material-audio.zip)

## 制包与调用

ZIP 根目录放 UTF-8 的 `pack.json`。音效文件使用 16 位 PCM WAV、44.1 或 48 kHz、单声道或双声道。清单中的路径使用正斜杠，大小写与 ZIP 条目一致。同包可另放 PNG 和独立的 COMBO、判定文字及判定特效组。以下是只替换普通长按音的完整清单，ZIP 中还需有 `sounds/hold.wav`。

```json
{
  "schemaVersion": 1,
  "packId": "example.holds",
  "version": "1.0",
  "groups": [
    {
      "id": "soft",
      "category": "audio",
      "slots": { "audio.long": { "file": "sounds/hold.wav" } }
    }
  ]
}
```

从 Unity 主线程调用管理接口。单独导入只增加可选修订，显式保存才改变之后的开局。组引用由 `GroupReference` 生成，包含包 ID、内容摘要和组 ID。同类别列表中先提供该槽的组优先，逐槽指定的优先级更高，值为 `null` 表示强制使用内置音效。

```csharp
var store = MaterialPackStore.Shared;
var installed = await store.ImportAsync(zipPath, cancellationToken);
var state = store.List();
state.Audio.Groups.Insert(0, installed.GroupReference("soft"));
state.Audio.Overrides["audio.tap"] = null;
var preview = store.Resolve(state.Audio);
long revision = store.SaveSelection(state.Audio, state.Revision);
```

`ValidateAsync` 校验 ZIP 而不发布。`Resolve` 逐槽返回来源、候选组和生效原因。`Uninstall(packId, digest, revision)` 移除指定修订并清理下一局选择。修订不匹配或局内写入时调用会失败。加载时发现媒体丢失或内容摘要改变，会汇总受影响的槽位到 `PlayStartResult.MaterialPackDiagnostics` 并沿用各自原有的音效。索引和选择位于应用私有目录 `material-packs/state.json`，上一完整状态保存在同目录的 `.bak` 文件，上一状态引用的媒体会保留到下一次提交。

当前音效试作的临时上限为 ZIP 256 项、单文件 8 MiB、清单 256 KiB、解压合计 32 MiB。组合预算为 32 MiB 解码采样数据，按内容摘要与采样区间去重。该值仅计算选中音效的浮点采样，真机运行峰值还包括读取缓冲、创建 clip 的临时内存及游戏自身开销。

## 已接入的音效槽

下表的名称都以 `audio.` 开头。内置回退沿用所选的打击音套装和原有 cue 查找顺序。所有槽均沿用原播放入口的音量与触发时机。

| 槽位后缀 | 使用处和内置 cue | 验收场景 |
| --- | --- | --- |
| `tap` | `TapEffectView.Unpicked`，`se_live_tap` | 点空轨道 |
| `perfect`、`great`、`good`、`critical` | `TapEffectView`、`LiveSoundDefine.NextRushCue`，对应 `se_live_perfect/great/good/critical` | 普通与 critical 音符、不同判定档 |
| `long`、`longCritical` | `LongTapEffectView`、`NextRushConnectorAudio`，`se_live_long` / `se_live_long_critical` | 按住超过 WAV 时长、松手、重试 |
| `connect`、`connectCritical` | 连接点，`se_live_connect` / `se_live_connect_critical` | 长条中继 |
| `flick`、`flickCritical` | 划键，`se_live_flick` / `se_live_flick_critical` | 普通与 critical 划键及摩擦划键 |
| `trace`、`traceCritical` | 摩擦判定与摩擦保持，`se_live_trace` / `se_live_trace_critical` | 同一 WAV 单次使用全长，保持时使用循环区间 |
| `countdown` | `CountdownView`，`SE_LIVE_COUNT_DOWN` | 倒数 |
| `clear`、`fullCombo`、`allPerfect` | `LiveResultView.Execute`，`se_live_clear/full_combo/all_perfect` | 三种成功结果 |
| `startVoice` | `LiveFrontView`，`se_live_start` | 原设置启用开始语音 |
| `finishVoice`、`clearVoice`、`fullComboVoice`、`allPerfectVoice` | `LiveResultView.PlayEndVoice`，`se_live_end_finish/clear/perfect/all_perfect` | 启用结束语音后触发失败、耗尽、Clear、FC、AP |

## 已核对的图像入口

COMBO、判定文字和判定特效已接入打歌资源包，其余图像入口仍消费内置资源。

| 类别 | 当前素材与消费点 | 首轮验证位置 |
| --- | --- | --- |
| 背景 | `Background2DView` 的 `backgroundRenderer` 使用 `live/2dmode/background/default`，封面由 `jackets` 显示，视频另有渲染表面 | 全屏、封面遮罩、MV 与亮度 |
| Notes | `NotesViewManager` 建立普通／critical 各类音头池。`NoteLineView` 消费 `longNoteLine.png` 与 `traceLine_eff.png`，`PairNoteLineView` 消费 `simultaneousLine.png` | 局部轨道，重点检查弯曲、变宽和 NextRUSH 四层网格的 UV |
| 轨道 | `LaneView` 的 `defaultLaneBase`、`defaultLaneLine`、`defaultJudgeLine` 分别持有底板、分道线、判定线 | 轨道视觉子节点、透明度和触摸投影 |
| HUD | `ScoreView` 绑定分数槽与段位字，`LifeView` 绑定普通／受伤血条和暂停／跳过按钮，`LiveFrontView` 管理 Auto 标识 | 分数跨档、血条填充、暂停点击区 |
| 判定文字 | `JudgmentView` 依判定档取图，覆盖含 AUTO 的六种主文字。快慢提示由 `JudgmentDescriptionView` 在同帧最差判定确定后显示。[制包与试用说明](material-pack-judgment.md) | 连续同档、Auto 显示设置、同帧多个音符 |
| COMBO | `ComboView` 为普通、AP、光圈各提供一个标签及 0–9 字形，共 33 个槽。[制包与试用说明](material-pack-combo.md) | 跨位数、AP 转普通、断连、重试 |
| 判定特效 | `TapEffectView` 为普通、critical、摩擦、划键、长条和连接点建立效果池，`LongTapEffectView` 持有持续模板，NextRUSH 另有轨道反馈。[判定特效制包说明](material-pack-hit-fx.md) | 轨道局部空间，开始、持续、释放、暂停重开 |
| 结算特效 | `LiveResultView` 先淡入背景，再选择 `fx_result_failed_v2`、`fx_result_finish`、`fx_result_clear_v2`、`fx_result_full_combo_v2` 或 `fx_result_all_perfect_v2` | 全屏，五种结果的触发与跳过 |

## 已验证的边界

2026-11-21，OPPO PLQ110 的独立 Android 测试播放器使用本页 ZIP 通过两项用例。一项检查实际播放源的单次和循环 clip 长度、共用 clip、重试停止和退出释放。另一项在安装后破坏短音文件，检查短音改用原声、长按仍播放样包。打击到出声的物理延迟、完整打歌的内存峰值和视觉素材仍待测量。[实际播放测试](../../Assets/Tests/PlayMode/MaterialPackPlaybackTests.cs) · [包与组合测试](../../Assets/Tests/EditMode/App/MaterialPackStoreTests.cs)
