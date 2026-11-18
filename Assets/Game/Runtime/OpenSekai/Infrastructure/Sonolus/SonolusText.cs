using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenSekai.Infrastructure.Sonolus
{
    /// <summary>
    /// Servers write some labels as Sonolus text constants (<c>#NEWEST</c>, <c>#KEYWORDS</c>...)
    /// that the client is expected to localize. This table covers the constants servers use for
    /// level browsing; an unknown constant is shown as its words in title case.
    /// </summary>
    public static class SonolusText
    {
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["#NEWEST"] = "最新",
            ["#OLDEST"] = "最早",
            ["#RANDOM"] = "随机",
            ["#POPULAR"] = "热门",
            ["#RECOMMENDED"] = "推荐",
            ["#FEATURED"] = "精选",
            ["#TRENDING"] = "趋势",
            ["#HOT"] = "热门",
            ["#NEW"] = "新",
            ["#DEFAULT"] = "默认",
            ["#KEYWORDS"] = "关键词",
            ["#KEYWORD"] = "关键词",
            ["#SEARCH"] = "搜索",
            ["#ADVANCED"] = "高级",
            ["#QUICK"] = "快速",
            ["#TITLE"] = "标题",
            ["#SUBTITLE"] = "副标题",
            ["#ARTISTS"] = "艺术家",
            ["#AUTHOR"] = "作者",
            ["#AUTHORS"] = "作者",
            ["#DESCRIPTION"] = "描述",
            ["#GENRE"] = "曲风",
            ["#TAGS"] = "标签",
            ["#TAG"] = "标签",
            ["#RATING"] = "等级",
            ["#MIN_RATING"] = "最低等级",
            ["#MAX_RATING"] = "最高等级",
            ["#DIFFICULTY"] = "难度",
            ["#LENGTH"] = "时长",
            ["#ENGINE"] = "引擎",
            ["#SORT"] = "排序",
            ["#SORT_BY"] = "排序方式",
            ["#ORDER"] = "顺序",
            ["#ASCENDING"] = "升序",
            ["#DESCENDING"] = "降序",
            ["#LIKES"] = "点赞",
            ["#LIKE"] = "点赞",
            ["#COMMENTS"] = "评论",
            ["#COMMENT"] = "评论",
            ["#PLAYS"] = "游玩次数",
            ["#DOWNLOADS"] = "下载次数",
            ["#PUBLIC"] = "公开",
            ["#PRIVATE"] = "私有",
            ["#UNLISTED"] = "未列出",
            ["#UPLOAD"] = "上传",
            ["#ALL"] = "全部",
            ["#ANY"] = "任意",
            ["#NONE"] = "无",
            ["#ON"] = "开",
            ["#OFF"] = "关",
            ["#YES"] = "是",
            ["#NO"] = "否",
            ["#EASY"] = "EASY",
            ["#NORMAL"] = "NORMAL",
            ["#HARD"] = "HARD",
            ["#EXPERT"] = "EXPERT",
            ["#MASTER"] = "MASTER",
            ["#APPEND"] = "APPEND",
            ["#COLLABORATOR"] = "合作者",
            ["#STAFF_PICK"] = "精选",
            ["#TIME"] = "时间",
            ["#DATE"] = "日期",
            ["#PUBLISHED_AT"] = "发布时间",
            ["#CREATED_AT"] = "上传时间",
            ["#UPDATED_AT"] = "更新时间",
            ["#USER"] = "用户",
            ["#LEVEL"] = "谱面",
            ["#LEVELS"] = "谱面",
            ["#ANNOUNCEMENT"] = "公告"
        };

        /// <summary>The label to show for a server string; non-constants pass through unchanged.</summary>
        public static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '#' || text.Length < 2)
            {
                return text ?? string.Empty;
            }

            if (Table.TryGetValue(text, out string localized))
            {
                return localized;
            }

            string words = text.Substring(1).Replace('_', ' ').ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
        }
    }
}
