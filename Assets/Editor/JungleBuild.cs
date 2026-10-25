using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Called by the GitHub build (GameCI buildMethod: JungleBuild.BuildAndroid).
// It creates the scene and base material, sets Android options and builds the APK.
public static class JungleBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string MatPath = "Assets/Resources/JungleBase.mat";

    [MenuItem("Jungle Ride/Prepare Project")]
    public static void Prepare()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) == null)
        {
            var m = new Material(Shader.Find("Standard"));
            m.name = "JungleBase";
            AssetDatabase.CreateAsset(m, MatPath);
        }

        // Scene with the default camera + sun. Fog is switched on here so the
        // fog shader variants are kept in the build; the game builds the world at runtime.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.017f;
        RenderSettings.fogColor = new Color(0.56f, 0.64f, 0.56f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void BuildAndroid()
    {
        Prepare();

        PlayerSettings.companyName = "AppForge";
        PlayerSettings.productName = "Jungle Ride";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.appforge.jungleride");
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.buildAppBundle = false;

        string output = GetArg("-customBuildPath");
        if (string.IsNullOrEmpty(output)) output = "build/Android/JungleRide.apk";
        if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "JungleRide.apk");
        string dir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Jungle Ride build result: " + report.summary.result + "  ->  " + output);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Jungle Ride Android build failed: " + report.summary.result);
    }

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
