# OpenSekai Android 延迟优化计划

源码审计基线为 2026-11-17 工作区，HEAD `eab1bcbb`，Unity `6000.6.7f1`，Input System `1.20.1`，Android minSdk 26。外部《音游触控.md》作为架构提案逐项核对。实施顺序按同日 `09-14-21` 补光侧拍和同局 P0 日志调整。

**P1 音频实机测试收尾，当前进入 P2 触控。** 日常使用 A 档，实测为 48 kHz、144 × 4。触控先修复可回放的问题，再用真机长条和密谱检查正确性。画面反馈继续用补光侧拍看帧数分布。BGM 排程和 Android 原生输入分别由起播、漂移和输入缺失的复现结果触发。

## 1. 已实现

| 提案内容 | 项目现状与证据 |
| --- | --- |
| 用触控事件发生时间计分 | `InputSystemTouchSource` 对按下使用 EnhancedTouch 的 `startTime`，对后续阶段使用该记录的 `time`。默认映射为 `frameMusicTime + clamp(inputTime - frameGameTime, -0.25, 0)`，`frameGameTime` 来自 `Time.realtimeSinceStartupAsDouble`。旧模式把时间差乘以 1/60，接近按当前帧判定，默认关闭。当前的 250 ms 是映射截断，跨帧等待需单独设计。[InputSystemTouchSource](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/InputSystemTouchSource.cs#L13) · [LiveClock](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveClock.cs#L26) · [InputTimeMapping](../../Assets/Game/Runtime/OpenSekai.Core/Simulation/InputTimeMapping.cs) |
| 输入、判定、表现有明确边界 | `ILiveTouchSource` 支持注入，`LiveInputCollector` 和 `LiveInputRouter` 处理触控、去重与车道，`JudgmentScheduler` 负责结算，表现最后更新。按下通道先于当帧按下超时结算。多指、长条归属、松手、滑动、重叠音符和结果发布已有规则，仍由 Live 主线程逐帧驱动。[SoloLiveController](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L199) · [JudgmentScheduler](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/JudgmentScheduler.cs#L30) |
| 音乐时间有单调计时和漂移保护 | BGM 经 `SoundManager` 与 `CriAtomShim` 走 Unity `AudioSource.Play()`。播放位置从 `Stopwatch` 推进，并用主音源 `timeSamples` 观测纠偏。漂移保护的最小死区为 15 ms，达到 60 ms 进入硬重同步决策。前奏、音频结束后使用帧步进，控制器的音乐时间以整毫秒传递。这条时间轴已可用，但实现与提案的 DSP 排程不同。[BaseLiveController](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/BaseLiveController.cs#L458) · [CriAtomShim 起播](../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L797) · [音频纠偏](../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L1164) · [AudioClockDrift](../../Assets/Game/Runtime/OpenSekai.Core/Simulation/AudioClockDrift.cs#L78) |
| 音频、显示、玩家偏移分开 | 设置界面及存储已有 `AudioLatencyMs`、`VisualLatencyMs`、`JudgeOffsetMs`。音频位置为 A，三个偏移为 L、V、J，则判定时间为 `A − L − J`，显示时间为 `A − L + V`，手动判定中的正 J 补偿习惯晚打；自动演奏忽略 J。谱面 `fillerSec` 独立应用。已有旧设置迁移，参考页仍描述旧界面，以代码为准。[设置界面](../../Assets/Game/Runtime/OpenSekai/App/UI/LibrarySettingsPanel.cs#L117) · [TimingCompensation](../../Assets/Game/Runtime/OpenSekai.Core/Simulation/TimingCompensation.cs) · [历史设置迁移](../../Assets/Game/Runtime/OpenSekai.Game/Shared/Settings/LiveSettingsStore.cs#L30) |
| 暂停、恢复和低延迟音频配置已有基础 | 后台或失焦触发暂停，恢复会刷新 Live 输入、恢复音频并重置计时关联。启动时读取 Android 输出属性，按条件调整采样率、DSP 缓冲并记录实际配置。音频设备变更目前更新诊断信息，未联动校准和歌曲会话。[SoloLiveController](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L236) · [恢复流程](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/SoloLiveController.cs#L449) · [AudioOutputTuning](../../Assets/Game/Runtime/OpenSekai.Game/Platform/AudioOutputTuning.cs#L37) |
| P0 采样和汇总 | 诊断开关已能记录 EnhancedTouch 回调、判定消费、判定发布、打击音请求、特效触发、帧时间、BGM 修正和溢出。已在一台真机完成补光侧拍配对。打击音请求埋点位于 `SoundManager` 调用前，播放提交与混音边界尚需补测。 [P0 基线采集手册](../operations/android-latency-p0.md) |

## 2. 可实现，现有链路尚未覆盖

| 缺口 | 依据及改动位置 |
| --- | --- |
| 完整的触控事件流和采样间隔 | Gameplay 读 `activeTouches` 帧视图，保留最新位置和差值，未消费每指完整历史。Input System 本身有历史和事件合并机制，读其历史只能找回 Unity 已收到并保留的样本。现有去重、两遍位置缓存及 128 项候选缓冲按帧视图设计。直接喂入每个 MOVE，可能改变同帧短触、重复敲击、归属与溢出行为。普通 Flick 仍使用 `Time.deltaTime`，部分尾划使用判定帧间隔。[LiveInputCollector](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveInputCollector.cs#L45) · [LiveInputRouter](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveInputRouter.cs#L127) · [FlickNote](../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/FlickNote.cs#L293) · [FlickTailJudge](../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/FlickTailJudge.cs#L93) |
| 跨帧迟到事件和取消语义 | 同帧输入能先于按下 Miss 被消费，但后续帧送来的命中可能遇到已经结算或移出候选的音符。P2 首批已区分 `Canceled` 和 `Ended`，取消用于清理接触状态，正常抬起按事件时间算分。现有长条 50 ms 持握宽限属于玩法规则。[LiveNoteFrame](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveNoteFrame.cs#L115) · [LiveHoldCoordinator](../../Assets/Game/Runtime/OpenSekai.Game/Live/Runtime/LiveHoldCoordinator.cs#L157) · [LongNote](../../Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs#L386) |
| Android MotionEvent、事件队列与时钟映射 | 源 Manifest 已指向自定义 `UnityPlayerActivity`，现有 Java 实现只转发分享回调。可以在同一入口旁路采集 DOWN、UP、CANCEL、当前和历史 MOVE，再批量交给 Unity。需验证实际合并后的 Manifest、Activity 和 IL2CPP 真机运行。API 26–33 走毫秒；API 34+ 可读取纳秒单位的当前和历史事件；API 34 可先用毫秒同源锚点并实测误差；API 35+ 可用 `uptimeNanos()`。原生队列和直接 DSP 映射尚未实现。[Activity](../../Assets/Plugins/Android/com/opensekai/CustomUnityPlayerActivity.java) · [Manifest](../../Assets/Plugins/Android/AndroidManifest.xml#L16) · [MotionEvent](https://developer.android.com/reference/android/view/MotionEvent) · [SystemClock](https://developer.android.com/reference/android/os/SystemClock) |
| DSP 预排程与段落锚点 | 可以让 BGM 使用 `PlayScheduled()`，为开局、跳转和恢复建立新锚点，同时保留现有混音与 SE 路径供比较。统一歌曲位置后继续派生出判定时间和显示时间。DSP 读数粒度及混音与实际输出间隔仍要测量。[Unity PlayScheduled](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/audiosource/playscheduled) · [Unity dspTime](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/audiosettings/dsptime) |
| 校准流程、输出路由档案、低开销测量 | 已有三个可手调值、逐类追踪和性能 HUD。还需节拍校准、路由切换失效策略、物理基线、分位数和丢样统计。当前输出设备枚举只能作为线索，已连接蓝牙设备不等于当前 BGM 正经该设备输出。[AudioOutputTuning](../../Assets/Game/Runtime/OpenSekai.Game/Platform/AudioOutputTuning.cs#L81) · [历史帧率基线](../operations/android-performance.md) |

## 3. 当前架构无法提供的保证

- **从 Activity 拿到所有硬件采样或恢复系统已经丢弃的事件。** Activity 只能看到系统交给应用的事件。NDK 可降低传输开销，仍需按入口和系统版本确认可取得的数据。API 26–33 的公开 Java 接口无法直接调用 `MotionEvent.getEventTimeNanos()`，毫秒时间乘一百万也无法恢复低位精度。[MotionEvent](https://developer.android.com/reference/android/view/MotionEvent) · [Android NDK Input](https://developer.android.com/ndk/reference/group/input)
- **从 DSP、输出属性或缓冲大小直接得出扬声器、耳机或屏幕的实际延迟。** 蓝牙缓冲、显示合成和触控扫描等仍在应用控制边界外，偏移只能补偿可重复的时间差。纳秒单位、低延迟特性标志或历史 165 Hz 的帧时间，都无法证明跨设备亚毫秒判定或端到端低延迟。[Unity dspTime](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/audiosettings/dsptime) · [Android 低延迟音频](https://developer.android.com/games/sdk/oboe/low-latency-audio)
- **让命中反馈早于它依赖的输入，或在主线程卡顿时按原定时刻呈现 Unity 特效。** 时间戳能保留判定所需的时间信息。精确打击音仍须等待命中成立，渲染反馈仍须等到实际呈现。有限队列只能在验证过的负载范围内承诺零丢失，超载需要可观测的恢复策略。

## 4. 分阶段实施与验收

### P0 · 建立可复测的基线

单机基线已完成。补光侧拍使用 OnePlus PLQ110、Android 16、165 Hz、`LatencyTest` Master、L/V/J = 0。Unity 实际输出配置为 48 kHz、144 × 4，Android 报告 144 帧 burst。

| 补光侧拍与同局日志 | 样本 | p50 | p95 / 分布 |
| --- | ---: | ---: | --- |
| 触控事件时间戳 → 判定发布 | 40 | 6.96 ms | 7.60 ms |
| 触控事件时间戳 → 打击音请求 `note_se` | 40 | 7.18 ms | 7.82 ms |
| 判定发布 → 特效触发 | 40 | 0.20 ms | 0.25 ms |
| 敲屏声峰值 → 同轨打击音波形对齐点 | 29 / 40 | 70.29 ms | 75.29 ms |
| 可辨接触 → 首个明确可见反馈 | 20 | 2 帧，约 33 ms | 1 / 2 / 3 帧各 3 / 15 / 2 次 |

同局无路由缓冲溢出和 BGM 纠偏。该谱的 40 次匹配以普通单键为主，密谱、取消、同帧短触和长时间漂移仍待专项覆盖。60 fps 画面每帧约 16.7 ms，先看帧数分布。音频约 70 ms 是共用录音时基下的声学读数，触控事件时间戳使用应用时基，两者的差额只用于定位优先级。早先三个俯拍样本的约 84 ms 属于另一组录制条件，差值无法归因到软件。 [P0 基线采集手册](../operations/android-latency-p0.md)

| 指标 | 测法与建议首轮门槛 |
| --- | --- |
| 判定时间准确性 | 同一事件流重放，对比无卡顿与注入 20/50/100 ms 卡顿的成绩、选中音符、Combo 及时间差。目标是在明确支持的迟到范围内结果一致，超范围需单独记数。候选时钟先看映射残差 p95 ≤ 2 ms、p99 ≤ 4 ms，再看每 30 分钟残差变化是否 ≤ 2 ms。时钟内部残差与物理判定误差分别报告。**首轮已做**：用 11:16:50 真机输入流重放，无卡顿时 1,007 次按键与设备一致。按下分批判定后，50 ms 以内的卡顿不增 Miss、不断 Combo；100 ms 每相位仍多 2 个 Miss。 |
| 实际反馈延迟 | 单机首轮采用下文的声学 A/B 与 60 fps 帧数门槛。推广前用各目标路由的同一采集时基验证 p50/p95。更高精度测量可用接触夹具、音频回环和光电传感器或高帧率相机，并报告测量误差。 |
| 音画校准与稳定性 | 节拍音与屏幕标记用同一采集时基测量，报告校准前后的中位偏差、绝对误差分位数和抖动。扬声器及有线路由校准后绝对误差 p95 ≤ 5 ms 留作高精度测量目标，60 fps 相机只做帧级筛查，蓝牙单独报告。 |

覆盖至少一台 60 Hz 中低端和一台 120/165 Hz 高刷机，按 API 26–33、34、35+ 补足兼容性样本。覆盖扬声器、有线或 USB、蓝牙，冷启动、热机 30 分钟、复读密集谱、暂停恢复、失焦、拔插、前奏跳过、非零起点、Retry 和自动演奏。各核心组合至少三轮，尾部关键对照累计至少 1,000 次可匹配触控；低样本的物理测量报告样本数和测量分辨率，限制 p99 结论。原始数据按构建、设备、路由、刷新率和设置留存。

### P1 · 打击音分段和真机 A/B

已完成一台 OnePlus PLQ110 的扬声器同机 A/B。A 为 48 kHz、144 × 4，B 为 48 kHz、288 × 4，各录 3 局、120 次有效单键。声学 p50 为 A 79.30 ms、B 87.83 ms，三组 B 均慢 8.40–9.63 ms，应用内从请求到首次播放调用均约 0.05 ms。保留 A 作为日常配置。另做两局音源滤波器采样，两档各 40 键，调用 `Play()` 到首个非静音块的 p50 为 A 2.20 ms、B 3.15 ms，p95 为 A 4.22 ms、B 8.24 ms。若恢复音频优化，可先确认 Unity 是否能实际采用比 A 更低的缓冲，并用同机外录验证出声收益。音源与外录来自不同轮次，输出各段的绝对耗时仍待测。

当前手感下暂停进一步调整音频缓冲和 Oboe 原型。已有录音覆盖手机扬声器的整条输出链，DAC 自身耗时没有独立测量。以下是将来重启 P1 时可复用的步骤。

1. **先补应用内边界。** 沿 `TapEffectView.SubmitSe`、`SoundManager`、`CriAtomShim` 为测试打击音配对 `note_se` 请求和 `AudioSource.Play()` 调用。保留 cue、音符和播放标识，分别看首音、稳态和密谱下的延迟，并检查音源池、音频数据就绪和并发。只有请求到 `Play()` 出现可复现的耗时，才针对预热、音源池或提交流程改动。若需要继续区分 Unity 混音等待和设备输出，加入可识别测试声的混音侧探针，并验证它的开销。混音侧时间仍需配合同轨声学读数。[请求点](../../Assets/Game/Runtime/OpenSekai.Game/Live/View/TapEffectView.cs#L518) · [播放调用](../../Assets/Game/Runtime/OpenSekai.Game/Platform/CriWare/CriAtomShim.cs#L797)
2. **先对照现有 Unity 路径。** A 使用当前自动调优和实际 48 kHz、144 × 4。B 保持采样率相同，尝试一个 Unity 实际接受、符合设备条件的不同缓冲配置，记录请求值、Reset 结果以及重读的块长与块数。当前块长已经等于报告的 burst，优先确认有无可用配置差异。若没有更低的有效配置，较大且实际生效的块长可用于判断声学读数对缓冲的敏感程度。若实际配置相同，结束该分支。候选只在启动时应用，保留现有策略作回退。144 × 4 对应 12 ms 的名义缓冲容量，声学延迟仍由实测确定。[AudioOutputTuning](../../Assets/Game/Runtime/OpenSekai.Game/Platform/AudioOutputTuning.cs#L37) · [AudioOutputPlan](../../Assets/Game/Runtime/OpenSekai.Core/Simulation/AudioOutputPlan.cs)
3. **现有设备就能验证。** 固定同一台手机、扬声器、音量、机位、麦克风位置、游戏设置、谱面和 165 Hz 档位，交替录 A/B 各 3 局。每局目标至少 20 个可匹配单键，报告每局中位数、合并 p95、有效比例和排除理由，两组用相同的波形匹配规则。每轮用同一支麦克风在同一音轨录敲屏和扬声器。采集卡单独检查有线输出的波形、爆音或中断。主观手感在揭晓各组读数前记录。用相同设置补一组关闭采样的外录，检查埋点对结果的影响。
4. **单机候选验收。** 与同期 A 组比较，整组声学 p50 至少下降 10 ms 且超过 A 组的局间中位数波动，至少 2/3 对同向改善，合并 p95 同时下降。以 70 ms 为参考，约 60 ms 是首轮筛选目标。比较范围内可匹配比例应相近，打击音无新增爆音、断声或漏放，输入到请求的 p95、判定和帧时无可复现回退。通过后加做冷启动、热机密谱、暂停恢复与路由切换，再进入跨设备验收。
5. **确认输出受限后再试 Oboe/AAudio。** 若多轮仍显示请求很快、声学反馈偏慢，且有效 Unity 配置没有达到收益门槛，做可回退的独立 SE 原型，用同一协议对比。覆盖 BGM 与 SE 并行时的音量、焦点、路由、暂停和连续播放。优先替换证明确实有收益的 SE 路径。[Android 低延迟音频](https://developer.android.com/games/sdk/oboe/low-latency-audio)

### P2 · 可见反馈和输入正确性

**画面。** 对照 P0 的补光侧拍，画面专项 A/B 每组累计至少 30 次清晰配对，保持机位和标注口径。报告 0 / 1 / 2 / 3+ 帧及排除比例。先关联现有的 Unity 帧时和特效触发，另用同机 60 Hz 与 165 Hz 档位、密谱和热机复测。若慢帧可重复定位到游戏内等待或渲染，才做专项修改。候选以中位数从 2 帧降到 1 帧且 3+ 帧占比无回退，或稳定减少可复现的 3+ 帧长尾且中位数无回退，作为当前相机下的验收口径。低于一帧的变化留待高时基测量。

**本轮跳过画面复测。** P2 只改判定时间戳、长尾结算和日志导出，渲染与特效触发路径未变；11:16:50 同谱日志中判定到特效触发 p50 0.24 ms，帧间隔稳定在 6.04 ms，与 P0 一致。事件到消费 p95 不足 8 ms，低于 60 fps 的一帧（16.7 ms），30 次以内的侧拍分不出亚帧变化。旁路采样中唯一的明显长尾发生在系统交付应用之前，位于屏幕左缘，测试机当时开着系统的边缘防误触，属于系统设置而非游戏问题，不再单独复测。改动渲染或反馈路径（如压缩约一帧的 Activity → 回调等待、调整帧节奏），或玩家反馈画面变慢时，再恢复复测，优先使用高帧率相机或光电传感器。

**输入和判定。** 首批让普通长条尾按抬起样本时间检查判定窗并算分，保留同时间戳的有效结束边沿，系统取消只清理该接触及其尾划状态。用实际判定链回放 60 ms 延迟交付、同时间戳边沿、普通尾和划尾取消。真机优先选有普通长尾、划尾和同押的熟悉密谱，看抬键 Fast/Late、意外断条和漏押，辅以 `consumed`、`release_settled` 和帧时间日志。下列后续改动以复现的缺失、采样间隔错误或结算回归为触发条件。

首批改动通过 915 项 EditMode 测试。真机首轮用日常 A 档，关闭混音采样，开启延迟采样。165 Hz 下选一首熟悉的谱面打一至两局，优先记录集中出现异常的位置和手感。首轮可只留游戏日志。若发现可重复的漏押、意外断条或尾划问题，再针对相同谱段收集补光侧拍和对照数据。

真机首轮两局记录 2,585 次按下、2,583 次抬起和 130 次普通长尾抬起结算，采样及路由无溢出。第二局两次按下的 EnhancedTouch 回调和快照在同一帧，但时间戳不同，保留作后续输入源对照。用户报告最后一键后掉帧，当前优先级是复测收尾。旧版同步写出全局 TSV，日志在该写入前截止。导出已移入后台，同时补记结算切换帧和成绩历史保存耗时。

后台导出通过 916 项 EditMode 测试。10:35:02 同谱开启采样复测，明显卡顿未复现，109,365 行完整保存。末帧到进入结算的实时间隔为 17.55 ms，成绩历史保存耗时 15.82 ms，留作性能观测。

三局共 3,904 次按下，发现 3 次同帧抬起的时间被用作按下时间，偏晚 6–13 ms。已在游戏的 EnhancedTouch 适配层改为用 `startTime` 和 `startScreenPosition` 构造按下，保留后续样本的原始记录。虚拟触屏用例覆盖同帧按下后移动和同帧按下后抬起。真机下一轮对照回调和判定侧时间戳。

10:48:29 同谱复测覆盖 1,311 次按下和 1,311 次抬起，全部严格配对。唯一一次按下、抬起在同帧送达，两个原始时间戳相差 11.0 ms，游戏保留了按下时间。该次事件到 EnhancedTouch 回调耗时 37.06 ms，回调后 0.57 ms 被游戏消费，附近主线程帧间隔正常。后续若复现影响手感的同类长尾，再用旁路采样拆分 Android 交付与 Unity 回调前等待。

`CustomUnityPlayerActivity` 旁路采样已上线，跟随「延迟采样」开关，最终 APK 确认使用该 Activity。11:16:50 同谱一局，2,658 条按下、抬起边沿与 EnhancedTouch 全部配对；Unity 实时时钟与 Android `CLOCK_MONOTONIC` 只差固定偏移，残差 p95 0.004 ms。按下的事件 → Activity、Activity → 回调、回调 → 消费 p50 / p95 为 1.23 / 2.11、5.20 / 5.91、0.55 / 0.74 ms，中段约为一帧。EnhancedTouch 时间戳为毫秒取整，平均早 0.54 ms。唯一 ≥ 20 ms 的长尾是首次触控，24.52 ms 耗在事件 → Activity；该段 ≥ 10 ms 的 3 条均在屏幕左下边缘，左缘按下平均 6.61 ms，其余 1.32 ms。长尾发生在系统交付应用之前，暂不接入原生输入。

1. 通过 `ILiveTouchSource` 对比当前快照与 EnhancedTouch 回调或历史；针对 MOVE 合并做同机 A/B，记录样本差和开销。区分离散边沿、逐指轨迹和仍在接触的状态。用接触代次和递增序号区分指针复用及同毫秒不同事件，以时间和序号稳定排序。保留按下、抬起、取消；轨迹按采样时间计算速度，只有相邻样本可信、间距符合上限时才插值。
2. 复用现有三通道判定，保留同押候选竞争。以测出的输入交付尾部确定有上限的等待时间，先处理可命中的事件再发布最终超时结果。验证按下、抬起、长条点、候选退休和结束结算，勿将 250 ms 映射截断或持握宽限当作等待时间。旧会话或超过等待上限的事件按明确规则处理并计数。卡顿回放找到三处相关问题。**已修复**：迟到帧里的按下按事件时间分批判定（间隔 ≥ 1/60 s 另起一批），同一手指再按下或多指先后按下不再被当作同押，50 ms 卡顿不再增加 Miss 或断 Combo，100 ms 卡顿的 Combo 中断从三个相位全断降到一个相位。默认判定已改为逐个按时间判定、跳过候选去重，旧式同帧候选匹配保留在「多押分配」开关里，选旧式判定时机时锁定旧式。**待决定**：音乐时间以整毫秒传给判定，映射随消费帧变化约 1 ms；已接住的摩擦 Flick 按到线帧的时间评级；100 ms 下滑动被合并的 Flick 与滑条连接点。验收用 `StallReplayTests.StallSweep`。
3. 暂停、失焦、跳转、Retry 和后端切换时切换输入会话，隔离旧队列并重建锚点后再开始判定；对仍压在屏幕上的手指明确恢复握持规则，避免暂停菜单输入变成新按下。让系统取消清理手指状态，经独立规则处理长条后续。
4. 扩充已有 `FakeTouchSource` / `LiveHarness` 回放，覆盖同帧 DOWN→UP、同指快速连击、双指同押、乱序及同毫秒、越线与折返、迟到抬键和取消。目标为测试负载范围内边沿零丢失、零重复结算、旧会话零串入，输入热路径在稳态无新增托管分配。实际超载计数并受控清理或暂停，避免残留长按。以现有曲库自动门禁及手动真机谱共同验收。

### P3 · 按证据验证 BGM 锚点和设备校准

- **BGM A/B。** 先测冷起播、跳转、暂停恢复和连续 30 分钟的锚点偏差。复现问题后以现有单调时钟为对照，在起播已准备好的 BGM 上试验 `PlayScheduled`，用 `歌曲位置 = 段落起点 + (映射后的事件 DSP − 排程 DSP) × 播放速率` 派生逻辑和显示时间。引入 DSP 与单调时钟的多组前后夹取采样，跟踪读数粒度、调用抖动和长时漂移，限制同一段时间轴的修正速率。暂停、设备重建、跳转和重新排程建立新段落。验证正负谱面偏移、前奏、静默段、歌曲末尾、多个音轨与暂停恢复，并与现有的自动演奏、视图与分数流程一起对比。起播偏差或漂移有可重复改善且无玩法回退时启用新时钟。
- **校准 A/B。** 复用 L/V/J，维护按实际音频路由选择的 L、按显示模式选择的 V，以及独立的个人 J。听节拍手测只能估计综合偏差，纯设备 L/V 用物理测量验证；避免把同一次听打偏差同时写入 L 和 J。使用多次有效点击的中位数和离散度给出建议值，保留手调和重置，校验旧档迁移与主动设置零值的区别。路由变化时暂停或显式切换档案，在安全边界重新验证音频时间轴后恢复。

### P4 · Android 原生采集按证据升级

若 P2 复现影响判定的边沿缺失、轨迹缺口或明显时间偏差，在现有 Activity 旁路记录 MotionEvent，继续转发 `super.dispatchTouchEvent`，保留菜单、曲库和分享功能。用手势边沿、采样时间与坐标把探针和 Unity 数据配对，区分 Android 前端、Unity 输入处理和 Gameplay 消费各自的丢样。只有探针证明可修复的问题仍在 Unity 输入入口，才接入原生 Gameplay 输入源。每局使用一个输入源，保留 Unity 路径作为回退。旁路探针已实现（`TouchProbe` + `AndroidTouchProbe`）。首轮真机数据未发现 Unity 输入入口的丢样或长尾，触发条件未满足。

原型采用预分配批量缓冲，按生产线程模型选队列，给边沿事件预留容量并记录溢出。跨 JNI 一次传一批，验证指针 ID 复用、横屏、View 到 Surface 的缩放与裁剪、安全区和镜像。若 Java 批量方案本身成为可测瓶颈，再比较 NDK Ring Buffer，验证内存可见性、暂停期间写读边界和 ABI 兼容。验收要求涵盖 P2 的输入门槛，并证明原先复现的设备问题已改善。

## 5. 发布与证据

每阶段交付基线数据、对照结果、已覆盖设备与路由、回归报告和可回退开关。新输入源、DSP 排程、校准和缓冲策略各自可回退，切换在局间或重建会话时生效。收紧判定窗属于手感变更，在跨机型实测后独立决策。

首轮单机通过后，再按 P0 的设备、路由和负载矩阵推广。历史 165 Hz 帧时和自动演奏记录仍用于帧率与流程回归，物理延迟使用本轮及后续同条件 A/B 数据。[真机性能](../operations/android-performance.md)
