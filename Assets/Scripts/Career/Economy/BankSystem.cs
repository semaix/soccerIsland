using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 銀行と闇金。
    /// ・銀行：信用スコアと主人公の市場価値で借入枠が決まる。延滞すると信用が落ちる。
    /// ・闇金：裏市場で利用できる。トイチ（10日で1割）。延滞すると嫌がらせ→八百長の要求→差し押さえ。
    /// </summary>
    public class BankSystem
    {
        public double savingsAnnualRate = 0.002;
        public int minimumCreditForBank = 40;
        public long loanSharkLimit = 50000000;
        /// <summary>闇金の利率：10日で1割。</summary>
        public double loanSharkDailyRate = Math.Pow(1.1, 1.0 / 10.0) - 1.0;

        readonly PlayerState _player;
        readonly EconomyState _state;
        readonly List<EconomyEvent> _events;
        readonly Func<int> _today;

        public BankSystem(PlayerState player, EconomyState state, List<EconomyEvent> events, Func<int> today)
        {
            _player = player;
            _state = state;
            _events = events;
            _today = today;
        }

        // ---------------- 預金 ----------------

        public bool Deposit(long amount)
        {
            if (!_player.TrySpend(amount, "預金", _today())) return false;
            _player.savings += amount;
            return true;
        }

        public bool Withdraw(long amount)
        {
            if (amount <= 0 || amount > _player.savings) return false;
            _player.savings -= amount;
            _player.Earn(amount, "引き出し", _today());
            return true;
        }

        // ---------------- 借入 ----------------

        /// <summary>銀行の年利。信用が高いほど低い（3%〜15%）。</summary>
        public double BankAnnualRate => 0.03 + (100 - _player.creditScore) / 100.0 * 0.12;

        public long BankLimit()
        {
            if (_player.creditScore < minimumCreditForBank) return 0;
            long limit = Math.Max(1000000, _player.marketValue * 2 + (long)(_player.fame * 100000));
            limit = (long)(limit * (_player.creditScore / 70.0));
            return Math.Max(0, limit - _state.TotalDebt(Lender.Bank));
        }

        public long LoanSharkAvailable() =>
            _player.blackMarketAccess ? Math.Max(0, loanSharkLimit - _state.TotalDebt(Lender.LoanShark)) : 0;

        /// <summary>借りる。termDays 後が返済期限。失敗したら null。</summary>
        public Loan Borrow(Lender lender, long amount, int termDays)
        {
            if (amount <= 0) return null;
            int day = _today();
            double rate;
            if (lender == Lender.Bank)
            {
                if (amount > BankLimit()) return null;
                termDays = Math.Max(30, Math.Min(365 * 3, termDays));
                rate = BankAnnualRate / 365.0;
            }
            else
            {
                if (amount > LoanSharkAvailable()) return null;
                termDays = Math.Max(10, Math.Min(60, termDays));
                rate = loanSharkDailyRate;
            }

            var loan = new Loan
            {
                id = _state.nextLoanId++, lender = lender, principal = amount, balance = amount,
                dailyRate = rate, startDay = day, dueDay = day + termDays,
            };
            _state.loans.Add(loan);
            _player.Earn(amount, lender == Lender.Bank ? "銀行から借入" : "闇金から借入", day);
            _events.Add(new EconomyEvent { type = EconomyEventType.LoanTaken, day = day, amount = amount, refId = loan.id });
            return loan;
        }

        /// <summary>返済。返した額を返す。</summary>
        public long Repay(int loanId, long amount)
        {
            var loan = _state.GetLoan(loanId);
            if (loan == null || amount <= 0) return 0;
            long pay = Math.Min(amount, loan.Balance);
            if (!_player.TrySpend(pay, loan.lender == Lender.Bank ? "銀行へ返済" : "闇金へ返済", _today())) return 0;
            loan.balance -= pay;
            if (loan.IsPaid)
            {
                _state.loans.Remove(loan);
                if (loan.lender == Lender.Bank && loan.overdueDays == 0) _player.creditScore = Math.Min(100, _player.creditScore + 3);
                _events.Add(new EconomyEvent { type = EconomyEventType.LoanRepaid, day = _today(), amount = loan.principal, refId = loan.id });
            }
            return pay;
        }

        // ---------------- 毎日の処理 ----------------

        public void TickDay()
        {
            int day = _today();
            _player.savings = (long)Math.Floor(_player.savings * (1 + savingsAnnualRate / 365.0));

            foreach (var loan in _state.loans.ToArray())
            {
                loan.balance *= 1 + loan.dailyRate;
                if (day <= loan.dueDay) continue;

                loan.overdueDays++;
                if (loan.lender == Lender.Bank)
                {
                    loan.balance *= 1 + 0.146 / 365.0; // 遅延損害金 年14.6%
                    _player.creditScore = Math.Max(0, _player.creditScore - 1);
                    if (loan.overdueDays == 1 || loan.overdueDays % 30 == 0)
                        _events.Add(new EconomyEvent { type = EconomyEventType.LoanOverdue, day = day, amount = loan.Balance, refId = loan.id });
                }
                else if (loan.overdueDays % 5 == 1)
                {
                    LoanSharkPressure(loan, day);
                }
            }
        }

        void LoanSharkPressure(Loan loan, int day)
        {
            loan.pressureLevel = Math.Min(3, loan.pressureLevel + 1);
            switch (loan.pressureLevel)
            {
                case 1:
                    _events.Add(new EconomyEvent { type = EconomyEventType.LoanSharkHarassment, day = day, amount = loan.Balance, refId = loan.id,
                        text = "家の前に怖い人たちが立っている……" });
                    break;
                case 2:
                    _events.Add(new EconomyEvent { type = EconomyEventType.LoanSharkDemandsFix, day = day, amount = loan.Balance, refId = loan.id,
                        text = "「次の試合、わざと負けたら借金を半分にしてやる」" });
                    break;
                default:
                    long taken = _player.ForceSpend(loan.Balance, "闇金の差し押さえ", day);
                    loan.balance -= taken;
                    _events.Add(new EconomyEvent { type = EconomyEventType.LoanSharkSeizure, day = day, amount = taken, refId = loan.id,
                        text = "財布の中身を持っていかれた。" });
                    if (loan.IsPaid) _state.loans.Remove(loan);
                    break;
            }
        }

        /// <summary>八百長を引き受けて実行したとき（試合イベント側から呼ぶ）。借金が半分になる。</summary>
        public void ForgiveHalfForFix(int loanId)
        {
            var loan = _state.GetLoan(loanId);
            if (loan == null || loan.lender != Lender.LoanShark) return;
            loan.balance *= 0.5;
            loan.dueDay = _today() + 20;
            loan.overdueDays = 0;
            loan.pressureLevel = 0;
        }
    }
}
