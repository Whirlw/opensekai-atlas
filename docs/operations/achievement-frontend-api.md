# 成就后端接入

入口是 Unity 主线程上的 `OpenSekai.App.Library.PlayerProfileStore.Local`。一份玩家档案包含 145 项现行成就，分属 28 个系列。109 项有奖牌，18 项同时有头像。ENCORE-01 按同一 ScoreKey 的 Lv.33+ Ranked 手动通关累计 100 次，跨日、换谱和中途退出均保留该谱已有次数。

## 成就页

```csharp
using System.Linq;
using OpenSekai.App.Library;

var store = PlayerProfileStore.Local;
var goals = store.ReadAchievements();
int completed = goals.Count(row => row.State == AchievementState.Unlocked);
var series = AchievementSeries.Sort(AchievementSeries.Group(goals));
var first = series[0];
var visible = first.Steps.Take(first.OpenCount);
var next = first.Steps[first.CurrentStep];
bool canClaim = first.State == AchievementState.Claimable;
```

| 读取入口 | 返回值与用途 |
| --- | --- |
| `AchievementGoals.All`、`AchievementGoals.Find(id)` | 静态目录，按策划表排序。ID 如 `FC-02`，未知 ID 返回 null。 |
| `store.ReadAchievements()` | 145 项进度快照，包含未达成、待领取、已领取。分母用列表长度，分子只计 `Unlocked`。 |
| `AchievementSeries.Group(goals)`、`Sort(series)` | 依次分组、排序。系列内 `Steps` 从低到高，`CurrentStep` 是从 0 起的下标，全部领完时指向末级。 |
| `row.Goal` | `Id`、`Series`、`Category`、`Metric`、`Title`、`Description`。五个主表分组分别为 Practice、Technique、Consistency、Course、Special。 |
| `row.Current`、`row.Target` | 与该项目标同单位。计量可以是键数、局数、谱面数、天数、连续局数或排名分。单张达标类目标以合资格谱面或达成标志计数。进度条显示时可将 Current 截到 Target。 |
| `row.ReachedAtUtc`、`row.UnlockedAtUtc` | 首次达成、实际领取的 UTC 时间，类型均为 `DateTimeOffset?`。导入旧记录或删除历史后重新读取。 |

同局可令多级同时进入 `Claimable`。领取按钮绑定系列的下一项，并检查 `series.State`。每次领取后重新读取系列快照。

## 领取与发奖

```csharp
var receipt = store.ClaimAchievement(next.Id);
if (receipt.NewlyClaimed)
{
    // 每个非空字段代表本次新获得的奖励。两项均为空也是一次有效领取。
    string medalKey = receipt.MedalKey;
    string newAvatarId = receipt.AvatarId;
}
```

`ClaimAchievement(string id)` 返回 `(bool NewlyClaimed, string MedalKey, string AvatarId)`。原有的 `UnlockAchievement(string id)` 也可继续使用。重复领取返回 `(false, null, null)`。未知 ID、越级、未达成或正在备份时抛出 `InvalidOperationException`。存储错误可重试，界面应在成功返回后更新计数。已通过旧档拥有的头像再次出现在主表奖励中时，回执的 AvatarId 为空。

## 奖励预览、收藏与佩戴

| 字段或调用 | 显示和存储口径 |
| --- | --- |
| `goal.GivesMedal` | 为真时渲染奖牌预览。 |
| `goal.MedalName`、`MedalGrade`、`MedalDisplayName` | 分别是牌名主体、右侧等级字形、带等级的完整文字。横牌绘制前两项，纯文本提示直接用完整文字。 |
| `goal.MedalTier`、`MedalFrameTier`、`MedalFaceId` | 第一项是主表阶位，第二项是实际边框。初至虹阶为 0–4，成就特殊阶为 5。用稳定的 FaceId 查找牌面资源。CODA-01 的阶位为特殊，边框为 4。两组皆传的希腊牌和三个 ω 牌分别有独立 FaceId。 |
| `goal.AvatarId`、`goal.AvatarName` | 稳定资源键与展示名，无头像时为空。沿用 beat、crest、sparkle。其余资源键可从目录枚举。 |
| `store.ReadMedals()` | 已拥有的段位牌和成就牌。成就牌的 `medal.Achievement.Goal` 提供同一份显示定义，旧收藏的 `IsLegacy` 为真。 |
| `store.WearMedal(slot, key)`、`store.RemoveMedal(key)` | 指定槽位 0 或 1，传入收藏中的 `Medal.Key`。传 null 清空指定槽位。另有 `WearMedal(key)` 自动选槽并返回下标。 |
| `store.Read().WornMedals` | 两个槽，空槽为 null。旧档原有佩戴键仍可解析。 |
| `store.OwnsAvatar(id)`、`store.SetAppearance(nickname, id)` | 对目录中列出的成就头像查询所有权和选用。旧版已领头像同样参与所有权判断。 |

## 结算和数据刷新

- 普通演奏读取 `PlayResult.Settlement.ReachedAchievementIds`。保存失败时先展示保存状态。现有 `CustomMusicScorePlayHistoryStorage.RetrySave(playId)` 成功后使用它返回的新回执。
- 段位读取 `DanChallengeService.Local.ReachedAchievementIds`，由现有 `Changed` 通知驱动刷新。每首提交后击打数即可增长，整套通过后再判定通过次数、后光与终乐章。
- 备份预览读取 `ProfileImportPreview.ReachedAchievementIds`；确认导入或删除记录后重读进度、收藏和已佩戴槽位。旧版收藏不会增加 145 项的分母。
- 暂停重试与曲库、服务器结果页重试均由游玩入口记录。新局启动按新局 ID 去重。重新选择同一谱面不会增加 RETRY。页面无需自行累加或写入存档。
- 历史缺少游戏日、专项音符统计或段位 Ranked／等级快照时，对应项目等待有证据的新记录。成绩可证明的其他进度仍会回算。前端不需要解析历史 JSON。

## 前端实现

| 内容 | 位置 |
| --- | --- |
| 各系列的颜色、短文案、图章下标 | `UI/Achievements/AchievementSeriesCopy.cs`。名称、奖牌和等级直接取自 `AchievementGoal` |
| 普通奖牌左侧图案 | `UI/Medals/MedalEmblemArt.cs`，按系列编号绘制；B30 六级各不相同 |
| 特殊奖牌 | `UI/Medals/SpecialPlateArt*.cs`。每块牌实现牌面、边框、图案、圆章和各自的动画，横牌、首页小牌和奖励缩略图共用同一份绘制，只换 `PlateSpace` |
| 成就头像 | `UI/Profile/AvatarArt.cs`，登记在 `ProfileAvatars` |
| 矢量绘制 | `UI/VectorMesh.cs` 生成三角形，`UI/VectorGraphic.cs` 放到画布上。会动的部分各有一个子画布，画布外或不可见时停止重绘 |

新增系列要同时补 `AchievementSeriesCopy` 和 `MedalEmblemArt`，`AchievementMedalArtTests` 会检查缺漏、越界和每帧顶点数。
