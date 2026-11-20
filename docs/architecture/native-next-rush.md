# NextRUSH+ 原生谱面加载

> 更新于 2026-11-18。已实现并行加载、基础玩法、原始曲包导入、P0 兼容预检和 P1 有符号音乐时间映射。

预检记录 `Skill`、`FeverChance`、`FeverStart` 后跳过其业务逻辑。预检字段、固定版本和真实样本哈希见 [兼容基线](next-rush-compatibility.md)，后续阶段见 [NextRUSH+ 完整支持计划](../plans/next-rush-support-plan.md)。

## 结构

```text
现有 JSON / SUS → ChartFileReader → RuntimeChartConverter ──┐
                                                         ├→ RuntimeChartDefinition → PlaySession → Live
NextRUSH+ LevelData → NextRushChart → NextRushChartLoader ──┘
                              ↘ NextRushCompatibility 预检
```

NextRUSH+ 不经过 ChartDocument，不生成中间 score.json，也不反向转换成旧格式。
原始压缩谱面作为曲包文件保存，成绩哈希继续基于该文件的原始字节。
`RuntimeChartDefinition.Document` 在原生路径为 null，`NextRush` 保留解析后的原始实体；可玩标记、判定点数、最大 ticks 由对应加载器构建。
播放入口改用 `RuntimeChartLoader.Load`，再由 `PlaySession.FromChart` 接入共同运行时。旧的 `PlaySession.Create` 保留给现有调用与测试。
零判定点的原生谱面可通过曲库的自动游玩观赏。播放入口检查转换后是否有可显示的音符、Guide 或已启用的同时押线。手动开局会提示切换到自动游玩，仅含初始化和 BPM 的谱面仍会提示没有可游玩的音符。

| 位置 | 职责 |
| --- | --- |
| `OpenSekai.Core/Charts/NextRushChart.cs` | 不依赖 Unity 的数据解析，保留实体名称、archetype 和 value/ref 字段；拒绝重复名称/字段、非有限数值 |
| `OpenSekai.Core/Charts/NextRushCompatibility.cs` | 按固定上游 play 输入分类，记录 revision、实体类型和数量、约定忽略项与预检阻塞原因 |
| `OpenSekai/Infrastructure/Charts/NextRushChartLoader.cs` | 读取 GZip/JSON；解释 native BPM、音符及连接，直接创建 MusicScore；检查兼容性 |
| `OpenSekai/Infrastructure/Charts/RuntimeChartLoader.cs` | 根据曲包格式标识分派，未知格式报错 |
| `OpenSekai/Charts/RuntimeChartDefinition.cs` | 两种来源共用的播放结果与统计 |
| `OpenSekai/Infrastructure/Packages/SongPackageManager.cs` | 原样替换 native 文件、保留格式标识，继续复用目录和 ZIP 导入/导出 |

路径均相对 `Assets/Game/Runtime/`。

## 曲包约定

原生曲包示例：

```text
manifest.json
level-data.json.gz
audio.mp3
jacket.png
sonolus-source.json   可选，下载工具保留服务器返回的详情与 engine 信息
```

manifest 示例：

```json
{
  "formatVersion": 1,
  "id": "example-native",
  "chartFormat": "next-rush",
  "title": "Example",
  "scoreTitle": "Example",
  "scoreFileName": "level-data.json.gz",
  "audioFileName": "audio.mp3",
  "jacketFileName": "jacket.png",
  "musicDifficultyType": "master",
  "playLevel": 20,
  "secForMusicScoreMaker": 0,
  "fillerSec": 0
}
```

`chartFormat` 为空时继续使用现有格式；也识别显式文件后缀 `.next-rush.json` / `.next-rush.json.gz`。
不能仅因为 JSON 包含 entities 就认作 NextRUSH+，其它 Sonolus 引擎也使用 LevelData。
曲包 ZIP 的文件名可以为 `.zip` 或 `.oskp`，继续通过曲库已有导入按钮安装。

`ReplaceScoreFile` 对显式 native 后缀校验解析后原样复制；替换回旧格式时会清除 chartFormat。
普通 `.json` 的旧读取行为保持不变，调用方不应将 native 数据交给 ChartFileReader。

## 当前兼容范围

