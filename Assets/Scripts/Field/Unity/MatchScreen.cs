using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.NPC;
using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>
    /// 試合画面（キャプテン翼式のコマンドRPG）。
    ///   ミーティング：陣形・作戦・話すこと → キックオフ
    ///   試合：ドット絵のコートの上で、ボールを持つ選手と守る選手が向き合う。実況が1行ずつ流れ、
    ///         必殺技やゴールはカットインで大きく出る。自分の番が来たらコマンドを選ぶ
    ///   C：コーチング　A：おまかせ（最後まで自動）
    /// </summary>
    public class MatchScreen
    {
        public bool Finished { get; private set; }
        public MatchSession Session { get; }

        readonly FieldArt _art;
        readonly RetroMenuView _menu = new RetroMenuView { visibleRows = 9 };
        readonly GameObject _root;
        readonly Dictionary<MatchPlayer, Sprite[]> _sprites = new Dictionary<MatchPlayer, Sprite[]>();
        SpriteRenderer _attacker, _defender, _cutIn;
        int _shownLog;
        float _logTimer;
        readonly List<string> _ticker = new List<string>();
        MatchEvent _cutInEvent;
        float _cutInTimer;
        MatchPrompt _prompt;
        bool _promptMenuOpen;
        static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);
        const int PitchW = 25, PitchH = 13;

        public MatchScreen(MatchSession session, FieldArt art)
        {
            Session = session;
            _art = art;
            _root = new GameObject("MatchScreen");
            var pitch = new GameObject("Pitch");
            pitch.transform.SetParent(_root.transform);
            pitch.transform.position = Origin;
            var sr = pitch.AddComponent<SpriteRenderer>();
            sr.sprite = _art.BakeMap(TownMapGenerator.PitchMap());
            sr.sortingOrder = -50;
            _attacker = MakeRenderer("Attacker", 10);
            _defender = MakeRenderer("Defender", 9);
            _cutIn = MakeRenderer("CutIn", 50);
            _cutIn.transform.localScale = new Vector3(4f, 4f, 1f);
            _menu.Push(MeetingMenu());
        }

        SpriteRenderer MakeRenderer(string name, int order)
        {
            var go = new GameObject(name);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }

        public void Close()
        {
            foreach (var r in new[] { _attacker, _defender, _cutIn }) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            UnityEngine.Object.Destroy(_root);
        }

        MatchTeam MyTeam => Session.MyTeam;
        MatchEngine E => Session.Engine;

        // ------------------------------------------------------------------
        // ミーティング
        // ------------------------------------------------------------------

        MenuPage MeetingMenu() => MenuPage.List($"ミーティング　{Session.Setup.home.name} vs {Session.Setup.away.name}", () =>
        {
            var list = new List<MenuItem>();
            if (Session.Event != null) list.Add(new MenuItem("【イベント】" + Session.Event.Title, () => new MenuResult(), enabled: false));
            list.Add(new MenuItem("チームの雰囲気を見る", () => MenuResult.Open(Chemistry())));
            if (MyTeam != null && MyTeam.players.Count >= 11) list.Add(new MenuItem($"陣形（{FormationSystem.Name(MyTeam.formation)}）", () => MenuResult.Open(Formations())));
            if (MyTeam != null) list.Add(new MenuItem($"作戦（{FormationSystem.Name(MyTeam.tactic)}）", () => MenuResult.Open(Tactics())));
            if (MyTeam != null && Session.MeetingResult == null) list.Add(new MenuItem("みんなに話す", () => MenuResult.Open(Topics())));
            list.Add(new MenuItem("キックオフ！", () => { Session.Kickoff(); return new MenuResult { close = true }; }));
            list.Add(new MenuItem("おまかせで試合を終える", () => { Session.AutoPlay(); return new MenuResult { close = true }; }));
            return list;
        });

        MenuPage Chemistry()
        {
            var c = Session.Diagnose();
            var t = new List<string> { $"雰囲気 {c.score:0}" };
            t.AddRange(c.reasons);
            if (MyTeam != null) foreach (var p in MyTeam.players) t.Add($"{p.name}（{p.position}）{NPCLabels.Of(p.soccerPersonality)}");
            return MenuPage.Text("チームの雰囲気", t);
        }

        MenuPage Formations() => MenuPage.List("陣形", () =>
        {
            var list = new List<MenuItem>();
            foreach (Formation f in Enum.GetValues(typeof(Formation)))
            {
                var ff = f;
                list.Add(new MenuItem(FormationSystem.Name(f), () => { Session.SetFormation(ff); return new MenuResult { back = true }; }));
            }
            return list;
        });

        MenuPage Tactics() => MenuPage.List("作戦", () =>
        {
            var list = new List<MenuItem>();
            foreach (TeamTactic t in Enum.GetValues(typeof(TeamTactic)))
            {
                var tt = t;
                list.Add(new MenuItem(FormationSystem.Name(t), () =>
                {
                    var res = new MenuResult { back = true };
                    foreach (var r in Session.SetTactic(tt)) res.messages.Add($"{r.player.name}「{r.line}」");
                    return res;
                }));
            }
            return list;
        });

        MenuPage Topics() => MenuPage.List("何を話す？", () =>
        {
            var list = new List<MenuItem>();
            foreach (MeetingTopic t in Enum.GetValues(typeof(MeetingTopic)))
            {
                if (t == MeetingTopic.TrustAce || t == MeetingTopic.BonusPromise) continue;
                var tt = t;
                list.Add(new MenuItem(MatchText.Topic(t), () =>
                {
                    var r = Session.HoldMeeting(tt);
                    var res = new MenuResult { back = true };
                    if (r == null) return res;
                    foreach (var x in r.reactions) res.messages.Add($"{x.player.name}「{x.line}」");
                    foreach (var a in r.arguments)
                    {
                        res.messages.Add($"口論！ {a.a.name}「{a.lineA}」");
                        res.messages.Add($"{a.b.name}「{a.lineB}」" + (a.resolvedByMoodMaker ? "　……ムードメーカーがその場を収めた。" : ""));
                    }
                    return res;
                }));
            }
            return list;
        });

        // ------------------------------------------------------------------
        // 進行
        // ------------------------------------------------------------------

        readonly Queue<string> _messages = new Queue<string>();

        public void Update()
        {
            if (Finished) return;
            FollowCamera();

            if (_messages.Count > 0) { if (RetroUI.Ok || RetroUI.Cancel) _messages.Dequeue(); return; }

            if (_menu.IsOpen)
            {
                var r = _menu.HandleInput();
                if (r != null) Apply(r);
                return;
            }

            if (Session.Current == MatchSession.Phase.Meeting) { _menu.Push(MeetingMenu()); return; }

            // 実況を1行ずつ流す
            if (_cutInTimer > 0f) { _cutInTimer -= Time.deltaTime; if (_cutInTimer <= 0f) _cutIn.sprite = null; return; }
            if (E != null && _shownLog < E.Log.Count)
            {
                _logTimer -= Time.deltaTime;
                if (_logTimer > 0f && !RetroUI.Ok) return;
                var ev = E.Log[_shownLog++];
                ShowEvent(ev);
                _logTimer = ev.cutIn ? 1.2f : 0.55f;
                return;
            }

            if (Session.Current == MatchSession.Phase.Finished) { Finished = true; return; }

            if (Input.GetKeyDown(KeyCode.A)) { Session.AutoPlay(); return; }
            if (Input.GetKeyDown(KeyCode.C) && E != null && E.CanCoach) { _menu.Push(CoachTargets()); return; }

            if (_prompt == null)
            {
                _prompt = Session.Advance();
                if (_prompt != null) { ShowDuel(_prompt.actor, _prompt.opponent); _menu.Push(CommandMenu(_prompt)); }
            }
        }

        void Apply(MenuResult r)
        {
            foreach (var m in r.messages) _messages.Enqueue(m);
            if (r.open != null) _menu.Push(r.open);
            if (r.back) _menu.Pop();
            if (r.close) _menu.Clear();
        }

        MenuPage CommandMenu(MatchPrompt p) => MenuPage.List($"{MatchText.Prompt(p.kind)}：{p.actor.name}　ガッツ {p.guts}/{p.maxGuts}", () =>
        {
            var list = new List<MenuItem>();
            foreach (var c in p.options)
            {
                var cmd = c;
                list.Add(new MenuItem(MatchText.Command(c), () =>
                {
                    if (MatchText.NeedsTarget(cmd) && p.passTargets.Count > 0) return MenuResult.Open(TargetMenu(p, cmd));
                    return Choose(cmd, null);
                }, $"消費ガッツ {MatchEngine.GutsCost(c)}　{p.situation}"));
            }
            return list;
        });

        MenuPage TargetMenu(MatchPrompt p, MatchCommand cmd) => MenuPage.List("誰に？", () =>
        {
            var list = new List<MenuItem>();
            foreach (var t in p.passTargets)
            {
                var tt = t;
                list.Add(new MenuItem($"{t.name}（{t.position}）", () => Choose(cmd, tt)));
            }
            return list;
        });

        MenuResult Choose(MatchCommand c, MatchPlayer target)
        {
            Session.Choose(c, target);
            _prompt = null;
            return new MenuResult { close = true };
        }

        MenuPage CoachTargets() => MenuPage.List("誰に指示する？", () =>
        {
            var list = new List<MenuItem> { new MenuItem("チーム全員", () => MenuResult.Open(CoachCommands(null))) };
            foreach (var p in MyTeam.Active)
            {
                if (p.isProtagonist) continue;
                var pp = p;
                list.Add(new MenuItem(p.name + (p.demand.HasValue ? "（！）" : ""), () => MenuResult.Open(CoachCommands(pp)), p.demandLine));
            }
            return list;
        });

        MenuPage CoachCommands(MatchPlayer target) => MenuPage.List("指示", () =>
        {
            var list = new List<MenuItem>();
            foreach (CoachingCommand c in Enum.GetValues(typeof(CoachingCommand)))
            {
                var cc = c;
                list.Add(new MenuItem(MatchText.Coaching(c), () =>
                {
                    var r = Session.Coach(cc, target);
                    var res = new MenuResult { close = true };
                    res.messages.Add(r.notAllowed ? "今は指示を出せない" : target != null ? $"{target.name}「{r.line}」" : $"主人公「{r.line}」");
                    return res;
                }));
            }
            return list;
        });

        // ------------------------------------------------------------------
        // 絵
        // ------------------------------------------------------------------

        Sprite[] SpritesOf(MatchPlayer p, bool home)
        {
            if (_sprites.TryGetValue(p, out var s)) return s;
            var db = Session.Career.Population.Database;
            var npc = p.npcId >= 0 ? db.Get(p.npcId) : null;
            var look = p.isProtagonist ? CharacterLooks.Protagonist() : npc != null ? CharacterLooks.For(npc) : CharacterLooks.Protagonist();
            // ユニフォーム：ホームは青、アウェイは赤。キーパーは黄色
            var kit = p.position == Position.GK ? new Rgb(230, 200, 50) : home ? new Rgb(40, 110, 220) : new Rgb(210, 50, 50);
            var uni = new CharacterLook { skin = look.skin, hair = look.hair, outfit = kit, accent = new Rgb(250, 250, 250), pants = new Rgb(250, 250, 250), shoes = new Rgb(30, 30, 30) };
            foreach (var layer in look.layers) if (layer.StartsWith("char_hair") || layer == "char_body") uni.layers.Add(layer);
            s = _art.Character(uni);
            _sprites[p] = s;
            return s;
        }

        bool IsHome(MatchPlayer p) => Session.Setup.home.players.Contains(p);

        Vector3 BallPos()
        {
            if (E == null) return Origin + new Vector3(PitchW / 2f, PitchH / 2f, 0f);
            // 攻撃方向：ホームは右へ、アウェイは左へ
            bool home = E.Possession == Side.Home;
            float zx = 2f + E.Zone * 3.4f;
            float x = home ? zx : PitchW - zx;
            float y = 2.5f + (2 - E.Lane) * 3.5f;
            return Origin + new Vector3(x, y, 0f);
        }

        /// <summary>ボールを持つ側をボールの位置に、守る側をその正面に立たせる。</summary>
        void ShowDuel(MatchPlayer a, MatchPlayer b)
        {
            var pos = BallPos();
            bool homeAttacks = E == null || E.Possession == Side.Home;
            MatchPlayer att = a, def = b;
            if (E != null && a != null && b != null && !E.TeamOf(E.Possession).players.Contains(a)) { att = b; def = a; }
            SetSprite(_attacker, att, pos, homeAttacks ? 2 : 1);
            if (def != null) SetSprite(_defender, def, pos + new Vector3(homeAttacks ? 1.4f : -1.4f, 0f, 0f), homeAttacks ? 1 : 2);
            else _defender.sprite = null;
        }

        void SetSprite(SpriteRenderer r, MatchPlayer p, Vector3 pos, int dir)
        {
            if (p == null) { r.sprite = null; return; }
            r.sprite = SpritesOf(p, IsHome(p))[FieldArt.FrameIndex(dir, 0)];
            r.transform.position = pos;
        }

        void ShowEvent(MatchEvent ev)
        {
            _ticker.Add($"{ev.minute,2}' {ev.text}");
            if (_ticker.Count > 5) _ticker.RemoveAt(0);
            if (ev.actor != null) ShowDuel(ev.actor, ev.other);
            if (ev.cutIn && ev.actor != null)
            {
                _cutInEvent = ev;
                _cutInTimer = 1.4f;
                _cutIn.sprite = SpritesOf(ev.actor, IsHome(ev.actor))[FieldArt.FrameIndex(0, 0)];
                _cutIn.transform.position = Origin + new Vector3(5f, 4f, 0f);
            }
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(Origin.x + PitchW / 2f, PitchH / 2f, -10f);
        }

        public void OnGUI()
        {
            int s = RetroUI.Scale;
            if (E != null || Session.Current != MatchSession.Phase.Meeting)
            {
                var top = new Rect(12 * s, 6 * s, Screen.width - 24 * s, 22 * s);
                RetroUI.Window(top);
                var eng = E;
                string score = eng != null ? $"{eng.Home.name} {eng.Home.score} - {eng.Away.score} {eng.Away.name}　{eng.MinuteInt}分" : "";
                if (eng != null && eng.ProtagonistTeam != null) score += $"　評価 {eng.Trust:0}" + (eng.FixingActive ? $"　疑い {eng.Suspicion:0}" : "");
                GUI.Label(new Rect(top.x + 8 * s, top.y + 4 * s, top.width, 16 * s), score, RetroUI.Text(10, true));
            }

            // 実況
            var log = new Rect(12 * s, Screen.height - 86 * s, Screen.width * 0.55f, 74 * s);
            RetroUI.Window(log);
            for (int i = 0; i < _ticker.Count; i++)
                GUI.Label(new Rect(log.x + 8 * s, log.y + 6 * s + i * 13 * s, log.width - 16 * s, 14 * s), _ticker[i], RetroUI.Text(9));

            // カットイン
            if (_cutInTimer > 0f && _cutInEvent != null)
            {
                var band = new Rect(0, Screen.height * 0.32f, Screen.width, 46 * s);
                RetroUI.Fill(band, new Color(0f, 0f, 0f, 0.7f));
                string title = !string.IsNullOrEmpty(_cutInEvent.moveName) ? $"{_cutInEvent.actor.name}「{_cutInEvent.shout}」　{_cutInEvent.moveName}！" : _cutInEvent.text;
                GUI.Label(new Rect(band.x + Screen.width * 0.3f, band.y + 10 * s, Screen.width * 0.7f, 30 * s), title, RetroUI.Text(14, true));
            }

            if (Session.Current == MatchSession.Phase.Playing && !_menu.IsOpen && _messages.Count == 0)
                GUI.Label(new Rect(Screen.width * 0.6f, Screen.height - 20 * s, Screen.width * 0.4f, 16 * s), "Z：送る　C：コーチング　A：おまかせ", RetroUI.Dim());

            _menu.Draw();

            if (_messages.Count > 0)
            {
                var r = new Rect(12 * s, Screen.height * 0.45f, Screen.width - 24 * s, 50 * s);
                RetroUI.Window(r);
                GUI.Label(new Rect(r.x + 10 * s, r.y + 8 * s, r.width - 20 * s, r.height - 16 * s), _messages.Peek() + "　▼", RetroUI.Text(11));
            }
        }
    }
}
