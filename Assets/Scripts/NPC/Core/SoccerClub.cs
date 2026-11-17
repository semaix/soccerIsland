using System;

namespace SoccerIsland.NPC
{
    public enum ClubCategory
    {
        Town,        // 街のクラブ（草サッカー・地域対抗）
        Workplace,   // 職場チーム（病院FC、警察FC など）
        Underground, // 裏フットサル・裏トーナメント
        Prison,      // 刑務所リーグ
        Beach,       // ビーチサッカー
    }

    [Serializable]
    public class SoccerClub
    {
        public int id;
        public string name;
        public Town town;
        public ClubCategory category;
        /// <summary>サッカー協会加盟クラブか。</summary>
        public bool registeredWithAssociation = true;
        /// <summary>キャプテン／ボスのNPC ID。-1 は未設定。</summary>
        public int leaderId = -1;

        public override string ToString() => $"[{id}] {name} ({NPCLabels.Of(town)}/{category})";
    }
}
