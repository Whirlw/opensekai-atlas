# 特性与序列化

> 目的：统一 `[Attribute]` 的排列方式，以及 Inspector 可见字段的正确开法。
> 适用读者：写 `MonoBehaviour`、`ScriptableObject`、可序列化数据类的人。
> 前置条件：知道 `[SerializeField]`、`[Tooltip]` 的作用。
> 最后更新：2026-11-02

## 概述

两件事合并成一篇，因为它们总是同时出现在字段上方：**特性怎么排**、**字段怎么声明**。

## 3.3 特性：一行一个

**单个特性独占一行，禁止多个特性挤在同一行。**

```csharp
// 正确写法
[Tooltip("单个特性独占一行")]
[SerializeField]
float anotherStat;

// 错误写法
[Tooltip("多个特性同行")][SerializeField] float anotherStat;
```

理由：字段上的特性会越加越多（`[SerializeField]`、`[Tooltip]`、`[Range]`、`[Header]`、`[FormerlySerializedAs]`、Odin 的 `[Summary]` 等）。同行堆叠后，改一个、删一个都容易看错行，代码 Review 时 diff 也不清晰。

Rider 配置建议：进入 `编辑器 - 代码样式 - C# - 换行 - 特性的排列`，把"每个特性单独一行"设为默认。

## 3.4 序列化：默认私有，按需暴露

### 规则

| 规则 | 说明 |
| --- | --- |
| 访问权限 | 默认 `private` 字段 + `[SerializeField]` 实现序列化，**禁止直接使用 `public` 字段序列化** |
| 访问控制 | 外部需要访问的序列化字段，通过公共属性（`public Property`）或方法（`public Method`）暴露，避免外部直接操作私有字段 |
| 序列化范围 | 只对需要在 Inspector 编辑或需要保存的字段加 `[SerializeField]`，减少序列化开销 |

### 正确写法

```csharp
#region Serialized Fields

[SerializeField]
bool isPlayerDead;

[Tooltip("单个特性独占一行，提升可读性")]
[SerializeField]
float anotherStat;

#endregion

// 需要对外只读时，用属性转发
public bool IsPlayerDead => isPlayerDead;
```

### 错误写法

```csharp
// 错：public 字段，外部可以随意改，Inspector 里也改，溯源困难
public bool isPlayerDead;

// 错：加了 [SerializeField] 又补一个 public 属性 setter，等于没封装
[SerializeField] bool isPlayerDead;
public bool IsPlayerDead { get => isPlayerDead; set => isPlayerDead = value; }
```

### 为什么禁止 public 字段

1. **无法在 setter 里加逻辑**：血量变化要发事件、要 clamp，public 字段没有拦截点。
2. **无法控制读写**：外部拿到就能改，出了问题只能全局搜赋值点。
3. **序列化语义混乱**：public 字段既被 Inspector 改又被代码改，谁最后写入不可知。

改成属性后，至少有 `get`/`set` 两个可以下钩子的位置。

### `[SerializeField]` 不是越多越好

只序列化**真正需要在 Inspector 调或需要存盘**的字段。序列化会带来：

- 每个实例一份数据副本（内存）；
- Inspector 序列化/反序列化开销；
- 改字段名时旧数据丢失（需要 `[FormerlySerializedAs]` 迁移）。

纯运行时缓存（`_cachedTransform`、`_idCount` 这类）不要加。

## 与 `[Tooltip]` 的分工

- **Inspector 可见的字段**：用 `[Tooltip("说明")]`，而不是代码注释——策划/美术在 Inspector 里能直接看到。
- **代码里才需要的说明**：写 `//` 注释，且只注释公共 API 的行为契约（见 [07 篇](07-design-practices.md)）。

## 相关文档

- [命名规范](03-naming.md)（序列化字段用 camelCase）
- [区域与 .editorconfig](06-regions-editorconfig.md)（`#region Serialized Fields`）
- [样式样例](02-style-sample.md)
