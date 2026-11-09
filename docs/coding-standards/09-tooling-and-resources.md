# 工具与扩展资源

> 目的：把规范落到工具链上——用模板自动生成合规骨架，用测试守住逻辑，用权威资料补齐细节。
> 适用读者：维护工程基建的人、想深入的人。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

规范靠自觉执行不可靠。能自动化的部分（模板、格式、测试）交给工具，剩下的才是 Review 的事。

## 7.1 脚本模板：让新文件天生合规

自定义 Unity 脚本模板，把规范格式嵌进模板，新建脚本时自动生成标准结构（`using` 排列、`namespace`、`#region Serialized Fields`、生命周期方法占位）。

### 模板命名格式

```text
序号-菜单路径__脚本名称.文件扩展名.txt
```

例如：

```text
1-Scripting__MonoBehaviour Script-NewMonoBehaviourScript.cs.txt
```

拆开看：

| 片段 | 含义 |
| --- | --- |
| `1` | 排序序号，决定在 `Assets > Create` 菜单里的位置 |
| `Scripting` | `Create` 菜单下的子菜单路径 |
| `__` | 分隔符（两个下划线） |
| `MonoBehaviour Script-NewMonoBehaviourScript.cs` | 菜单项显示名 `-` 默认文件名 `.cs` 扩展名 |
| `.txt` | 模板文件必须以 `.txt` 结尾，Unity 才会当作模板而不是脚本编译 |

### 落地步骤

1. 在项目里建 `Assets/ScriptTemplates/` 目录（Unity 约定的模板根目录）；
2. 按上面的命名放模板文件；
3. 重启/刷新编辑器，`Assets > Create > Scripting > ...` 出现新项；
4. 把 [02 篇](02-style-sample.md) 的骨架抄进模板。

完整的占位符与排查说明见 [脚本模板整理](references/unity-script-templates.md)。

## 7.2 单元测试：强制拆分复杂逻辑

**通过编写单元测试，强制拆分复杂逻辑，提升代码可测试性和复用性。**

这句话的因果关系值得注意：测试不是"给代码上保险"，而是**设计约束**——一个 500 行、依赖 6 个单例的方法根本没法测，于是测试会逼你拆。

Unity 官方提供 Unity Test Framework，支持对核心业务逻辑做自动化测试：

- **EditMode**：不进入播放模式，跑纯逻辑（最适合本项目的计分、谱面解析、配置校验）；
- **PlayMode**：进入播放模式，能测 `MonoBehaviour`、协程、物理。

测试程序集需要单独 asmdef 并勾选 Test Assemblies。详见 [Unity Test Framework 整理](references/unity-test-framework.md)。

## 7.3 权威规范参考

四份原始资料，规范细则有争议时以它们为准：

| 资料 | 内容 | 整理 |
| --- | --- | --- |
| Unity-Code-Style-Guide | Unity 官方示例风格指南（本规范的直接来源） | [references/unity-code-style-guide-repo.md](references/unity-code-style-guide-repo.md) |
| CSS 命名规范指南 | BEM 与 CSS 类名约定 | [references/css-naming-conventions.md](references/css-naming-conventions.md) |
| .NET 框架设计指南 | 微软官方命名、类型、成员设计准则 | [references/dotnet-design-guidelines.md](references/dotnet-design-guidelines.md) |
| C# 通用编码约定 | 微软官方 C# 编码约定 | [references/csharp-coding-conventions.md](references/csharp-coding-conventions.md) |

原文链接（备查）：

- <https://github.com/thomasjacobsen-unity/Unity-Code-Style-Guide>
- <https://www.freecodecamp.org/news/css-naming-conventions-that-will-save-you-hours-of-debugging-35cea737d849/>
- <https://learn.microsoft.com/zh-cn/dotnet/standard/design-guidelines/>
- <https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions>

## 7.4 项目扩展设计

两篇"代码怎么随项目长大"的资料：

| 资料 | 类型 | 整理 |
| --- | --- | --- |
| How to architect your code for a project that scales | 博客文章 | [references/architect-code-that-scales.md](references/architect-code-that-scales.md) |
| From Pong to 15-person project | 视频演讲（Unity 官方） | [references/from-pong-to-15-person-project.md](references/from-pong-to-15-person-project.md) |

原始链接：

- <https://unity.com/how-to/how-architect-code-your-project-scales>
- <https://www.youtube.com/watch?v=1le4vScG3gk>

## 7.5 电子书原文

本目录全部正文的源头——Unity 官方电子书《使用 C# 风格指南编写简洁且可扩展的游戏代码（Unity 6 版）》：

- 中文：<https://unity.com/cn/resources/c-sharp-style-guide-unity-6>
- 整理与可访问性说明：[references/unity-csharp-style-guide-ebook.md](references/unity-csharp-style-guide-ebook.md)

## 相关文档

- [文档总入口](README.md)
- [外部资料目录](references/README.md)
