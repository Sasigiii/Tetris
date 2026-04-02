using UnityEngine;
using UnityEditor;

public static class LexiconConfigCreator
{
    [MenuItem("Tools/Create LexiconConfig Asset")]
    public static void Create()
    {
        string path = "Assets/Resources/LexiconConfig.asset";

        var existing = AssetDatabase.LoadAssetAtPath<LexiconConfig>(path);
        if (existing != null)
        {
            Debug.Log("[LexiconConfigCreator] LexiconConfig.asset already exists.");
            Selection.activeObject = existing;
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var config = ScriptableObject.CreateInstance<LexiconConfig>();
        config.entries = new LexiconConfig.LexiconEntry[]
        {
            new LexiconConfig.LexiconEntry { lexicon = LexiconDatabase.Lexicon.ChuZhong, maxWordLength = 8, wordsPerLevel = 8 },
            new LexiconConfig.LexiconEntry { lexicon = LexiconDatabase.Lexicon.GaoZhong, maxWordLength = 10, wordsPerLevel = 10 },
            new LexiconConfig.LexiconEntry { lexicon = LexiconDatabase.Lexicon.CET4, maxWordLength = 12, wordsPerLevel = 10 },
            new LexiconConfig.LexiconEntry { lexicon = LexiconDatabase.Lexicon.CET6, maxWordLength = 14, wordsPerLevel = 10 },
        };

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = config;
        Debug.Log("[LexiconConfigCreator] Created LexiconConfig.asset at " + path);
    }
}
