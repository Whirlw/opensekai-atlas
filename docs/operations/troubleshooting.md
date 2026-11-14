# 故障排查

> 目的：把本工程实际踩过的坑整理成「症状 → 原因 → 处理」，避免重复排查。
> 适用读者：开发者、打包与测试人员。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

按症状查表。每条都来自实际发生过的事故，处理步骤可直接照做。

## 核心内容

### Unity 卡死 / 弹「Opening file failed」

**症状**：启动时弹窗 `Opening file ... Packages/<包名>/...dll.meta: 系统找不到指定的路径。`，
编辑器主线程被阻塞，菜单与 MCP 都没反应。

**原因**：在 Unity 运行中直接改 `Packages/manifest.json`，`Library/SourceAssetDB` 留下指向已删包文件的记录。

**处理**：

```text
1. Force Quit Unity
2. 删除 Library/ArtifactDB、ArtifactDB-lock、SourceAssetDB、SourceAssetDB-lock
3. 重开工程，等待全量重导入（约 6 分钟）
```

**预防**：摘包用 Package Manager（UI 或 API），不要在编辑器运行时手改 manifest。

### `.meta` 解析失败

**症状**：导入时刷 `Unable to parse file ...: [Parser Failure at line 11: Expect ':' between key and value within mapping]`。

**原因**：`.meta` 文件末尾缺换行。

**处理**：给报错文件末尾补一个 `\n`（GUID 不变，引用不受影响），然后重新导入。

### 编译错误排不掉

**症状**：控制台一直显示 `error CS`，但代码看不出问题。

**原因**：`Editor.log` 里的错误可能是上一轮编译的残留行。

**处理**：

```powershell
# 找最后一次编译时间与错误行号的关系
Select-String -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Pattern 'CompileScripts|error CS' | Select-Object -Last 20
```

判定标准：错误行号在最后一次 `CompileScripts` **之后**才是当前错误。最快的判据是**能不能进 Play 模式**。

### 按 Play 后画面全白

**原因**：曲库页是 `ScreenSpaceCamera` 画布，需要 `LibraryScreenCamera` 渲染；用别的相机或空场景会白屏。

**处理**：确认打开的是 `Assets/Game/Scenes/Library.unity`；截图工具指定 `LibraryScreenCamera`。

### MCP 断线（`Unity session not ready`）

**症状**：Play 之后 MCP 调用连续失败，`Editor.log` 刷 `MCP-FOR-UNITY [WebSocket] Connection closed`。

**原因**：进 Play 触发的 domain reload 会打断 MCP 会话，插件退回 WebSocket 客户端无限重连。

**处理**：在 Unity 里按 **Stop** 退出播放模式，会话通常立刻恢复；仍不行则 `mcp_connector_reconnect(key="custom-unity")`。

### `Temp/UnityLockfile` 被占用

**症状**：新开的 Unity 立刻退出；删锁文件报「正在被另一进程使用」。

**原因**：编辑器其实还在跑（只是窗口不可见/被最小化回收）。

**处理**：先确认进程存在再决定是否强杀；强杀后删 `Temp/UnityLockfile` 再启动。

### adb 截图是坏图

**症状**：`adb exec-out screencap -p > x.png` 得到的文件 `read_image` 打不开。

**原因**：PowerShell 会改写二进制流。

**处理**：

```powershell
adb shell screencap -p /sdcard/x.png
adb pull /sdcard/x.png .\x.png
```

### 构建产物本机起不来

**症状**：`Builds/.../OpenSekaiLite.exe` 启动即退，`-logFile` 也不生成文件。

**原因**：Unity 玩家默认把 `Player.log` 写到 `%LOCALAPPDATA%\Unity\...`；在受限环境（不可写该目录）里会直接失败。

**处理**：在普通桌面环境运行，或改用真机/CI 验证。

## 详细说明

### 常用日志位置

| 日志 | 路径 |
| --- | --- |
| 编辑器 | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| 玩家（Android） | `adb logcat -s Unity` |
| 游戏自身 | `%LOCALAPPDATA%Low\OpenSekai\Open Sekai Atlas\OpenSekaiLogs\` |

## 示例

一次性导出最近 200 行编辑器日志并只保留错误：

```powershell
$log = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
Get-Content $log -Tail 2000 | Select-String -Pattern 'error CS|Exception|Exception:' | Select-Object -Last 40
```

## 常见问题与故障排查

- 上表没覆盖到的现象：先看 [重构日志](../changelog/2026-11-refactor-log.md)，其中记录了大量一次性事故。

## 相关文档

- [编辑器菜单](editor-menus.md)
- [Android 构建](android-build.md)
- [重构日志 2026-11](../changelog/2026-11-refactor-log.md)
