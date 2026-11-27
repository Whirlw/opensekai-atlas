# 截图工具（素材快速预览）

> 目的：不上真机，直接把打歌画面、曲目信息页、曲库、设置页、结算页渲染成 PNG，用来快速检查素材改动（音符皮肤、HUD、判定特效、界面）。
> 适用读者：美术、改界面/素材的开发者、写回归的测试者。
> 前置条件：工程能在 Unity 6000.6.7f1 里打开且无编译错误；用户数据目录里至少装有一首曲包（或用 `Tools/fixtures/` 里的样例）。
> 最后更新：2026-11-25

## 概述

一次「截图运行」= 一个请求（JSON）：输出目录、分辨率、按顺序要拍的若干张。工具会：

1. 把 Game 视图切到请求的固定分辨率；
2. 在 `Library.unity` 上进入 Play 模式（冷启动先是开屏和首页；曲库类截图会先把曲库换上屏）；
3. 逐张拍摄（打歌类会自动开一局自动游玩，拍完退出回曲库）；
4. 写出 PNG 和 `report.json`，退出 Play 模式，把分辨率、`runInBackground` 等恢复原样。

同一请求会按相同的时间点绘制画面，适合对比素材改动。像素级对比的渲染差异见「已知限制」。

代码在 `Assets/Game/Editor/OpenSekai/Screenshots/`，命令行入口是 `Tools/capture-screens.ps1`。

## 核心内容

### 三种跑法

| 跑法 | 什么时候用 | 说明 |
| --- | --- | --- |
| `Tools/capture-screens.ps1` | 日常使用（推荐） | 自动判断：编辑器已打开本工程 → 通过 Unity CLI 驱动它；没打开 → 自己启动 Unity，拍完退出 |
| 菜单 `OpenSekai/Screenshots/*` | 已在编辑器里，想拍一套默认样张 | 见下文「编辑器菜单」 |
| 直接调用 C# 入口 | 自己写脚本/CI | `ScreenshotSession.Start(请求路径)` 或 `-executeMethod`，见「C# 入口」 |

注意：

- **驱动已打开的编辑器时不要操作它**：运行期间它会切换 Game 视图分辨率、进出 Play 模式，结束后会恢复。
- 编辑器不能停在 Library 以外的场景里 Play；有未保存的场景改动时，工具会拒绝切场景。
- 「自己启动 Unity」模式不是 `-batchmode/-nographics`：需要显卡和桌面会话，Game 视图要真的渲染。日志在 `Logs/capture-screens-<时间>.log`。
- 运行期间所有声音静音（`AudioListener.volume = 0`），音乐被停掉。

### 脚本参数（`Tools/capture-screens.ps1`）

