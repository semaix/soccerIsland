using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// toto（試合の勝敗予想）・裏toto・宝くじ。
    /// オッズは「表で見えている実力」から計算されるので、裏で本気を出す選手を知っていると裏totoで勝てる。
    /// </summary>
    public class BettingSystem
    {
        public float totoPayoutRate = 0.85f;
        public float undergroundPayoutRate = 0.95f;
        public float drawProbability = 0.26f;
        public long minimumStake = 100;
        public long maximumStake = 10000000;

        public const long LotteryTicketPrice = 300;

        readonly NPCDatabase _db;
        readonly WorldState _world;
        readonly EconomyState _state;
        readonly PlayerState _player;
        readonly SeededRandom _rng;
        readonly List<EconomyEvent> _events;

        public BettingSystem(NPCDatabase db, WorldState world, EconomyState state, PlayerState player, SeededRandom rng, List<EconomyEvent> events)
        {
            _db = db;
            _world = world;
            _state = state;
            _player = player;
            _rng = rng;
            _events = events;
        }

        // ---------------- toto ----------------

        public bool IsBettable(Match m)
        {
            if (m == null || m.played || m.day < _db.currentDay) return false;
            if (m.category == MatchCategory.PrisonLeague) return false;
            if (m.IsUnderground && !_player.blackMarketAccess) return false;
            return true;
        }

        /// <summary>表の実力から見た勝率（ホーム勝ち・引き分け・アウェイ勝ち）。</summary>
        public float[] Probabilities(Match m)
        {
            float home = PublicStrength(m.homeLineup);
            float away = PublicStrength(m.awayLineup);
            float homeShare = 1f / (1f + (float)Math.Exp(-(home - away) / 6f));
            float rest = 1f - drawProbability;
            return new[] { rest * homeShare, drawProbability, rest * (1f - homeShare) };
        }

        public float Odds(Match m, TotoPick pick)
        {
            float p = Math.Max(0.01f, Probabilities(m)[(int)pick]);
            float rate = m.IsUnderground ? undergroundPayoutRate : totoPayoutRate;
            return Math.Max(1.05f, (float)Math.Round(rate / p, 2));
        }

        public TotoBet PlaceBet(int matchId, TotoPick pick, long stake)
        {
            var m = _world.GetMatch(matchId);
            if (!IsBettable(m) || stake < minimumStake || stake > maximumStake) return null;
            if (!_player.TrySpend(stake, (m.IsUnderground ? "裏toto: " : "toto: ") + m.homeName + " vs " + m.awayName, _db.currentDay)) return null;
            var bet = new TotoBet { id = _state.nextBetId++, matchId = matchId, pick = pick, stake = stake, odds = Odds(m, pick), underground = m.IsUnderground };
            _state.bets.Add(bet);
            return bet;
        }

        public void SettleBets()
        {
            int day = _db.currentDay;
            foreach (var bet in _state.bets)
            {
                if (bet.settled) continue;
                var m = _world.GetMatch(bet.matchId);
                if (m == null) { Refund(bet, day); continue; } // 試合が消えたら返金
                if (!m.played) continue;

                var result = m.homeScore > m.awayScore ? TotoPick.Home : m.homeScore < m.awayScore ? TotoPick.Away : TotoPick.Draw;
                bet.settled = true;
                bet.won = result == bet.pick;
                if (bet.won)
                {
                    bet.payout = (long)Math.Floor(bet.stake * bet.odds);
                    _player.Earn(bet.payout, bet.underground ? "裏toto 的中" : "toto 的中", day);
                    _events.Add(new EconomyEvent { type = EconomyEventType.TotoWon, day = day, amount = bet.payout, refId = bet.id });
                }
                else
                {
                    _events.Add(new EconomyEvent { type = EconomyEventType.TotoLost, day = day, amount = bet.stake, refId = bet.id });
                }
            }
            _state.bets.RemoveAll(b => b.settled && _world.GetMatch(b.matchId) == null);
        }

        void Refund(TotoBet bet, int day)
        {
            bet.settled = true;
            bet.payout = bet.stake;
            _player.Earn(bet.stake, "toto 返金", day);
        }

        float PublicStrength(List<int> lineup)
        {
            if (lineup.Count == 0) return 65f; // 本土選抜
            float sum = 0f;
            foreach (var id in lineup)
            {
                var n = _db.Get(id);
                if (n != null) sum += n.PublicOverall;
            }
            return sum / lineup.Count;
        }

        // ---------------- 宝くじ ----------------

        /// <summary>次の抽選日（毎週日曜）。</summary>
        public int NextDrawDay()
        {
            int d = _db.currentDay;
            while (d % 7 != 6) d++;
            return d;
        }

        public List<LotteryTicket> BuyLottery(int count)
        {
            var bought = new List<LotteryTicket>();
            if (count <= 0 || !_player.TrySpend(LotteryTicketPrice * count, $"宝くじ {count}枚", _db.currentDay)) return bought;
            int draw = NextDrawDay();
            for (int i = 0; i < count; i++)
            {
                var t = new LotteryTicket { number = _rng.Range(0, 1000000), drawDay = draw };
                _state.lotteryTickets.Add(t);
                bought.Add(t);
            }
            return bought;
        }

        /// <summary>賞金：6桁一致1億円 / 下5桁100万円 / 下4桁5万円 / 下3桁3000円 / 下1桁300円</summary>
        public static long Prize(int ticket, int winning)
        {
            if (ticket == winning) return 100000000;
            if (ticket % 100000 == winning % 100000) return 1000000;
            if (ticket % 10000 == winning % 10000) return 50000;
            if (ticket % 1000 == winning % 1000) return 3000;
            if (ticket % 10 == winning % 10) return 300;
            return 0;
        }

        /// <summary>抽選日なら抽選する。</summary>
        public void DrawIfDue()
        {
            int day = _db.currentDay;
            if (day % 7 != 6 || _state.lastDrawDay == day) return;
            _state.lastDrawDay = day;
            _state.lastWinningNumber = _rng.Range(0, 1000000);
            _events.Add(new EconomyEvent { type = EconomyEventType.LotteryDrawn, day = day, refId = _state.lastWinningNumber });

            foreach (var t in _state.lotteryTickets)
            {
                if (t.checkedResult || t.drawDay != day) continue;
                t.checkedResult = true;
                t.prize = Prize(t.number, _state.lastWinningNumber);
                if (t.prize <= 0) continue;
                _player.Earn(t.prize, "宝くじ当選", day);
                if (t.prize >= 50000)
                    _events.Add(new EconomyEvent { type = EconomyEventType.LotteryWon, day = day, amount = t.prize, refId = t.number });
            }
            _state.lotteryTickets.RemoveAll(t => t.checkedResult && t.drawDay < day - 28);
        }
    }
}
