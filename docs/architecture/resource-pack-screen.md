# 资源包页面

> 目的：说明首页「资源包」入口打开的管理页面：页面结构、每次选择怎样保存、预设与混搭的规则、内置套装的迁移，以及随包元数据与预设的清单格式。
> 适用读者：改资源包页面、资源包存储或打歌开局边界的开发者；制作资源包的人可只读「随包元数据与预设」。
> 前置条件：读过 [首页、开屏与品牌标](home-and-brand.md)、[页面系统](screen-system.md) 和 [音效资源包](../operations/material-pack-audio.md) 的制包部分。
> 最后更新：2026-11-22

## 概述

资源包页面与服务器页面一样，由代码构建，自带相机（`depth 103`）和 Canvas，并与首页和曲库共用 `Library.unity`。首页的资源包卡片会放大并铺满整页，然后页面从同色幕布中升起。从设置面板的「音符皮肤、打击音与特效」行也能进入。返回时回到进入前的页面，可能是首页、曲库或服务器。

页面不自行保存状态，所有数据都来自 `MaterialPackStore.Shared`。进入页面时读取 `List()`。玩家每确认一次选择，页面都会通过 `SaveConfiguration` 在同一次提交中写入九类选择、内置套装和方案来源，然后重新读取。新的选择从下一次新开局起生效。当前局和局内重开继续使用开局时捕获的组合。

代码在 `Assets/Game/Runtime/OpenSekai/App/UI/Packs/`：

| 文件 | 职责 |
| --- | --- |
| `PackScreen.cs` | 组件本体：相机与 Canvas、页面栈和滑动、幕布、返回键，以及保存、应用预设、卸载和导入的入口与错误反馈 |
| `PackOverviewPage.cs` | 首页：当前方案卡、已安装修订列表、九类卡片 |
| `PackDetailPage.cs` | 内置和外部整合包共用的资源组页面，按类别显示预览和选用按钮。导入的修订另有版本信息、随包预设和卸载入口 |
| `PackCategoryPage.cs` | 单个类别：主组与候补顺序、内置回退、组合说明，以及逐槽来源和逐槽选择 |
| `PackPanels.cs` | 选项卡片、导入进度、预览卡片、预设核对和卸载确认 |
| `PackMedia.cs` | 读取并校验图标和预览素材，处理试听和内置音符或音效的预览 |
| `PackImport.cs` | 系统选档（Android 使用 `NativeFilePicker` 的 ZIP 流程，Editor 使用 ZIP 对话框）和错误文案 |
| `PackWidgets.cs` / `MaterialSlotCatalog.cs` | 共用部件和类别小图，类别与槽位的显示名和分组 |

后端新增内容位于 `OpenSekai.Core/Packaging/MaterialPackConfiguration.cs`，包括类别注册、内置套装、完整配置、随包预设和图标，以及 `Infrastructure/Packages/MaterialPackStore.cs`。

## 核心内容

### 页面

- 预设和混搭的左下方都列出游戏内置整合包和所有带随包预设的已安装修订。预设页使用叠层图标，右侧演示当前组合。点任一整合包右侧箭头进入资源组页面。内置整合包按音符皮肤、打击特效和打歌音效分组，每类列出两套，点对应行的「选用」立即保存。混搭页保留九宫格，点整合包会把九类配置复制到当前混搭。其他单模块资源包也可以从列表进入详情。
- **修订详情**：显示随包预设的声明类别，可点击「查看并应用」。按类别列出资源组，以及每组提供的槽位数和当前用途（主组、第几候补、逐槽几项）。每组都可预览或进入所在类别选用。旧版本安装的修订尚未回填元数据时，页面会提示重新导入。
- **类别页**：左侧为启用组列表（第一个是主组，可上移、下移、移除）、「选择 / 更换主组」「添加候补」、内置回退（音符皮肤、打击特效和打歌音效有两套，其余类别为「游戏默认」）、当前组合说明，以及「清除本类逐槽选择」。右侧按分组列出全部槽位，并显示来源和标签：「指定」表示逐槽指定，「跟随」表示随类别解析，「不会显示」表示被整段演出覆盖。点击行会打开选择卡片，可选「跟随类别」「使用本类内置」，或任一提供该槽的已安装组（不要求已启用）。音效槽可直接试听，其他槽可预览当前来源。
- **预览**：只读。图片从安装修订中读取，并按清单记录的哈希校验后再切出对应矩形。音效可以试听，再点一次停止。关闭卡片或切换来源会停止试听。卡片标明素材来自资源包还是游戏内置，并说明该组提供的槽位范围。单项读取失败时，该格显示「无法预览」，其他内容不受影响。内置音符皮肤从 `Resources/live/note/custom0N` 读取，内置音效通过 `SoundManager` 播放对应的 cue。其他内置资源是打歌中的动态演出，卡片会说明无法预览。

### 保存与方案

| 动作 | 结果 |
| --- | --- |
| 在混搭页载入整合包，或调整主组、候补顺序和内置套装 | 通过一次 `SaveConfiguration` 保存当前混搭，之后可继续逐类调整 |
| 选中当前已经生效的选项、只浏览或取消 | 不提交，方案保持不变 |
| 在预设页选择整合包 | 切换当前整合包，保留混搭配置 |
| 保存失败（预算超限、引用无效、修订冲突、本局进行中） | store 不提交，页面重新读取并保留最后一次有效组合，同时提示原因 |

`SaveConfiguration` 在 store 的写锁内校验九类选择、套装和预算。内置整合包的模块选择单独保存，修改后当前预设及其演示立即读取新的回退套装。已载入的混搭保留自己的副本。

