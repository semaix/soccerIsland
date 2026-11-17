using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.Career.UnityIntegration;
using SoccerIsland.Match;
using SoccerIsland.NPC;
using SoccerIsland.NPC.UnityIntegration;
using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>
    /// 島を歩き回るフィールド画面（16bit 風の見下ろし型ドット絵）。ゲームの入口。
    ///
    ///   矢印キー / WASD … 1マスずつ移動（区域に入ると「今いる場所」がそこになる）
    ///   Z / Space / Enter … 島民に話しかける（話す・スカウト・練習・デート…）、メッセージを送る
    ///   X / Esc … メニュー（つよさ・もちもの・しりあい・でんわ・しんぶん・そうかんず・おかね・チーム…）
    ///   C … この場所でできること（お店・銀行・協会・交番・裏市場・練習・試合・病院・寝る）
    ///   大通りの左右の端 … 隣の街へ（時間が進む）
    ///
    /// NPCManager・WorldManager・CareerManager と同じシーンに置く。
    /// サインの作成画面（CreatorStudioView）を creatorStudio に入れておくと、メニューから開ける。
    /// </summary>
    public class FieldController : MonoBehaviour
    {
        public int maxIslandersPerZone = 10;
        public float cameraTilesTall = 15f;
        [Tooltip("サイン・ゴールパフォーマンスの作成画面（CreatorStudioView）")]
        public Behaviour creatorStudio;

        FieldArt _art;
        FieldSession _session;
        TownMap _map;
        GameObject _mapObject;
        SpriteRenderer _mapRenderer;
        FieldWalker _player;
        readonly List<FieldWalker> _islanders = new List<FieldWalker>();
        readonly Queue<string> _messages = new Queue<string>();
        readonly RetroMenuView _menu = new RetroMenuView();
        MatchScreen _match;
        int _builtDay = -1;
        TimeOfDay _builtTime;
        Town _builtTown;
        IslandCareer _builtFor;
        float _wanderTimer;
        readonly SeededRandom _rng = new SeededRandom(11);

        IslandCareer Career => CareerManager.Instance != null ? CareerManager.Instance.Career : null;

        void Start()
        {
            _art = new FieldArt();
            if (creatorStudio != null) creatorStudio.enabled = false;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = cameraTilesTall / 2f;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            }
        }

        void Update()
        {
            var c = Career;
            if (c == null) return;
            if (_session == null || _builtFor != c) { _session = new FieldSession(c); _builtFor = c; _map = null; }
            if (creatorStudio != null && creatorStudio.enabled) return;

            if (_match != null) { UpdateMatch(); return; }

            if (_map == null || _builtTown != c.Here.town) BuildTown(c, null);
            if (_builtDay != c.Population.Database.currentDay || _builtTime != c.Now) SpawnIslanders(c);

            if (_messages.Count > 0) { if (RetroUI.Ok || RetroUI.Cancel) _messages.Dequeue(); FollowCamera(); return; }
            if (_session.PendingFan != null)
            {
                if (RetroUI.Ok || RetroUI.Cancel) Show(_session.AnswerFan(RetroUI.Ok));
                return;
            }
            if (_menu.IsOpen)
            {
                var r = _menu.HandleInput();
                if (r != null) Apply(c, r);
                FollowCamera();
                return;
            }

            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)) { _menu.Push(_session.MainMenu()); return; }
            if (Input.GetKeyDown(KeyCode.C)) { _menu.Push(_session.PlaceMenu()); return; }
            HandleMove(c);
            if (RetroUI.Ok) TalkToFacing();
            Wander();
            FollowCamera();
        }

        // ------------------------------------------------------------------
        // メニューの結果
        // ------------------------------------------------------------------

        void Apply(IslandCareer c, MenuResult r)
        {
            foreach (var m in r.messages)
            {
                if (m == "SAVE") { NPCManager.Instance?.Save(); Show("冒険の記録をつけた。"); continue; }
                if (m == "RETIRE") { Retire(); continue; }
                Show(m);
            }
            if (r.back) _menu.Pop();
            if (r.close) _menu.Clear();
            if (r.open != null) _menu.Push(r.open);
            if (r.openCreator && creatorStudio != null) creatorStudio.enabled = true;
            if (r.match != null) StartMatch(r.match);
            if (r.timeChanged) _builtDay = -1; // 島民を並べ直す
        }

        void Retire()
        {
            var legacy = CareerManager.Instance.RetireAndStartNextLife();
            if (legacy == null) { Show("まだ引退できない。"); return; }
            Show($"……{legacy.playerName}の現役生活が終わった。");
            Show("そして、新しい人生が始まる。");
            _session = null;
            _map = null;
            if (_player != null) { Destroy(_player.gameObject); _player = null; }
        }

        // ------------------------------------------------------------------
        // 試合
        // ------------------------------------------------------------------

        void StartMatch(MatchSession session)
        {
            _menu.Clear();
            SetFieldVisible(false);
            _match = new MatchScreen(session, _art);
        }

        void UpdateMatch()
        {
            _match.Update();
            if (!_match.Finished) return;
            foreach (var line in _session.FinishMatch(_match.Session)) Show(line);
            _match.Close();
            _match = null;
            SetFieldVisible(true);
            _builtDay = -1;
        }

        void SetFieldVisible(bool visible)
        {
            if (_mapRenderer != null) _mapRenderer.enabled = visible;
            foreach (var w in _islanders) { var sr = w.GetComponent<SpriteRenderer>(); if (sr != null) sr.enabled = visible; }
            if (_player != null) { var sr = _player.GetComponent<SpriteRenderer>(); if (sr != null) sr.enabled = visible; }
        }

        // ------------------------------------------------------------------
        // 街を作る
        // ------------------------------------------------------------------

        void BuildTown(IslandCareer c, MapExit arrivedFrom)
        {
            var db = c.Population.Database;
            _map = TownMapGenerator.Generate(c.Here.town, c.Player.homeTown, db.seed);
            _builtTown = c.Here.town;

            if (_mapObject == null)
            {
                _mapObject = new GameObject("TownMap");
                _mapRenderer = _mapObject.AddComponent<SpriteRenderer>();
                _mapRenderer.sortingOrder = -100;
            }
            _mapRenderer.sprite = _art.BakeMap(_map);

            if (_player != null) Destroy(_player.gameObject);
            var go = new GameObject("Player");
            _player = go.AddComponent<FieldWalker>();
            _player.tilesPerSecond = 7f;
            if (arrivedFrom != null)
            {
                // 来た方向と反対側の端の大通りに立つ
                MapExit entry = _map.exits[0];
                foreach (var e in _map.exits) if (e.westEdge != arrivedFrom.westEdge) entry = e;
                _player.Init(_map, _art.Character(CharacterLooks.Protagonist()), entry.westEdge ? 1 : _map.width - 2, entry.rect.y, entry.westEdge ? 2 : 1);
            }
            else
            {
                var zone = _map.Zone(c.Here.place) ?? _map.Zone(IslandPlace.Workplace);
                _player.Init(_map, _art.Character(CharacterLooks.Protagonist()), zone.entryX, zone.entryY, 0);
            }
            UpdatePlace(c);
            SpawnIslanders(c);
        }

        void SpawnIslanders(IslandCareer c)
        {
            foreach (var w in _islanders) if (w != null) Destroy(w.gameObject);
            _islanders.Clear();
            var db = c.Population.Database;
            _builtDay = db.currentDay;
            _builtTime = c.Now;
            // 朝になると家で目を覚ます（マップも自宅の街に）
            if (_map.town != c.Here.town) { BuildTown(c, null); return; }
            var taken = new HashSet<int> { _player.y * _map.width + _player.x };
            var bmTown = (int)c.BlackMarket.CurrentTown;

            foreach (var zone in _map.zones)
            {
                var cells = _map.WalkableCells(zone);
                if (cells.Count == 0) continue;
                int count = 0;
                foreach (var n in db.npcs)
                {
                    if (count >= maxIslandersPerZone) break;
                    var loc = c.Schedule.Locate(n, db.currentDay, c.Now, bmTown);
                    if (loc.town != _map.town || loc.place != zone.place) continue;
                    int start = (int)((uint)(n.id * 7919 + db.currentDay) % (uint)cells.Count);
                    int cell = -1;
                    for (int k = 0; k < cells.Count; k++)
                    {
                        int candidate = cells[(start + k) % cells.Count];
                        if (!taken.Contains(candidate)) { cell = candidate; break; }
                    }
                    if (cell < 0) continue;
                    taken.Add(cell);
                    var go = new GameObject(n.FullName);
                    var w = go.AddComponent<FieldWalker>();
                    w.tilesPerSecond = 3f;
                    w.npcId = n.id;
                    w.Init(_map, _art.Character(CharacterLooks.For(n)), cell % _map.width, cell / _map.width, n.id % 4);
                    _islanders.Add(w);
                    count++;
                }
            }
            ApplyTint(c.Now);
        }

        void ApplyTint(TimeOfDay t)
        {
            var tint = t == TimeOfDay.Evening ? new Color(1f, 0.85f, 0.7f) : t == TimeOfDay.Night ? new Color(0.45f, 0.5f, 0.8f) : Color.white;
            if (_mapRenderer != null) _mapRenderer.color = tint;
            foreach (var w in _islanders) { var sr = w.GetComponent<SpriteRenderer>(); if (sr != null) sr.color = tint; }
            if (_player != null) { var sr = _player.GetComponent<SpriteRenderer>(); if (sr != null) sr.color = tint; }
        }

        // ------------------------------------------------------------------
        // 移動
        // ------------------------------------------------------------------

        bool Occupied(int x, int y)
        {
            foreach (var w in _islanders) if (w.x == x && w.y == y) return true;
            return false;
        }

        void HandleMove(IslandCareer c)
        {
            if (_player.Moving) return;
            int d = -1;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) d = 0;
            else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) d = 1;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) d = 2;
            else if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) d = 3;
            if (d < 0) return;

            var exit = _map.ExitAt(_player.x, _player.y);
            if (exit != null && ((exit.westEdge && d == 1) || (!exit.westEdge && d == 2)))
            {
                var reports = c.MoveTo(exit.to, IslandPlace.Workplace);
                Show($"{NPCLabels.Of(exit.to)}へ向かった。");
                foreach (var ev in c.LastTravelEvents) Show(ev.text);
                foreach (var line in _session.Digest(reports)) Show(line);
                if (c.Here.town == exit.to) BuildTown(c, exit);
                return;
            }

            if (_player.TryStep(d, Occupied)) UpdatePlace(c);
        }

        /// <summary>今いるマスの区域を「今いる場所」にする。</summary>
        void UpdatePlace(IslandCareer c)
        {
            var z = _map.ZoneAt(_player.x, _player.y);
            c.Player.currentTown = _map.town;
            c.Player.currentPlace = (int)(z != null ? z.place : IslandPlace.Workplace);
        }

        void Wander()
        {
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer > 0f) return;
            _wanderTimer = 0.8f;
            foreach (var w in _islanders)
            {
                if (_rng.Value > 0.25f || w.Moving) continue;
                int d = _rng.Range(0, 4);
                int nx = w.x + (d == 1 ? -1 : d == 2 ? 1 : 0), ny = w.y + (d == 3 ? -1 : d == 0 ? 1 : 0);
                var home = _map.ZoneAt(w.x, w.y);
                if (home == null || !home.rect.Contains(nx, ny)) { w.Face(d); continue; }
                w.TryStep(d, (x, y) => Occupied(x, y) || (x == _player.x && y == _player.y));
            }
        }

        void TalkToFacing()
        {
            int fx = _player.x + (_player.dir == 1 ? -1 : _player.dir == 2 ? 1 : 0);
            int fy = _player.y + (_player.dir == 3 ? -1 : _player.dir == 0 ? 1 : 0);
            foreach (var w in _islanders)
            {
                if (w.x != fx || w.y != fy) continue;
                w.Face(_player.dir == 0 ? 3 : _player.dir == 3 ? 0 : _player.dir == 1 ? 2 : 1); // こっちを向く
                _menu.Push(_session.TalkMenu(w.npcId));
                return;
            }
        }

        void Show(string text) { if (!string.IsNullOrEmpty(text)) _messages.Enqueue(text); }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null || _player == null || _map == null) return;
            var p = _player.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float x = Mathf.Clamp(p.x, halfW, Math.Max(halfW, _map.width - halfW));
            float y = Mathf.Clamp(p.y + 0.75f, halfH, Math.Max(halfH, _map.height - halfH));
            cam.transform.position = new Vector3(Mathf.Round(x * 16f) / 16f, Mathf.Round(y * 16f) / 16f, -10f);
        }

        // ------------------------------------------------------------------
        // 画面の文字
        // ------------------------------------------------------------------

        void OnGUI()
        {
            var c = Career;
            if (c == null || (creatorStudio != null && creatorStudio.enabled)) return;
            if (_match != null) { _match.OnGUI(); DrawMessages(); return; }
            int s = RetroUI.Scale;
            var z = _map != null && _player != null ? _map.ZoneAt(_player.x, _player.y) : null;
            var band = new Rect(8 * s, 4 * s, Screen.width - 16 * s, 18 * s);
            RetroUI.Window(band);
            GUI.Label(new Rect(band.x + 8 * s, band.y + 3 * s, band.width, 14 * s),
                $"{NPCLabels.Of(c.Here.town)}・{(z != null ? z.label : "道")}　{c.Population.Database.currentDay}日目 {DailySchedule.Label(c.Now)}（{SoccerIsland.World.WeatherSystem.Label(c.World.TodaysWeather)}）　{NPCLabels.Yen(c.Player.cash)}",
                RetroUI.Text(9));
            if (!_menu.IsOpen && _messages.Count == 0)
                GUI.Label(new Rect(8 * s, Screen.height - 18 * s, Screen.width, 14 * s), "Z：話す　X：メニュー　C：この場所でできること", RetroUI.Dim());
            _menu.Draw();
            DrawMessages();
        }

        void DrawMessages()
        {
            if (_messages.Count == 0 && (_session == null || _session.PendingFan == null)) return;
            int s = RetroUI.Scale;
            float h = 64 * s;
            var rect = new Rect(12 * s, Screen.height - h - 12 * s, Screen.width - 24 * s, h);
            RetroUI.Window(rect);
            string text = _messages.Count > 0 ? _messages.Peek() + "　▼" : "Z：応える　X：断る";
            GUI.Label(new Rect(rect.x + 10 * s, rect.y + 8 * s, rect.width - 20 * s, rect.height - 16 * s), text, RetroUI.Text(11));
        }
    }
}
