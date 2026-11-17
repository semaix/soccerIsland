using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.Career.UnityIntegration;
using UnityEngine;

namespace SoccerIsland.Match.UnityIntegration
{
    /// <summary>
    /// 自作サインとゴールパフォーマンスを作る仮UI（OnGUI）。
    /// サインはマウス（スマホなら指）でなぞって描く。描いたデータは端末内のセーブにだけ保存する。
    /// </summary>
    public class CreatorStudioView : MonoBehaviour
    {
        enum Mode { Signature, Performance }

        Mode _mode;
        readonly SignatureDesign _draft = new SignatureDesign();
        SignatureStroke _current;
        Texture2D _preview;
        bool _dirty = true;
        string _perfName = "マイパフォーマンス";
        readonly List<PerformanceMove> _moves = new List<PerformanceMove>();
        string _message = "";

        IslandCareer Career => CareerManager.Instance != null ? CareerManager.Instance.Career : null;

        void OnGUI()
        {
            var c = Career;
            if (c == null) return;
            GUILayout.BeginArea(new Rect(10, 10, Math.Min(Screen.width - 20, 720), Screen.height - 20));
            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "サインを作る", "ゴールパフォーマンスを作る" });
            if (_mode == Mode.Signature) DrawSignature(c); else DrawPerformance(c);
            GUILayout.Label(_message);
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------
        // サイン
        // ------------------------------------------------------------------

        void DrawSignature(IslandCareer c)
        {
            GUILayout.Label("枠の中をなぞってサインを書こう（12画まで）");
            var rect = GUILayoutUtility.GetRect(680, 260);
            GUI.Box(rect, "");
            HandleDrawing(rect);

            if (_dirty)
            {
                var all = new SignatureDesign();
                all.strokes.AddRange(_draft.strokes);
                if (_current != null && _current.Count >= 2) all.strokes.Add(_current);
                _preview = SignatureTexture.Render(all, 340, 130, new Color(0.1f, 0.1f, 0.2f), 2);
                _dirty = false;
            }
            if (_preview != null) GUI.DrawTexture(rect, _preview);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1画もどす") && _draft.strokes.Count > 0) { _draft.strokes.RemoveAt(_draft.strokes.Count - 1); _dirty = true; }
            if (GUILayout.Button("全部消す")) { _draft.strokes.Clear(); _dirty = true; }
            if (GUILayout.Button("このサインにする"))
            {
                string err = _draft.Validate();
                if (err != null) _message = err;
                else
                {
                    var saved = new SignatureDesign();
                    saved.strokes.AddRange(_draft.strokes);
                    c.Player.signature = saved;
                    _message = "サインを登録した！ ファンにサインを頼まれたら書けるようになった。";
                }
            }
            GUILayout.EndHorizontal();
        }

        void HandleDrawing(Rect rect)
        {
            var e = Event.current;
            if (e == null) return;
            Vector2 p = e.mousePosition;
            bool inside = rect.Contains(p);
            float nx = (p.x - rect.x) / rect.width;
            float ny = 1f - (p.y - rect.y) / rect.height;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!inside || _draft.strokes.Count >= SignatureDesign.MaxStrokes) return;
                    _current = new SignatureStroke();
                    _current.Add(nx, ny);
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (_current == null) return;
                    if (_current.Count < SignatureDesign.MaxPointsPerStroke * 4) _current.Add(Clamp01(nx), Clamp01(ny));
                    _dirty = true;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (_current == null) return;
                    var simple = SignatureDesign.Simplify(_current);
                    if (simple.Count >= 2) _draft.strokes.Add(simple);
                    _current = null;
                    _dirty = true;
                    e.Use();
                    break;
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ------------------------------------------------------------------
        // ゴールパフォーマンス
        // ------------------------------------------------------------------

        void DrawPerformance(IslandCareer c)
        {
            GUILayout.Label("名前");
            _perfName = GUILayout.TextField(_perfName);
            GUILayout.Label($"動き（{_moves.Count}/{GoalPerformanceDesign.MaxMoves}）：" +
                            (_moves.Count == 0 ? "なし" : string.Join(" → ", _moves.ConvertAll(m => MatchEngine.PerformanceMoveName(m)))));

            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (PerformanceMove m in Enum.GetValues(typeof(PerformanceMove)))
            {
                if (GUILayout.Button(MatchEngine.PerformanceMoveName(m)) && _moves.Count < GoalPerformanceDesign.MaxMoves) _moves.Add(m);
                if (++col % 5 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();

            var draft = new GoalPerformanceDesign { name = _perfName };
            draft.moves.AddRange(_moves);
            GUILayout.Label(draft.IsValid ? $"派手さ {draft.Score():0.0} / 10" : draft.Validate());
            if (draft.RisksYellowCard) GUILayout.Label("※ ユニフォームを脱ぐとイエローカード！");
            if (draft.IsProvocative) GUILayout.Label("※ 挑発的なパフォーマンスは相手を怒らせ、ゴシップにもなる");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1つもどす") && _moves.Count > 0) _moves.RemoveAt(_moves.Count - 1);
            if (GUILayout.Button("保存して試合で使う") && draft.IsValid)
            {
                c.Player.goalPerformances.Add(draft);
                c.Player.selectedPerformance = c.Player.goalPerformances.Count - 1;
                _message = $"「{draft.name}」を保存した。次の試合でゴールを決めたら披露する！";
                _moves.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("── 保存したパフォーマンス ──");
            for (int i = 0; i < c.Player.goalPerformances.Count; i++)
            {
                var p = c.Player.goalPerformances[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(i == c.Player.selectedPerformance ? "★" : "　")}{p.name}　派手さ {p.Score():0.0}　使った回数 {p.timesUsed}");
                if (GUILayout.Button("これを使う", GUILayout.Width(100))) c.Player.selectedPerformance = i;
                GUILayout.EndHorizontal();
            }
        }
    }
}
