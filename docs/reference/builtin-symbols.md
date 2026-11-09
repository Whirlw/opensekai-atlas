# 内置文本里的符号字符

目的：统计游戏自带文案里出现的非汉字/非假名符号字符（★、♪、※ 这类），确认换成思源黑体后是否都还有字形。曲包自带的谱面数据不在统计范围内。
适用读者：改文案、换字体、排查「方框字」的人。
前置条件：无。
最后更新：2026-11-02

## 概述

内置文案全部来自两张表：

- `Assets/Graphics/Resources/wording/master_wording.txt`（日文，默认）
- `Assets/Graphics/Resources/wording/master_wording_zh.txt`（中文）

两张表里出现的「符号类」字符一共 9 种（中文表多两个弯引号，共 11 种），合计 286 / 666 次。**没有任何私用区（U+E000–U+F8FF）字符**，也没有 emoji。

## 统计

| 字符 | 码位 | 日文次数 | 中文次数 | 用途示例（文案 key） |
| --- | --- | --- | --- | --- |
| `※` | U+203B | 172 | 172 | 注记符号，`CONNECT_LIVE_TICKET_COUNT_OVERFLOW_WARN_MESSAGE` 等 |
| `★` | U+2605 | 52 | 52 | 稀有度，`MSG_AD_REWARD_PRESENT_BOX`（★2～★4） |
| `×` | U+00D7 | 27 | 27 | 数量，`FORMAT_MYSEKAI_HARVEST_TOOL_QUANTITY`（×{0}） |
| `…` | U+2026 | 21 | 21 | 省略号，`MSG_CHEERFUL_CARNIVAL_TEAM_CHANGE_FIRST` |
| `■` | U+25A0 | 7 | 7 | 列表项，`MSG_OLD_COLOFRUL_PASS_USER_DIALOG_BODY` |
| `♪` | U+266A | 3 | 3 | 语气符号，`MSG_TOTAL_LIVE_POINT`（今月の累計ライブP♪） |
| `¥` | U+00A5 | 2 | 2 | 价格，`WORD_SHOP_PRICE`（¥{1}） |
| `÷` | U+00F7 | 1 | 1 | 除法，`WORD_DIVIDE_FORMAT` |
| `→` | U+2192 | 1 | 1 | 箭头，`WORD_RIGHT_ARROW` |
| `“` | U+201C | 0 | 190 | 中文引号，`FORMAT_MYSEKAI_HOUSING_COMPETITION_SUBMIT_THEME` |
| `”` | U+201D | 0 | 190 | 同上 |

合计：日文 286 次、中文 666 次。

此外，我们自己的 UI 代码（`Assets/Game/Runtime/OpenSekai/App/**`）在字符串里用到 `· — … 、 。 ， ： （ ） ＋ － ？ ；`，都是全角标点，量级很小。

## 字形覆盖

用 `Font.HasCharacter`（走字体文件的 cmap）逐个检查两个思源黑体 OTF：

| 字体 | ¥ | × | ÷ | … | ※ | → | ■ | ★ | ♪ | “ | ” |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SourceHanSansSC-Medium | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| SourceHanSansSC-Bold | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

结论：这 11 个字符思源黑体全部覆盖，不需要额外的符号 fallback。

## 怎么复查

1. 编辑器里打开 `Tools` 目录下的临时脚本思路：按 `m_text:` / 代码字符串正则扫 `Assets/Game`，把码位 ≥ 0x80 且不属于 CJK / 假名 / 全角标点的字符挑出来。
2. 覆盖检查用 `unity_eval`：`AssetDatabase.LoadAssetAtPath<Font>(otf路径).HasCharacter(c)`。

注意：TMP 的 `TMP_FontAsset.HasCharacter(char)` 只查「已经烘进图集」的字形，对 Dynamic 资产几乎总是返回 false；判断字体文件本身有没有某个字，要用 `Font.HasCharacter`（`UnityEngine.Font`）。

## 遗留

信息页（进谱面前的曲目介绍）在真机上曲名与难度前面会显示方框。那不是上面这些字符缺字形——那些位置挂的是图片/图标资源，与字体无关，属于另一个待查项。

## 相关文档

- [字体](fonts.md)
- [界面与页面系统](../architecture/screen-system.md)