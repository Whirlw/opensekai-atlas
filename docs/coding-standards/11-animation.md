# 动画入口规范

> 目的：说明何时使用 `Tween*` 组件或直接调用 LitMotion，以及动画的清理责任。
> 适用读者：写 UI 或打歌视图动画的人，以及 Review 动画相关改动的人。
> 前置条件：知道 `MonoBehaviour` 生命周期，了解 LitMotion 的 `MotionHandle`。
> 最后更新：2026-11-16

## 一句话规矩

**Inspector 里配的动画用 `Tween*` 组件。时机、目标或时长由运行时状态决定时，代码直接使用 LitMotion。由创建者负责在禁用或销毁时取消。**

新增第三个入口（Animator、Playables、自写协程插值）需要先说明为什么前两个不够，不要为了"顺手"另起一套。

## 决策表

| 入口 | 谁决定时长与目标 | 适合 | 现有使用者 |
| --- | --- | --- | --- |
| `Tween*` 组件（`OpenSekai.Game/Animation/`） | prefab 上的序列化字段（曲线、时长、延迟、循环） | 会被策划/美术反复调的展示动画：闪烁、呼吸、滑入 | 3 个 prefab，11 个组件 |
| LitMotion 直接调用 | 代码（按分数、结算、转场、按压状态推导） | 时机依赖运行时状态的反馈动画：按压缩放、加分飘字、淡入淡出 | 曲库、通用 UI、Live |
| Animator + clip | 动画师产出的 clip | 美术已经有 clip 的特效短片 | `LoadingIndicatorAnimation`（下载指示器）、`CountdownView`（倒计时特效） |

前三行的顺序就是优先级：能用配置文件定下来的，不要写进代码；必须由代码算的，不要硬塞成 prefab 变体。

## `Tween*` 组件族

`Animation/` 下 13 个脚本，其中 9 个属于 Tween 家族，另有 1 个接口：

| 文件 | 职责 |
| --- | --- |
| `TweenBase.cs` | 抽象基类：序列化选项、`Play`/`Stop`/`Pause`/`Resume`/`Restart`/`ForceFinish`、`OnDisable` 与 `OnDestroy` 时取消动画 |
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
| `Resources/screen/prefabs/ScreenLayerHeader.prefab` | `TweenPosition` | 1 | 0.2s、`Once`、**`Manual`** | 当前没有调用方 |

## 代码里的 LitMotion

| 场景 | 代表文件 |
| --- | --- |
| 曲库动效 | `LibraryDetailPanel.cs`、`LibraryDropdown.cs`、`LibraryView.cs` |
| 通用 UI | `CustomButton.cs`、`DialogBase.cs`、`ScreenSlideInOut.cs`、`SubWindowFadeAnimation.cs` |
| Live 开场、加分和收尾 | `MusicInfoView.cs`、`LiveFrontView.cs`、`ScoreView.cs`、`LiveResultView.cs` |

- 持有 `MotionHandle` 的视图在重播前调用 `TryCancel()`，并在关闭、禁用或销毁时取消仍在运行的动画。
- 延时回调也要随视图取消。阶段切换依赖回调时，用同一条时间线推进属性和回调，例如 `MusicInfoView` 的开场和退出。
- 按需求选择受 `Time.timeScale` 影响的时间或独立时间。Live 动画统一通过 `LiveMotion` 选择调度器，截图时使用固定步长的手动调度器。

## `Tween*` 组件的生命周期

- `dontKillIfDisable` 为 false 时，组件禁用会取消动画。组件销毁时始终取消动画并停止 `Reference` 的轮询协程。
- `PlayOnEnable` 每次启用都按当前方向从对应端点重播。需要从当前位置继续时用 `Pause()` 和 `Resume()`。
- `syncGameTime` 用于将循环动画对齐到同一时间相位。现有挂载点未启用。
- 修改序列化字段时，核对本页列出的三个 prefab。循环动画应有明确的停止路径。

## 相关文档

- [模块划分](../architecture/modules.md) —— `Animation/` 目录的职责与文件清单
- [特性与序列化](05-attributes-serialization.md) —— `[SerializeField]` 字段的写法
- [设计实践](07-design-practices.md) —— 生命周期与单一职责
