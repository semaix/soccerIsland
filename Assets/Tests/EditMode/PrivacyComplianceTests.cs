using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SoccerIsland.NPC.Tests
{
    /// <summary>
    /// 個人情報を使わない・外部に出さない方針（PrivacyPolicy）を守っているかの検査。
    /// Assets/Scripts 以下のコードに、端末の個人情報を読むAPI・通信APIが入っていたら失敗する。
    /// </summary>
    public class PrivacyComplianceTests
    {
        /// <summary>{検出する文字列, 理由}</summary>
        static readonly string[,] ForbiddenApis =
        {
            // 端末内の個人情報
            { "SystemInfo.deviceUniqueIdentifier", "端末ID" },
            { "SystemInfo.deviceName", "端末名（持ち主の名前が入ることがある）" },
            { "Input.location", "位置情報" },
            { "LocationService", "位置情報" },
            { "WebCamTexture", "カメラ" },
            { "Microphone.", "マイク" },
            { "RequestUserAuthorization", "カメラ・マイク等の権限要求" },
            { "RequestAdvertisingIdentifierAsync", "広告ID" },
            { "advertisingIdentifier", "広告ID" },
            { "systemCopyBuffer", "クリップボード" },
            { "Contacts", "連絡先" },
            { "AndroidJavaClass", "ネイティブAPI経由の端末情報アクセス" },
            { "DllImport", "ネイティブAPI経由の端末情報アクセス" },
            // 外部への送信
            { "UnityWebRequest", "通信" },
            { "new WWW(", "通信" },
            { "System.Net", "通信" },
            { "HttpClient", "通信" },
            { "Socket", "通信" },
            { "Application.OpenURL", "外部URL" },
            { "UnityEngine.Analytics", "アナリティクス送信" },
            { "Unity.Services", "クラウドサービス送信" },
            { "Social.", "外部ソーシャル連携" },
        };

        static string ScriptsRoot()
        {
            // Unity の EditMode テストではカレントディレクトリがプロジェクトルート
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts");
        }

        [Test]
        public void ScriptsDoNotUseDevicePersonalDataOrNetwork()
        {
            var root = ScriptsRoot();
            Assert.IsTrue(Directory.Exists(root), root);

            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var code = StripComment(lines[i]);
                    for (int k = 0; k < ForbiddenApis.GetLength(0); k++)
                        if (code.Contains(ForbiddenApis[k, 0]))
                            violations.Add($"{Path.GetFileName(file)}:{i + 1} {ForbiddenApis[k, 0]}（{ForbiddenApis[k, 1]}）");
                }
            }
            Assert.IsTrue(violations.Count == 0, "個人情報・通信APIが使われています:\n" + string.Join("\n", violations));
        }

        [Test]
        public void PhoneNumbersAreFictionalIslandFormat()
        {
            var s = NPCGenerationSettings.CreateDefault();
            var db = NPCGenerator.Generate(s);
            foreach (var n in db.npcs.Where(n => !string.IsNullOrEmpty(n.phoneNumber)))
            {
                Assert.IsTrue(PrivacyPolicy.IsIslandPhoneNumber(n.phoneNumber), n.phoneNumber);
                Assert.IsTrue(!n.phoneNumber.StartsWith("0"), "実在の電話番号形式になっている: " + n.phoneNumber);
            }
        }

        /// <summary>行コメント以降を除く（コメント内の説明文で誤検出しないため）。</summary>
        static string StripComment(string line)
        {
            int idx = line.IndexOf("//");
            return idx >= 0 ? line.Substring(0, idx) : line;
        }
    }
}
