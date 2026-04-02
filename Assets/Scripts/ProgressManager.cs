using UnityEngine;

public static class ProgressManager
{
    private static string GetKey(LexiconDatabase.Lexicon lexicon)
    {
        return $"Progress_{lexicon}";
    }

    public static int GetMaxLevel(LexiconDatabase.Lexicon lexicon)
    {
        return PlayerPrefs.GetInt(GetKey(lexicon), 0);
    }

    public static void SetMaxLevel(LexiconDatabase.Lexicon lexicon, int level)
    {
        string key = GetKey(lexicon);
        int current = PlayerPrefs.GetInt(key, 0);
        if (level > current)
        {
            PlayerPrefs.SetInt(key, level);
            PlayerPrefs.Save();
        }
    }
}
