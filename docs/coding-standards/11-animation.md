# 动画入口规范

> 目的：本仓库的 UI 动画有两个正式入口（`Tween*` 组件与 DOTween），另有一个既有的 Animator 例外。这份文档定下"什么时候用哪个"，并把现有调用点和清理责任清点成可核对的事实，避免第三套做法悄悄长出来。
> 适用读者：写 UI 或打歌视图动画的人，以及 Review 动画相关改动的人。
> 前置条件：知道 `MonoBehaviour` 生命周期，知道 DOTween 的 `Tween`/`Sequence` 是什么。
> 最后更新：2026-11-12（P3-4）

## 一句话规矩

**Inspector 里配的动画用 `Tween*` 组件；代码按运行时状态决定时机、目标或时长的动画用 DOTween。谁创建，谁负责在禁用或销毁时 Kill。**

新增第三个入口（Animator、Playables、自写协程插值）需要先说明为什么前两个不够，不要为了"顺手"另起一套。

## 决策表

| 入口 | 谁决定时长与目标 | 适合 | 现有使用者 |
| --- | --- | --- | --- |
| `Tween*` 组件（`OpenSekai.Game/Animation/`） | prefab 上的序列化字段（曲线、时长、延迟、循环） | 会被策划/美术反复调的展示动画：闪烁、呼吸、滑入 | 3 个 prefab，11 个组件 |
| DOTween 直接调用 | 代码（按分数、结算、转场、按压状态推导） | 时机依赖运行时状态的反馈动画：按压缩放、数字滚动、淡入淡出 | 12 个文件 |
| Animator + clip | 动画师产出的 clip | 美术已经有 clip 的特效短片 | `LoadingIndicatorAnimation`（下载指示器）、`CountdownView`（倒计时特效） |

前三行的顺序就是优先级：能用配置文件定下来的，不要写进代码；必须由代码算的，不要硬塞成 prefab 变体。

## `Tween*` 组件族

`Animation/` 下 13 个脚本，其中 9 个属于 Tween 家族，另有 1 个接口：

| 文件 | 职责 |
| --- | --- |
| `TweenBase.cs` | 抽象基类：序列化选项、`Play`/`Stop`/`Pause`/`Resume`/`Restart`/`ForceFinish`、`OnDisable` 与 `OnDestroy` 的 Kill |
| `TweenAlpha.cs` | 透明度，`_from`/`_to` 两个 0..1 值 |
| `TweenPosition.cs` | 局部坐标 |
| `TweenScale.cs` | 局部缩放 |
| `TweenTargetNormal/UnityUI.cs` | 普通渲染器与 uGUI 的位置/缩放适配器 |
| `TweenTargetCanvasAlpha.cs` | `CanvasGroup.alpha` 适配器 |
| `TweenTargetColorNormal/ColorUnityUI.cs` | 颜色适配器，只给 `TweenAlpha` 用 |
| `ITweenTargetComponent.cs` | 上面五个适配器的接口 |

目标适配器**不挂在 prefab 上**：`TweenBase.SetupTarget()` 在 `Awake` 里按 `componentType`（`Normal` / `UnityUI` / `CanvasGroup`）new 出对应实现并 `Initialize(gameObject)`，`TweenAlpha` 覆写成颜色版。所以在 Inspector 里能配的只有行为参数，不是目标类型。

行为参数都在 `TweenBase` 上：`curve`、`duration`、`delay`、`loopType`（`Once`/`Loop`/`PingPong`）、`loopCount`（≤0 视为无限）、`componentType`、`direction`、`startTiming`（`PlayOnAwake`/`PlayOnStart`/`PlayOnEnable`/`Manual`）、`behaviour`（`Master`/`Reference`）、`reference`、`dontKillIfDisable`、`syncGameTime`、`onFinished`（`UnityEvent<TweenBase>`）。

### Master / Reference

`Behaviour = Reference` 的 tween 不跑自己的时间轴：`PlayReferenceCore()` 先让 `reference`（master）播，然后每帧轮询 `reference.IsActive`，master 结束时把末值抄进自己的目标（各实现 override 的 `OnReference()`）。这样同屏多个同构动画只需要算一条曲线。

唯一的用法是加载圆环：`UIPartsLoadingCircle.SetReferenceTween(master)` 把本体的 8 个 `TweenScale` 依次设成 `Reference`，`ReferenceTween` 指向 master 同序号那一个。

