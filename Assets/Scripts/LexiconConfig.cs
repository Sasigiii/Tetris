using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LexiconConfig", menuName = "Config/LexiconConfig")]
public class LexiconConfig : ScriptableObject
{
    public LexiconEntry[] entries;

    [Serializable]
    public class LexiconEntry
    {
        public LexiconDatabase.Lexicon lexicon;
        public int maxWordLength = 10;
        public int wordsPerLevel = 10;
    }

    public LexiconEntry GetEntry(LexiconDatabase.Lexicon lexicon)
    {
        if (entries == null) return null;
        foreach (var entry in entries)
        {
            if (entry.lexicon == lexicon)
                return entry;
        }
        return null;
    }
}
