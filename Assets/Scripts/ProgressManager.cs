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

    private static string GetStarKey(LexiconDatabase.Lexicon lexicon, int level)
    {
        return $"Star_{lexicon}_{level}";
    }

    public static int GetMaxStar(LexiconDatabase.Lexicon lexicon, int level)
    {
        return PlayerPrefs.GetInt(GetStarKey(lexicon, level), 0);
    }

    public static void SetMaxStar(LexiconDatabase.Lexicon lexicon, int level, int star)
    {
        string key = GetStarKey(lexicon, level);
        int current = PlayerPrefs.GetInt(key, 0);
        if (star > current)
        {
            PlayerPrefs.SetInt(key, star);
            PlayerPrefs.Save();
        }
    }
}
