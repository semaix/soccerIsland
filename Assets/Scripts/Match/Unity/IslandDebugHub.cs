using System;
using System.Collections.Generic;
using System.Linq;
using SoccerIsland.Career;
using SoccerIsland.Career.UnityIntegration;
using SoccerIsland.NPC;
using SoccerIsland.NPC.UnityIntegration;
using SoccerIsland.World;
using UnityEngine;

namespace SoccerIsland.Match.UnityIntegration
{
    /// <summary>
    /// ゲーム全体を画面上で触るための仮UI（OnGUI）。本番の2D画面ができるまでの確認用。
    /// NPCManager・WorldManager・CareerManager と同じシーンに置く（試合は MatchDebugView）。
    /// </summary>
    public class IslandDebugHub : MonoBehaviour
    {
        enum Tab { Islanders, Relations, News, Money, Shop, Training, Tournament, Me }

        static readonly string[] TabNames = { "島民", "相関図", "新聞", "お金", "お店", "練習", "大会", "主人公" };

        Tab _tab;
        Vector2 _scroll, _scroll2;
        string _search = "";
        int _selected = -1;
        readonly List<string> _messages = new List<string>();
        TournamentSystem _tournament;
        CareerDayReport _lastDay;

        IslandCareer Career => CareerManager.Instance != null ? CareerManager.Instance.Career : null;

        void Say(string s)
        {
            _messages.Add(s);
            if (_messages.Count > 12) _messages.RemoveAt(0);
        }

        void OnGUI()
        {
            var c = Career;
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            if (c == null) { GUILayout.Label("CareerManager がありません"); GUILayout.EndArea(); return; }

            var db = c.Population.Database;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{db.currentYear}年 {db.currentDay}日目 {DailySchedule.Label(c.Now)}（{WeatherSystem.Label(c.World.CurrentSeason)}・{WeatherSystem.Label(c.World.TodaysWeather)}）　{c.Player.name}（{c.Player.age}歳・{c.Player.cycle}周目）　所持金 {NPCLabels.Yen(c.Player.cash)}　知名度 {c.Player.fame:0}");
            if (GUILayout.Button("時間を進める", GUILayout.Width(120)))
            {
                if (c.Now == TimeOfDay.Night)
                {
                    CareerManager.Instance.OnCareerDay += Capture;
                    c.Player.timeOfDay = (int)TimeOfDay.Morning;
                    NPCManager.Instance.AdvanceDay();
                    CareerManager.Instance.OnCareerDay -= Capture;
                }
                else c.Player.timeOfDay++;
            }
            if (GUILayout.Button("1日進める", GUILayout.Width(120)))
            {
                CareerManager.Instance.OnCareerDay += Capture;
                NPCManager.Instance.AdvanceDay();
                CareerManager.Instance.OnCareerDay -= Capture;
            }
            if (GUILayout.Button("セーブ", GUILayout.Width(80))) NPCManager.Instance.Save();
            GUILayout.EndHorizontal();

            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height * 0.62f));
            switch (_tab)
            {
                case Tab.Islanders: DrawIslanders(c); break;
                case Tab.Relations: DrawRelations(c); break;
                case Tab.News: DrawNews(c); break;
                case Tab.Money: DrawMoney(c); break;
                case Tab.Shop: DrawShop(c); break;
                case Tab.Training: DrawTraining(c); break;
                case Tab.Tournament: DrawTournament(c); break;
                default: DrawMe(c); break;
            }
            GUILayout.EndScrollView();

