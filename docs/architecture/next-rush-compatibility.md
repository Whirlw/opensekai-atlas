# NextRUSH+ 兼容基线

核对日期为 2026-11-18。源码锁定为 `1f086fb6fcc56fcb38cbf9eba6b98dbefc630316`，许可为 MIT，版权归 Kyle Chang（2025）。兼容清单依据 play 模式的注册表、输入字段和实际加载逻辑。 [固定源码及许可证](https://github.com/UntitledCharts/sonolus-next-rush-engine/tree/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316)

## 实体分类

| 实体 | 当前处理 |
| --- | --- |
| `Initialization`、`#BPM_CHANGE`、`#TIMESCALE_GROUP` | 接受兼容子集，限制见字段表 |
| `Connector`、`SimLine` | 校验连接，重建基础长条和同时押线 |
| `Normal`、`Critical` 音符 | 两个前缀分别与下表各行组合，再加 `Note` 后缀 |
| `TransientHiddenTickNote` | 附着在线性长条上时提供一个隐藏判定点 |
| `AnchorNote` | P3 |
| `DamageNote`、`TransientHiddenDamageTickNote` | P4 |
| `Fake` 系列 | P4。上游为上述 32 个 Normal/Critical 组合，以及 Anchor、Damage、隐藏 Tick、隐藏 Damage Tick 各提供一个对应的 `Fake` 前缀实体，共 36 个 |
| `#TIMESCALE_CHANGE` | 编译链接事件，供原生分组轨迹求值 |
| `Stage`、`CameraChange`、`StageTransformChange`、`StageMaskChange`、`StagePivotChange`、`StageStyleChange` | P5 |
| `Skill`、`FeverChance`、`FeverStart` | 约定忽略。保留原始实体，只校验 LevelData 语法 |
| `StaticStage`、`_InputManager`、`SlideManager`、`ConnectorSfxManager`、`SlotGlowEffect`、`SlotEffect`、`ComboJudge`、`JudgmentAccuracy`、`DamageFlash`、`ParticleManager` | 引擎生成，作为 LevelData 输入时拒绝 |
| 其余名称 | 未知输入，拒绝 |

| Normal/Critical 后缀 | 已接入基础运行时 | 待 P4 |
| --- | --- | --- |
| 独立 | Tap、Flick、Trace、TraceFlick | Release |
| Head | Tap | Flick、Trace、TraceFlick、Release |
| Tail | Flick、Trace、TraceFlick、Release | Tap |
| 中间 | Tick | |

候选实体仍需通过字段、值域与引用图检查。预检报告 `NextRushCompatibility.Analyze` 可直接用于不可播放的谱面，提供 revision、每个实体类型的数量、分类及预检阻塞原因。成功编译后的 `RuntimeChartDefinition.NextRushCompatibility` 保留该报告。

## 输入字段与缺省值

下表列出 play 资源声明的字段。等号表示显式导入默认值，省略时的游戏效果还需结合回调（如连接透明度、缓动、生命配置）。编辑器数据保留在原始实体上。

| 实体 | 输入字段 | OpenSekai 当前处理 |
| --- | --- | --- |
| LevelData | `bgmOffset`、`entities` | P1 支持有符号偏移，换算规则见 [音乐偏移与三秒规则](native-next-rush.md#音乐偏移与三秒规则) |
| Initialization | `revision=1`、`initialLife=1000`、`firstCamera` | 记录有效 revision。自定义生命待 P4，相机引用需指向 CameraChange，实际使用待 P5 |
| #BPM_CHANGE | `#BEAT`、`#BPM` | 从第 0 拍开始，BPM 为正。同拍多事件拒绝。样本附带的 `meter` 只接受 4（默认 4） |
| #TIMESCALE_GROUP | `first`、`forceNoteSpeed` | 编译各组独立轨迹，保留 `editorName`、`editorHidden` 编辑器元数据 |
| #TIMESCALE_CHANGE | `#BEAT`、`#TIMESCALE`、`#TIMESCALE_SKIP`、`#TIMESCALE_GROUP`、`#TIMESCALE_EASE=0`、`transitionStyle=0`、`hideNotes`、`next` | 已接入分组轨迹。Life Reset Button 附带的 `editorLane` 作为编辑器元数据保留 |
| 全部音符（含 Anchor、Fake、Damage、隐藏点） | `#BEAT`、`#TIMESCALE_GROUP`、`stage`、`lane`、`size`、`elevation=0`、`direction`、`style`、`activeHead`、`isAttached`、`connectorEase`、`isSeparator`、`segmentKind`、`segmentAlpha`、`segmentLayer`、`segmentThroughJudgeLine`、`segmentPresentation`、`attachHead`、`attachTail`、`next`、`prev`、`effectKind` | 基础音符使用 12 轨浮点几何、方向 0–2、线性全不透明连接。其余字段组合见 P2–P5。当前隐藏 Tick 只接受 `#BEAT`、`isAttached=1`、`attachHead`、`attachTail` |
| Connector | `head`、`tail`、`segmentHead`、`segmentTail`、`activeHead`、`activeTail`、`legacyHiddenPop` | 当前要求判定头尾与连接头尾一致，几何片段与 next 相符。独立段和 `legacyHiddenPop` 待 P3 |
| SimLine | `left`、`right` | 校验为同拍音符，按 OpenSekai 设置重建 |
| Stage | `fromStart`、`untilEnd`、`firstMaskChange`、`firstPivotChange`、`firstStyleChange`、`firstTransformChange` | P5 |
| CameraChange | `#BEAT`、`lane`、`size`、`zoom=1`、`zoomTargetLane`、`zoomTargetY`、`zoomVerticalAlign`、`rotate`、`stageTilt=1`、`ease`、`next` | P5 |
| StageTransformChange | `#BEAT`、`stage`、`rotate`、`xLaneTranslate`、`yLaneTranslate`、`elevation`、`anchor`、`rotationCenter`、`rotationCenterXOffset`、`rotationCenterYOffset`、`ease`、`next` | P5 |
| StageMaskChange | `#BEAT`、`stage`、`lane`、`size`、`maskNotes=0`、`ease`、`next` | P5 |
| StagePivotChange | `#BEAT`、`stage`、`lane`、`divisionSize`、`divisionParity`、`yOffset`、`yBeatOffset`、`ease`、`next` | P5 |
| StageStyleChange | `#BEAT`、`stage`、`laneColor=0`、`judgeLineColor`、`judgeLineStyle`、`leftBorderStyle`、`rightBorderStyle`、`fullWidth`、`alpha=1`（弃用）、`laneAlpha`、`judgeLineAlpha`、`divisionLineAlpha=1`、`noteAlpha=1`、`ease`、`next` | P5 |
| Skill | `#BEAT`、`effect=0`、`level=1`、`value=250`、`scale=1`、`duration=6` | 约定忽略全部业务值 |
| FeverChance / FeverStart | `#BEAT`，FeverChance 另有 `force` | 约定忽略全部业务值 |

上游 revision 0 固定 `WEIGHTED_COMBO`，1 和 2 使用选项指定的计分模式。revision 0/1 的高度受舞台倾角影响，2 使用不随倾角缩放的表观高度。P0 保留 revision 并继续使用项目现有计分。上游计分和高度映射分别落在 P4/P6 与 P5。 [计分分支](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/level_config.py) · [高度分支](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/layout.py)

方向 0/1/2 为向上任意/左/右，3/4/5 为向下任意/左/右。变速模式为 TIMESCALE=0 和 SCROLL=1。缓动编号为 NONE=0、LINEAR=1，2–41 为各曲线变体。计分模式 0–3 依次为 WEIGHTED_FLAT、WEIGHTED_COMBO、UNWEIGHTED_FLAT、UNWEIGHTED_COMBO。连接种类、样式及舞台枚举以固定源码为准。 [连接枚举](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/connector.py) · [布局枚举](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/layout.py) · [缓动枚举](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/ease.py)

## 真实样本档案

三个样本来自 UntitledCharts，记录的 engine 名称为 `NextRUSH_P`、资源版本为 13、描述版本为 v2.19.1。读取服务器 playData 并验证 SHA-1 后，核对到 97 个注册实体，与固定源码的 play 注册表一致。所核对的输入字段及显式默认值一致。源码与部署资源的编译产物等价性仍待建立。Kamisama 的来源信息取自已有曲包，另外两个样本的谱面数据重新下载并核对。

| 共享的引擎资源 | SHA-1 |
| --- | --- |
| playData（已校验字节） | `292eda6a567f8c414f650d41db00228d7a0cfa15` |
| configuration（来源元数据） | `e264fe02b5bb05dc0c96b326fc86058e72de72ce` |
| rom（来源元数据） | `29df2ba4f867e95a5c87664c5d595b6c53425748` |

| 样本 | LevelData SHA-1 | 原始 GZip SHA-256 |
| --- | --- | --- |
| You're Walking Down The Hallway · `UnCh-81fb32f7724a4a1eb92ddbbe045082c9` | `8c6d45d2973cd858bb6661d4c6971ec9402c2754` | `81da96ad1331afe019de8b8e49588b76d2fef4a4b523e5c82f50c5fda3c33df5` |
| Kamisama · `UnCh-c22c3d9ac1e94636a10cea38591f6b9a` | `19f7138861b97f9408f2cd822e25bd598c68f0c9` | `76ba7d5809e61ab9f8543c857d51415a96fb112587786788e3b4e876069a4603` |
| Life Reset Button · `UnCh-690d4ea6de0443399b9d8f4be42f815b` | `abee22bc7bcc892da5cfe734f1110b060d8fd891` | `e2423aaafdd0a23786d6f9c29b1c31a503a655fd1a17a888f6e3eb0a5767571c` |

| 样本 | BGM SHA-1 | 封面 SHA-1 |
| --- | --- | --- |
| Hallway（来源元数据） | `fb1014779ab038678e8fb99c65b0b307ac2b2034` | `4d40ba2de8b774a3583bb811bf3e5cb3d2fe1751` |
| Kamisama（曲包来源元数据） | `960ccb6835feafa57289746fa637c4321680a290` | `8653d9aa94b1779651877a132379ef6f3acb89f7` |
| Life Reset Button（来源元数据） | `563927d8ee170ba4809c3e2e4e5bd7f287e64835` | `52e37292425a7d53d3081219e936354dabc54aeb` |

| 样本 | 摘要 | 样本域中服务器未导入的编辑字段 |
| --- | --- | --- |
| Hallway | 995 实体，848 判定点，偏移 0 秒，revision 2 | `meter`、`editorName` |
| Kamisama | 1265 实体，偏移 −0.511 秒，2 组变速、7 事件、51 Anchor、154 隐藏 Tick、178 Connector，缺省 revision 1，含 FeverChance 和 FeverStart 各 1 | `editorName`、`editorHidden` |
| Life Reset Button | 8576 实体，偏移 +1.271 秒，3 组变速、136 事件、4997 Anchor、248 隐藏 Tick、1286 Connector、31 个 HeadTrace，revision 2 | `meter`、`editorName`、`editorLane` |

P3 的 Kamisama 样本需处理 32 个 Anchor 到 Anchor 的连接，以及 11 个 Anchor 到真实尾键的连接。样本使用连接缓动 1–5、片段种类 1 / 2 / 103、透明度 0 / 1 和附着标记 0 / 1。

谱面、引擎资源和媒体置于本地测试目录。后续应使用上表的原始字节哈希确认版本。
