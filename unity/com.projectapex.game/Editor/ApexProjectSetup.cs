using System.Collections.Generic;
using System.IO;
using Apex.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.EditorTools
{
    /// <summary>
    /// One-click project configuration for Project Apex (menu "Project Apex").
    /// Applies player settings for landscape mobile, generates material assets so
    /// they are included in builds, and creates the Boot scene. Safe to run again.
    /// </summary>
    public static class ApexProjectSetup
    {
        const string GeneratedRoot = "Assets/ProjectApex";
        const string MaterialFolder = GeneratedRoot + "/Resources/" + ApexMaterials.ResourceFolder;
        const string ScenePath = GeneratedRoot + "/Scenes/Boot.unity";

        [MenuItem("Project Apex/Projekt einrichten (alles)", priority = 0)]
        public static void SetupAll()
        {
            ApplyPlayerSettings();
            CreateMaterials();
            CreateBootScene();
            Debug.Log("[Apex] Projekt eingerichtet. Szene: " + ScenePath + " – Play drücken.");
        }

        /// <summary>Opens the Boot scene and enters Play mode (used by Setup-Windows.bat).</summary>
        [MenuItem("Project Apex/Spiel starten", priority = 1)]
        public static void OpenBootSceneAndPlay()
        {
            if (!File.Exists(ScenePath)) SetupAll();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        [MenuItem("Project Apex/1. Player-Einstellungen anwenden", priority = 20)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Project Apex";
            PlayerSettings.productName = "Project Apex";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.projectapex.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.projectapex.game");
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.projectapex.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.projectapex.game");
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Active input handling: Input System package only (0 = old, 1 = new, 2 = both).
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 1)
                {
                    prop.intValue = 1;
                    so.ApplyModifiedProperties();
                    Debug.Log("[Apex] Active Input Handling auf 'Input System Package' gesetzt (Editor-Neustart nötig).");
                }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Project Apex/2. Materialien erzeugen", priority = 21)]
        public static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            foreach (var key in new List<string>(ApexMaterials.Palette.Keys))
            {
                string path = $"{MaterialFolder}/{key}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                AssetDatabase.CreateAsset(ApexMaterials.Create(key), path);
            }
            string rainPath = $"{MaterialFolder}/Rain.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(rainPath) == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var rain = new Material(shader);
                    var c = new Color(0.8f, 0.85f, 0.95f, 0.45f);
                    if (rain.HasProperty("_BaseColor")) rain.SetColor("_BaseColor", c);
                    if (rain.HasProperty("_Color")) rain.SetColor("_Color", c);
                    AssetDatabase.CreateAsset(rain, rainPath);
                }
            }
            string skyPath = $"{MaterialFolder}/Sky.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(skyPath) == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null)
                {
                    var sky = new Material(shader);
                    if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.035f);
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.9f);
                    if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.15f);
                    AssetDatabase.CreateAsset(sky, skyPath);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Project Apex/3. Boot-Szene erzeugen", priority = 22)]
        public static void CreateBootScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("ApexGame");
            boot.AddComponent<ApexGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
