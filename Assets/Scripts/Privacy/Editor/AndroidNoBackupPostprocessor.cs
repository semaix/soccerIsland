#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace SoccerIsland.Privacy.EditorTools
{
    /// <summary>
    /// Android ビルド時に AndroidManifest.xml へ allowBackup="false" を書き込み、
    /// セーブデータが Google のクラウドバックアップに含まれないようにする。
    /// </summary>
    class AndroidNoBackupPostprocessor : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string ToolsNs = "http://schemas.android.com/tools";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // unityLibrary と launcher の両方の manifest を書き換える（マージ時に上書きされないように）
            Patch(Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml"));
            Patch(Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml"));
        }

        static void Patch(string manifestPath)
        {
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;

            var app = manifest.SelectSingleNode("application") as XmlElement;
            if (app == null)
            {
                app = doc.CreateElement("application");
                manifest.AppendChild(app);
            }

            if (!manifest.HasAttribute("xmlns:tools")) manifest.SetAttribute("xmlns:tools", ToolsNs);

            app.SetAttribute("allowBackup", AndroidNs, "false");
            app.SetAttribute("fullBackupContent", AndroidNs, "false");
            app.SetAttribute("replace", ToolsNs, "android:allowBackup,android:fullBackupContent");

            doc.Save(manifestPath);
            Debug.Log("[Privacy] allowBackup=false を設定しました: " + Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(manifestPath)))));
        }
    }
}
#endif
