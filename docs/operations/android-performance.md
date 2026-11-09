# 真机性能

> 目的：记录真机帧率基线的采集方法、结果与已知系统级限制，供后续对比。
> 适用读者：性能调优、测试人员。
> 前置条件：读过 [Android 构建](android-build.md)。
> 最后更新：2026-11-02

## 概述

设备是 OnePlus `PLQ110`（Android 16，Adreno 830，8 核，11.1 GB，1272×2800，最高 165 Hz 自适应）。
在 165 Hz 下打歌的稳态帧时 **p95 = 6.04 ms**（= 1/165.6），几乎没有掉帧。
采集工具是应用内 HUD，默认不编译，需要 `OPENSEKAI_PERF_HUD` 宏。

## 核心内容

### 帧率设置

`Assets/Scripts/Assembly-CSharp/CP/FramerateUtility.cs`：

- 可选档位：跟随屏幕 / 120 / 144 / 165 / 185 / 240
- `ApplyFrameRateLimit(limit)`：`QualitySettings.vSyncCount = 0` → `RequestDisplayRefreshRate(limit)` →
  按实际显示模式夹 `Application.targetFrameRate`
- 日志：`[FramerateUtility] targetFrameRate=165 limit=165 display=55Hz appliedMode=165Hz vSync=0`

**曾经的缺陷**：`TryRequestAndroidDisplayMode` 在 UnityMain 线程直接调 `window.setAttributes(...)`，
Android 抛 `CalledFromWrongThreadException`，导致显示模式从没真正切换过（`appliedMode=0Hz`）。
已改为 `activity.runOnUiThread(new AndroidJavaRunnable(...))`。

### 采集方法

```text
1. Player Settings 宏加 OPENSEKAI_PERF_HUD（例如 DOTWEEN;OPENSEKAI_PERF_HUD）
2. 出包、安装、启动，让 HUD 出现在左上角
3. adb logcat -s Unity > perfhud.log，打一首歌
4. 按 [PerfHud] 行聚合
```

HUD 输出：

```text
[PerfHud] device=.. gpu=.. api=.. window=.. screen=.. refresh=.. targetFrameRate=..
[PerfHud] fps=165.6 frames=166 avgMs=6.04 p50=6.04 p95=6.04 p99=6.04 worst=6.04 res=1750x795
```

统计窗口固定 1 秒、首窗丢弃（避开加载卡顿），`p50/p95/p99` 只在当前窗口内计算。

### 结果（手动打歌，165 Hz）

| 指标 | 数值 |
| --- | --- |
| 稳态窗口 | 44 / 47 个窗口 ≥ 150 fps |
| fps | avg 165.4，min 163.6，max 165.6 |
| p50 / p95 帧时 | 6.04 ms / 6.04 ms |
| 最差单窗口 | 12.08 ms（偶发两帧合并） |
| 非稳态 | 启动加载 483 ms / 1177 ms、切场景 148.6 fps |

渲染分辨率 HUD 报 `1750x795`（窗口模式）与 `2800x1272`（全屏启动）两种；系统把游戏按 1750×795 渲染后放大。

### 已知限制

1. **自适应刷新率**：无触摸时系统把面板降到 55 Hz，`adb shell settings put system min_refresh_rate 165` 压不住。
   打歌时有触摸即 165，因此不影响手感；要做常驻需要专门的系统侧方案，暂不处理。
2. **`dumpsys gfxinfo` 无数据**：Unity 用独立 SurfaceView，不经 HWUI。
3. **`dumpsys SurfaceFlinger --latency` 不可直接用**：BLAST 层可用，但自适应刷新率下空闲帧稀疏，
   算出的帧间隔会出现负值，只能作为辅助手段。

## 示例

手工算一轮 fps（从 logcat 里抓 `frames` 与窗口数）：

```powershell
# 只看稳态窗口
adb logcat -d -s Unity | Select-String '\[PerfHud\] fps=' | Select-String -NotMatch 'fps=1[0-4][0-9]\.'
```

## 常见问题与故障排查

- **HUD 不显示**：宏没加或没重新出包；`PerformanceHud.cs` 整体在 `#if OPENSEKAI_PERF_HUD` 里。
- **帧率锁在 60**：设置里选了「跟随屏幕」而系统此时是 55/60 Hz；选 165 档位才会请求高刷。
- **只有触摸时才是 165**：系统自适应策略，见「已知限制」。

## 相关文档

- [Android 构建](android-build.md)
- [打歌运行时](../architecture/live-runtime.md)
- [参考：构建宏](../reference/build-defines.md)
