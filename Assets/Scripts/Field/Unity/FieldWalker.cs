using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>マスからマスへ歩くキャラ（主人公・島民共通）。4方向・歩きアニメつき。</summary>
    public class FieldWalker : MonoBehaviour
    {
        public float tilesPerSecond = 6f;
        public int x, y;            // マップ上のマス（y=0 が上）
        public int dir;             // 0下 1左 2右 3上
        public int npcId = -1;      // 島民なら ID
        public bool Moving => _moving;

        Sprite[] _frames;
        SpriteRenderer _sr;
        TownMap _map;
        bool _moving;
        Vector3 _target;
        float _animTime;

        public void Init(TownMap map, Sprite[] frames, int x, int y, int dir)
        {
            _map = map;
            _frames = frames;
            _sr = gameObject.AddComponent<SpriteRenderer>();
            this.x = x; this.y = y; this.dir = dir;
            transform.position = WorldPos(x, y);
            UpdateSprite(0);
        }

        public Vector3 WorldPos(int tx, int ty) => new Vector3(tx + 0.5f, _map.height - 1 - ty, 0f);

        public void Face(int d) { dir = d; UpdateSprite(0); }

        /// <summary>1マス進む。進めたら true。</summary>
        public bool TryStep(int d, System.Func<int, int, bool> blocked)
        {
            dir = d;
            if (_moving) return false;
            int nx = x + (d == 1 ? -1 : d == 2 ? 1 : 0);
            int ny = y + (d == 3 ? -1 : d == 0 ? 1 : 0);
            if (_map.IsSolid(nx, ny) || (blocked != null && blocked(nx, ny))) { UpdateSprite(0); return false; }
            x = nx; y = ny;
            _target = WorldPos(x, y);
            _moving = true;
            return true;
        }

        public void Warp(int tx, int ty, int d)
        {
            x = tx; y = ty; dir = d; _moving = false;
            transform.position = WorldPos(tx, ty);
            UpdateSprite(0);
        }

        void Update()
        {
            if (_sr != null) _sr.sortingOrder = 1000 - y; // 下にいるほど手前
            if (!_moving) return;
            transform.position = Vector3.MoveTowards(transform.position, _target, tilesPerSecond * Time.deltaTime);
            _animTime += Time.deltaTime * tilesPerSecond;
            UpdateSprite(((int)_animTime % 2) + 1);
            if ((transform.position - _target).sqrMagnitude < 0.0001f)
            {
                transform.position = _target;
                _moving = false;
                UpdateSprite(0);
            }
        }

        void UpdateSprite(int step)
        {
            if (_sr == null || _frames == null) return;
            _sr.sprite = _frames[FieldArt.FrameIndex(dir, step)];
        }
    }
}