            GUILayout.Label("――――");
            foreach (var m in _messages) GUILayout.Label(m);
            GUILayout.EndArea();
        }

        void Capture(CareerDayReport r)
        {
            _lastDay = r;
            if (r.newspaper?.Headline != null) Say("【朝刊】" + r.newspaper.Headline.title);
            foreach (var call in r.incomingCalls) Say("☎ " + call.text);
        }

        // ------------------------------------------------------------------

        void DrawIslanders(IslandCareer c)
        {
            var db = c.Population.Database;
            GUILayout.Label($"今いる場所：{c.Here}（{DailySchedule.Label(c.Now)}）");
            var fest = c.CurrentFestival;
            if (fest != null && GUILayout.Button($"「{fest.name}」に参加する")) Say(c.JoinFestival().text);
            foreach (var f in c.Schedule.calendar.On(c.Population.Database.currentDay))
                GUILayout.Label($"今日の行事：{f.name}（{NPCLabels.Of(f.town)}・{IslandMap.PlaceLabel(f.place)}・{DailySchedule.Label(f.time)}）");
            DrawMove(c);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Screen.width * 0.4f));
            _scroll2 = GUILayout.BeginScrollView(_scroll2, GUILayout.Height(Screen.height * 0.45f));
            GUILayout.Label("── ここにいる人 ──");
            foreach (var n in c.PeopleHere().Take(60))
            {
                var e = c.Population.Knowledge.Get(n.id);
                string name = e != null && e.Knows(NPCInfoFlags.Name) ? n.FullName : $"見知らぬ人（{NPCLabels.Of(n.occupation)}の格好）";
                if (GUILayout.Button(name + (c.Player.dejaVu.Contains(n.id) ? "　（なぜか気になる）" : ""))) _selected = n.id;
            }
            GUILayout.Label("── 知り合い ──");
            _search = GUILayout.TextField(_search);
            foreach (var e in c.Population.Knowledge.entries.Take(300))
            {
                var n = db.Get(e.npcId);
                if (n == null || !e.Knows(NPCInfoFlags.Name) || (_search.Length > 0 && !n.FullName.Contains(_search))) continue;
                var loc = c.Locate(n.id);
                if (GUILayout.Button($"{n.FullName}　好感度{e.affinity}　今は{DailySchedule.PlaceName(n, loc.place)}")) _selected = n.id;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            var sel = db.Get(_selected);
            if (sel != null) DrawProfile(c, sel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void DrawMove(IslandCareer c)
        {
            foreach (Town t in Enum.GetValues(typeof(Town)))
            {
                GUILayout.BeginHorizontal();
                int cost = IslandMap.Distance(c.Here.town, t);
                GUILayout.Label($"{NPCLabels.Of(t)}（{(cost == 0 ? "ここ" : cost + "時間帯")}）", GUILayout.Width(150));
                foreach (var place in IslandMap.PlacesIn(t, c.Player.homeTown))
                    if (GUILayout.Button(IslandMap.PlaceLabel(place))) Moved(c.MoveTo(t, place));
                if (c.BlackMarket.KnowsLocation && c.Player.blackMarketAccess && c.BlackMarket.CurrentTown == t && GUILayout.Button("裏市場"))
                    Moved(c.MoveTo(t, IslandPlace.BlackMarket));
                GUILayout.EndHorizontal();
            }
        }

        void Moved(List<CareerDayReport> reports)
        {
            if (reports == null) { Say("そこには行けない"); return; }
            var c = Career;
            foreach (var ev in c.LastTravelEvents)
            {
                Say("（移動中）" + ev.text);
                if (ev.fanRequest != null) Say(c.Fans.Respond(ev.fanRequest, true) == FanResponseResult.NoSignature ? "サインをまだ作っていない！" : "ファンに応えた");
            }
            foreach (var r in reports) Capture(r);
            _selected = -1;
        }

        void DrawProfile(IslandCareer c, NPCData n)
        {
            var p = c.Population.GetProfile(n.id);
            GUILayout.Label($"{p.name}（{p.age}）{p.occupation}　{p.town}");
            GUILayout.Label($"今いる場所：{DailySchedule.PlaceName(n, c.WhereIs(n.id))}（{DailySchedule.Label(c.Now)}）");
            GUILayout.Label($"クラブ {p.club}　ポジション {p.position}");
            GUILayout.Label($"性格 {p.personality} / {p.soccerPersonality}");
            GUILayout.Label($"年俸 {p.salary}　市場価値 {p.marketValue}");
            GUILayout.Label($"才能度 {p.talentRank}　努力度 {p.effortRank}　電話 {p.phoneNumber}　好感度 {p.affinity}");
            foreach (var note in p.notes) GUILayout.Label("※ " + note);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(c.IsHere(n.id) || !c.requirePresence ? "話す" : "（ここにはいない）"))
            {
                var r = c.Interact(n.id);
                if (r.notHere) { Say("その人はここにいない"); GUILayout.EndHorizontal(); return; }
                foreach (var l in r.conversation.lines) Say($"{p.name}「{l}」");
                if (r.fanRequest != null)
                {
                    Say(r.fanRequest.line);
                    var res = c.Fans.Respond(r.fanRequest, true);
                    Say(res == FanResponseResult.NoSignature ? "サインをまだ作っていない！" : "応えた");
                }
            }
            if (GUILayout.Button($"スカウト（{NPCLabels.Yen(c.Scouting.Price(n))}）")) Say("スカウト：" + c.Scouting.Scout(n.id));
            if (c.Phone.CanCall(n.id) && GUILayout.Button("電話"))
            {
                var r = c.Phone.Call(n.id, CallPurpose.Chat);
                Say(r.answered ? $"☎ {p.name}「{r.line}」" : "☎ " + r.line);
            }
            if (c.Romance.Unlocked && GUILayout.Button("デートに誘う"))
            {
                var r = c.Romance.Date(n.id, DateSpot.Cafe);
                Say(r.ok ? $"{p.name}「{r.line}」" : r.reason);
            }
            if (c.Romance.Unlocked && GUILayout.Button("告白する"))
            {
                var r = c.Romance.Confess(n.id);
                Say(r.ok ? $"{p.name}「{r.line}」" : $"{r.reason}　{r.line}");
            }
            if (n.isStranded && GUILayout.Button("協会に登録してあげる")) Say(c.Castaways.Register(n.id) ? "登録した！" : "まだ頼まれていない／お金が足りない");
            GUILayout.EndHorizontal();
        }

        void DrawRelations(IslandCareer c)
        {
            var standings = c.World.League.Table();
            GUILayout.Label($"── 協会リーグ {c.World.League.League.season}年 ──");
            for (int i = 0; i < Math.Min(8, standings.Count); i++)
                GUILayout.Label($"{i + 1}位 {standings[i].name}　勝点{standings[i].Points}（{standings[i].won}勝{standings[i].drawn}分{standings[i].lost}敗）");
            foreach (var sc in c.World.League.TopScorers(5))
                GUILayout.Label($"得点ランキング　{(sc.npcId == -1 ? c.Player.name : c.Population.Database.Get(sc.npcId)?.FullName)}　{sc.goals}点");
            var db = c.Population.Database;
            GUILayout.Label($"わかっている関係：{c.Population.Knowledge.knownRelations.Count}本");
            foreach (var r in c.Population.Knowledge.knownRelations)
            {
                var a = db.Get(r.fromId); var b = db.Get(r.toId);
                if (a == null || b == null) continue;
                GUILayout.Label($"{a.FullName} ―{NPCLabels.Of(r.type)}― {b.FullName}");
            }
        }

        void DrawNews(IslandCareer c)
        {
            var issue = c.Media.Latest;
            if (issue == null) { GUILayout.Label("まだ新聞は届いていない"); return; }
            GUILayout.Label($"サッカー新聞　{issue.year}年 {issue.day}日目");
            foreach (var a in issue.articles)
            {
                GUILayout.Label($"■ {a.title}");
                GUILayout.Label("　" + a.body);
            }
        }

        void DrawMoney(IslandCareer c)
        {
            GUILayout.Label($"所持金 {NPCLabels.Yen(c.Player.cash)}　預金 {NPCLabels.Yen(c.Player.savings)}　信用 {c.Player.creditScore}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("100万円預ける")) Say(c.Bank.Deposit(1000000) ? "預けた" : "お金が足りない");
            if (GUILayout.Button($"銀行で100万円借りる（枠 {NPCLabels.Yen(c.Bank.BankLimit())}）")) Say(c.Bank.Borrow(Lender.Bank, 1000000, 180) != null ? "借りた" : "借りられない");
            if (c.Player.blackMarketAccess && GUILayout.Button("闇金で100万円借りる（トイチ）")) Say(c.Bank.Borrow(Lender.LoanShark, 1000000, 10) != null ? "借りてしまった……" : "借りられない");
            GUILayout.EndHorizontal();
            foreach (var loan in c.Economy.loans)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(loan.lender == Lender.Bank ? "銀行" : "闇金")} 残り {NPCLabels.Yen(loan.Balance)}　期限 {loan.dueDay}日目" + (loan.overdueDays > 0 ? $"（延滞{loan.overdueDays}日）" : ""));
                if (GUILayout.Button("全額返す", GUILayout.Width(100))) c.Bank.Repay(loan.id, loan.Balance);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("── 株 ──");
            foreach (var s in c.Economy.stocks)
            {
                var h = c.Economy.GetHolding(s.id);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{s.name} {s.Price}円　保有 {(h != null ? h.shares : 0)}株", GUILayout.Width(Screen.width * 0.5f));
                if (GUILayout.Button("100株買う")) c.Stocks.Buy(s.id, 100);
                if (h != null && GUILayout.Button("全部売る")) c.Stocks.Sell(s.id, h.shares);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("── toto ──");
            foreach (var m in c.World.State.matches.Where(x => c.Betting.IsBettable(x)).Take(8))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{m.day}日目 {(m.IsUnderground ? "【裏】" : "")}{m.homeName} vs {m.awayName}", GUILayout.Width(Screen.width * 0.45f));
                foreach (TotoPick pick in Enum.GetValues(typeof(TotoPick)))
                    if (GUILayout.Button($"{pick} x{c.Betting.Odds(m, pick):0.00}")) Say(c.Betting.PlaceBet(m.id, pick, 10000) != null ? "1万円賭けた" : "賭けられない");
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("宝くじを10枚買う（3000円）")) Say($"宝くじ {c.Betting.BuyLottery(10).Count}枚");
        }

        void DrawShop(IslandCareer c)
        {
            GUILayout.Label("── 表のお店 ──");
            foreach (var i in c.World.State.items.Where(x => !x.isUnderground))
            {
                GUILayout.BeginHorizontal();
                var e = ItemEffects.Of(i.name);
                GUILayout.Label($"{i.shopName}「{i.name}」{NPCLabels.Yen(i.price)}　{ItemLabels.Of(i.SurfaceStatus)}　{e?.description}", GUILayout.Width(Screen.width * 0.7f));
                if (i.SurfaceStatus == ItemStatus.OnSale && GUILayout.Button("買う")) Say(c.Inventory.Buy(i.id) ? "買った" : "買えない");
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(c.BlackMarket.CanEnter ? $"── 裏市場（{c.BlackMarket.CurrentLocation}）──" : "── 裏市場（場所を知らない）──");
            foreach (var i in c.BlackMarket.Items())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"「{i.name}」{NPCLabels.Yen(i.price)}　{ItemLabels.Of(i.status)}", GUILayout.Width(Screen.width * 0.7f));
                if (i.status == ItemStatus.OnSale && GUILayout.Button("買う")) Say(c.BlackMarket.Buy(i.id) ? "買った" : "買えない");
                GUILayout.EndHorizontal();
            }
            foreach (var n in c.BlackMarket.UndergroundRanking(10)) GUILayout.Label($"裏ランキング　{n.FullName}　{NPCLabels.Yen(n.marketValue)}");

            GUILayout.Label("── 持ち物 ──");
            foreach (var i in c.Player.inventory.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i.name} ×{i.count}" + (c.Player.equipped.Contains(i.name) ? "（装備中）" : ""), GUILayout.Width(Screen.width * 0.5f));
                if (GUILayout.Button("使う／装備")) Say(c.Inventory.Use(i.name).message);
                GUILayout.EndHorizontal();
            }
            foreach (var b in c.Player.matchBuffs) GUILayout.Label("次の試合：" + ItemEffects.BuffName(b));
        }

        void DrawTraining(IslandCareer c)
        {
            GUILayout.Label($"疲労 {c.Player.fatigue:0}　今日の練習 {c.Player.trainingsToday}/3");
            var partner = c.Population.Database.Get(_selected);
            GUILayout.Label(partner != null ? $"練習相手：{partner.FullName}（島民タブで選んだ人）" : "練習相手：なし（島民タブで選べる）");
            foreach (TrainingMenu m in Enum.GetValues(typeof(TrainingMenu)))
            {
                if (!GUILayout.Button(TrainingSystem.Name(m))) continue;
                var r = c.Training.Train(m, partner != null ? partner.id : -1);
                if (!r.done) { Say(r.refusal); continue; }
                Say($"{TrainingSystem.Name(m)}：能力アップ {r.statUps.Count}");
                foreach (var s in r.impressions) Say($"（{s}）");
                if (r.partnerLine != null) Say($"{partner.FullName}「{r.partnerLine}」");
            }
            if (GUILayout.Button($"能力検定を受ける（{NPCLabels.Yen(c.Exam.fee)}）"))
            {
                var r = c.Exam.TakeExam();
                Say(r == null ? "今は受けられない" : $"検定結果：{ExamGrades.Label(r.after)}");
            }
        }

        void DrawTournament(IslandCareer c)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("サッカーの神様を目指す大会")) _tournament = TournamentSystem.CreateGodCup(c);
            if (GUILayout.Button("地域対抗カップ")) _tournament = TournamentSystem.CreateRegionalCup(c);
            if (GUILayout.Button("裏トーナメント")) _tournament = TournamentSystem.CreateUndergroundTournament(c) ?? _tournament;
            if (c.World.CurrentSeason == Season.Summer && GUILayout.Button("夏のビーチサッカー大会")) _tournament = TournamentSystem.CreateBeachFestival(c) ?? _tournament;
            if (c.Player.cycle >= 2 && GUILayout.Button("島の頂上決戦")) _tournament = TournamentSystem.CreateSummitCup(c) ?? _tournament;
            if (c.Player.cycle >= 3 && GUILayout.Button("転生の謎を解く最終決戦")) _tournament = TournamentSystem.CreateReincarnationFinal(c) ?? _tournament;
            GUILayout.EndHorizontal();
            if (_tournament == null) return;
            var st = _tournament.State;
            GUILayout.Label($"{st.name}　{(st.finished ? "終了" : st.RoundName)}");
            foreach (var h in st.history)
                GUILayout.Label($"{h.round}回戦　{st.entries[h.a].name} {h.scoreA}-{h.scoreB} {st.entries[h.b].name}" + (h.penalties ? $"（PK {h.pkA}-{h.pkB}）" : ""));
            if (st.finished) GUILayout.Label("優勝：" + st.entries[st.champion].name);
            else if (GUILayout.Button(st.PlayerAlive ? "次の試合（おまかせ）" : "残りの試合を見る")) _tournament.AutoPlayRound();
        }

        void DrawMe(IslandCareer c)
        {
            var p = c.Player;
            GUILayout.Label($"{p.name}　{p.age}歳　{p.cycle}周目　Lv.{p.level}（次まで {PlayerGrowth.ExpToNext(p.level) - p.exp}）");
            GUILayout.Label($"知名度 {p.fame:0.0}　市場価値 {NPCLabels.Yen(p.marketValue)}　調子 {p.form:+0.00;-0.00}");
            GUILayout.Label($"試合 {p.matchesPlayed}　ゴール {p.goals}　アシスト {p.assists}　検定 {ExamGrades.Label(p.examGrade)}");
            string[] names = { "シュート", "パス", "ドリブル", "テクニック", "守備", "フィジカル", "スピード", "スタミナ", "メンタル", "キーパー" };
            for (int i = 0; i < NPCAbilities.StatCount; i++) GUILayout.Label($"{names[i]} {p.abilities[i]}");
            GUILayout.Label($"チーム {p.teamName}（{p.contracts.Count}人）　人気投票 {(c.Media.playerVoteRank > 0 ? c.Media.playerVoteRank + "位" : "圏外")}");
            GUILayout.Label($"謎のプレーヤーの手がかり {c.Mystery.Clues}/{MysteryPlayerQuest.CluesNeeded}" + (c.Mystery.Found ? "（正体を突き止めた）" : ""));
            if (c.Romance.State.PartnerId >= 0) GUILayout.Label("恋人：" + c.Population.Database.Get(c.Romance.State.PartnerId).FullName);
            var dbm = c.Population.Database;
            if (p.IsInjured(dbm.currentDay)) GUILayout.Label($"けが：あと{p.injuredUntilDay - dbm.currentDay + 1}日（試合はベンチから指示だけ）");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("病院に行く（勇心町）")) Say(c.Hospital.TreatProtagonist());
            if (c.Economy.loans.Exists(l => l.lender == Lender.LoanShark) && GUILayout.Button("交番に闇金を通報する")) Say(c.Police.ReportLoanShark());
            GUILayout.EndHorizontal();
            if (ReincarnationSystem.CanRetire(p) && GUILayout.Button("引退する（次の人生へ）"))
            {
                var legacy = CareerManager.Instance.RetireAndStartNextLife();
                if (legacy != null) Say($"……{legacy.playerName}の現役生活が終わった。そして、新しい人生が始まる。");
            }
        }
    }
}