### 现有挂载点

命中的只有 3 个 prefab、11 个组件，没有场景（`.unity`）使用它们：

| prefab | 组件 | 数量 | 配置 | 谁在播 |
| --- | --- | --- | --- | --- |
| `Resources/live/view/FrontUIView.prefab` | `TweenAlpha` | 2 | 0.7s、`PingPong`、无限、`PlayOnEnable` | 组件自己 |
| `Resources/Common/transition/LiveTransitioner.prefab` | `TweenScale` | 8 | 1s、`Loop`、延迟 0→0.875 阶梯、`PlayOnEnable` | 组件自己（转场时由 `UIPartsLoadingCircle` 设成 `Reference` 跟随 master） |
| `Resources/screen/prefabs/ScreenLayerHeader.prefab` | `TweenPosition` | 1 | 0.2s、`Once`、**`Manual`** | 无（见缺口 1） |

## 代码里的 DOTween

12 个文件直接调用 DOTween，按清理方式分三类。

### 一、缓存句柄，重建前先 Kill

| 文件 | 句柄 | 说明 |
| --- | --- | --- |
| `Live/View/LiveFrontView.cs` | `_musicStartTween`、`_backgroundTween`、`_startVoiceTween`、`_spriteAlphaTween`、`_fadeTween` | 开场演出，`OnDestroy` 逐个 `Kill()` 并 `_backgroundMaterial.DOKill()` |
| `Live/View/LiveSpriteButton.cs` | `_pressTween` | 按压缩放，下一次按下时 `Kill()` 上一个 |
| `UI/CustomButton.cs` | `pressTween` | 同上，`KillPressTween()` 里 `Kill(false)` |
| `UI/CommonButtonTapEffect.cs` | `_buttonImageTweener`、`_glowButtonImageTweener`、`_textTweener`、`_additionalTextTweener`（Sequence） | 按下高亮与回弹 |
| `UI/GraphicButtonTapEffect.cs` | `_effectTweener`、`_effectIconTweener` | 同上，uGUI 版 |
| `Live/View/ScoreView.cs` | `_addScoreSequence` | 加分飘字，`SetAutoKill(false)` 后重复复用 |

### 二、按目标 `DOKill()` 兜底

| 文件 | 目标 | 说明 |
| --- | --- | --- |
| `Live/View/MusicInfoView.cs` | `_viewGroup`、`_infoGroup[]`、`_mask`、`_difficultyJacket` | 另有 `_activeTweens` 列表把每次创建的 tween 收进去，`KillTweens()` 先逐个 `Kill()` 再按目标 `DOKill()`；在 `Setup`、展示流程、`OnDisable`、`OnDestroy` 四处调用 |
| `Screens/SubWindowFadeAnimation.cs` | `_canvasGroup` | 每次开合前先 `DOKill()` 再 `DOFade` |
| `Live/View/LiveResultView.cs` | `_background` | `DOKill()` 后 `DOColor` 压暗 |

### 三、链式一次性和即发即忘

| 文件 | 形态 | 说明 |
| --- | --- | --- |
| `Screens/DialogBase.cs` | `DOScale(...).SetEase(...).OnComplete(...)` | 开、正常关、关但不销毁三条链共用一个 `_animationTween`，`OnDisable` 里 `KillAnimationTween()` |
| `Screens/ScreenSlideInOut.cs` | `DOAnchorPos(...)` + `OnComplete`/`OnKill` | 转场滑入滑出，`OnComplete` 与 `OnKill` 都把 `finished` 置真，所以"播完"和"被中止"都算结束 |
| `Screens/ScreenLayerLiveLoading.cs` | `DOTween.To(...).SetDelay(0.1f)`，**不存句柄** | 唯一的即发即忘（见缺口 2） |

## 清理责任

### `Tween*` 组件

- `OnDisable`：`dontKillIfDisable` 为 false（现有 3 个 prefab 全部如此）时 `KillTween()`；设为 true 才会让 tween 在禁用后继续跑。
- `OnDestroy`：无条件 `KillTween()`。`KillTween()` 同时 `StopCoroutine(referenceCoroutine)`，所以 `Reference` 模式的轮询不会漏。
- `PlayOnEnable` 是**从头重播**，不是 resume：每次重新启用都从 `_from`（或反向时的 `_to`）开始，`direction` 保持上一次的播放方向。需要"停在原处继续"的动画不要用 `PlayOnEnable`。
- `syncGameTime` 用于把循环动画对齐到真实时间的同一相位（`Mathf.Repeat` 后 `Goto`）。现有 3 个 prefab 都没开，全仓也没有代码把它设成 true。

