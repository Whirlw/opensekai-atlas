# 命名规范

> 目的：统一所有标识符的写法，让"看到名字就知道它是什么、能不能改"。
> 适用读者：所有写 C# 的人。
> 前置条件：读过 [02-style-sample.md](02-style-sample.md)。
> 最后更新：2026-11-02

## 概述

总规则只有一句：**除常量外，其余命名遵循 Rider 默认配置。** 也就是说，不要自己发明一套命名法，Rider 装好默认配置就能对齐。

在此基础上，有几条是明确写死的：

## 规则表

| 元素 | 规则 | 示例 | 反例 |
| --- | --- | --- | --- |
| 常量 | 全大写，单词间下划线 | `MAX_COUNT`、`DEFAULT_FADE_TIME` | `MaxCount`、`maxCount` |
| 枚举 | PascalCase | `Direction`、`AttackModes` | `direction` |
| 枚举成员 | PascalCase | `North`、`MeleeAndSpecial` | `NORTH`、`melee` |
| 类 / 结构体 | PascalCase | `PlayerStats`、`CustomEventArgs` | `playerStats` |
| 接口 | PascalCase + `I` 前缀 | `IDamageable`、`IPoolable` | `Damageable`、`iDamageable` |
| 私有字段 | `_` 前缀 + camelCase | `_elapsedTimeInDays`、`_maxHealth`、`_sharedCount` | `elapsedTimeInDays`、`m_maxHealth` |
| 公共属性 | PascalCase | `MaxHealth`、`DamageTypeName` | `maxHealth` |
| 公共方法 | PascalCase | `SetMaxHealth`、`ApplyDamage` | `setMaxHealth` |
| 事件 | PascalCase（用过去分词或名词短语描述"发生了什么"） | `DoorOpened`、`PointsScored`、`ThingHappened` | `onDoorOpened`、`DoorOpenEvent` |
| 局部变量 | camelCase，可结合 `var` 简化声明 | `var powerUps = new List<PlayerStats>();` | `PowerUps`、`list1` |
| 序列化字段 | camelCase，**不加下划线** | `isPlayerDead`、`stats`、`anotherStat` | `_isPlayerDead`、`IsPlayerDead` |
| 方法参数 | camelCase，语义完整 | `numberOfHits`、`newMaxValue` | `n`、`x1` |

## 判断顺序

起名时按这个顺序过一遍，就不会互相打架：

1. **是常量吗？** 是 → 全大写 + 下划线，结束。
2. **是接口吗？** 是 → PascalCase + `I`。
3. **是字段吗？** 往下看 4、5。
4. **要挂 `[SerializeField]` 吗？** 是 → camelCase，**不加下划线**（Inspector 里显示的就是这个名字，加下划线难看且和 Unity 惯例不符）。
5. **普通私有字段** → `_` + camelCase。
6. **其他**（属性/方法/类型/事件）→ PascalCase；局部变量和参数 → camelCase。

## 几条容易踩的

- **`_` 前缀只给私有字段**，不要用在属性、参数、局部变量上。
- **不要用匈牙利命名法**（`strName`、`iCount`）。C# 有类型推断和 IDE 提示，前缀只增加噪音。
- **缩写词按单词处理**：`ID` 写成 `Id`（`ObjectID` 在示例里保留了原样，但新代码建议 `ObjectId`）——这一点以团队 `.editorconfig` 为准，先统一再谈对错。
- **布尔值**用 `is` / `has` / `can` 开头（`isPlayerDead`、`hasHealthPotion`），方法用 `Is...`（`IsNewPosition`），读起来就是一句判断。
- **事件用过去分词**表示"已经发生"，别用 `OnXxx`（`OnXxx` 是 Unity 消息方法和 UI 回调的写法，两者混用会分不清谁触发谁）。

## 与 Unity 电子书原版的差异

Unity 官方电子书原文用的是 **`m_` / `k_` / `s_` 前缀**（`m_` 私有字段、`k_` 常量、`s_` 静态字段），这与本文的 `_camelCase` + `MAX_COUNT` **不一致**。

原因：本站整理自 Unity 中国开发者社区的文章《Unity 电子书｜C# 代码编写规范》，该文在电子书基础上做了本地化取舍，明确写"除常量外，其余命名遵循 Rider 默认配置"，示例代码用的是 `_elapsedTimeInDays`、`MAX_COUNT`、`_sharedCount`。

**结论：以本文为准（`_camelCase` / `MAX_COUNT`），因为团队已经按这个写了。** 两种写法各有拥趸，`m_` 的好处是 `m_`/`s_`/`k_` 一眼区分实例/静态/常量，代价是和 Rider 默认、和微软规范都相反，且下划线前缀在 Unity 社区不流行。电子书原文见 [references/unity-csharp-style-guide-ebook.md](references/unity-csharp-style-guide-ebook.md)。

> 另：电子书建议省略隐式的 `private` 修饰符（直接写 `int _count;`），本文示例保留了 `private`。这一点按 `.editorconfig` 统一即可，不影响语义。

### 再补一句：微软规范其实禁止下划线

.NET 框架设计准则的原话是"**请勿使用下划线来区分字词，也不要在标识符的任意位置使用下划线**"（见 [references/dotnet-design-guidelines.md](references/dotnet-design-guidelines.md)）。按字面理解，`_elapsedTimeInDays` 是违规的。

不冲突，因为适用范围不同：

- 微软那条的**适用对象是公开 API**（public/protected 类型与成员、显式实现的接口）——原文明确"只需将它们应用于公开的 API"；
- `_camelCase` 是**私有字段**，不属于公开 API；
- 而且 C# 生态里私有字段用 `_` 前缀是事实标准（Rider / VS 默认规则都这么生成），比 `m_` 更容易被工具识别为字段。

所以：**对外接口用 PascalCase 且不带下划线；私有字段用 `_camelCase`。** 两条规则各管一段，不矛盾。

## 本项目的现状

`Assets/Game/Runtime/OpenSekai/` 里私有字段已经在用 `_camelCase`，与规范一致，例如：

- `Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs` 的 `private int _idCount;`
- `Assets/Game/Runtime/Shared/Audio/PackageAudio.cs` 的 `private AudioClip _audioClip;`

属性用的是 PascalCase 只读自动属性（`Assets/Game/Runtime/OpenSekai/App/Playback/PlayRequest.cs` 的 `PackageDirectory`、`IsAuto`），同样符合。

## 相关文档

- [样式样例](02-style-sample.md)
- [事件规范](04-events.md)
- [特性与序列化](05-attributes-serialization.md)
- [外部权威命名准则](references/dotnet-design-guidelines.md)
