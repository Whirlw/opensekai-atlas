using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.UI
{
    internal static class PracticeForm
    {
        internal static double Seconds(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                && !double.IsInfinity(seconds) && !double.IsNaN(seconds)) return seconds;
            throw new FormatException("请输入有效的秒数。");
        }

        internal static (double start, double? end) ReadRange(string from, string to)
        {
            double start = string.IsNullOrWhiteSpace(from) ? 0d : Seconds(from);
            double? end = string.IsNullOrWhiteSpace(to) ? null : Seconds(to);
            if (end.HasValue && end.Value <= start) throw new FormatException("结束时间必须晚于开始时间。");
            return (start, end);
        }

        internal static float Speed(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)
                && speed >= PracticeStep.MIN_SPEED && speed <= PracticeStep.MAX_SPEED) return speed;
            throw new FormatException($"倍速范围为 {PracticeStep.MIN_SPEED} 至 {PracticeStep.MAX_SPEED}。");
        }

        internal static PracticeTemplate ReadSequence(string name, string sequence)
        {
            var steps = new List<PracticeStep>();
            foreach (string field in sequence.Split(',', '，'))
            {
                string token = field.Trim();
                if (token.Length < 2) throw new FormatException("请用逗号分隔步骤，例如 0.5x, 2s, 0.7x。");
                string value = token.Substring(0, token.Length - 1);
                switch (char.ToLowerInvariant(token[token.Length - 1]))
                {
                    case 'x':
                    case '×':
                        steps.Add(PracticeStep.Play(Speed(value)));
                        break;
                    case 's':
                        double rest = Seconds(value);
                        if (rest <= 0d) throw new FormatException("休息时长必须大于零。");
                        steps.Add(PracticeStep.Rest(rest));
                        break;
                    default:
                        throw new FormatException("播放倍速以 x 结尾，休息秒数以 s 结尾。");
                }
            }
            if (!steps.Any(step => step.Kind == PracticeStepKind.Play))
                throw new FormatException("序列需要至少一个播放步骤。");
            return new PracticeTemplate(name, steps);
        }

        internal static string FormatSequence(PracticeTemplate template) =>
            string.Join(", ", template.Steps.Select(step => step.Kind == PracticeStepKind.Play
                ? step.Speed.ToString("R", CultureInfo.InvariantCulture) + "x"
                : step.RestSeconds.ToString("R", CultureInfo.InvariantCulture) + "s"));
    }
}
