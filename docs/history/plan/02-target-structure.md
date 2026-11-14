# 方案：目标结构与数据契约

> 目的：记录目标模块划分、最小数据契约，以及当时决定「第一轮不强推 asmdef」的理由。
> 适用读者：开发者。
> 前置条件：先读 [方案索引](../player-lite-plan.md)。
> 最后更新：2026-11-02（仅按章节拆分为独立文件，正文未改）

> 这是设计稿的一个分册。全文索引与「现状对照」见 [方案索引](../player-lite-plan.md)。

## 4. 目标结构：少量模块，明确职责

建议的代码组织如下。初期以目录和接口约束落地，待依赖闭合后再增加程序集边界。

```text
Assets/OpenSekai/
  App/                 启动、曲库、设置、结果 UI、场景导航、开局协调
  Charts/              谱面文档、旧 JSON／SUS 读取、校验、时间轴与转换
  Live/                会话、既有判定计分、输入适配、音符渲染与对象池
  Infrastructure/      曲包存储、设置与成绩、音频后端、资源和平台文件选择
  Optional/VideoExport/ 可选录屏工具；不进入默认轻量构建
```

目标依赖约束：

- `Charts` 的文档与时间计算不引用页面、Presenter、ScreenManager 或编辑器事件总线。
- `Live` 消费运行时谱面和设置快照，输出结果；不负责曲库导航、存档路径或编辑器状态。
- `Infrastructure` 实现音频、资源与存储接口；不直接控制页面。
- `App` 组装服务，决定何时开局、展示结果与返回曲库。
- 可选录屏模块订阅会话事件、获取渲染目标；核心模块不引用录屏模块的具体类型。

**实现顺序以依赖为准，不要求一次搬完全部现有代码。** 可以先让新 App 通过一个适配器调用现有 `SoloLiveController`，再收缩旧类型。

### 4.1 最小数据契约

| 契约 | 最少内容 | 用途 |
|---|---|---|
| `SongPackage` | 稳定包 ID、元信息、谱面／音频／封面／视频路径 | 替代对编辑器 Common 命名空间的依赖 |
| `ChartDocument` | 文档版本、音符 ID 与连接、tick 事件、表现字段 | 独立于编辑状态的谱面源数据 |
| `ChartTimeline` | tick／秒映射、BPM 与速度事件 | 承接现有转换中的纯计算 |
| `RuntimeChartDefinition` | 已展开的时间、音符与事件定义，不包含已判定状态或 Unity 对象 | 连接谱面转换与局内实例创建 |
| `PlayRequest` | 曲包、手动／自动、镜像等实际支持的选项、设置快照 | UI 向开局服务提交请求 |
| `PlaySession` | 本局 ID、资源句柄、时钟、可变音符状态、状态机 | 管理一次开局到释放的生命周期 |
| `PlayResult` | 完成原因、分数、判定统计、最大连击、血量、模式和谱面标识 | 结果展示与持久化共用一份快照 |

`PlayRequest` 不包含 `MasterMusic`、虚拟卡组、发布状态或页面枚举。过渡期由唯一的 `LegacyLiveBootAdapter` 转成 `FreeLiveBootData`；旧启动数据的复杂性被限制在这里。

`ChartDocument` 与可缓存的转换结果应保持只读。`MusicScore`、`NoteBase` 含有已判定等可变状态，**不能把同一实例跨局复用**。每次重开创建新的运行状态，资源句柄可以受控复用。

最终由 Charts 输出 `RuntimeChartDefinition`，Live 内的工厂将它实例化为 `MusicScore`／`NoteBase`。Charts 不反向引用 Live 的可变模型；过渡期仍直接生成旧音符的转换代码留在适配层，待其完成拆分后再建立程序集边界，避免 Charts 与 Live 互相引用。

### 4.2 不在第一轮强推大量 asmdef

当前主体几乎全在 `Assembly-CSharp`。新建一个命名程序集后，它不能反向依赖仍留在预定义程序集中的主体类型，因此“先按目录加 asmdef”会导致大量编译问题。

先提取不依赖旧主体的契约和算法，再按依赖闭包迁移。最终可形成 Charts、Live、Infrastructure、App 及可选工具程序集；必要的底层契约独立，禁止循环引用。已有 MonoBehaviour 搬迁需保留 `.meta` 并核验场景／prefab 引用；旧 JSON 的类型名也需兼容映射。


## 相关文档

- [方案索引](../player-lite-plan.md)
- [架构总览](../../architecture/README.md)
- [重构日志](../../changelog/2026-11-refactor-log.md)