- 接受 Initialization revision 0、1、2，缺省为 1。`initialLife` 接受非负的 32 位整数，缺省为 1000，开局和重开均以谱面值初始化生命和 HUD。旧谱面沿用皮肤配置的初始生命。当前使用项目自己的计分规则。
- 保留 `Skill`、`FeverChance`、`FeverStart` 原始实体和忽略记录。它们不创建播放事件或判定点。引用方仍须满足所用字段的实体类型。
- 第 0 拍对应谱面时间 0 秒。首个 BPM 事件可以在负拍、第 0 拍或正拍。OpenSekai 将首个 BPM 向前延伸，直至覆盖更早的音符和变速事件。此后每次 BPM 变化从事件所在拍生效。数值型 `meter` 可随事件变化，时间换算只使用拍数与 BPM。原生时间计算使用 double，共同运行时的时间字段为 float。
- Normal/Critical Tap、Flick、Trace、TraceFlick、Release。已支持的 Flick 和 TraceFlick（含划尾）接受 `direction` 0–5，分别显示向上、左上、右上、向下、左下、右下的箭头。直上和直下接受任意划动方向，斜箭头以目标方向为中心接受 ±90° 内的划动。加载器的镜像选项翻转轨道并交换左右斜向。
- HeadTap、HeadTrace、HeadRelease、HeadFlick、HeadTraceFlick 起点，Tick 中继，TailTap、TailRelease、TailFlick、TailTrace、TailTraceFlick 尾部。Anchor 可作隐藏中继、无判定尾和 Guide 端点，允许同拍几何连接。Guide 可以借用独立键或尾键的几何，自身无判定。
- 按实体字符串名称建立引用关系，实体顺序无关。Connector 的 `head/tail` 表示实际边，`segmentHead/segmentTail` 提供覆盖该边的样式区间，`activeHead/activeTail` 对应当前长条判定区间。继续读取早期加载器接受的连接表示。
- `legacyHiddenPop=1` 时，从谱面时间 −2 秒起，在 `segmentHead` 自己的变速组中查找它首次进入可见范围的时间，并计入上游的提前显示余量。预读时长使用玩家流速。到达该时刻后，条身仍按实际端点、画面裁切和当前的 `hideNotes` 绘制。字段省略或为 0 时，条身按普通连接段显示。
- 同一条几何链可在连续可判定段和 Guide 段之间切换。HeadTap、HeadTrace、HeadRelease、Anchor 和 Tick 均可作为可判定段起点。HeadTrace 和自身无 `activeHead` 且未附着的 Tick 在前后各 5 个 60 Hz 参考帧（±83.3 ms）内接受接触。Tick 参与条身几何时也按自身字段选窗。HeadTrace、HeadRelease 作为单独的键结算，条身继续使用连接几何。相邻段共享交界的几何，头、尾和 Tick 各自只结算一次。
- 独立 Release、HeadRelease 与 Guide 端点的 TailRelease 按抬手时间和松手窗口结算，同一抬手只分配给一个候选。TailTap 沿用现有 Tap 判定表，要求在尾键位置重新按下。即使原长条已结束或未按住，尾键仍可独立完成判定。
- HeadFlick 需要在头部按下，再由已进入该划键的手指划动。HeadTraceFlick 接受移动中的接触，提前做对方向的划动时等头部到达判定线再结算。手指进入头部后可以划出原轨道。两类头部均独立于条身计分，按谱面方向判定，漏掉头部后仍可接住后续条身。极短的 HeadFlick 长条须等头部收到按下或判 Miss 后，才接受抬手判定尾键。
- 独立 DamageNote 在目标时刻检查谱面范围内的触碰，每个键计入一个判定点。触碰判 Miss、扣 40 点生命并打断 Combo，避开判成功，自动播放安全通过。伤害键与同轨、同拍的普通键可独立判定。键身和中央晶体使用由 Friction 绘图脚本生成的紫色贴图。
- DamageNote 可作为普通或高亮可判定连接段的 `activeHead`。起点只检查一次伤害，沿条身移动的伤害键面仅供显示。触碰或避开起点后都可接住后续 Tick；短段的 Release 尾要等起点结算后才能抬手。手动长条持续音到达段尾即停止。
- 隐藏伤害 Tick（`TransientHiddenDamageTickNote`）也可开启普通或高亮连接段。起点沿用隐藏伤害点的接触窗口，起点和沿条身移动的键面均隐藏。短段及分叉上的 Release 尾都等待该伤害点结算。
- 连续 Damage Connector 支持默认紫色及 31–38 号颜色。碰撞范围随曲线移动，条身在有接触时点亮。扣血由谱面中各个 TransientHiddenDamageTickNote 单独结算。每个隐藏点从严格早于其目标拍的最后一个半拍网格点开始生效，有 `activeHead` 时取两者较晚者。窗口内首次接触即判 Miss，否则在目标时间结算为成功。支持附着点、跨前一个几何节点的窗口及无连接的独立隐藏点。
- 普通长条头可作为 Damage 段的 `activeHead`。头键按自身键型判定一次，到拍后沿连接路径显示。接触条身会点亮该段，触碰谱面指定的伤害判定点才会扣血。
- 普通 Tap（`NormalTapNote`、`CriticalTapNote`）可作为可判定连接段的 `activeHead`。Tap 仍按一次按下结算，条身在起点到拍后沿连接路径显示移动头。按中 Tap 后可以同帧接住条身，漏掉 Tap 后仍可在后续位置接回并判定 Tick 和尾键。
- 普通 Flick（`NormalFlickNote`、`CriticalFlickNote`）也可作为 `activeHead`。在窗口内按下即可接住到拍的条身，Flick 的成绩由已登记手指的划动单独决定。漏判后仍可在后续位置接回。起点显示谱面指定的方向箭头，移动头沿条身只显示键面。
- 普通 Trace（`NormalTraceNote`、`CriticalTraceNote`）可作为可判定连接段的 `activeHead`。已按住的手指可以滑入起点，提前进入接触窗口会在到拍时结算一次。移动头沿条身显示 Trace 键面。起点漏判后，仍可接回后续 Tick 和尾键；同一判定段中途断开再出现时，接回时机按原 Trace 的时间计算。
- 普通 TraceFlick（`NormalTraceFlickNote`、`CriticalTraceFlickNote`）可作为可判定连接段的 `activeHead`。已按住的手指可滑入起点并划动，提前划对方向会在到拍时结算。方向箭头显示在起点，移动头只显示键面和中心标记。短段的 Release 尾可以在起点取得一次按下或完成判定后接受抬手，同拍重叠的 Tap 优先取得按下。起点漏判后仍可接回后续 Tick 和尾键。
- 普通 Release（`NormalReleaseNote`、`CriticalReleaseNote`）可作为可判定连接段的 `activeHead`。在起点的轨道和时间窗口内抬手，起点只结算一次。随后可用另一根手指接住移动条身，起点漏判后也可接回后续 Tick。若短段的尾键也是 Release，起点结算后，尾键才接受另一次抬手。
- 普通 Tap、Flick、Trace、TraceFlick 和 Release 也可作为 Damage 连接段的 `activeHead`。移动头保留起点的键型和高亮状态，优先采用连接段指定的颜色。划动箭头只在起点显示，移动头本身不增加判定点。
- 同一判定段的两条相邻 Connector 可以共用一个可判定长条头作为中间节点。条身经过该头的位置，并沿用它给下一段指定的曲线。中途头键按自身的 Tap、Trace、Flick、TraceFlick 或 Release 动作独立判定一次。后续 Tick 和尾键仍归谱面指定的原判定段。
- 可判定长条尾也可作为同一判定段的中间节点。中途 Tap 需要重新按下，Trace 检查接触，Release 检查抬手，Flick 和 TraceFlick 都可用已经按住的手指划动。每个尾键只判定一次，后续连接仍沿该节点指定的曲线延伸到真正的段尾。
- 普通或高亮 TailTap 可以结束上一判定段，同时作为下一段连接的 `activeHead`。交接处需要重新按下，TailTap 只判定一次。按中即可接住新条身；漏按后仍可在后续位置接回 Tick 和尾键。
- 普通或高亮 TailFlick 也可结束上一判定段并开始下一段。上一段按住的手指可以直接划尾，交接处只结算一次。重新按下可在划尾结算前接住新段；漏划后仍可接回后续 Tick。方向箭头显示在交接处，移动头沿新段只显示键面。
- 普通或高亮 TailTrace 可用同一个尾键结束上一段并开始下一段。按住的手指可直接滑入交接处，接触窗口为提前 6.5 帧到延后 8 帧（每帧按 1/60 秒计算）。提前接触在到拍时结算一次，移动头沿新段显示 Trace 键面。漏接后仍可接住后续 Tick 和尾键。
- 普通或高亮 TailTraceFlick 可作为下一段的 `activeHead`。上一段按住的手指可直接划过交接处，尾键只结算一次。新段的条身可在到拍后接触，分叉短段的 Release 尾须等交接键结算后才能抬手。方向箭头显示在交接处，移动头保留键面和中心标记。
- 普通或高亮 TailRelease 可同时作为上一段的尾键和下一段的 `activeHead`。交接处抬手只结算一次；新段可由另一根手指接住。漏掉交接键后仍可接回后续 Tick。若下一段也以 Release 收尾，即使两键同轨且窗口重叠，也需要两次独立抬手才能全部命中。
- 36 种 Fake 实体共用对应键型的显示规则。Fake 键到判定线时退场。Fake Anchor 与两种 Fake 隐藏 Tick 保持隐藏，可以参与连接和附着。51–53、61–68、71–78、81–88 号 Fake 连接沿谱面曲线显示条身和移动头。Fake 不注册判定、不播放命中反馈、不扣生命，也不进入 Combo 或计分分母。真实长条可借 Fake 节点成形。Fake 普通或高亮条身始终激活，其中的真实可见和隐藏 Tick 各自按时结算。
- 开局仍要求谱面至少有一个真实判定点。
- Tick 可用作判定段起点或终点。端点 Tick 使用自身的轨道和接触判定，条身结束时仍可独立结算。Guide 可以借用其它链的非附着节点而保持原来的判定归属。
- 可判定 Connector 支持一处分叉、多路合流，以及同一判定区间包含不连续的几何边。每条分支按自己的端点、曲线、宽度和变速组显示并接收接触；共享的头、尾和可见 Tick 按源实体各结算一次。附在具体分支上的隐藏 Tick 由该分支处理。区间内只有谱面明确给出的几何边会产生条身，中间可以留空或使用 Guide。独立的判定端点可以早于或晚于绘制区间。
- 相邻连接共用同一对 `activeHead` 和 `activeTail` 时，普通或高亮条身可以在中间 Anchor 切换成 Damage 或 Fake 条身，也可以从 Damage 或 Fake 切回。切换处保持几何连续，头键和尾键各结算一次。
- 隐藏伤害点的接触窗口可以跨过合流点。窗口起始处沿附着节点的 `prev` 回溯到谱面指定的来路，再沿该节点的 `next` 计算触碰位置。旧谱面省略 `prev` 时，只在来路唯一处补全引用。
- 可见 Tick 和隐藏 Tick 可以用相同的 `attachHead`、`attachTail` 直接附在分叉点或合流点。先用 Tick 或节点指定的 `activeHead` 筛选相邻判定段，再依次匹配节点的 `next`、`prev`。没有匹配引用时可使用唯一候选。若仍有多个候选，加载器会报告归属不明确。分支尾端的可见 Tick 独立接收触碰，同拍隐藏 Tick 仍检查所选分支的接触状态。
- 按谱面创建 TransientHiddenTickNote，允许附在连接两端，也允许与可见 Tick 同拍。可见 Tick 和独立键可附着在同一条连接的相邻几何节点上，使用曲线计算判定轨道，以两端各自的变速轨迹计算纵向显示位置。每个原生判定点单独结算。
- 普通隐藏 Tick 省略 `isAttached` 或设为 0 时，直接使用自身的 `lane`、`size`。独立 Tick 可记住窗口内的提前接触，在目标时间结算。带 `activeHead` 的 Tick 在到拍帧检查接触，可通过 `next` 随后续节点插值。Tick 作为 Connector 端点时也只计一次。两种写法均隐藏键面，手动结算静音。
- Connector 可以经过附着分隔点，也可从附着端点起止。附着点从源路径继承轨道、宽度和变速轨迹，相邻子段使用源缓动的对应区间。`next` 和 `prev` 可以经过附着点、跳过附着点，也可以只声明一个方向。条身按显式 Connector 分段，单独带有前后引用的可见或隐藏 Tick 仍按所属区间结算一次。
- 可判定音符与 Anchor 均使用浮点轨道边界，允许零宽、部分越界及完全位于舞台外；边界按中心轨道减、加半宽计算，可超过中心线两侧 4096 轨。镜像翻转中心位置并保留半宽。边界按运行时单精度坐标保存；端点或跨度超出 32 位轨道索引范围，以及浮点精度无法表示的零宽键，会在加载时报错。连接缓动 0–41 用于绘制、条身命中范围和附着位置，其中回弹及弹性曲线保留超调。普通和高亮长条支持带颜色变体，透明度固定为 1。8 种 Guide 颜色按样式端点的透明度渐变，保留大于 1 的源值参与插值。Guide 中间也可借用独立键的几何，独立键照常结算一次。样式可引用跨链的区间，连接是否隐藏及是否穿判定线由样式起点决定。穿线时按两端的变速轨迹绘制。无判定尾的连接在尾节点过线后最多继续绘制 5 秒，真实尾键仍按本身规则结算。
- 连接按样式起点的 `segmentLayer` 排在舞台下方、舞台上方的两个层级，或音符键上方。同层 Guide 位于普通和高亮条上方。原生 Guide 使用 NextRUSH+ v3 参考皮肤的纯色色板，在谱面指定的宽度内填色，透明度沿端点插值。`segmentPresentation=1` 在该边的时间范围内铺满游戏镜头，透明度按谱面时间插值，并遵循样式起点变速组的隐藏设置。用 Life Reset Button 原谱核对了第 55.5–64 拍的粉紫色全屏效果，以及第 187 和 188.6875 拍附近的白色和红色纯色笔画。[参考皮肤图集](https://untitledcharts.com/sonolus/repository/6bfa776e45202685a99f35829b3c7605ad6e65f3)
- 同一拍只要有显式 `SimLine`，就按谱面指定的左右端点逐条绘制，包括纯真实键和含 Fake 键的组合。该拍没有 `SimLine` 时沿用本地自动配对。关闭同时押线设置后两者都不显示。
- 跨拍或同拍不同变速组的 `SimLine` 保留谱面的左右端点顺序，分别按两端轨迹计算位置，并在可视边界裁切线段。两端进度差从 0.5 增至 1 时连线从完全可见线性淡出。任一端的组隐藏，或两端分处判定线两侧时，连线暂时隐藏。超过左端点的目标时间或任一端点退场后结束。两端所在拍均使用谱面指定的连线。
- Anchor 和隐藏 Tick 可作为连线的一端或两端。连线按端点轨迹独立出现，无需生成键面。附着的隐藏 Tick 使用其插值位置和实际判定节点；任一隐藏端点宽度为零时不显示连线。
- 变速组支持独立轨迹、跳距、隐藏、强制速度及 TIMESCALE / SCROLL 混用。适用于当前可编译的音符与线性长条。
- 原始文件最多 32 MiB；解压后最多 32 Mi 个字符；实体最多 100000，JSON 深度最多 32。

独立窄键、零宽键和舞台外音符在无变速组时也使用原生浮点几何。零宽键隐藏键面，仍按谱面中心和该键型的接触容差判定。触控横坐标可超出首末轨，镜像同步翻转键位。附着点的两个引用若指向同一个同拍附着点，可以逐层回溯到源连接取得曲线位置和宽度；纵向滚动使用直接引用点的变速组。加载时校验引用并拒绝循环。

独立常规键、可见 Tick 和隐藏 Tick 可以引用同一连接路径上的两个非相邻节点。横向位置和宽度按这两个参考点、谱面时间和起点继承的缓动插值，纵向位置使用直接参考点各自的变速组。参考点可以只提供几何，也可以再次附着并作为新 Connector 的起点或终点。加载器先按引用关系求出各层几何，允许打乱实体顺序，并检查循环和拍数范围。

跨边 Tick 按自身拍数归入路径上对应的判定段。路径经过分叉时，可用 `activeHead` 区分同拍的候选分支。如果仍有多个候选或没有可判定的条身，加载器报告归属错误。独立可见键只需确定插值位置。它可以引用一个同拍 Anchor，也可以在两个独立 Anchor 之间插值，即使谱面没有 Connector。

普通可见或隐藏 Tick 可附着在可判定长条、Damage 条、Guide 或跨链装饰连接上，并以另一条覆盖该拍的可判定长条作为 `activeHead`。这时附着端点提供位置、宽度和滚动轨迹，手动演奏在 Tick 到拍帧检查该位置的接触。若归属长条以按下起始，该头键需先收到按下或完成判定。同拍按中头键和 Tick 时，两者各结算一次。

连接型可见 Tick、跨条附着的隐藏 Tick，以及未附着但带 `activeHead` 的隐藏 Tick，都在过线帧按实际帧间隔查找命中范围。先沿前序节点回查本帧最早过线的未附着可见连接型 Tick，并使用该点的位置和宽度。如果回查无结果，就沿连接跨过已到时的几何节点，跳过附着节点，碰到本帧过线的第一个可见连接型 Tick 即停下。若停在两个几何节点之间，则按当前线段的缓动插值位置和宽度。同条附着的隐藏 Tick 沿用 `LongHoldCombo` 的 ±83.3 ms 条身接触窗与丢手宽限。

独立 Tap、Flick、Trace、TraceFlick 和 Release 也可以从附着路径取得键位，同时用 `activeHead` 引用另一条可判定长条。这些键在插值位置按自身动作结算一次。Release 还会检查所引用的头键：可判定头键须先完成判定，或在支持捕获按下时已收到按下，随后才接受抬手。
可见的 `DamageNote` 也支持这种跨长条附着。到达目标拍时，它按附着路径插值得到的键位检查接触，触碰该位置会触发一次伤害。玩家可以在同拍按住另一条长条上的 `activeHead`，只要手指在伤害键范围外就能避开伤害。

普通隐藏 Tick 的 `attachHead`、`attachTail` 均可引用其他已附着的普通隐藏 Tick。加载器先解析被引用点，再用子 Tick 的拍数在两端之间插值；两端引用同一个同拍点时沿用该点的位置和宽度。多层引用不依赖实体排列顺序。每个隐藏 Tick 保留自己的判定，循环引用会在加载时报错。

`Connector.head` 和 `Connector.tail` 也可直接引用已附着的普通隐藏 Tick。连接段使用该点插值后的位置、宽度和对应的曲线区间。相邻连接段共用一个隐藏 Tick 时，该 Tick 只结算一次。Guide 也可借用可判定长条上的隐藏 Tick 作为端点，Tick 的判定仍归原长条。

`style` 使用当前皮肤的键面、箭头、Tick、击中粒子和轨道闪光生成颜色变体。0 保留原图，1–8 依次为中性、红、绿、蓝、黄、紫、青、黑。调色保留纹理的明暗和透明度。彩色连接段的移动头使用连接段颜色，其他连接段使用样式起点的音符颜色。

`effectKind` 独立选择单次打击音。0 根据谱面键型选择，1 静音，2–5 为普通 Basic、Flick、Trace、Tick，6–9 为对应的高亮音，10 为 Damage。手动命中和自动播放共用此映射，普通漏按静音。Damage 音在触碰伤害键时播放。隐藏 Tick 的手动结算静音；自动播放时可按显式覆盖值发声。声音使用当前游戏音效包，判定动作沿用原键型。

长条持续音使用音效包里的普通和高亮长条音，各占一个通道。手动演奏由 `activeHead.segmentKind` 选音，按键头可提前起音，摩擦头可缓存提前接触，到拍后起音。手指离开当前曲线路径即停，重新接触可再起音。Fake 活动段在头键就绪后自动保持。自动播放按首条连接的 `segmentHead.segmentKind` 选音，在判定区间的头尾切换。组隐藏期间静音。Guide、Damage 和零时长区间自身没有持续音。

同通道的新起音会停止旧音并从头播放，新音结束后旧音保持静音，直到再次触发。跨过音频素材末尾时，播放器会循环该素材；暂停、重试和退出会停止播放。

当前仍有限制：

- 动态舞台。
- 未知实体或字段。

原生数据加载支持不等于复刻完整 NextRUSH+ 引擎。目前复用本项目判定、计分、音效和画面；后续玩法扩展应在原始数据保留的前提下逐项接入运行时。

## 分组变速

加载器按 `first` 和 `next` 编译各组，校验事件归属、顺序、循环及孤立事件。同拍事件保留链接顺序，最后一个事件决定该拍之后的速度和隐藏状态，跳距逐项累加。跳距用事件所在拍的 BPM 换算为秒。`editorName`、`editorHidden`、`editorLane` 作为编辑器元数据保留。

TIMESCALE 积分速度并累加跳距。SCROLL 使用当前瞬时速度与含跳距的时间宽度。混合模式在模式边界组合正向和反向距离，按组缓存每帧采样，使用预编译索引查找事件与组合区间。正的 `forceNoteSpeed` 取代该组的玩家显示速度，隐藏事件只影响画面。

画面按补偿后的谱面时间求轨迹，音符与基础长条的端点使用各自所属组，按原有判定时钟处理输入与成绩。显示调度在可能进入画面前激活，出画后可重入。纯 TIMESCALE 和单向 SCROLL 使用预编译的入场界限，其他 SCROLL 组合使用保守的提前激活，复杂谱面的成本仍需实测。

上游导出器允许有限的 `forceNoteSpeed`。播放公式在 12 时给出 0.35 秒的提前显示时间；大于 12 时，分数次幂的底数变为负数。OpenSekai 保留谱面原值，对大于 12 的值统一使用 0.35 秒作为该组的基础提前显示时间。组内变速事件继续影响实际入场时刻。[上游显示时间公式](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/layout.py#L1325-L1331)

事件速度与 BPM 换算后的跳距各限于绝对值 10⁹。数值溢出的混合变速链会在加载时拒绝。变速缓动接受上游无超调的曲线和阶跃，距离采用 double 解析积分。[固定上游轨迹实现](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/sekai/lib/timescale.py)

## 音乐偏移与三秒规则

以秒为单位，`音频时间 = 谱面时间 + bgmOffset`。音频文件第 0 秒对应谱面时间 `−bgmOffset`。正偏移让第 0 拍晚于文件开头，负偏移让第 0 拍早于文件开头。固定上游的 SUS 分析器将 `WAVEOFFSET` 取反并交给 USC 转换器，后者把该值写入 `bgmOffset`。 [SUS 分析器](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/js/src/sus/analyze.ts) · [USC 转换器](https://github.com/UntitledCharts/sonolus-next-rush-engine/blob/1f086fb6fcc56fcb38cbf9eba6b98dbefc630316/js/src/usc/convert.ts)

用原始音频交叉核对。Kamisama 首个音符在第 2 拍、119.5 BPM，换算到音频约 0.493 秒，解码后起音约 0.495 秒。Life Reset Button 首个音符在第 0.0625 拍、200 BPM，换算到音频约 1.290 秒，解码后起音约 1.280 秒。起音以 5 ms 帧估算。

运行时保留音符的谱面时间。Live 统一用文件时间启动、暂停、恢复、重开和执行跳过。判定用 `音频时间 − bgmOffset − L − J`，画面用 `音频时间 − bgmOffset − L + V`，自动播放的 `J` 为 0。原生偏移以 double 保留，曲包原有 `fillerSec` 仍原样存储，并供 JSON / SUS 路径使用。原生谱面时间和偏移各限于 ±1,000,000 秒。

- **开局**以曲目信息页退场为起点。音乐可听起点与首个音符中较早的时刻落在 2 秒后。若音符入场需要更久，则按所需入场时间提前开局。
- **结尾**以音乐可听终点与最后一个音符中较晚的时刻为准。若音符延续到音频文件结束之后，时钟继续推进。
- **结算**在上述结尾后 1 秒启动结算演出，随后沿用演出与转场时长。前奏跳过从开局显示到首键前 2 秒，点击跳到首键前 2 秒。尾奏跳过从末键后 1 秒显示到结算特效开始，点击立即播放结算特效。手动和自动演奏使用同一套时序。

## 下载验证与使用

开发者可先下载一张谱面生成原生曲包：

```powershell
./Tools/download-sonolus-chart.ps1 -LevelUrl 'https://untitledcharts.com/levels/UnCh-81fb32f7724a4a1eb92ddbbe045082c9'
```

也支持 `sonolus://host[/prefix]/levels/{name}` 链接和服务器路径前缀。
工具调用 `probe-sonolus.ps1` 获取详情、下载谱面/音频/封面并验证 SHA-1，识别媒体文件头，保留谱面原始压缩字节和来源详情，再打包为 `.oskp`。
UntitledCharts 使用 `NextRUSH_P`，Sbuga 使用 `next-rush`。在服务器页面添加 `https://sonolus.sbuga.com` 即可浏览 Sbuga。两种引擎的原始 LevelData 共用本页的加载器和功能检查。
下载完成不代表所有玩法均可播放，不兼容的数据仍可保存，播放时会给出原因。

默认输出到随机临时目录；也可指定 `-OutputPath 'E:/Downloads/chart.oskp'`，已有文件不会覆盖。
将输出曲包通过曲库“导入”按钮导入，即可离线加载。
工具的 master 难度是显示默认值；Sonolus 的通用 LevelItem 没有项目难度类别的直接对应字段。

游戏内可直接浏览 Sonolus 服务器并下载入库，详见 [服务器页面](server-screen.md)和[服务器谱面下载](../guides/server-chart-downloads.md)。命令行工具保留给开发验证，网络部分沿用调研探针的超时与校验。

## 验证

`Assets/Tests/EditMode/Charts/NextRushChartLoaderTests.cs` 覆盖：原生来源不依赖 ChartDocument、分数轨道、变 BPM、命名长条连接、判定点数量、自动播放、同时押线、坏引用/循环、未知功能拒绝、GZip、失败替换保护和 ZIP 导入导出字节保留。

2026-11-17 实测样本 `UnCh-81fb32f7724a4a1eb92ddbbe045082c9`：995 个实体，848 个判定点，120 Hz 自动播放可完成。完整 EditMode 回归第一轮为 992 项：974 通过、18 跳过、0 失败；后续专项测试最终为 29 项全部通过，包含真实曲包下载、导入后的字节保留与自动播放。
真实媒体与原谱未加入仓库。重复真实样本集成测试时：

```powershell
$env:OPENSEKAI_SONOLUS_SAMPLE_DIR = '<probe-sonolus.ps1 的输出目录>'
$env:OPENSEKAI_SONOLUS_PACKAGE = '<download-sonolus-chart.ps1 生成的 .oskp>'
unity test . --mode EditMode --filter NextRush --output TestResults/next-rush.xml --non-interactive
```

第二个变量可选；提供时还验证真实曲包经现有安装链路后媒体存在、谱面字节不变和原生播放完成。
没有提供第一个变量时，线上样本测试会跳过，其余合成测试不访问网络。

Android 真机画面与音乐同步尚待验收。

2026-11-18，P0 完整 EditMode 回归为 1001 通过、18 跳过、0 失败。其中 NextRUSH+ 专项 39 项全部通过，包含 Hallway 原始 LevelData 的 848 判定点和自动播放回归。

P1 完整 EditMode 回归为 1010 通过、18 跳过、0 失败。其中 NextRUSH+ 专项 46 项全部通过，覆盖 ±0.51 秒、±1 秒、零偏移、负拍与变 BPM、合成音频点击及三种玩家补偿。负时间划键用例验证提前划键松手和尾部即时判定。

Windows 的曲库 PlayMode 回归 5 项全部通过。原生用例使用 +0.51 秒偏移和一段 4 秒音频，验证开局预滚、预滚和音乐播放中暂停恢复、局内重开、音频文件结束后的最后一个音符，以及末音符 3 秒后触发结算演出。旧 JSON 用例在同一轮从曲库完成整首自动播放并返回结算。

P2 在 Windows 的隔离 Unity 项目运行完整 EditMode 回归，1047 通过、19 跳过、0 失败。NextRUSH+ 专项 55 项全部通过，包含 Hallway 的 848 判定点及自动播放、双组独立轨迹、隐藏时手动判定、停止与倒流重入、同拍跳距和混合变速。

P2 使用本地原始谱面单独验证变速编译。Kamisama 的 2 组 7 事件正确读入，核对第 203、204 和 207 拍的速度边界。Life Reset Button 的 3 组 136 事件正确读入。Android 未连设备，未采集真机同步与性能数据。

P3 已用原始 Kamisama 曲包通过安装、原始谱面字节校验和 120 Hz 自动播放。1265 个实体中有 51 个 Anchor 和 154 个隐藏 Tick，共 885 个判定点。全量 EditMode 回归 1048 通过、19 跳过、0 失败。Hallway 原谱单独复验，848 个判定点及自动播放通过。Windows 曲库 PlayMode 5 项通过，合成谱覆盖曲线 Anchor 尾、Guide、暂停恢复、重开和结算。用户已确认 M1 验收。

普通长条头到达判定线后固定在线上，可见性跟随当前连接片段，尾节点到线时退场。合成谱回归覆盖经过 Anchor 继续显示、变速组隐藏与恢复、尾部退场。全量 EditMode 回归 1053 通过、19 跳过、0 失败，包含原始 Kamisama 曲包。修复后的原谱画面待人工复核。

P3 后续全量 EditMode 回归 1058 通过、19 跳过、0 失败。合成谱覆盖可见 Tick 的曲线位置和双组变速、阶跃两侧的命中位置、Guide 末端的独立手动输入以及跨链样式透明度。原始 Kamisama 曲包再次通过导入和自动播放，仍为 885 个判定点。

P4 已用校验过 SHA-256 的 Life Reset Button 原始 LevelData 完成 120 Hz 自动播放，8576 个实体、1837 个判定点。Kamisama 与 Hallway 原谱再次通过，分别为 885 和 848 个判定点。合成输入覆盖普通和高亮 HeadTrace 的早触、晚触和超窗，以及独立 Tick 重开后的接触清除。全量 EditMode 回归 1098 通过、19 跳过、0 失败，Windows 曲库 PlayMode 5 项通过。Life Reset Button 的原谱手动输入、画面与 Android 表现仍待验收。

独立 Release、HeadRelease 和 TailTap 的合成输入验证覆盖了跨帧抬手、松手头漏判后的条身接触、隐藏 Tick 和尾部重新按下。全量 EditMode 回归 1104 通过、19 跳过、0 失败。Kamisama、Hallway、Life Reset Button 原谱再次完成自动播放。
