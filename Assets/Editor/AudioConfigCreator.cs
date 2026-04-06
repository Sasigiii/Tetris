using UnityEditor;
using UnityEngine;

public static class AudioConfigCreator
{
    [MenuItem("Assets/Create/Game/AudioConfig Asset")]
    public static void CreateAsset()
    {
        var config = ScriptableObject.CreateInstance<AudioConfig>();
        AssetDatabase.CreateAsset(config, "Assets/Resources/AudioConfig.asset");
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;
    }
}
