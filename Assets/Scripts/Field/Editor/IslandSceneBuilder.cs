using System.Collections.Generic;
using SoccerIsland.Career.UnityIntegration;
using SoccerIsland.Field.UnityIntegration;
using SoccerIsland.Match.UnityIntegration;
using SoccerIsland.NPC.UnityIntegration;
using SoccerIsland.World.UnityIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoccerIsland.Field.EditorTools
{
    /// <summary>
    /// メニュー SoccerIsland > ゲームのシーンを作る で、すぐ遊べるシーン（Assets/Scenes/Island.unity）を作る。
    /// 島民・ワールド・主人公の管理役、フィールド画面、サイン作成画面、カメラを置き、ビルド設定にも登録する。
    /// </summary>
    public static class IslandSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Island.unity";

        [MenuItem("SoccerIsland/ゲームのシーンを作る")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var game = new GameObject("Game");
            game.AddComponent<NPCManager>();
            game.AddComponent<WorldManager>();
            game.AddComponent<CareerManager>();
            var studio = game.AddComponent<CreatorStudioView>();
            var field = game.AddComponent<FieldController>();
            field.creatorStudio = studio;

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[SoccerIsland] " + ScenePath + " を作りました。再生ボタンで遊べます。");
        }
    }
}