| 参数 | 含义 | 默认 |
| --- | --- | --- |
| `-Package` | 要打的曲包：曲包 id、目录名（或其一部分）、曲名、或目录路径。先找已安装曲包，再找 `Tools/fixtures/` | — （`-Live`/`-Info`/`-Finish` 必填） |
| `-Difficulty` | `easy` / `normal` / `hard` / `expert` / `master` / `append`，覆盖曲包自带难度 | 曲包自带 |
| `-Live` | 打歌画面的**音乐时间**（秒），可多个：`-Live 45,92.5` | — |
| `-Info` | 曲目信息页动画开始后的秒数，可多个 | — |
| `-Finish` | 最后一个音符判完后的秒数，可多个（拍收尾演出） | — |
| `-FinishAnimation` | 收尾演出：`allperfect` / `fullcombo` / `clear` / `finish` / `none` | `allperfect` |
| `-PlainAuto` | 打歌画面显示原始自动游玩（AUTO、不计连击），不加时显示 PERFECT + 连击 | 关 |
| `-Library` | 拍曲库页 | 关 |
| `-Settings` | 拍设置页 | 关 |
| `-SettingsTrace` | 拍设置页里的「追踪选项」子页 | 关 |
| `-Result` | 拍结算页，可逗号分隔多个样例：`default`（首次通关）/ `fc`（同谱刷新）/ `ap`（新谱挤入第 30 名并跨档）/ `outside`（榜外谱刷新）/ `auto` / `failed` / `retired` / `savefailed` / `loaderror`。除 `loaderror` 外都取后端的 `SettlementSamples`，只换上当前选中曲包的封面和曲名。默认拍动画结束后的定格 | — |
| `-ResultAt` | 结算页开场时间线上的秒数，可多个，每个时间拍一帧（如 `-Result ap -ResultAt 2.45,2.7` 拍升级动画） | — |
| `-HomePage` | 拍首页（开屏时间线的最后一帧） | 关 |
| `-Packs` | 拍资源包页面（只读，不保存）：卡片放大两帧、总览、一个资源包的详情和首组预览、音效 / 音符 / HUD / 结算四个类别页、一个槽位的选项卡片、内置样式 2 的预览，最后返回首页。内容取决于编辑器数据目录中已安装的资源包 | 关 |
| `-ProfilePage` | 拍首页第二页与个人信息（用临时目录里的样例档案，不读也不改本机档案）：第二页、滑动中途、个人信息卡放大两帧、点进个人页、Best 30、全部历史、编辑资料卡、评级徽章弹窗、备份合并预览、返回后停在第二页，最后是新档案的卡片与页面。样例曲目优先取已安装曲包（带封面），不足时补虚构曲名 | 关 |
| `-DanPage` | 拍段位页（用临时目录里的样例段位，不读也不改本机档案）：首页第二页的段位卡与放大一帧、未挑战 / 已通过并佩戴 / 失败两次的段位、删除确认卡、编辑、选择等级与新建、粘贴分享文本与链接错误、准备卡与准备失败、曲间休息页、通过的结算与授牌特效三帧（0.45 s、0.75 s、2.1 s）、失败与中途放弃的结算，再是佩戴两块奖牌的个人页与选择奖牌卡，最后是全部奖牌阶位（段位牌、其他奖牌、未取得及小尺寸）和 34 个等级的对照 | 关 |
| `-AchievementsPage` | 拍成就页（用临时目录里的样例档案，不读也不改本机档案）：首页第二页的成就卡、奖牌未点亮一帧与有成就可解锁时的四帧（0.11 s 到 2.4 s）、卡片放大一帧、全部成就与练习曲库 / 单谱技巧 / 稳定发挥 / 段位组曲 / 特殊奖牌五个筛选、单项与双项奖励弹窗、高阶全连解锁动画的七帧（0.2 s 到 1.9 s）和解锁后的列表、点击解锁初获段位的过程与排序后的列表、左滑查看前一等级、一到八级的星星对照、单项提示卡、三项合并的提示卡、提示卡落下中途（0.3 s）、返回后停在第二页，最后是新档案的成就页 | 关 |
| `-ShopPage` | 拍商店页（用临时目录里的样例档案，不读也不改本机档案）：首页第二页的商店卡三帧、卡片放大一帧、尚无商品时的三类空货架与单类空货架、样例商品（已兑换两件）的全部与头像 / 主题 / 游戏背景三个分类、可兑换 / 已拥有 / 金币不足三种兑换卡、一次兑换的落定前后、返回后停在第二页，新档案的商店页，最后是钱包金币停在转动中的四个角度 | 关 |
| `-MedalsPage` | 拍成就的奖牌与头像（样例档案在临时目录）：28 个系列的普通奖牌、11 块特殊奖牌在各尺寸下的四个动画时刻、18 个成就头像，再到首页、个人信息、选择奖牌、编辑资料、成就页和奖励弹窗里各拍一遍。动画由 `MedalClock.Held` 定格 | 关 |
| `-RankingTiers` | 拍 Ranking Score 的七个评级（用临时目录里的空档案，不读也不改本机档案）：首页第二页的个人信息卡在每个评级下各一张（星空的待机动画停在同一时刻），再拍一张各评级徽章在 200 / 148 / 118 / 58 单位下的对照，每档升级动画并排的三帧（展开到 0.12、0.3、0.55），以及个人页徽章打开的「评级徽章」弹窗（样例分数 8,405，落在银河）静止一张、寰宇重播蓄力和展开各一张。拍摄前会等所有徽章在后台画完 | 关 |
| `-Sample` | 先装入示例曲库（虚构曲目、自绘封面、成绩、服务器和资源包），后面的截图都显示它；顺带拍一张空的服务器列表。只能在[示例工作区](#示例工作区)里用 | 关 |
| `-StyleGuide` | 在示例工作区拍打歌以外的全部界面，给[界面美术风格指南](../reference/ui-style-guide.md)配图。等于 `-Sample` 加上曲库各状态和所有界面类开关 | 关 |
| `-Server` | 拍服务器页面：卡片放大两帧、列表、添加卡，再连 UntitledCharts 拍首页栏目、高级搜索、全部谱面、一首谱面的简介 / 社区 / 排行榜，最后返回首页（需要联网；含第三方封面，只作本地检查，不要提交或发布）。在示例工作区里改连编辑器内的示例服务器，不联网，画面可以公开 | 关 |
| `-Intro` | 开屏动画开始后的秒数，可多个；`-Intro default` 拍覆盖整段开屏的 8 帧（约 4.4 s 交接到首页） | — |
| `-All` | 一次拍全：曲库、设置、结算（`default`）、信息页 `0.3,0.8,1.5,2.7`、打歌 `30,60`（需 `-Package`；已给的时间不会被覆盖） | 关 |
| `-Width` / `-Height` | 分辨率。真机是 `2800x1272`，另一常用尺寸 `1750x795` | `2800` / `1272` |
| `-OutDir` | 输出目录（相对工程或绝对路径） | `TestResults/screenshots/<yyyyMMdd-HHmmss>` |
| `-Request` | 直接使用自己写的请求 JSON（忽略上面所有拍摄参数） | — |
| `-UnityExe` | 没有打开的编辑器时用哪个 Unity | Hub 下的 6000.6.7f1 |
| `-TimeoutSec` | 最长等待秒数 | `900` |

退出码：全部成功 `0`，否则 `1`（失败原因打印在末尾，也写在 `report.json`）。

### 导出 AE / Premiere 可导入的 PNG 序列

在工程根目录运行，下面的命令会导出七次独立的评级升级、一次「三曲耐久」授牌和一次「全连达人」解锁：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/capture-screens.ps1 -Clip tier-upgrade,dan-award,achievement-unlock -Width 3840 -Height 2160 -Fps 60 -TimeoutSec 3600
```

`-Intro`、`-Live`、`-Info`、`-Finish`、`-ResultAt` 都接受秒数区间，可以用逗号混排单帧和多段区间。在 PowerShell 里直接调用脚本时，给区间列表加引号，例如 `./Tools/capture-screens.ps1 -Intro '0..3.1,1.0' -Result ap -ResultAt '0..3.4'`。打歌、信息页和收尾仍需 `-Package`。区间内的打歌时间必须位于谱面的可播放范围内。

| `-Clip` 名称 | 默认秒数 | 输出 |
| --- | --- | --- |
| `tier-upgrade` | −0.5 至 1.5 | 七档各一段，0 秒切换徽章 |
| `dan-award` | 0 至 4 | 「三曲耐久」授牌；`-Clean` 隐藏下方说明和按钮 |
| `achievement-unlock` | 0 至 2.6 | 临时样例档案中 FC-01 所在行的解锁动画 |
| `achievement-card` | 0 至 4.6 | 单项和三项合并提示卡，各一段 |
| `special-medals` | 0 至 4 | 11 块特殊奖牌的待机画廊 |

例如 `-Clip 'tier-upgrade:-0.28..0.6'` 只导出蓄力到收住这一段。`-Alpha` 对评级升级、提示卡和特殊奖牌输出透明 RGBA，默认底色为 `#0C0F14`。剪辑软件选择某段文件夹的第一张 PNG，勾选「图像序列」，按导出帧率解释素材即可。

每段独占一个文件夹。例如 `intro_0.000-3.100/intro_0.000-3.100_00000.png` 在 60 fps 下共 186 张，末帧编号 00185。第 i 帧位于 `from + i / fps` 秒，总帧数按 `round((to - from) × fps)` 取整，终点不入片。每段在 `report.json` 中占一条，包含帧数、帧率、起止秒数和用时。失败条目给出已写帧数。再次使用同一目录时，新序列文件夹会追加 `-2`、`-3` 等后缀，保留上次的 PNG。脚本启动前打印总帧数，长时间 4K 导出可用 `-TimeoutSec` 延长等待。

### 四类时间分别从哪算

| 类型 | 时间的含义 | 注意 |
| --- | --- | --- |
| 打歌 `live` | 音乐时钟，即音频文件自身的时间（含 `fillerSec`） | 早于开局点（第一个音符减提前量）的时间，拍到的是开局第一帧，`report.json` 会注明；晚于全部音符判完的时间会失败，改用 `finish` |
| 信息页 `info` | 信息页被要求开始播放的那一刻起 | 页面约 2.9 s 后淡出完毕：0.1 + 0.01 + `JacketFadeOutDelaySec`（`FrontUIView.prefab` 里为 2.2）+ 收起 0.6 |
| 收尾 `finish` | 最后一个音符判完起 | 收尾演出（ALL PERFECT 等）在 +1 s 开始，`+1.6`～`+3` 比较好看 |
| 曲库 / 设置 | 无时间 | 面板打开后等两帧排版再拍 |
| 结算 `result` | 结算页开场时间线的秒数（`-ResultAt`） | 不给时间就跳到定格再拍；升级动画约在 2.3 s 开始、3.3 s 结束 |
| 开屏 `intro` | 开屏时间线的秒数 | 时间线与背景漂移同时定格，同一时间点画面相同；`home` 即最后一帧 |
| 交接 `handoff` | 无时间 | 曲库卡放大到 0.35 与 0.70 各一帧，然后走真实的「点曲库卡 → 曲库」「返回 → 首页」各拍一张 |
| 关于 `about` | 无时间 | 首页的「关于」卡，以及点「资源包」后弹出的提示条（`-soon`） |
| 服务器 `server` | 无时间 | 服务器卡放大到 0.35 与 0.70，然后走真实的进入、浏览、返回；Sonolus 画面取自线上服务器，内容随时间变化 |

### 打歌画面是怎么「像真打一样」的

- **自动游玩 + 假 PERFECT**：原始自动游玩的判定是 AUTO，不显示连击和 PERFECT 字样。工具默认把游戏自带的「自动假 Perfect」选项（`CustomMusicScoreAutoFakePerfectMode = 1`）打开，于是判定字、连击数、打击特效都和手打一致。只改内存里的设置，**不写 `LiveSettingData.json`**，离开这一局就还原。`-PlainAuto` / `"fakePerfect": false` 关掉它。
- **收尾演出**：自动游玩全是 AUTO 时，收尾演出由 `CustomMusicScoreAutoResultAnimation` 决定（默认是「无」）；`finish` 拍摄会按 `-FinishAnimation` 临时设置它，同样只改内存。
- **信息页**：打歌拍摄会跳过信息页（省时间）；信息页拍摄不管设置怎么写都会强制显示它。
- 其余设置（流速、皮肤、打击特效、背景模式等）都用你当前的设置，和真机一致。

### 为什么可复现

真机上时钟跟着音频走，帧的位置每次都不一样。工具做了四件事：

1. 打歌开始时停掉音乐，把控制器的时钟停在谱面的起始点。此后每帧由工具按固定步长推进音乐时间；
2. 用 `Time.captureDeltaTime` 固定每帧的游戏时间，粒子、动画、`WaitForSeconds` 都按这个走；
3. 进入 Live 前选定截图专用的 LitMotion 调度器，让信息页、打歌和收尾补间统一按截图步长推进；
4. 每个粒子系统按它在层级里的位置给一个固定随机种子。

推进方式：先以 100 ms 一帧快进到目标时间前 `lead`（默认 1.5 s）的位置，再按 `fps`（默认 60）逐帧走到目标，最后一步裁到正好落在目标毫秒上。判定特效、连击数都是这 1.5 s 里自然打出来的。

### 请求 JSON 完整格式

`-Request` 用的文件，也是脚本内部生成的文件（`Library/OpenSekaiShots/request-<时间>.json`）：

```json
{
  "outDir": "TestResults/screenshots/my-check",
  "width": 2800,
  "height": 1272,
  "quitWhenDone": false,
  "shots": [
    { "kind": "library" },
    { "kind": "settings" },
    { "kind": "settings-trace" },
    { "kind": "intro", "from": 0, "to": 3.1, "fps": 60 },
    { "kind": "clip", "name": "tier-upgrade", "from": -0.5, "to": 1.5, "alpha": true },
    { "kind": "result", "name": "result-fc",
      "result": { "kind": "Completed", "score": 1112806, "rank": "S", "maxCombo": 1250,
                  "totalCombo": 1250, "perfect": 1206, "great": 44, "good": 0, "bad": 0, "miss": 0 } },
    { "kind": "info", "package": "DEINOS", "difficulty": "expert", "times": [0.3, 0.8, 1.5, 2.7] },
    { "kind": "live", "package": "DEINOS", "times": [45.0, 92.5], "lead": 1.5, "fps": 60, "fakePerfect": true },
    { "kind": "finish", "package": "Test3", "times": [1.6, 3.0], "finishAnimation": "fullcombo" }
  ]
}
```

顶层字段：

| 字段 | 含义 | 默认 |
| --- | --- | --- |
| `outDir` | 输出目录，相对工程根或绝对路径 | `TestResults/screenshots/<时间>` |
| `width` / `height` | 分辨率（64～8192） | `2800` / `1272` |
| `quitWhenDone` | 拍完退出编辑器（命令行启动时自动为 `true`） | `false` |
| `shots` | 按顺序执行的拍摄列表 | — |

每张拍摄（`shots[]`）：

| 字段 | 适用 | 含义 | 默认 |
| --- | --- | --- | --- |
| `kind` | 全部 | `clip` / `live` / `info` / `finish` / `library` / `settings` / `settings-trace` / `result` / `actions` / `loading` / `return` / `home` / `intro` / `handoff` / `about` / `achievements` / `shop` / `medals` / `ranking` / `sample` / `library-states` | — |
| `name` | 全部 | `kind: "clip"` 时填上表中的动画名；其余类型为文件名前缀 | 等于 `kind` |
| `package` | live / info / finish | 曲包（匹配规则同 `-Package`） | — |
| `difficulty` | live / info / finish | 难度覆盖 | 曲包自带 |
| `times` | live / info / finish | 时间列表（秒），会按升序拍，同一局里连续拍 | live `30`、info `1.5`、finish `2` |
| `lead` | live | 目标前按 `fps` 逐帧播放的秒数 | `1.5` |
| `from` / `to` | 五种按时间拍摄的类型、clip | 有 `to` 时忽略 `times`，`from` 默认 0；clip 省略区间时使用上表默认值 | 秒 |
| `fps` | 所有序列、live / info / finish 单帧 | 序列帧率 1～240；单帧打歌步进 10～240 | `60` |
| `alpha` / `clean` | clip | 分别对应 `-Alpha` 和 `-Clean` | `false` |
| `fakePerfect` | live / finish | 自动游玩显示为 PERFECT + 连击 | `true` |
| `finishAnimation` | finish | `allperfect` / `fullcombo` / `clear` / `finish` / `none` | `allperfect` |
| `result` | result | 结算样例，见下表 | 见下表 |

`result` 字段（全部可省，省略即用默认值）：

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `kind` | `Completed` | `Completed` / `Failed` / `Retired` / `LoadError` |
| `auto` | `false` | 自动模式：面板显示「自动判定」而非判定分布 |
| `title` | `DEINOS PHAINEIN` | 曲名 |
| `playLevel` | `35` | 等级 |
| `score` | `1084217` | 分数 |
| `rank` | `S` | `S` / `A` / `B` / `C` / `D`；全 PERFECT 的完成局显示为 `AP` |
| `maxCombo` / `totalCombo` | `812` / `1250` | 最大连击 / 总音符 |
| `perfect` / `great` / `good` / `bad` / `miss` | `1183` / `51` / `9` / `4` / `3` | 判定分布 |
| `life` | `820` | 剩余血量 |
| `message` | `找不到音频文件。` | 仅 `LoadError` 时显示 |

脚本 `-Result` 预设在默认值上改这些字段：

| 预设 | 改动 |
| --- | --- |
| `default` | 不改 |
| `fc` | 分数 1,112,806，连击 1250，PERFECT 1206 / GREAT 44，其余 0 |
| `ap` | 分数 1,135,420，连击 1250，PERFECT 1250，其余 0（面板显示 AP） |
| `auto` | 自动模式，分数 1,135,420，连击 1250 |
| `failed` | 失败，412,803 / C，连击 96，PERFECT 402 / GREAT 87 / GOOD 31 / BAD 22 / MISS 58，血量 0 |
| `retired` | 中途退出，233,118 / D，连击 207 |
| `loaderror` | 加载失败，只显示 `message` |

### 输出

```text
TestResults/screenshots/<时间>/
  library.png
  settings.png
  result-fc.png
  info_000.600s.png        <name>_<时间>s.png，时间补零到 3 位整数、3 位小数
  live_045.000s.png
  finish_002.500s.png
  report.json              每张图：ok、file、width、height、time（实际时间）、detail、note（提示/失败原因）
Library/OpenSekaiShots/   （不放 Temp/：Unity 启动时会清空 Temp/）
  request-<时间>.json         本次请求
  request-<时间>.status.json  running / done / failed，脚本靠它判断结束
```

`TestResults/` 已在 `.gitignore` 里，截图不会进版本库。拍出的尺寸和请求不一致时，`note` 里会写明。

### 编辑器菜单

| 菜单 | 用途 |
| --- | --- |
| `OpenSekai/Screenshots/Capture Sample Set (2800x1272)` | 用第一首已安装曲包拍一套：曲库、设置、结算（fc）、信息页 0.3/0.8/1.5/2.7 s、打歌 20/30 s |
| `OpenSekai/Screenshots/Capture Sample Set (1750x795)` | 同上，1750x795 |
| `OpenSekai/Screenshots/Open Last Output` | 在资源管理器里打开最近一次的输出目录 |

### C# 入口

```csharp
// 编辑器已打开：返回一句说明，立即返回；结果看 outDir/report.json 和 status 文件
OpenSekai.EditorTools.Screenshots.ScreenshotSession.Start(@"E:\path\to\request.json");
```

```bash
# 没有打开的编辑器：拍完自动退出，全部成功退出码 0
"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -projectPath . -executeMethod OpenSekai.EditorTools.Screenshots.ScreenshotSession.CaptureFromCommandLine -shotRequest Temp/OpenSekaiShots/my-request.json -logFile Logs/shots.log
```

通过 Unity CLI 驱动已打开的编辑器（脚本内部就是这么做的）：

```bash
unity command script eval_file --project-path . --result-only -- --file Temp/OpenSekaiShots/start.cs
```

`start.cs` 内容就是上面那行 `ScreenshotSession.Start(...)`，前面加 `return`。

## 示例工作区

截图会进仓库、文档或宣传片时，画面里不能有第三方的封面和谱面。示例工作区是工程的一份副本，有自己的数据目录，里面只有为本项目虚构的内容：

| 内容 | 来源 |
| --- | --- |
| 22 首虚构曲目和封面 | `SampleLibrary.cs` 里的曲目表；封面在 `ArtSource/SampleCovers/`，由 `ArtSource/Tools/SampleCovers/render_covers.js` 画出 |
| 音频 | 装入时生成的两音长音，只为有时长和试听，录片时自行换音乐 |
| 成绩、Ranked 状态、资料 | 装入时生成；已有游玩记录时不再添加 |
| 示例服务器 | `SampleServer.cs`。对 `https://charts.example.com` 的请求由本地文件应答，不发到网络 |
| 资源包 | 四组生成的打击音 |

做一个并打开：

```powershell
powershell -File Tools/new-sample-workspace.ps1
```

脚本用 `git worktree` 建副本（默认在工程旁边，名为 `<工程>-sample`），把公司名改成 `OpenSekaiSample`，并复制包缓存。第一次在 Unity 里打开要导入几分钟。副本只含已提交的文件。之后在副本目录里运行：

```powershell
powershell -File Tools/capture-screens.ps1 -StyleGuide -OutDir TestResults/screenshots/style-guide
```

这一条拍 181 张（2800x1272），要几分钟。录宣传片时先用 `-Sample` 装入内容，再在编辑器里播放 `Library.unity` 录制。

保护措施：

- 示例内容只在公司名为 `OpenSekaiSample` 的工程里安装，示例服务器也只在那里应答。在普通工程里用 `-Sample` 会报错，不会写入你的曲库。
- `Tools/export-style-guide-images.ps1` 从当次 `report.json` 读取示例曲库安装状态，并校验每张源 PNG 的 SHA-256。缺少校验值的旧截图需要重新运行 `-StyleGuide`。
- 副本里的 `ProjectSettings.asset` 是有意改过的，不要从副本提交它。
- 加曲目时封面要自己画，不临摹任何已有的封面、标志或角色。

用完先关掉它的编辑器，再 `git worktree remove --force <路径>`；数据在 `%USERPROFILE%/AppData/LocalLow/OpenSekaiSample`，可以整个删除。

## 示例

改了音符皮肤，看真机分辨率下中段和高潮各一帧：

```powershell
powershell -File Tools/capture-screens.ps1 -Live 45,92.5 -Package DEINOS
```

改了信息页（`MusicInfoView` / `FrontUIView.prefab`，布局由菜单 `OpenSekai/Art/Rebuild Music Info Page` 生成），看整段动画：

```powershell
powershell -File Tools/capture-screens.ps1 -Info 0.15,0.3,0.5,0.8,1.5,2.5,2.7,2.9 -Package DEINOS -Difficulty append
```

改了 HUD 或判定字，同时看两种分辨率：

```powershell
powershell -File Tools/capture-screens.ps1 -Live 30 -Package DEINOS -OutDir TestResults/screenshots/hud-2800
```

```powershell
powershell -File Tools/capture-screens.ps1 -Live 30 -Package DEINOS -Width 1750 -Height 795 -OutDir TestResults/screenshots/hud-1750
```

改了收尾演出：

```powershell
powershell -File Tools/capture-screens.ps1 -Finish 1.3,1.6,2,3 -FinishAnimation fullcombo -Package Test3
```

改了曲库/设置/结算界面：

```powershell
powershell -File Tools/capture-screens.ps1 -Library -Settings -SettingsTrace -Result failed -Width 1750 -Height 795
```

一次全拍：

```powershell
powershell -File Tools/capture-screens.ps1 -All -Package DEINOS -Difficulty expert -Finish 2.5
```

参考耗时（编辑器已打开，2800x1272）：上面这条一次全拍实测约 45 秒，其中 `finish` 要把整首 DEINOS（约 175 s）快进完；只拍界面类几秒钟。

## 已知限制

- **部分像素可能有渲染差异**：少数着色器按游戏累计时间（`_Time`）闪烁。以 `baseline_tap` 重拍时，差异出现在部分信息页文字的字形边缘和打歌画面的轨道边线。
- **4K 评级徽章**：纹章以较高密度生成。星尘底面、六边形光环和闪白沿用游戏的 320 像素源图，原尺寸查看时边缘会比纹章柔和。
- **信息页**：序列用模拟时间计算页面等待上限，4K 编码耗时不会提前结束页面。旧单帧模式仍按真实时间等待，编辑器低于约 18 fps 时可能提前开打，报告会标注。
- **短谱**：时间超过最后一个音符就拍不到打歌画面（例如 Test3 约 22 s 就判完了），报错会写出判完的时刻，改用 `finish`。
- **运行中重新编译会中断**：脚本重编译会让 Unity 退出 Play 模式，本次运行记为失败（`status.json` 里写明），不会一直占着。
- **难度只改显示**：每个曲包只有一张谱，`difficulty` 只改难度标签和配色，不换谱面。

## 常见问题与故障排查

- **`Unity has this project open but the Unity CLI cannot reach it`**：编辑器还在启动/导入/编译，或停在安全模式（有编译错误）。等它就绪，或先修编译错误；`unity status --project-path .` 可以看状态。
- **`A screenshot run is already in progress.`**：上一次运行还没结束（或被异常打断）。等它结束；仍不行就重启编辑器（状态存在 SessionState 里，重启即清空）。
- **`Scripts have compile errors; Play Mode cannot start.`**：先修编译错误。
- **`No package matches '...'`**：错误信息里列出了能找到的全部曲包目录名，照着写。
- **`The chart was over before N s`**：见上文「短谱」。
- **图是黑的/尺寸不对**：看 `report.json` 的 `note`；确认 Game 视图窗口存在（工具会自动打开），且没有在运行中手动切换分辨率。
- **Windows PowerShell 5.1 下 `-Live 45,92.5`**：`-File` 方式会把它当成一个字符串传入，脚本已兼容逗号或空格分隔。

## 相关文档

- [界面美术风格指南](../reference/ui-style-guide.md)
- [编辑器菜单](editor-menus.md)
- [音符皮肤制作](note-skin-authoring.md)
- [材质包导入](material-pack-import.md)
- [测试](testing.md)
- [打歌运行时](../architecture/live-runtime.md)
- [设置项](../reference/live-settings.md)