### 直接调用 DOTween

- 项目**没有** `DOTween.Init()`、`DOTween.KillAll()`、`DOTween.Clear()`、`SetLink()`，全部依赖默认配置：`Assets/Graphics/Resources/DOTweenSettings.asset`（放在 `Resources` 下，DOTween 启动时自动加载）。
- 该资产的关键值：`useSafeMode: 1`、`defaultAutoKill: 1`、`defaultEaseType: 6`（`OutQuad`）、`timeScale: 1`、`defaultTimeScaleIndependent: 0`。
- 含义：`defaultAutoKill` 让 tween 播完自动回收，不必手动 Kill；`useSafeMode` 让"目标已被销毁"变成一条 warning 而不是异常——**它是兜底，不是清理**。命中 safe mode 说明有人没在销毁前 Kill。
- 场景切换时**不要**统一 `DOTween.KillAll()`：`LiveTransitioner` 在转场过程中自己也在跑 tween，一刀切会把转场动画本身杀掉。按句柄 `Kill()`，或按目标 `DOKill()`。
- `DOKill()` 是目标级的：同一个 `transform`/`CanvasGroup` 上所有 tween 都会被杀掉，包括别人建的。句柄在你手上时优先 `Kill()`，只有在"这个目标上只该有我的动画"时才用 `DOKill()`。

## 现状缺口（记录，P3-4 不改代码）

1. `ScreenLayerHeader.prefab` 的 `TweenPosition` 配成 `startTiming: Manual`，但代码里没有任何地方 `Play()` 它——`ScreenLayerHeader` 目前是空壳（`ShowScreen(bool isAnim)` 直接 `SetActive`，`isAnim` 没接）。等 Header 实现时要接上，否则这条配置一直是死的。
2. `ScreenLayerLiveLoading.cs:45` 的 `DOTween.To(...)` 不存句柄：视图在 0.3s 内被销毁时没人 Kill，只能靠 safe mode。
3. `ScoreView` 的 `_addScoreSequence` 设了 `SetAutoKill(false)`，而 `ScoreView` 没有 `OnDisable`/`OnDestroy`，只在下次重建时 Kill 上一个。
4. 以下 7 个 DOTween 消费类没有 `OnDisable`/`OnDestroy` 钩子：`LiveSpriteButton`、`SubWindowFadeAnimation`、`ScreenSlideInOut`、`CommonButtonTapEffect`、`GraphicButtonTapEffect`、`ScreenLayerLiveLoading`、`LiveResultView`。它们走正常关闭流程时是安全的（下次重建前 Kill，或按目标 `DOKill()`），只有在视图被直接销毁、绕过关闭流程时才靠 safe mode。对照：`MusicInfoView` 是唯一同时挂 `OnDisable` 与 `OnDestroy` 的，`DialogBase` 与 `CustomButton` 只有 `OnDisable`。
5. `TweenBase` 家族 9 个脚本里，`TweenAlpha`/`TweenPosition`/`TweenScale` 之外没有其他实现；需要旋转或颜色以外的属性时，先扩 `TweenTarget*` 而不是在业务代码里另写插值。

## Review 清单

- 新增动画先回答"谁决定时长"：配置能定的走 `Tween*`，必须代码算的走 DOTween。
- 缓存 `Tween`/`Sequence` 句柄；重建前先 `Kill()`，不要叠加。
- 至少有一个停止路径：`OnDisable`、关闭流程或 `OnDestroy` 三者之一。
- 循环动画（`Loop`/`PingPong`、无限 `loopCount`）必须有明确的停止条件，否则离开界面后还在跑。
- 不在转场里 `KillAll`，不用 `DOKill()` 去动别人的目标。
- 改动 `DOTweenSettings.asset` 或 `Tween*` 的序列化字段前，先确认 3 个 prefab 的现有配置不会被改掉（`verify-structure.ps1` 只管生成资产，不管这两个）。

## 相关文档

- [模块划分](../architecture/modules.md) —— `Animation/` 目录的职责与文件清单
- [特性与序列化](05-attributes-serialization.md) —— `[SerializeField]` 字段的写法
- [设计实践](07-design-practices.md) —— 生命周期与单一职责
