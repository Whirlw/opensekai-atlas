using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OpenSekai.Shared.Playback
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PracticeStepKind
    {
        Play,
        Rest
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PracticeStep
    {
        // AudioSource.pitch supports forward playback up to 3x.
        public const float MIN_SPEED = 0.01f;
        public const float MAX_SPEED = 3f;

        [JsonProperty("kind", Required = Required.Always)]
        public PracticeStepKind Kind { get; }

        [JsonProperty("speed", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float Speed { get; }

        [JsonProperty("restSeconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double RestSeconds { get; }

        [JsonConstructor]
        private PracticeStep(PracticeStepKind kind, float speed, double restSeconds)
        {
            switch (kind)
            {
                case PracticeStepKind.Play:
                    ValidateSpeed(speed);
                    if (restSeconds != 0) throw new ArgumentException("A play step cannot also rest.");
                    break;
                case PracticeStepKind.Rest:
                    if (!(restSeconds > 0) || double.IsInfinity(restSeconds))
                        throw new ArgumentOutOfRangeException(nameof(restSeconds));
                    if (speed != 0) throw new ArgumentException("A rest step cannot also play.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
            Speed = speed;
            RestSeconds = restSeconds;
        }

        public static PracticeStep Play(float speed) => new PracticeStep(PracticeStepKind.Play, speed, 0);
        public static PracticeStep Rest(double seconds) => new PracticeStep(PracticeStepKind.Rest, 0, seconds);

        internal static void ValidateSpeed(float speed)
        {
            if (!(speed >= MIN_SPEED && speed <= MAX_SPEED))
                throw new ArgumentOutOfRangeException(nameof(speed));
        }
    }

    /// <summary>A named sequence applicable to any chart. Segment bounds belong to each run.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PracticeTemplate
    {
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; }

        [JsonProperty("steps", Required = Required.Always)]
        public IReadOnlyList<PracticeStep> Steps { get; }

        [JsonConstructor]
        public PracticeTemplate(string name, IEnumerable<PracticeStep> steps)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A template needs a name.", nameof(name));
            var copy = new List<PracticeStep>(steps ?? throw new ArgumentNullException(nameof(steps)));
            if (copy.Contains(null) || !copy.Exists(step => step.Kind == PracticeStepKind.Play))
                throw new ArgumentException("A template needs a play step and cannot contain empty steps.", nameof(steps));

            Name = name.Trim();
            Steps = copy.AsReadOnly();
        }
    }
}
