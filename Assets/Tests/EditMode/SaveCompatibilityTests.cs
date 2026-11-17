using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    /// <summary>
    /// セーブデータが Unity の JsonUtility で保存できる形になっているかの検査。
    /// JsonUtility は Dictionary・Nullable・インターフェース・入れ子の List などを黙って捨てるので、
    /// ここで型を調べて、保存されないフィールドがないことを確かめる。
    /// </summary>
    public class SaveCompatibilityTests
    {
        static readonly Type[] Roots =
        {
            typeof(IslandSaveData), typeof(WorldState), typeof(CareerSaveData), typeof(TournamentState),
        };

        [Test]
        public void SaveDataUsesOnlyJsonUtilityFriendlyTypes()
        {
            var problems = new List<string>();
            var seen = new HashSet<Type>();
            foreach (var t in Roots) Check(t, t.Name, problems, seen, 0);
            Assert.IsTrue(problems.Count == 0, "保存できないフィールド:\n" + string.Join("\n", problems));
        }

        static void Check(Type t, string path, List<string> problems, HashSet<Type> seen, int depth)
        {
            if (depth > 9) { problems.Add(path + "：入れ子が深すぎる（JsonUtility は10段まで）"); return; }
            if (!seen.Add(t)) return;
            if (!t.IsDefined(typeof(SerializableAttribute), false)) problems.Add(path + "：[Serializable] がない (" + t.Name + ")");

            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                CheckFieldType(f.FieldType, path + "." + f.Name, problems, seen, depth + 1, false);
            }
        }

        static void CheckFieldType(Type ft, string path, List<string> problems, HashSet<Type> seen, int depth, bool insideList)
        {
            if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string)) return;
            if (Nullable.GetUnderlyingType(ft) != null) { problems.Add(path + "：Nullable は保存されない"); return; }
            if (ft.IsArray)
            {
                if (ft.GetArrayRank() > 1) { problems.Add(path + "：多次元配列は保存されない"); return; }
                if (insideList) { problems.Add(path + "：入れ子のリスト・配列は保存されない"); return; }
                CheckFieldType(ft.GetElementType(), path + "[]", problems, seen, depth, true);
                return;
            }
            if (ft.IsGenericType)
            {
                var def = ft.GetGenericTypeDefinition();
                if (def == typeof(List<>))
                {
                    if (insideList) { problems.Add(path + "：入れ子のリストは保存されない"); return; }
                    CheckFieldType(ft.GetGenericArguments()[0], path + "[]", problems, seen, depth, true);
                    return;
                }
                problems.Add(path + "：" + def.Name + " は保存されない");
                return;
            }
            if (ft.IsInterface || ft.IsAbstract) { problems.Add(path + "：インターフェース・抽象型は保存されない"); return; }
            if (ft == typeof(object)) { problems.Add(path + "：object は保存されない"); return; }
            Check(ft, path, problems, seen, depth);
        }

        [Serializable]
        class BadSave
        {
            public Dictionary<int, int> dict;
            public int? maybe;
            public List<List<int>> nested;
            public IComparable iface;
            public int ok;
        }

        [Test]
        public void CheckerCatchesBadTypes()
        {
            var problems = new List<string>();
            Check(typeof(BadSave), "BadSave", problems, new HashSet<Type>(), 0);
            Assert.AreEqual(4, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void SaveContainsEverySystemsState()
        {
            var c = new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));
            var save = c.CreateSave();
            Assert.IsTrue(save.player != null && save.economy != null && save.media != null && save.phone != null &&
                          save.legacies != null && save.romance != null && save.events != null);

            // セーブから作り直すと、同じ状態が戻る
            c.Player.cash = 12345;
            c.Player.matchBuffs.Add(MatchBuff.Warded);
            c.Events.mysteryClues = 2;
            var again = new IslandCareer(c.World, c.CreateSave());
            Assert.AreEqual(12345L, again.Player.cash);
            Assert.IsTrue(again.Player.matchBuffs.Contains(MatchBuff.Warded));
            Assert.AreEqual(2, again.Mystery.Clues);
            Assert.AreEqual(c.Economy.stocks.Count, again.Economy.stocks.Count, "ロード時に株が二重に上場されない");
        }
    }
}