### 逐槽解析

优先使用逐槽指定，其次使用该类别启用列表中第一个提供该槽的组，最后使用本类内置资源。`MaterialPackStore.Describe(category, selection)` 返回每个槽的 `Source`、`Reason`（`override`、`priority`、`builtin`）和 `Candidates`，`IsExplicitBuiltin` 用来区分「指定为内置」和「跟随后落到内置」。`MaterialPackResolver.HiddenSlots` 给出当前组合下不会显示的槽：结算事件被整段序列替换时，该事件的部件和布局不显示；打击特效被整段序列替换时，对应的拖尾不显示。`ComboLayoutSource` 给出 COMBO 布局来自哪一组，即启用列表中第一个提供布局的组。

### 内置套装与旧设置

`MaterialPackState.Builtin` 保存三类的套装编号（1 或 2）。读取到缺少该字段的索引时，包括尚未写过的新档，store 会从旧设置的 `NoteSkinIndex`、`NoteEffect` 和 `NoteSeIndex` 导入一次。原有外部组、顺序和逐槽指定保持不变。迁移结果在第一次写入时落盘；在此之前，每次读取都从同样的旧值得出同样结果，因此迁移是幂等的。

开局时，`MaterialAudioPreparation` 在捕获资源组合的同时取得内置套装。`PlayService` 用 `PlaybackSettingsSnapshot.WithBuiltinSets` 把套装写入本局的设置快照，不修改 `LiveSettingsStore` 的数据。打歌场景中的 `BaseLiveController.Setup` 从 `BootData.LiveSettingData` 设置音符和打击特效的资源包名，打击音加载也读取同一份快照，因此局内重开继续使用开局时的选择。资源包索引无法读取时，快照保留旧设置的值。设置面板的三组开关已移除，原位置保留一个跳转到资源包页的入口。

### 导入与卸载

- 导入：系统选档后显示「正在导入…」和取消按钮，处理过程中再次点击导入不会生效。`ImportAsync` 完成后刷新列表，并自动打开新修订的详情页。导入只安装资源，不改变当前组合。相同 `PackId + Digest` 的内容会提示已安装过。相同包 ID 的新内容会作为新修订安装，当前选择继续引用原修订。
- 卸载：确认卡片使用 `PreviewUninstall` 列出受影响的类别、将移除的逐槽选择，以及当前预设是否会失效。`Uninstall(..., out impact)` 会移除引用并按剩余候补或内置资源重新计算。卸载预设来源或改变了当前组合时，方案变为混搭。卸载未参与当前方案的修订不会改变方案。

### 随包元数据与预设

根级 `extensions` 中的两个命名空间由本游戏读取，其余命名空间会被忽略。这两个对象中的字段名固定，出现未知字段或结构无效时作为清单错误拒绝导入。没有这两个字段的 v1 ZIP 照常导入，包名显示为 `PackId`，并使用默认图标。

```json
{
  "extensions": {
    "opensekai.metadata": { "displayName": "墨色打歌", "icon": "art/icon.png" },
    "opensekai.preset": {
      "name": "清脆打击",
      "categories": {
        "audio": { "groups": ["dry-hits"] },
        "notes": { "groups": ["ink-notes"] }
      }
    }
  }
}
```

| 字段 | 规则 |
| --- | --- |
| `displayName` | 可选，非空字符串 |
| `icon` | 可选，ZIP 内 PNG 的相对路径，须通过现有路径与 PNG 校验 |
| `name` | 预设名，必填 |
| `categories.<类别>.groups` | 本包内属于该类别的组 ID，按优先级排列 |
| `categories.<类别>.builtinSet` | 兼容旧包。允许在 `notes`、`hitFx`、`audio` 中填写 1 或 2。游戏使用玩家在内置整合包中选择的套装，新包可以省略此字段 |

应用整合包时，组 ID 绑定到发布该预设的安装修订。未提供的槽位和类别使用内置整合包对应的资源。旧版本安装的修订没有这些元数据（`MetadataVersion` 为 0）。页面打开时会调用 `BackfillMetadataAsync`，从已安装目录读取根清单回填一次，并在回填前确认文件仍与摘要一致。

## 示例

用 EditMode 测试核对接口行为：

```powershell
unity test . --mode EditMode --filter "OpenSekai.Tests.EditMode.MaterialPackConfigurationTests" --output TestResults/editmode-packs.xml
```

在编辑器中打开页面：进入 `Library.unity` 并播放，点击首页右下角的「资源包」卡片，然后用「导入 ZIP」选择 `Tools/fixtures/material-audio.zip`。截图工具可以调用 `HomeScreen.ShowPacksImmediately()` 或 `SeekPacksHandoff(t)`。

## 常见问题与故障排查

- **保存后仍显示原组合**：保存被拒绝时页面会提示原因。常见原因是预算超限（每类 32 MiB）、本局进行中或修订冲突。页面已重新读取磁盘上的最后一次有效结果。
- **方案在重启后仍显示预设名**：方案来源保存在 `state.json` 的 `Scheme` 中。只有手动修改、应用其他预设或卸载来源修订时才会变为混搭。
- **详情页提示「此修订安装于旧版本」**：回填失败，通常是安装目录中的文件已被改动或缺失。重新导入同一 ZIP 即可。
- **内置资源预览为空**：只有音符皮肤和打歌音效提供内置预览，其他类别会说明在打歌中呈现。

## 相关文档

- [首页、开屏与品牌标](home-and-brand.md)
- [开局数据契约](play-session.md)
- [设置项](../reference/live-settings.md)
- [音效资源包](../operations/material-pack-audio.md)
