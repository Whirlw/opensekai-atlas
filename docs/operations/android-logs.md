# Android 日志采集

在仓库根目录用 PowerShell 7 运行。连接手机，开启 USB 调试并授权。工具会查找 Android SDK 或 Unity 自带的 ADB，也可以用 `-Adb` 指定路径。多台设备连接时，用 `-Device` 指定序列号。

```powershell
# 看到 Recording 提示后，在手机上复现
./Tools/read-android-logs.ps1 -Duration 60 -Scenario '长条换手漏判' -IncludeTrace

# 闪退之后立即抓取缓冲区
./Tools/read-android-logs.ps1 -Snapshot
```

判定问题先在游戏设置里开启追踪并选中相关类别。限时录制结束后，`-IncludeTrace` 会拉取新增或增长的文件，并标出采样范围内的行号。快照加上 `-IncludeTrace` 时会拉取最近创建的整份追踪文件。测性能时，在游戏设置里打开「性能 HUD」，并在录制期间游玩。

要单独看长条状态，先在游戏里启用「Hold」追踪，然后运行：

```powershell
./Tools/read-android-logs.ps1 -Duration 60 -IncludeTrace -HoldTransitions
./Tools/read-android-logs.ps1 -Duration 60 -IncludeTrace -BarTime 12.345
```

`hold-transitions.json` 只保留 `barExcute` 中前后状态不同的记录，包括谱面头时间、发生时的音乐时间、前后状态和原始行号。`-BarTime` 按日志中的谱面头时间（秒）筛选，同一时刻起头的多条长条均会匹配。Logcat 和 LiveTrace 可能记录同一个事件，以各自的来源和行号保留。这个筛选只涵盖日志中已经记录的状态变化。排查换手和回按时，可同时查看原始的 `Hold`、`Resident` 和 `Release` 记录。

`live-*.log` 的第一行写着生成它的构建：`commit=<短哈希>[+dirty] built=<UTC 时间>`（`BuildStampBuildStep` 在每次打包前写入，`editor` 表示没有打包信息）。分析前先确认这个提交与仓库里要对照的代码一致；不一致时，日志反映的是另一份代码。

开启「判定结果」（`Judge`）追踪后，长条内部点（`LongHoldCombo`）的每次变化都有一行：`pointDue`（到点，进入唯一的一次输入判定，`by=` 是推进它的方法）、`pointHit`、`pointMiss`、`pointInherit`（尾已结算，直接继承）、`pointTerminateHit` / `pointTerminateMiss`（条结束时结算）。每行带 `now`、`dueAt` 和所属长条的状态、是否有手指、当前判定车道。内部点只在到点后的那一次输入判定里能被判中，所以一个 Miss 要看它 `dueAt` 那一帧条和手指的情况。

每次运行会在 `TestResults/android-logs/` 下创建子目录，并输出 `summary.md` 的路径。先读摘要，再按行号查看 `logcat.log` 或 `live-*.log`。用 `report.json` 查看全部错误组、每次出现的位置、设备和版本、采集状态、追踪类别计数以及各性能窗口的数据。

Logcat 按标签采集 Unity 和 Android 崩溃信息。快照可能包含历史记录，同一台设备上的其他应用也可能产生这些标签。先核对时间、进程 ID 和崩溃记录中的包名，再归因到 OpenSekai。工具不清空设备的日志缓冲区。断连时会保留已采集的日志，并在报告中标注采集未完成。没有样本时，先核对采集范围和游戏内的开关。

复测时用同一台设备、同一谱面和相同设置。比较 FPS、最慢帧和各窗口 P95。单个窗口的 P95 和整局 P95 是不同的统计量。
