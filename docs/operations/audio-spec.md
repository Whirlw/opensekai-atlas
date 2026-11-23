# 音效创作与交付说明

> 用途：为 OpenSekai 制作不同风格的音效。本文描述现有素材接口和代码中的播放行为；音色、节奏、调性和风格由创作者决定。最后更新：2026-11-15。

## 创作范围

项目现有两套可在设置中切换的音符音效 `custom01`、`custom02`，一套后备音效 `default`，以及共享的演出音效 `live_se_common`。现有完整素材共有 43 个文件：13 + 13 + 12 + 5。制作新风格时可以只交付需要替换的分组或文件，无须重做其他组；导入项目时将新文件覆盖对应源素材，再执行全量导入。

各套风格可以自由选择音源、旋律、音高、声像、时长、响度和效果处理，无需遵循固定题材、音色家族或数值。请在实际游戏中试听，留意密集连打的清晰度、与音乐的平衡、循环接缝和松手时的听感；这些是创作参考，不是固定的时长或响度门槛。

## 交付格式

- 每个 cue 交付一个 WAV（RIFF、未压缩 PCM），16 位，单声道或立体声。当前导入工具接受 48000 Hz 和 44100 Hz；新素材推荐 48000 Hz。
- 设备输出采样率并非固定值：项目会尝试使用设备原生采样率，具体设备可能是 48000 Hz 或其他值。素材与输出采样率不同时可能发生重采样。
- 文件名必须与下表 cue 一致，包括大小写，扩展名为 `.wav`。同一套中的不同音效可以采用不同的声道或时长。
- 提供每个交付文件的来源和授权信息，以便项目维护署名和确认可分发；使用第三方素材时记录作者、作品、授权版本、链接和修改情况。

目录名与项目源素材一致：

```text
音效包/
  custom01/           13 个音符音效
  custom02/           13 个音符音效
  default/            12 个后备音效
  live_se_common/      5 个共享演出音效
```

## 音符音效

`custom01` 和 `custom02` 使用相同的 13 个名称。下表说明当前代码的用途，并非指定音色设计。

| cue 文件名（不含 .wav） | 代码中的用途 |
| --- | --- |
| `se_live_tap` | 点到没有可判定音符的位置时的点击反馈，不是普通音符 PERFECT 的默认音效。 |
| `se_live_perfect` | 普通单点 PERFECT / AUTO；也映射到长条端点的判定反馈。 |
| `se_live_great` | 普通单点 GREAT。 |
| `se_live_good` | 普通单点 GOOD。 |
| `se_live_critical` | critical 普通单点的判定反馈。 |
| `se_live_long` | 普通长条被按住时启动的持续音。 |
| `se_live_long_critical` | critical 长条被按住时启动的持续音。 |
| `se_live_connect` | 普通连接点的判定反馈。 |
| `se_live_connect_critical` | critical 连接点的判定反馈。 |
| `se_live_flick` | 普通划键及普通摩擦划键的判定反馈。 |
| `se_live_flick_critical` | critical 划键及 critical 摩擦划键的判定反馈。 |
| `se_live_trace` | 普通摩擦音符的判定反馈，同时用于普通摩擦长条的保持音。 |
| `se_live_trace_critical` | critical 摩擦音符的判定反馈，同时用于 critical 摩擦长条的保持音。 |

普通单点按判定等级选择 `perfect` / `great` / `good`；其他类型主要按音符类别和 critical 类型选音，不应假设所有音符都根据等级使用这三个 cue。`Bad`、`Miss`、`Pass` 和未产生判定的事件不播放命中音；隐藏摩擦保持音不播放。配对音符的声音还可能被去重。

### 长条和摩擦持续音：当前实现限制

`se_live_long`、`se_live_long_critical`、`se_live_trace`、`se_live_trace_critical` 在长条保持时由 `LongTapEffectView` 启动，并在实际松开、音符移除或清理时停止；短暂离开后仍在宽限期内时不会重新启动。它们的用途是持续伴随按住的声音，可以设计为首尾自然衔接的循环素材。长条端点判定音是另一条反馈，不应把 `se_live_long` 当成只在起始播放的短音。

**当前随包 WAV 播放路径尚未启用循环**：播放器从头播放一次，到文件结尾自然停止；如果按住时间更长，后面会静音，宽限期返回也不会自动重启。代码发出停止时也没有专门的淡出。WAV 内循环点目前没有被该路径读取。因而素材即使制作成无缝循环，也要等播放实现支持循环后才能一直响；现阶段请用实际游戏版本试听，不要把循环效果视为已实现。

摩擦 cue 还会用于摩擦音符的单次判定反馈，因此设计时可兼顾一次触发和持续播放两种用法。普通/critical 变体之间的关系由创作者决定。

### 后备音效

`default` 使用上表除 `se_live_trace_critical` 以外的 12 个名称。代码先查当前选中的音符音效包，再在已加载的其他音效包中查找缺失 cue；后备并非独立的一套固定创作风格。可以沿用某一套，也可以单独设计。新增或删除 cue 应同步检查代码的预解码名单和来源记录。

## 共享演出音效

| cue 文件名（不含 .wav） | 当前用途 |
| --- | --- |
| `SE_LIVE_COUNT_DOWN` | 开局倒计时提示，代码会在倒计时过程中触发。 |
| `se_live_clear` | CLEAR 结算演出。 |
| `se_live_full_combo` | FULL COMBO 结算演出。 |
| `se_live_all_perfect` | ALL PERFECT 结算演出。 |
| `SE_AREA_TRANSITION_SEKAI` | 加载及画面过渡音效。 |

结算音由对应结果演出分别触发，不要求三段依次播放。可以按画面和音乐自由设计；请在实际演出中核对听感。

## 导入与试听

把交付的 WAV 放到项目的 `AudioSource/<分组>/`，双击 `AudioSource/Import-All.bat`。工具检查完整源素材中的各分组、文件格式以及是否意外遗漏已有 cue，成功后复制至 `Assets/StreamingAssets/sound/` 并更新清单。来源信息由项目维护到 `docs/reference/audio-credits.md`；具体操作见 [音频素材替换](audio-replacement.md)。

建议实际试玩普通和 critical 音符、密集连打、长条按住和松手、摩擦及划键，再试听倒计时、过场和结算。测试两套设置时分别确认其音效是否被正确选用；持续音的现有限制以上文为准。

代码参考：`Assets/Game/Runtime/OpenSekai.Game/Live/View/TapEffectView.cs`、`LongTapEffectView.cs`、`LiveResultView.cs`、`CountdownView.cs`、`LiveTransitioner.cs`，以及 `Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs`。
