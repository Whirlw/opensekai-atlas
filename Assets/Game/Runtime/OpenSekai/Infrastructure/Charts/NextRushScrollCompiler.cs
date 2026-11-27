using System;
using System.Collections.Generic;
using System.IO;
using OpenSekai.Charts;

namespace OpenSekai.Infrastructure.Charts
{
    internal static class NextRushScrollCompiler
    {
        private const string GROUP = "#TIMESCALE_GROUP";
        private const string MARKER = "#TIMESCALE_CHANGE";

        internal static Dictionary<NextRushEntity, NextRushScrollTimeline> Compile(
            NextRushChart chart, Func<double, (double time, double bpm)> timingAt, bool constantScroll = false)
        {
            var groups = new Dictionary<NextRushEntity, NextRushScrollTimeline>();
            var visited = new HashSet<NextRushEntity>();
            foreach (var group in chart.Entities)
            {
                if (group.Archetype != GROUP) continue;
                CheckFields(group, "first", "forceNoteSpeed", "editorName", "editorHidden");
                var changes = new List<NextRushScrollTimeline.Change>();
                var marker = Resolve(chart, group, "first", MARKER);
                while (marker != null)
                {
                    if (!visited.Add(marker)) throw Invalid("变速事件链存在循环或共享节点", marker);
                    CheckFields(marker, "#BEAT", "#TIMESCALE", "#TIMESCALE_SKIP", "#TIMESCALE_EASE",
                        GROUP, "transitionStyle", "hideNotes", "next", "editorLane");
                    var owner = Resolve(chart, marker, GROUP, GROUP);
                    if (owner != null && owner != group) throw Invalid("变速事件所属组不一致", marker);
                    // Match the runtime's bounded chart time while retaining double precision.
                    double beat = marker.Number("#BEAT");
                    if (!marker.Has("#BEAT") || Math.Abs(beat) > 1000000)
                        throw Invalid("变速拍数缺失或超出范围", marker);
                    var (time, bpm) = timingAt(beat);
                    double speed = marker.Number("#TIMESCALE");
                    double skip = marker.Number("#TIMESCALE_SKIP") * 60 / bpm;
                    double ease = marker.Number("#TIMESCALE_EASE");
                    double style = marker.Number("transitionStyle");
                    if (Math.Abs(time) > NextRushChart.MAX_ABSOLUTE_TIME_SECONDS || Math.Abs(speed) > 1e9 || Math.Abs(skip) > 1e9)
                        throw Invalid("变速时间、速度或跳距超出范围", marker);
                    if (ease < 0 || ease > 41 || ease % 1 != 0 || ease >= 30 && ease <= 37)
                        throw Invalid("变速缓动无效", marker);
                    if (style != 0 && style != 1) throw Invalid("变速模式无效", marker);
                    changes.Add(new NextRushScrollTimeline.Change(time, constantScroll ? 1d : speed, constantScroll ? 0d : skip,
                        constantScroll ? 0 : (int)ease, !constantScroll && style == 1,
                        marker.Number("hideNotes") != 0));
                    marker = Resolve(chart, marker, "next", MARKER);
                }
                double forceSpeed = group.Number("forceNoteSpeed");
                if (changes.Count > 0 || forceSpeed > 0)
                    groups.Add(group, new NextRushScrollTimeline(changes, constantScroll ? 0d : forceSpeed));
            }
            foreach (var entity in chart.Entities)
                if (entity.Archetype == MARKER && !visited.Contains(entity))
                    throw Invalid("变速事件未连接到变速组", entity);
            return groups;
        }

        private static NextRushEntity Resolve(NextRushChart chart, NextRushEntity source, string field, string archetype)
        {
            string name = source.Reference(field);
            if (name == null) return null;
            if (!chart.NamedEntities.TryGetValue(name, out var target) || target.Archetype != archetype)
                throw Invalid("变速引用无效 " + field + " → " + name, source);
            return target;
        }

        private static void CheckFields(NextRushEntity entity, params string[] allowed)
        {
            foreach (string field in entity.Fields)
                if (Array.IndexOf(allowed, field) < 0)
                    throw new InvalidDataException("NextRUSH+ 暂不支持：" + entity.Archetype + "." + field);
        }

        private static InvalidDataException Invalid(string message, NextRushEntity entity) =>
            new InvalidDataException("NextRUSH+ " + message + "：" + (entity.Name ?? entity.Archetype));
    }
}
