# Data 与 Model 的职责划分

> 目的：说清"数据类"和"模型类"各存什么、谁负责改状态、变更怎么通知。
> 适用读者：写运行时状态、配置数据的人。
> 前置条件：读过 [07 篇](07-design-practices.md) 的单一职责。
> 最后更新：2026-11-02

## 概述

原文第八节以开源插件 Odin Toolkits（作者与本规范整理者为同一人）为例，讲它在设计上怎么落地本规范。核心只有一组区分：**Data** 和 **Model**。

## Data：只存静态不变数据

> Data 类仅存储"静态不变数据"，无任何业务逻辑或状态变更行为，作为数据模板使用。

关键词：

- **静态**：运行时不改，或只在加载时赋值一次；
- **不变**：没有 setter，没有 `Add`/`Remove` 之外的变更手段；
- **无行为**：不订阅事件、不发事件、不持有运行时状态。

典型场景：曲包 manifest、谱面常量、配置表条目、静态描述文本。

```csharp
// Data：只读的数据模板
public sealed class ChartMeta
{
    public string Title { get; }
    public string Composer { get; }
    public float Bpm { get; }

    public ChartMeta(string title, string composer, float bpm)
    {
        Title = title;
        Composer = composer;
        Bpm = bpm;
    }
}
```

## Model：存运行时状态并封装变更

> Model 类存储"运行时动态变化的状态数据"，并封装状态变更逻辑。当数据发生变化时，通过事件（如 `OnDataChanged`）发布通知，供其他模块订阅响应。

关键词：

- **动态**：随游戏进程变化（分数、进度、选中项、开关）；
- **封装变更**：外部只能通过方法/属性改，不能在外部直接改字段；
- **发布通知**：变更后发事件，让 UI、存档、音效各自响应，而不是互相 `GetComponent`。

```csharp
// Model：状态 + 变更逻辑 + 变更通知
public sealed class ScoreModel
{
    int _score;
    int _combo;

    public event Action<int> ScoreChanged;
    public event Action<int> ComboChanged;

    public int Score => _score;
    public int Combo => _combo;

    public void AddScore(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _score += delta;
        ScoreChanged?.Invoke(_score);
    }

    public void ResetCombo()
    {
        if (_combo == 0)
        {
            return;
        }

        _combo = 0;
        ComboChanged?.Invoke(_combo);
    }
}
```

## 对照表

| 维度 | Data | Model |
| --- | --- | --- |
| 数据性质 | 静态、不变 | 运行时、动态变化 |
| 有行为吗 | 没有，纯载体 | 有，封装变更逻辑 |
| 对外暴露 | 只读属性/字段 | 只读属性 + 变更方法 |
| 变更通知 | 不需要 | 事件（`OnDataChanged` 等） |
| 生命周期 | 加载后长期复用 | 随会话创建/销毁 |
| 典型例子 | 曲包元数据、配置条目 | 分数、连击、当前选中页、播放进度 |

## 判断流程

```text
这个数据在运行时会变吗？
├─ 不会 → Data（只读属性，无事件）
└─ 会
   ├─ 谁改？只有本类方法能改 → 对
   └─ 改完谁需要知道？
      ├─ 没人 → 可以不发事件（但通常至少 UI 要知道）
      └─ 有人 → 发事件（Model）
```

## 在本项目怎么用

- 曲包/谱面读出来的元信息属于 **Data**：`ChartMeta` 这类，构造后不再改。
- 一局游戏的分数、连击、进度属于 **Model**：见 [打歌运行时](../architecture/live-runtime.md) 里的计分逻辑（[ADR-0004](../decisions/0004-cumulative-scoring.md)），变更应当从 Model 发出，UI 订阅，而不是 UI 每帧去拉。
- 别把 Model 写成"带 setter 的 DTO"：如果外部能直接 `score.Value = 100`，事件就发不出来，封装也白做。

## 相关文档

- [设计实践](07-design-practices.md)
- [打歌运行时](../architecture/live-runtime.md)
- [计分规则 ADR](../decisions/0004-cumulative-scoring.md)
