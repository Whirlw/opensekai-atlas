using System;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Shared.Playback
{
    public sealed class DanDamage
    {
        public int Perfect { get; }
        public int Great { get; }
        public int Good { get; }
        public int Bad { get; }
        public int Miss { get; }

        public DanDamage(int perfect, int great, int good, int bad, int miss)
        {
            Perfect = CheckCost(perfect, nameof(perfect));
            Great = CheckCost(great, nameof(great));
            Good = CheckCost(good, nameof(good));
            Bad = CheckCost(bad, nameof(bad));
            Miss = CheckCost(miss, nameof(miss));
        }

        private static int CheckCost(int cost, string field) => cost >= 0 ? cost
            : throw new ArgumentOutOfRangeException(field, "Damage must be nonnegative.");

        internal int For(NoteResult result) => result switch
        {
            NoteResult.Perfect => Perfect,
            NoteResult.Great => Great,
            NoteResult.Good => Good,
            NoteResult.Bad => Bad,
            NoteResult.Miss => Miss,
            _ => 0
        };
    }

    /// <summary>Bindings for exactly one boot. Ordinary play has no course bindings.</summary>
    public sealed class DanLiveSong
    {
        public int InitialLife { get; }
        public int StartingLife { get; }
        public bool AllowPause { get; }
        public bool FailOnZero { get; }
        public bool IsLastSong { get; }
        public float Rate { get; }
        public DanDamage Damage { get; }
        public Action<int> LifeChanged { get; }
        public Action<PlayResultKind, LiveScore, long> Finish { get; }
        public Action<bool> Exit { get; }
        private bool _retireRequested;
        private Action _retire;

        internal void BindRetire(Action retire)
        {
            _retire = retire;
            if (_retireRequested) _retire?.Invoke();
        }

        public void RequestRetire()
        {
            _retireRequested = true;
            _retire?.Invoke();
        }

        public DanLiveSong(int initialLife, int startingLife, DanDamage damage, Action<int> lifeChanged,
            Action<PlayResultKind, LiveScore, long> finish, Action<bool> exit, bool allowPause = true, bool failOnZero = true, float rate = 1f, bool isLastSong = false)
        {
            PracticeStep.ValidateSpeed(rate);
            Rate = rate;
            IsLastSong = isLastSong;
            AllowPause = allowPause;
            FailOnZero = failOnZero;
            InitialLife = initialLife;
            StartingLife = startingLife;
            Damage = damage;
            LifeChanged = lifeChanged;
            Finish = finish;
            Exit = exit;
        }
    }
}
