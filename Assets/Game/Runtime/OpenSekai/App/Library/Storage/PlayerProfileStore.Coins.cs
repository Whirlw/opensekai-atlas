using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.Shared.Storage;

namespace OpenSekai.App.Library
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CoinPurchase
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; }
        [JsonProperty("item", Required = Required.Always)] public string Item { get; }
        [JsonProperty("amount", Required = Required.Always)] public int Amount { get; }
        [JsonProperty("time", Required = Required.Always)] public DateTimeOffset Time { get; }

        [JsonConstructor]
        internal CoinPurchase(string id, string item, int amount, DateTimeOffset time)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(item) || amount <= 0 || time == default)
                throw new ArgumentException("无效的金币消费记录。");
            Id = id;
            Item = item;
            Amount = amount;
            Time = time.ToUniversalTime();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CoinSpending
    {
        [JsonProperty("transactions", Required = Required.Always)]
        private Dictionary<string, CoinPurchase> _transactions = new(StringComparer.Ordinal);

        internal int Count => _transactions.Count;
        internal long Total => _transactions.Values.Sum(row => (long)row.Amount);
        internal IReadOnlyList<CoinPurchase> LatestFirst() => _transactions.Values.OrderByDescending(row => row.Time)
            .ThenBy(row => row.Id, StringComparer.Ordinal).ToArray();

        internal bool Contains(string id, string item, int amount)
        {
            if (!_transactions.TryGetValue(id, out var existing)) return false;
            if (existing.Item != item || existing.Amount != amount)
                throw new InvalidDataException("同一笔金币消费的商品或金额不同。");
            return true;
        }

        internal CoinSpending With(CoinPurchase row)
        {
            var next = new CoinSpending { _transactions = new Dictionary<string, CoinPurchase>(_transactions, StringComparer.Ordinal) };
            next._transactions.Add(row.Id, row);
            return next;
        }

        // Called on the staged copy only. Reject conflicting receipts before the import can be activated.
        internal int Merge(CoinSpending incoming)
        {
            int before = Count;
            foreach (var row in incoming._transactions.Values)
                if (!Contains(row.Id, row.Item, row.Amount)) _transactions.Add(row.Id, row);
            return Count - before;
        }

        internal static CoinSpending Decode(string json)
        {
            var spending = ProfileBackupArchive.Parse(json).ToObject<CoinSpending>();
            if (spending._transactions.Any(pair => pair.Value == null || pair.Key != pair.Value.Id))
                throw new InvalidDataException("无效的金币消费记录。");
            return spending;
        }
    }

    public sealed partial class PlayerProfileStore
    {
        internal const string SPENDING_FILE = "coin-spending.json";
        private string SpendingPath => Path.Combine(_directory, SPENDING_FILE);
        private CoinSpending _spending;
        private CoinSpending Spending => _spending ??= LoadSpending();
        private CoinSpending LoadSpending() => File.Exists(SpendingPath)
            ? CoinSpending.Decode(File.ReadAllText(SpendingPath)) : new CoinSpending();

        private static long CoinBalance(RankingHistory history, AchievementClaims claims, CoinSpending spending) =>
            history.PlayCoins + claims.CoinRewards - spending.Total;

        public long ReadCoins() => CoinBalance(History, ReadClaims(), Spending);
        public IReadOnlyList<CoinPurchase> ReadPurchases() => Spending.LatestFirst();

        /// <summary>Use the same transaction ID on retry. Returns false if the same purchase was already recorded.</summary>
        public bool SpendCoins(string transactionId, string item, int amount)
        {
            RequireIdle();
            var row = new CoinPurchase(transactionId, item, amount, DateTimeOffset.UtcNow);
            if (Spending.Contains(row.Id, row.Item, row.Amount)) return false;
            if (ReadCoins() < amount) throw new InvalidOperationException("金币余额不足。");
            var next = Spending.With(row);
            AtomicFile.WriteAllText(SpendingPath, JsonConvert.SerializeObject(next));
            _spending = next;
            CancelPendingPreview();
            return true;
        }
    }
}
