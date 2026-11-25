# 截图工具连续帧导出需求

> 目的：让 `Tools/capture-screens.ps1` 能把游戏动画导出成可直接放进 AE / Premiere 的 PNG 序列，供宣传片使用。
> 适用读者：实现这个功能的开发者或 AI 助手。
> 前置条件：读过[截图工具](../operations/screenshot-tools.md)。
> 最后更新：2026-11-25（基线 `9eb01aba`，尚未实现）

## 背景

宣传片的制作顺序是：先做好画面动画，再由人按画面手动剪配乐，最后才放谱面。所以导出的每段动画必须**互相独立、不绑定 BPM**，剪辑时可以自由移动和裁切。

现在的工具只拍指定时刻的单帧。评级升级每档 3 帧，段位授牌 3 帧，成就解锁 7 帧，都不够做视频。

改动以编辑器代码为主：`Assets/Game/Editor/OpenSekai/Screenshots/` 和 `Tools/capture-screens.ps1`。运行时代码只允许加 `internal` 入口（`OpenSekai.Editor` 已在 `InternalsVisibleTo` 里），不得改变游戏里的画面和时长。

## 一、要交付的功能

### P0-1 时间区间

给已有的按时间拍摄的类型加区间：`intro`、`live`、`info`、`finish`、`result`。

- 脚本写法：`-Intro 0..3.1`、`-Live 36.5..41 -Package X`、`-Info 0.3..2.2 -Package X`、`-Finish 0..4 -Package X`、`-Result ap -ResultAt 0..3.4`。区间和单个时间可以混写，逗号分隔，每个区间是一段独立的序列。
- 脚本加 `-Fps`，默认 60，对所有序列生效。
- 请求 JSON：`Shot` 加 `from`、`to`（可空的秒数），沿用已有的 `fps`。有 `to` 就是序列，`from` 默认 0，此时忽略 `times`。

### P0-2 指定动画

新增 `kind: "clip"`，脚本写法 `-Clip tier-upgrade,dan-award`，可带区间：`-Clip tier-upgrade:-0.5..1.5`。一个名字可以产出多段序列。

| 名字 | 内容 | 默认区间（秒） |
| --- | --- | --- |
| `tier-upgrade` | 七档评级徽章逐档升级，**每档一段**，共 7 段。画面只有居中的大徽章和它背后的光，没有文字和界面 | -0.5..1.5，0 是新一档出现的时刻 |
| `dan-award` | 段位授牌全过程，样例段位「三曲耐久」 | 0..4 |
| `achievement-unlock` | 成就页上解锁「全连达人」（`FC-01`）的那一行 | 0..2.6 |
| `achievement-card` | 成就达成提示卡从落下到消失，单项一段、三项合并一段 | 0..4.6 |
| `special-medals` | 11 块特殊奖牌的待机动画 | 0..4 |

样例档案继续用现有的临时目录做法（`DanSamples`、`AchievementSamples`），不读也不写玩家自己的档案。

### P0-3 输出格式

```text
<outDir>/
  tier-upgrade-3-galaxy/
    tier-upgrade-3-galaxy_00000.png
    tier-upgrade-3-galaxy_00001.png
  intro_0.000-3.100/
    intro_0.000-3.100_00000.png
  report.json
```

- 每段一个文件夹，帧号从 00000 起连续，不跳号。文件名用帧号，不用时间。
- 第 i 帧的时间是 `from + i / fps`，总帧数是 `round((to - from) * fps)`，不含 `to` 那一帧。
- `report.json` 每段一条，含 `frames`、`fps`、`from`、`to`，不要每帧一条。脚本结束时每段打印一行。
- 序列中途失败要报错，说明已写了多少帧，不能算成功。
- 单帧截图的文件名、内容和 `report.json` 格式保持不变。

### P1 建议做

1. **透明背景**：`tier-upgrade`、`special-medals`、`achievement-card` 支持 `alpha: true`，输出 RGBA PNG，便于在 AE 里叠层。可以在同一时刻用纯黑和纯白背景各拍一次，再算出 alpha 和颜色。默认仍用底色 `#0C0F14`。
2. **干净版授牌**：`dan-award` 加选项，隐藏下方文字和「佩戴段位牌」「继续」按钮。

### 不做

- 录声音、合成视频、调用 ffmpeg。
- 任何和 BPM、小节有关的参数。
- 曲库、服务器等页面的连续录制，这些用 OBS。
- 改动画本身。

## 二、现有入口

| 动画 | 入口 | 说明 |
| --- | --- | --- |
| 开屏 | `HomeScreen.SeekIntro(秒)` | 已是纯时间函数。3.15 秒后交接到首页 |
| 结算页 | `ResultPage.Seek(秒)` | 同上 |
| 打歌、信息页、收尾 | `ScreenshotDriver.LiveShot` / `FinishShot` | 已按 `fps` 固定步长推进，缺的是逐帧写文件 |
| 评级升级 | `RankingBadge.SetUpgrade(秒)` + `Tick(秒)` | 照 `RankingBadgeGallery.Tile.ApplyReplay`：负时间显示低一档，0 时换档，背后有一层 `_flare`。星尘没有更低一档，直接用自己 |
| 段位授牌 | `DanResultPage.SeekCeremony(秒)` | 有缺陷，见隐患 1 |
| 成就解锁 | `AchievementRow.SeekUnlock(after, 秒)` | `after` 由 `AchievementScreen.Unlock("FC-01")` 得到 |
| 成就提示卡 | `AchievementScreen.PreviewUnlock(ids, context, 秒)` | 全长 4.6 秒，纯时间函数 |
| 特殊奖牌 | `MedalClock.Held = 秒` | 画面用 `BuildSpecialGallery`，建一次即可 |

