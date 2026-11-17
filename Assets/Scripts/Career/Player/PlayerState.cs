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
        /// <summary>年齢。1年ごとに上がる。</summary>
        public int age = 26;
        /// <summary>今の時間帯（TimeOfDay）。</summary>
        public int timeOfDay;
        /// <summary>今いる街と場所。</summary>
        public Town currentTown = Town.Furugane;
        public int currentPlace; // IslandPlace
        /// <summary>人生何周目か（本人は気づいていない）。</summary>
        public int cycle = 1;
        /// <summary>前世の記憶で「なぜか知っている」島民（化ける選手）。</summary>
        public List<int> dejaVu = new List<int>();

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

        // ---- 練習・検定 ----
        /// <summary>疲労 0-100。高いと試合でガッツが減る。毎日回復する。</summary>
        public float fatigue;
        public int lastTrainingDay = -1;
        public int trainingsToday;
        /// <summary>各能力の練習の進み具合（1.0 で能力値+1）。</summary>
        public List<float> statProgress = new List<float>();
        /// <summary>能力検定の最高の級（-1 は未受験）。</summary>
        public int examGrade = -1;
        public int lastExamDay = -100;
        public NPCAbilities abilities = DefaultAbilities();

        public static NPCAbilities DefaultAbilities()
        {
            var a = new NPCAbilities();
            for (int i = 0; i < NPCAbilities.StatCount; i++) a[i] = 58;
            a.goalkeeping = 15;
            return a;
        }

        // ---- 持ち物 ----
        public List<InventoryItem> inventory = new List<InventoryItem>();
        /// <summary>身につけている物（商品名）。スパイク・ファッションなど。</summary>
        public List<string> equipped = new List<string>();
        /// <summary>次の試合で効く効果（アイテムを使うとたまる。試合が終わると消える）。</summary>
        public List<MatchBuff> matchBuffs = new List<MatchBuff>();
        public int lastRejuvenateDay = -1000;
        /// <summary>けがで試合に出られない最後の日。-1 はけがなし。</summary>
        public int injuredUntilDay = -1;
        public bool IsInjured(int day) => injuredUntilDay >= day;
        /// <summary>飾った記念品（一度だけ知名度が上がる）。</summary>
        public List<string> displayed = new List<string>();

        // ---- 自作 ----
        public SignatureDesign signature = new SignatureDesign();
        public List<GoalPerformanceDesign> goalPerformances = new List<GoalPerformanceDesign>();
        /// <summary>試合で使うゴールパフォーマンス（goalPerformances の番号。-1 はなし）。</summary>
        public int selectedPerformance = -1;

        public GoalPerformanceDesign SelectedPerformance =>
            selectedPerformance >= 0 && selectedPerformance < goalPerformances.Count ? goalPerformances[selectedPerformance] : null;

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
