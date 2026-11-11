# C# 代码规范（Unity）

> 目的：把《Unity 电子书｜C# 代码编写规范》及其引用的一手资料，整理成本仓库可以逐条照做的规范手册。
> 适用读者：本仓库所有写 C# / Unity 代码的人，以及做 Code Review 的人。
> 前置条件：会写 C#，知道 `MonoBehaviour`、`[SerializeField]` 是什么。
> 最后更新：2026-11-02

## 概述

本目录**不是个人偏好清单**，而是一份带出处的规范：正文 10 篇整理自 Unity 中国开发者社区文章《Unity 电子书｜C# 代码编写规范》（作者 Yuumix，原文最后更新 2025-12-11），该文基于 Unity 官方电子书《使用 C# 风格指南编写简洁且可扩展的游戏代码（Unity 6 版）》。

- 原文：<https://developer.unity.cn/projects/692daa3dedbc2a001d18599a>
- 电子书：<https://unity.com/cn/resources/c-sharp-style-guide-unity-6>
- 整理日期：2026-11-02
- 外部资料摘录：见 [references/README.md](references/README.md)

## 一句话规矩

**一致性 > 正确性 > 个人偏好。** 团队统一比"是不是行业主流"更重要；有争议时以本目录为准，本目录没写的，以现有代码为准。

## 速查表

| 主题 | 结论 | 详细 |
| --- | --- | --- |
| 命名 | 常量 `ALL_CAPS`；类型/方法/属性/事件 PascalCase；接口 `I` 前缀；私有字段 `_camelCase`；序列化字段 camelCase | [03-naming.md](03-naming.md) |
| 事件 | `event Action<T>`；`Raise` + 事件名 触发；订阅方法 `类名_事件名` | [04-events.md](04-events.md) |
| 特性 | 一行一个 `[Attribute]`，禁止同行堆叠 | [05-attributes-serialization.md](05-attributes-serialization.md) |
| 序列化 | `private` + `[SerializeField]`，禁止 `public` 字段 | [05-attributes-serialization.md](05-attributes-serialization.md) |
| 区域 | Rider 文件布局，**手动**控制 `#region` | [06-regions-editorconfig.md](06-regions-editorconfig.md) |
| 团队样式 | `.editorconfig` 提交到仓库根目录 | [06-regions-editorconfig.md](06-regions-editorconfig.md) |
| 设计 | KISS / YAGNI / 拥抱迭代 / 单一职责 / DRY（重复 3 次才提取） | [01-principles.md](01-principles.md)、[07-design-practices.md](07-design-practices.md) |
| 注释 | 自解释优先；只注释公共 API；Inspector 字段用 `[Tooltip]` | [07-design-practices.md](07-design-practices.md) |
| UI 命名 | BEM：`块__元素--修饰符` | [07-design-practices.md](07-design-practices.md) |
| 代码气味 | 命名模糊 / 职责混乱 / 耦合过高 / 重复代码 / 过度注释 | [08-code-smells.md](08-code-smells.md) |

## 分册

```text
docs/coding-standards/
  README.md                       ← 你在读的总入口
  01-principles.md                基本原则：KISS、YAGNI、渐进式、一致性
  02-style-sample.md              完整样式样例 + 逐段导读
  03-naming.md                    命名规范
  04-events.md                    事件规范
  05-attributes-serialization.md  特性与序列化
  06-regions-editorconfig.md      区域设置与 .editorconfig
  07-design-practices.md          单一职责、DRY、注释、UI Toolkit BEM
  08-code-smells.md               常见代码陷阱
  09-tooling-and-resources.md     脚本模板、单元测试、权威参考
  10-odin-toolkits.md             Data / Model 的职责划分
  references/                     外链原文摘录（电子书、GitHub、MS Learn 等）
```

- [01-principles.md](01-principles.md) —— 先读，定调子
- [02-style-sample.md](02-style-sample.md) —— 想抄格式直接抄这个
- [03-naming.md](03-naming.md) —— 起名前查表
- [04-events.md](04-events.md) —— 写事件前查表
- [05-attributes-serialization.md](05-attributes-serialization.md) —— 加 `[SerializeField]` 前查表
- [06-regions-editorconfig.md](06-regions-editorconfig.md) —— 配置 IDE、统一团队样式
- [07-design-practices.md](07-design-practices.md) —— 分层、注释、USS 类名
- [08-code-smells.md](08-code-smells.md) —— Review 时对照
- [09-tooling-and-resources.md](09-tooling-and-resources.md) —— 模板、测试、扩展阅读
- [10-odin-toolkits.md](10-odin-toolkits.md) —— 数据该放 Data 还是 Model

## 与项目现状的关系

2026-11-02 核对结果：

- `Assets/Game/Runtime/OpenSekai/` 下 47 个 `.cs`，私有字段已在用 `_camelCase`（如 `Assets/Game/Runtime/OpenSekai/Live/Conversion/RuntimeChartConverter.cs` 的 `_idCount`、`Assets/Game/Runtime/Shared/Audio/PackageAudio.cs` 的 `_audioClip`），与 03 篇一致，**不需要回头改**。
- 仓库根目录**没有** `.editorconfig`；只有 `Library/PackageCache/com.unity.ide.rider@3.0.36/.editorconfig`，那属于包缓存，不生效。要落地统一格式，按 [06 篇](06-regions-editorconfig.md) 补一份。
- 旧目录 `Assets/Game/Runtime/Sekai/` 已不存在：2026-11-02 的重构把全部代码收进 `Assets/Game/`（`Runtime/` 与 `Editor/`），因此**不再有"遗留层豁免"**——`Assets/Game/` 下的代码一律按本规范执行。
- 历史代码按 Tier 分批整改（先 `Runtime/Sekai/Core`、`Live` 等关键路径，再到 UI 与数据层，最后工具），每批都要过编译与打歌门禁；尚未整改到的文件不视为"允许违反规范"，只是还没轮到。

## 使用方式

1. 新写文件：先看 [02](02-style-sample.md) 的结构，再按 [03](03-naming.md)–[05](05-attributes-serialization.md) 起名、加特性。
2. Code Review：拿 [08](08-code-smells.md) 当清单，逐条对照。
3. 有分歧：查本目录；查不到就查 [references/](references/unity-csharp-style-guide-ebook.md) 的原文；原文也没写，按现有代码走，并把结论补进本目录。

## 相关文档

- [文档总入口](../README.md)
- [全量索引](../INDEX.md)
- [模块划分](../architecture/modules.md)
