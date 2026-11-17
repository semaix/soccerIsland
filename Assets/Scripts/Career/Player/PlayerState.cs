using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>お金の出入り1件。</summary>
    [Serializable]
    public class Transaction
    {
        public int day;
        public long amount; // 入金はプラス、出金はマイナス
        public string reason;
    }

    /// <summary>スカウトした選手との契約。</summary>
    [Serializable]
    public class Contract
    {
        public int npcId;
        /// <summary>年俸（円）。30日ごとに1/12ずつ払う。</summary>
        public long salary;
        public int signedDay;
        public long transferFee;
        /// <summary>元のクラブ（退団したら戻る）。</summary>
        public int previousClubId = -1;
        /// <summary>連続で給料を払えなかった回数。2回で退団。</summary>
        public int unpaidMonths;
    }

    /// <summary>主人公のデータ。</summary>
    [Serializable]
    public class PlayerState
    {
        public string name = "主人公";
        public Town homeTown = Town.Furugane;

        // ---- お金 ----
        public long cash = 3000000;
        public long savings;
        public List<Transaction> transactions = new List<Transaction>();
        /// <summary>信用スコア 0-100。延滞で下がり、銀行で借りられなくなる。</summary>
        public int creditScore = 70;
        /// <summary>裏市場（闇金・裏toto）を利用できるか。</summary>
        public bool blackMarketAccess;

        // ---- チーム ----
        public string teamName = "主人公FC";
        public int teamClubId = -1;
        public List<Contract> contracts = new List<Contract>();

        // ---- 選手としての評価 ----
        /// <summary>知名度 0-100。上がるとサイン・写真を求められ、ゴシップの対象になる。</summary>
        public float fame;
        public long marketValue = 500000;
        /// <summary>調子 -1〜+1（最近の試合評価の移動平均）。</summary>
        public float form;
        public int matchesPlayed;
        public int goals;
        public int assists;
        public int lastMatchDay = -1;

        // ---- 選手としての能力（主人公は自分の能力を知っている） ----
        public Position position = Position.MF;
        /// <summary>レベル（試合の経験値で上がる）。レベルで必殺技を覚える。</summary>
        public int level = 1;
        public int exp;
        public NPCAbilities abilities = DefaultAbilities();

        public static NPCAbilities DefaultAbilities()
        {
            var a = new NPCAbilities();
            for (int i = 0; i < NPCAbilities.StatCount; i++) a[i] = 58;
            a.goalkeeping = 15;
            return a;
        }

        // ---- 自作 ----
        public SignatureDesign signature = new SignatureDesign();
        public List<GoalPerformanceDesign> goalPerformances = new List<GoalPerformanceDesign>();

        public const int MaxTransactions = 300;

        public bool CanAfford(long amount) => cash >= amount;

        public void Earn(long amount, string reason, int day)
        {
            if (amount <= 0) return;
            cash += amount;
            Log(amount, reason, day);
        }

        public bool TrySpend(long amount, string reason, int day)
        {
            if (amount < 0 || cash < amount) return false;
            cash -= amount;
            Log(-amount, reason, day);
            return true;
        }

        /// <summary>差し押さえなど、足りなくても取れるだけ取る。</summary>
        public long ForceSpend(long amount, string reason, int day)
        {
            long taken = Math.Min(cash, Math.Max(0, amount));
            cash -= taken;
            if (taken > 0) Log(-taken, reason, day);
            return taken;
        }

        void Log(long amount, string reason, int day)
        {
            transactions.Add(new Transaction { day = day, amount = amount, reason = reason });
            if (transactions.Count > MaxTransactions) transactions.RemoveRange(0, transactions.Count - MaxTransactions);
        }

        public Contract FindContract(int npcId)
        {
            foreach (var c in contracts) if (c.npcId == npcId) return c;
            return null;
        }
    }
}
