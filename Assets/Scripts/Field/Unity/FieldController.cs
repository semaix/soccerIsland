using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.Career.UnityIntegration;
using SoccerIsland.NPC;
using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>
    /// 島を歩き回るフィールド画面（16bit 風の見下ろし型ドット絵）。
    /// ・矢印キー / WASD で1マスずつ移動。区域に入ると「今いる場所」がそこになる
    /// ・その時間にその場所にいる島民だけが歩いている
    /// ・島民の方を向いて Z / Space / Enter で話しかける（メッセージウィンドウ）
    /// ・大通りの左右の端から隣の街へ（時間が進む）
    /// ・夕方・深夜は画面の色が変わる
    /// NPCManager・WorldManager・CareerManager と同じシーンに置く。
    /// </summary>
    public class FieldController : MonoBehaviour
    {
        public int maxIslandersPerZone = 10;
        public float cameraTilesTall = 15f;

        FieldArt _art;
        TownMap _map;
        GameObject _mapObject;
        SpriteRenderer _mapRenderer;
        FieldWalker _player;
        readonly List<FieldWalker> _islanders = new List<FieldWalker>();
        readonly Queue<string> _messages = new Queue<string>();
        FanRequest _pendingFan;
        int _builtDay = -1;
        TimeOfDay _builtTime;
        Town _builtTown;
        float _wanderTimer;
        SeededRandom _rng = new SeededRandom(11);

        IslandCareer Career => CareerManager.Instance != null ? CareerManager.Instance.Career : null;

        void Start()
        {
            _art = new FieldArt();
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
            if (_map == null || _builtTown != c.Here.town) BuildTown(c, null);
            if (_builtDay != c.Population.Database.currentDay || _builtTime != c.Now) SpawnIslanders(c);

            if (_messages.Count > 0 || _pendingFan != null) { HandleMessageInput(c); FollowCamera(); return; }

            HandleMove(c);
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Talk(c);
            Wander();
            FollowCamera();
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

            if (_player == null)
            {
                var go = new GameObject("Player");
                _player = go.AddComponent<FieldWalker>();
                _player.tilesPerSecond = 7f;
                var zone = _map.Zone(c.Here.place) ?? _map.Zone(IslandPlace.Workplace);
                _player.Init(_map, _art.Character(CharacterLooks.Protagonist()), zone.entryX, zone.entryY, 0);
            }
            else
            {
                // 着いた街：来た方向と反対側の端の大通りに立つ
                var exits = _map.exits;
                MapExit entry = null;
                foreach (var e in exits) if (arrivedFrom != null && e.westEdge != arrivedFrom.westEdge) entry = e;
                if (entry == null) entry = exits[0];
                int ex = entry.westEdge ? 1 : _map.width - 2;
                _player.Init(_map, _art.Character(CharacterLooks.Protagonist()), ex, entry.rect.y, entry.westEdge ? 2 : 1);
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
                    // 決まった場所に立たせる（毎回同じ）
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

            // 端の出口：隣の街へ
            var exit = _map.ExitAt(_player.x, _player.y);
            if (exit != null && ((exit.westEdge && d == 1) || (!exit.westEdge && d == 2)))
            {
                c.MoveTo(exit.to, IslandPlace.Workplace);
                foreach (var ev in c.LastTravelEvents) Show(ev.text);
                BuildTown(c, exit);
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
                if (home == null || !home.rect.Contains(nx, ny)) { w.Face(d); continue; } // 区域から出ない
                w.TryStep(d, (x, y) => Occupied(x, y) || (x == _player.x && y == _player.y));
            }
        }

        // ------------------------------------------------------------------
        // 会話
        // ------------------------------------------------------------------

        void Talk(IslandCareer c)
        {
            int fx = _player.x + (_player.dir == 1 ? -1 : _player.dir == 2 ? 1 : 0);
            int fy = _player.y + (_player.dir == 3 ? -1 : _player.dir == 0 ? 1 : 0);
            FieldWalker target = null;
            foreach (var w in _islanders) if (w.x == fx && w.y == fy) target = w;
            if (target == null) return;
            target.Face(_player.dir == 0 ? 3 : _player.dir == 3 ? 0 : _player.dir == 1 ? 2 : 1); // こっちを向く

            var r = c.Interact(target.npcId);
            if (r.notHere) return;
            var n = c.Population.Database.Get(target.npcId);
            var known = c.Population.Knowledge.Knows(n.id, NPCInfoFlags.Name);
            string name = known ? n.FullName : "？？？";
            foreach (var line in r.conversation.lines) Show($"{name}「{line}」");
            if (r.fanRequest != null) { Show($"{name}「{r.fanRequest.line}」"); _pendingFan = r.fanRequest; }
        }

        void Show(string text) => _messages.Enqueue(text);

        void HandleMessageInput(IslandCareer c)
        {
            bool ok = Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            bool no = Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape);
            if (_messages.Count > 1 || (_messages.Count == 1 && _pendingFan == null))
            {
                if (ok) _messages.Dequeue();
                return;
            }
            if (_pendingFan != null && (ok || no))
            {
                if (_messages.Count > 0) _messages.Dequeue();
                var res = c.Fans.Respond(_pendingFan, ok);
                _pendingFan = null;
                if (res == FanResponseResult.NoSignature) Show("（サインをまだ作っていない……）");
                else Show(ok ? "（快く応えた）" : "（断った……）");
            }
        }

        void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null || _player == null) return;
            var p = _player.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float x = Mathf.Clamp(p.x, halfW, Math.Max(halfW, _map.width - halfW));
            float y = Mathf.Clamp(p.y + 0.75f, halfH, Math.Max(halfH, _map.height - halfH));
            // ドットがにじまないよう、1ピクセル単位にそろえる
            cam.transform.position = new Vector3(Mathf.Round(x * 16f) / 16f, Mathf.Round(y * 16f) / 16f, -10f);
        }

        // ------------------------------------------------------------------
        // 画面の文字（メッセージウィンドウと上の帯）
        // ------------------------------------------------------------------

        void OnGUI()
        {
            var c = Career;
            if (c == null) return;
            int scale = Mathf.Max(1, Screen.height / 360);
            var band = new GUIStyle(GUI.skin.label) { fontSize = 10 * scale };
            var z = _map != null && _player != null ? _map.ZoneAt(_player.x, _player.y) : null;
            GUI.Label(new Rect(8 * scale, 4 * scale, Screen.width, 16 * scale),
                $"{NPCLabels.Of(c.Here.town)}・{(z != null ? z.label : "道")}　{c.Population.Database.currentYear}年 {c.Population.Database.currentDay}日目 {DailySchedule.Label(c.Now)}　{NPCLabels.Yen(c.Player.cash)}", band);

            if (_messages.Count == 0) return;
            float h = 64 * scale;
            var rect = new Rect(12 * scale, Screen.height - h - 12 * scale, Screen.width - 24 * scale, h);
            DrawWindow(rect, scale);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 11 * scale, wordWrap = true };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + 10 * scale, rect.y + 8 * scale, rect.width - 20 * scale, rect.height - 16 * scale),
                      _messages.Peek() + (_pendingFan != null && _messages.Count == 1 ? "\n　▶ Z：応える　X：断る" : "　▼"), style);
        }

        static Texture2D _white;

        /// <summary>メッセージウィンドウ：濃い紺の地に白い縁取り。</summary>
        static void DrawWindow(Rect r, int scale)
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _white.SetPixels32(new[] { new Color32(255, 255, 255, 255) });
                _white.Apply();
            }
            var old = GUI.color;
            GUI.color = new Color(0.92f, 0.92f, 0.95f);
            GUI.DrawTexture(r, _white);
            GUI.color = new Color(0.08f, 0.1f, 0.25f, 0.95f);
            GUI.DrawTexture(new Rect(r.x + 2 * scale, r.y + 2 * scale, r.width - 4 * scale, r.height - 4 * scale), _white);
            GUI.color = old;
        }
    }
}
