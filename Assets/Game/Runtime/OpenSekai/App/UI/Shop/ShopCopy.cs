using System;
using OpenSekai.App.Library;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>The 商店's words and colors.</summary>
    internal static class ShopCopy
    {
        internal const string TITLE = "商店";
        internal const string SUBTITLE = "通关 Ranked 谱面、解锁成就可获得金币，用来兑换头像、主题和游戏背景";
        internal const string WALLET = "我的金币";
        internal const string BALANCE = "金币余额";
        internal const string SPENT = "累计消费";
        internal const string OWNED = "已拥有";
        internal const string HISTORY = "消费记录";
        internal const string COMING = "即将上架";
        internal const string EXCHANGE = "兑换";
        internal const string SHORT = "金币不足";
        internal const string UNIT = "金币";

        /// <summary>The tabs, in <see cref="ShopCategory"/> order.</summary>
        internal static readonly string[] CategoryNames = { "全部", "头像", "主题", "游戏背景" };

        /// <summary>Owned, as on the other pages' 已下载.</summary>
        internal static readonly Color32 OwnedTint = new Color32(123, 216, 143, 255);

        internal static string Name(ShopCategory category) => CategoryNames[(int)category];

        /// <summary>Each kind keeps one brand color: its tag, its empty shelf and its glyph in the history.</summary>
        internal static Color32 Tint(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Avatar: return HomeTheme.Colors.Violet;
                case ShopCategory.Theme: return HomeTheme.Colors.Rose;
                case ShopCategory.Background: return HomeTheme.Colors.Periwinkle;
                default: return HomeTheme.Colors.Gold;
            }
        }

        /// <summary>What a kind is for, on its empty shelf.</summary>
        internal static string Pitch(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Avatar: return "新的头像，在首页和个人信息页展示";
                case ShopCategory.Theme: return "换一套界面配色，页面的光晕和强调色随之改变";
                case ShopCategory.Background: return "游玩时铺在轨道后面的背景画面";
                default: return "头像、主题和游戏背景";
            }
        }

        /// <summary>Where a bought item is used, under its picture in the exchange card.</summary>
        internal static string Usage(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Avatar: return "兑换后在编辑资料里选用";
                case ShopCategory.Theme:
                case ShopCategory.Background: return "兑换一次，一直可用";
                default: return string.Empty;
            }
        }

        /// <summary>The draw a kind's empty shelf and history glyph use until it has goods of its own.</summary>
        internal static Color32[] SamplePalette(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Theme:
                    return new[] { HomeTheme.Colors.Rose, HomeTheme.Colors.Violet, HomeTheme.Colors.Gold };
                case ShopCategory.Background:
                    return new[] { HomeTheme.Colors.Periwinkle, HomeTheme.Colors.Violet, HomeTheme.Colors.Rose };
                default:
                    return new[] { HomeTheme.Colors.Violet };
            }
        }

        /// <summary>The empty tab's line: everything is still to come, or this kind is.</summary>
        internal static string Empty(ShopCategory category) =>
            category == ShopCategory.All ? "商品正在准备中，先攒一些金币吧" : Name(category) + "即将上架";

        /// <summary>One purchase per item: the same id on a retry, and the store says it is already there.</summary>
        internal static string TransactionId(ShopItem item) => "shop:" + item.Id;

        /// <summary>A count of coins, with the minus sign of the style guide when it is below zero.</summary>
        internal static string Coins(long value) => value < 0 ? "−" + ProfileCopy.Number(-value) : ProfileCopy.Number(value);

        internal static string Spent(int amount) => "−" + ProfileCopy.Number(amount) + " " + UNIT;

        internal static string Price(int price) => ProfileCopy.Number(price);

        internal static string Short(long missing) => "还差 " + ProfileCopy.Number(missing) + " " + UNIT;

        /// <summary>A balance below zero: coins taken back with deleted plays, after they were spent.</summary>
        internal const string NEGATIVE = "删除的游玩记录收回了已发放的金币";

        internal const string HISTORY_EMPTY = "还没有消费记录。\n通关 Ranked 谱面、解锁成就可获得金币";

        internal static string Bought(ShopItem item) => "已兑换「" + item.Title + "」";

        internal static string BoughtWhen(DateTimeOffset when) => "兑换于 " + HomeScreen.DescribeWhen(when);

        /// <summary>The count beside the tabs.</summary>
        internal static string OwnedCount(int owned, int total) => OWNED + " " + owned + " / " + total;

        internal static string LoadError(Exception exception) =>
            exception is System.IO.InvalidDataException ? "金币记录无法识别，请检查运行日志" : "金币记录读取失败，请稍后重试";

        /// <param name="insufficient">The balance turned out to be too low after all (spent elsewhere meanwhile).</param>
        internal static string BuyError(Exception exception, bool insufficient) =>
            insufficient ? SHORT : exception is InvalidOperationException ? "现在还不能兑换，请稍后再试" : "兑换没有保存，请稍后再试";
    }
}
