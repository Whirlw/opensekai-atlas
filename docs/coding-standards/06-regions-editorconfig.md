# 区域设置与 .editorconfig

> 目的：让同一个文件里的成员顺序固定，让不同 IDE 的人看到同样的格式。
> 适用读者：配置 IDE 的人、负责仓库基建的人。
> 前置条件：装了 Rider 或 Visual Studio。
> 最后更新：2026-11-02

## 概述

两件事：**文件内怎么分区**（`#region`），**团队怎么统一格式**（`.editorconfig`）。前者解决"找东西"，后者解决"各写各的"。

## 3.5 区域设置

### 推荐做法（按原文，含修订）

Rider 配置：进入 `编辑器 - 代码样式 - C# - 文件布局 - Unity`，选择"带区域的默认值"，并开启 `重新格式化并清理代码 - 内置：完全清理`，实现自动整理区域结构。

### 2025-12-11 修订：不要开自动分区

原文作者在使用中发现了问题：

> Rider 选择"带区域的默认值"的存在问题，12 月 11 日使用发现实际没有被序列化的私有字段，被归类到 "Serialized Fields" 的区域中。大部分归类正确，出于正确性第一考虑，建议不选择"带区域的默认值"，手动控制区域。

**结论：不要选"带区域的默认值"，手动写 `#region`。**

原因很实在：自动分区按"字段是否出现在 Inspector"判断，但 Rider 的判定和 `[SerializeField]` 的实际语义并不完全一致——没被序列化的私有字段被塞进 `Serialized Fields`，误导性强于便利性。

### 手动分区的固定顺序

```csharp
public class Example : MonoBehaviour
{
    // 1. 常量、静态字段
    const int MAX_COUNT = 100;
    static int _sharedCount;

    #region Serialized Fields

    [SerializeField]
    bool isPlayerDead;

    #endregion

    // 2. 普通私有字段
    int _elapsedTimeInDays;

    // 3. 属性
    public int MaxHealth => _maxHealth;

    // 4. 事件
    public event Action DoorOpened;

    // 5. Unity 生命周期
    void Awake() { }
    void Update() { }

    // 6. 公共方法
    // 7. 私有方法

    #region Nested type

    public struct CustomEventArgs { }

    #endregion
}
```

顺序不是教条，关键是**同一个项目里到处都一样**。新人打开任何一个文件，都能按固定位置找到字段、属性、生命周期、嵌套类型。

### 一点争议说明

Unity 电子书原文把 `#region` 点名为"很多开发者视为 code smell"，主张靠文件布局约定而不是 `#region` 来组织成员。本文（来自 Unity 中国社区的整理稿）主张保留 `#region`，理由是 Unity 的 Inspector 字段、生命周期方法、嵌套类型确实需要视觉分组。

两种做法都能接受，**关键是别一半用一半不用**。本项目的口径：可以用 `#region`，但只用于 `Serialized Fields` 和 `Nested type` 这两类，其余按上面顺序排列，不额外包区域。参考 [电子书整理](references/unity-csharp-style-guide-ebook.md)。

## 四、团队协作配置：`.editorconfig`

当团队成员使用不同 IDE（Rider、Visual Studio）时，用 `.editorconfig` 统一样式。

### 步骤

1. 在 Rider 中配置好代码样式（命名、换行、缩进等）；
2. 通过 `设置 - 代码样式 - 导出` 导出 `.editorconfig` 文件；
3. 将该文件提交到**项目仓库根目录**，IDE 会自动识别并应用配置，确保所有成员代码样式一致。

### 为什么是 `.editorconfig` 而不是口头约定

- 它是 IDE 中立的标准（Rider / VS / VS Code 都认），不绑定某一个人的环境；
- 配置进仓库 = 可 Review、可追溯、可回滚；
- 新人 clone 下来就自动生效，不需要"先看一遍规范文档"。

### 本项目的现状

2026-11-02 核对：仓库根目录**没有** `.editorconfig`。目前只有 `Library/PackageCache/com.unity.ide.rider@3.0.36/.editorconfig`，那是包缓存里的文件，不参与项目格式化。

要落地的话：

1. 在 Rider 里按 [03 篇](03-naming.md)–[05 篇](05-attributes-serialization.md) 配好命名与换行；
2. 导出 `.editorconfig` 放到仓库根目录（和 `OpenSekai.sln` 同级）；
3. 提交前把 `Library/`、`Temp/` 排除在版本控制外（`.gitignore` 已处理）；
4. 提一个 PR 让大家 `Reformat Code` 一次，之后靠配置约束。

## 相关文档

- [命名规范](03-naming.md)
- [特性与序列化](05-attributes-serialization.md)
- [样式样例](02-style-sample.md)
