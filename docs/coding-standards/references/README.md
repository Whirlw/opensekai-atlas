# 外部资料整理

> 目的：把《Unity 电子书｜C# 代码编写规范》里引用的外部资料逐份存档，说明它们是什么、能拿到多少内容、和本规范什么关系。
> 适用读者：想追原文出处、或需要更权威依据的人。
> 前置条件：无。
> 最后更新：2026-11-02

## 概述

`coding-standards/` 正文是"结论"，本目录是"证据"。每份资料一个文件，结构统一：

- `## 这是什么` —— 一句话定性；
- `## 来源与可访问性` —— 抓取时哪些链接能打开、哪些只有导航页/需要留资，抓取日期；
- `## 核心内容` —— 能核实的条款，尽量保留英文原词；
- `## 在本项目怎么用` / `## 与本站整理稿的关系` —— 落地建议。

**原则：抓不到就写"未获取"，不臆测条款。** 外部站点会改版、会加登录墙，可访问性一栏就是给后来人省时间的。

## 资料清单

| 资料 | 类型 | 文件 | 正文可得性 |
| --- | --- | --- | --- |
| 《使用 C# 风格指南编写简洁且可扩展的游戏代码（Unity 6 版）》 | Unity 官方电子书（PDF/留资） | [unity-csharp-style-guide-ebook.md](unity-csharp-style-guide-ebook.md) | 见文件内说明 |
| Unity-Code-Style-Guide | GitHub 示例规范仓库 | [unity-code-style-guide-repo.md](unity-code-style-guide-repo.md) | `StyleExample.cs` 可得（正文） |
| .NET 框架设计指南 | 微软官方文档 | [dotnet-design-guidelines.md](dotnet-design-guidelines.md) | 网页可得 |
| C# 通用编码约定 | 微软官方文档 | [csharp-coding-conventions.md](csharp-coding-conventions.md) | 网页可得 |
| 自定义 Unity 脚本模板 | Unity Support 文章 | [unity-script-templates.md](unity-script-templates.md) | 网页可得 |
| Unity Test Framework 手册 | Unity 官方包文档 | [unity-test-framework.md](unity-test-framework.md) | 网页可得 |
| CSS 命名规范指南 | freeCodeCamp 文章 | [css-naming-conventions.md](css-naming-conventions.md) | 网页可得 |
| How to architect your code for a project that scales | Unity 博客 | [architect-code-that-scales.md](architect-code-that-scales.md) | 网页可得 |
| From Pong to 15-person project | Unity 官方视频 | [from-pong-to-15-person-project.md](from-pong-to-15-person-project.md) | 视频，无字幕文本 |

## 怎么用

- 规范条目有争议 → 先看对应的这份整理，再看原始链接；
- 想确认"这条到底是 Unity 说的还是 .NET 说的" → 看每份的 `## 来源与可访问性`；
- 外部链接失效 → 本目录里的摘录仍然可用，可据此补链接。

## 抓取约定

- 抓取日期统一写在每份文件的「最后更新」和「来源与可访问性」里，本次为 2026-11-02。
- 只记录**实际读到**的内容；站点改版导致页面只剩导航时，如实标注。
- 二手转述（博客、视频总结）不能替代原文条款，需要在文件里注明来源层级。

## 相关文档

- [代码规范总入口](../README.md)
- [工具与扩展资源](../09-tooling-and-resources.md)
- [文档索引](../../INDEX.md)