界面动画统一走 `HomeMotion.Scheduler`。`HomeMotion.CaptureScheduler` 是为截图留的钩子，**目前只有测试在用，截图工具没有接**。接法可参考 `LiveMotion.CaptureScheduler` 和 `ScreenshotDriver.StartMotionClock`。

## 三、隐患

下面 1–6 来自读代码，没有运行验证过，实现时要逐条实测。

1. **授牌会少一道扫光**。`DanAwardCeremony.Seek` 把 `_shone` 置为 true，1.2 秒处的 `_plate.Shine(0f, 0.8f)` 就不播了。序列里必须有这道光。可以走真实的播放路径，用 `HomeMotion.CaptureScheduler` 按帧推进；`MedalPlate.Shine` 也走 `HomeMotion.Scheduler`，会一起被推进。
2. **有些动画跟真实时间走**，而 4K 截图每帧可能要几百毫秒，会看起来过快或一跳一跳：
   - `RankingBadgeClock` 用 `Time.unscaledDeltaTime`；
   - `MedalClock.Now` 用 `Time.unscaledTime`，要设 `Held`；
   - 成就页可解锁的行会呼吸闪烁（`AchievementView.StartPulse`，走 `HomeMotion`）。`CaptureScheduler` 要在页面打开**之前**装上，已经创建的动画不会换调度器。
   - 先实测 `Time.captureDeltaTime` 对 `unscaledDeltaTime` 是否有效，再决定怎么处理。
3. **装上手动调度器后，等待要按帧数**。现有代码用 `WaitForSecondsRealtime` 等页面打开动画，手动时钟下动画进度和真实时间无关。
4. **信息页有真实时间上限**。`BaseLiveController` 等信息页时用 `Time.realtimeSinceStartup` 计时，现有文档说编辑器低于约 18 fps 就会提前开打。4K 逐帧写 PNG 很可能达不到。可以先把帧存在内存里、用后台线程编码（`ImageConversion.EncodeArrayToPNG`），或在截图期间放宽这个上限。
5. **4K 下徽章可能发虚**。`RankingCrests` 按 `badgeSize * 1.5` 的密度画纹章，3840x2160 的画布缩放是 2；六边形底图固定 320 像素（`LibraryShapes.HEXAGON_SIZE`），影响星尘、光环和闪白。可以建更大的 `badgeSize` 再整体缩小来改善纹章。六边形仍然发虚的话，在交付说明里写明，不要改运行时尺寸。
6. **打歌步长取整**。打歌按毫秒推进，120 fps 时一帧是 8 或 9 毫秒，要保证一帧只推进一次。
7. **退出时要复原**：`HomeMotion.CaptureScheduler`、`MedalClock.Held`、`Time.captureDeltaTime`。出错和中途退出 Play 模式（`ScreenshotSession.OnPlayModeChanged` 的 `ExitingPlayMode`）也要复原。
8. **体积和超时**。3840x2160 的 PNG 每帧可能数 MB，10 秒就是 600 帧。脚本默认 `-TimeoutSec 900` 可能不够，开始前打印预计帧数，超时提示要说清楚怎么调。

## 四、验收

1. **帧数与命名**：`-Intro 0..3.1 -Fps 60` 得到 186 个文件，编号 00000–00185。
2. **时间准确**：`-Intro 0..3.1,1.0` 里，序列第 60 帧与单拍的 1.000 秒逐像素一致。`result` 也这样验。`tier-upgrade` 没有单拍可比，抽查 -0.28、0、0.6 秒附近的帧。
3. **与机器快慢无关**：同一请求分别用 960x540 和 3840x2160 导出，把 4K 缩小后对比，动作进度一致，没有跳帧、重帧和忽快忽慢。
4. **可复现**：同一请求跑两遍，界面类序列逐像素一致。已知的 `_Time` 着色器差异要列出来。
5. **授牌扫光**：`dan-award` 在 1.2–2.0 秒能看到光扫过奖牌。
6. **评级升级**：7 段都在 -0.28 秒开始变大变白，0 秒换档，0.6 秒左右收住，之后待机动画平稳。
7. **信息页**：3840x2160 的 `-Info 0.3..2.2` 不出现「打歌提前开始」的提示。
8. **不影响原功能**：改动前后各跑一遍 `-RankingTiers -DanPage -AchievementsPage -MedalsPage -Intro default`，文件清单一致，抽查画面一致。
9. **不碰玩家数据**：跑完后玩家的档案、设置和曲库没有变化。
10. **检查通过**：区间解析、帧数计算和文件命名有 EditMode 测试；`Tools/run-editmode-tests.ps1`、`Tools/verify-structure.ps1`、`Tools/verify-coding-standards.ps1` 通过。
11. **文档**：更新 [截图工具](../operations/screenshot-tools.md) 和 `capture-screens.ps1` 文件头的说明，含一条能直接运行的完整示例。

交付时附上 `tier-upgrade`、`dan-award`、`achievement-unlock` 各一段 3840x2160 的序列，并说明每段用时和体积。

## 五、注意

- 2026-11-25 时工作区有约 170 个未提交的改动，绝大多数是美术 PNG，属于别的工作。只改自己的文件，不要暂存、还原或提交其他文件；没有得到明确同意不要提交。
- 公开用的画面只从[示例工作区](../operations/screenshot-tools.md#示例工作区)拍。`clip` 用的是内置样例，开发时可以直接在主工程里跑；`live`、`info`、`finish` 会显示本机曲库，开发时拍的图只作本地检查，不要发布。
- 代码规范见 [coding-standards](../coding-standards/README.md)。
- 输出放在 `TestResults/`，已被 git 忽略。
