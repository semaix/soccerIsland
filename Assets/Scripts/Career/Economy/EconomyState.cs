using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    public enum Lender
    {
        Bank,      // 銀行
        LoanShark, // 闇金（裏市場）
    }

    [Serializable]
    public class Loan
    {
        public int id;
        public Lender lender;
        public long principal;
        /// <summary>残りの返済額（利息込み）。</summary>
        public double balance;
        /// <summary>1日あたりの利率。</summary>
        public double dailyRate;
        public int startDay;
        public int dueDay;
        public int overdueDays;
        /// <summary>闇金の取り立て段階（0〜3）。</summary>
        public int pressureLevel;

        public bool IsPaid => balance < 1;
        public long Balance => (long)Math.Ceiling(balance);
    }

    public enum StockKind
    {
        Club, // チーム株
        Shop, // 商店株
    }

    [Serializable]
    public class Stock
    {
        public int id;
        public string name;
        public StockKind kind;
        public int clubId = -1;
        public string shopName;
        public double price;
        public List<float> history = new List<float>();

        public long Price => (long)Math.Round(price);
    }

    [Serializable]
    public class Holding
    {
        public int stockId;
        public long shares;
        public double averageCost;
    }

    public enum TotoPick
    {
        Home,
        Draw,
        Away,
    }

    [Serializable]
    public class TotoBet
    {
        public int id;
        public int matchId;
        public TotoPick pick;
        public long stake;
        public float odds;
        /// <summary>裏toto（裏フットサルが対象）。</summary>
        public bool underground;
        public bool settled;
        public bool won;
        public long payout;
    }

    [Serializable]
    public class LotteryTicket
    {
        /// <summary>6桁の番号。</summary>
        public int number;
        public int drawDay;
        public bool checkedResult;
        public long prize;
    }

    public enum EconomyEventType
    {
        Scouted,           // 選手獲得
        Released,          // 選手放出
        PlayerLeft,        // 給料未払いで退団
        PayrollFailed,     // 給料が払えない
        LoanTaken,
        LoanRepaid,
        LoanOverdue,       // 銀行の延滞
        LoanSharkHarassment, // 闇金の嫌がらせ
        LoanSharkDemandsFix, // 闇金が八百長を要求
        LoanSharkSeizure,  // 闇金の差し押さえ
        TotoWon,
        TotoLost,
        LotteryWon,
        LotteryDrawn,
        StockSurge,        // 株価急騰
        StockCrash,        // 株価暴落
    }

    /// <summary>お金まわりの出来事。新聞・イベントのきっかけになる。</summary>
    public struct EconomyEvent
    {
        public EconomyEventType type;
        public int day;
        public int npcId;
        public long amount;
        public int refId;
        public string text;
    }

    [Serializable]
    public class EconomyState
    {
        public int nextLoanId;
        public int nextBetId;
        public List<Loan> loans = new List<Loan>();
        public List<Stock> stocks = new List<Stock>();
        public List<Holding> holdings = new List<Holding>();
        public List<TotoBet> bets = new List<TotoBet>();
        public List<LotteryTicket> lotteryTickets = new List<LotteryTicket>();
        /// <summary>直近の宝くじ当選番号。</summary>
        public int lastWinningNumber = -1;
        public int lastDrawDay = -1;

        public Stock GetStock(int id)
        {
            foreach (var s in stocks) if (s.id == id) return s;
            return null;
        }

        public Holding GetHolding(int stockId)
        {
            foreach (var h in holdings) if (h.stockId == stockId) return h;
            return null;
        }

        public Loan GetLoan(int id)
        {
            foreach (var l in loans) if (l.id == id) return l;
            return null;
        }

        public long TotalDebt(Lender lender)
        {
            double sum = 0;
            foreach (var l in loans) if (l.lender == lender) sum += l.balance;
            return (long)Math.Ceiling(sum);
        }
    }
}
