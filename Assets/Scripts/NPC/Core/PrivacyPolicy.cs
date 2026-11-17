namespace SoccerIsland.NPC
{
    /// <summary>
    /// 個人情報の取り扱い方針。
    ///
    /// ・端末（スマホ）内の個人情報は使わない：連絡先・電話番号・位置情報・カメラ・マイク・
    ///   端末ID・端末名・広告ID・クリップボードは読み取らない。
    /// ・データを外部に出さない：通信処理・アナリティクス・外部URLを開く処理は持たない。
    ///   セーブデータは端末内（Application.persistentDataPath）のみに保存し、クラウドバックアップ対象からも外す。
    /// ・NPCの名前・電話番号はすべて架空。電話番号は実在の番号と一致しない島内専用形式。
    ///
    /// Assets/Tests/EditMode/PrivacyComplianceTests.cs が、上記に反するAPIがコードに入っていないかを検査する。
    /// </summary>
    public static class PrivacyPolicy
    {
        /// <summary>島内電話番号の頭につく文字。現実の電話番号には使われない。</summary>
        public const string IslandPhonePrefix = "島";

        public static bool IsIslandPhoneNumber(string phone) =>
            !string.IsNullOrEmpty(phone) && phone.StartsWith(IslandPhonePrefix + "-");
    }
}
