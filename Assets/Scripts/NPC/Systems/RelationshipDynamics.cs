using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    public enum RelationChangeKind
    {
        Reconciled,    // ライバルが和解
        MentorSplit,   // 師弟が決別（弟子が師匠を超えた）
        Married,       // 結婚
        BecameFriends, // チームメイトと仲良くなった
    }

    public struct RelationChange
    {
        public RelationChangeKind kind;
        public int a;
        public int b;
        /// <summary>主人公がもともとこの関係を知っていたか。</summary>
        public bool wasKnown;
    }

    /// <summary>
    /// 島民同士の関係は時間とともに変わる（月に1回）。
    /// ・ライバルが和解して友人になる（大人になるほど起きやすい）
    /// ・師匠を超えた弟子が、師匠と決別してライバルになる
    /// ・仲の良い友人同士が結婚する
    /// ・同じクラブの選手同士が友人になる
    /// 主人公が知っていた関係なら、相関図の線も描き換わる。
    /// </summary>
    public class RelationshipDynamics
    {
        public float reconcileChance = 0.03f;
        public float splitChance = 0.15f;
        public float marriageChance = 0.012f;
        public int newFriendshipsPerMonth = 20;

        /// <summary>結婚させない島民（主人公の恋人・デート相手など）。Career 側が差し込む。</summary>
        public Func<int, bool> excludedFromMarriage;

        readonly NPCDatabase _db;
        readonly NPCKnowledgeBook _book;
        readonly SeededRandom _rng;

        public RelationshipDynamics(NPCDatabase db, NPCKnowledgeBook book, SeededRandom rng)
        {
            _db = db;
            _book = book;
            _rng = rng;
        }

        public List<RelationChange> RunMonth()
        {
            var changes = new List<RelationChange>();
            var married = new HashSet<int>();
            foreach (var n in _db.npcs)
                foreach (var r in n.relations)
                    if (r.type == RelationType.Spouse) married.Add(n.id);

            foreach (var a in _db.npcs)
            {
                foreach (var r in a.relations.ToArray())
                {
                    if (r.targetId <= a.id) continue; // 1組につき1回
                    var b = _db.Get(r.targetId);
                    if (b == null) continue;
                    switch (r.type)
                    {
                        case RelationType.Rival:
                            float c = reconcileChance * (Math.Min(a.age, b.age) >= 30 ? 2f : 1f);
                            if (_rng.Chance(c)) changes.Add(Change(RelationChangeKind.Reconciled, a, b, RelationType.Friend, RelationType.Friend, 40));
                            break;
                        case RelationType.Friend:
                            if (a.age >= 24 && b.age >= 24 && a.age <= 50 && b.age <= 50 && r.affinity >= 70 &&
                                !married.Contains(a.id) && !married.Contains(b.id) && !a.isIncarcerated && !b.isIncarcerated &&
                                (excludedFromMarriage == null || (!excludedFromMarriage(a.id) && !excludedFromMarriage(b.id))) &&
                                _rng.Chance(marriageChance))
                            {
                                changes.Add(Change(RelationChangeKind.Married, a, b, RelationType.Spouse, RelationType.Spouse, 95));
                                married.Add(a.id); married.Add(b.id);
                            }
                            break;
                        case RelationType.Mentor:   // b は a の師匠
                        case RelationType.Pupil:    // b は a の弟子
                            var pupil = r.type == RelationType.Mentor ? a : b;
                            var mentor = r.type == RelationType.Mentor ? b : a;
                            if (pupil.Overall >= mentor.Overall + 5 && _rng.Chance(splitChance))
                                changes.Add(Change(RelationChangeKind.MentorSplit, pupil, mentor, RelationType.Rival, RelationType.Rival, -40));
                            break;
                    }
                }
            }

            // チームメイトと仲良くなる
            for (int i = 0; i < newFriendshipsPerMonth; i++)
            {
                var a = _db.npcs[_rng.Range(0, _db.Count)];
                var mates = _db.GetClubMembers(a.clubId);
                if (a.clubId < 0 || mates.Count < 2) continue;
                var b = mates[_rng.Range(0, mates.Count)];
                if (b == a || a.HasRelation(b.id) || Math.Abs(a.age - b.age) > 15) continue;
                a.relations.Add(new NPCRelation(b.id, RelationType.Friend, _rng.Range(30, 70)));
                b.relations.Add(new NPCRelation(a.id, RelationType.Friend, _rng.Range(30, 70)));
                changes.Add(new RelationChange { kind = RelationChangeKind.BecameFriends, a = a.id, b = b.id });
            }
            return changes;
        }

        RelationChange Change(RelationChangeKind kind, NPCData a, NPCData b, RelationType aToB, RelationType bToA, int affinity)
        {
            var ra = a.FindRelation(b.id);
            var rb = b.FindRelation(a.id);
            if (ra != null) { ra.type = aToB; ra.affinity = affinity; }
            if (rb != null) { rb.type = bToA; rb.affinity = affinity; }
            bool known = _book.KnowsRelation(a.id, b.id);
            if (known) _book.UpdateRelationType(a.id, b.id, aToB);
            return new RelationChange { kind = kind, a = a.id, b = b.id, wasKnown = known };
        }
    }
}
