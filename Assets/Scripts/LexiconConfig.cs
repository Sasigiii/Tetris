using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LexiconConfig", menuName = "Config/LexiconConfig")]
public class LexiconConfig : ScriptableObject
{
    public enum DropSpeedFallbackMode
    {
        Hold = 0,
        Decrease = 1
    }

    public LexiconEntry[] entries;

    [Serializable]
    public class LexiconEntry
    {
        public LexiconDatabase.Lexicon lexicon;
        public int maxWordLength = 10;
        public int wordsPerLevel = 10;
        public int minGridColumns = 7;
        public int maxGridColumns = 18;
        public float minCellSize = 42f;
        public bool lengthPriorityForTesting = true;
        public bool syncTMPRectWithCell = true;
        public float tmpRectHorizontalPadding = 6f;
        public float tmpRectVerticalPadding = 4f;
        public float minTMPRectSize = 20f;
        public bool syncCrossBlockTMPRect = true;
        public float crossBlockTmpHorizontalPadding = 6f;
        public float crossBlockTmpVerticalPadding = 4f;
        public float crossBlockMinTmpRectSize = 20f;
        public bool enableCrossBlockScaleCompensation = true;
        public float crossBlockCompensationGamma = 0.9f;
        public float crossBlockCompensationMin = 1f;
        public float crossBlockCompensationMax = 1.6f;
        [Header("Score & Timer")]
        public int wordBaseScore = 20;
        public int comboBonusPerStreak = 5;
        public int comboBonusCap = 60;
        public float levelTimeLimitSeconds = 180f;

        [Header("Score-Driven Fall Speed")]
        public float v0 = 120f;
        public int s0 = ScoreManager.InitialScore;
        public int deltaS = 30;
        public float alpha = 20f;
        public float vMax = 260f;
        public float vFast = 600f;
        public DropSpeedFallbackMode fallbackMode = DropSpeedFallbackMode.Decrease;
    }

    public LexiconEntry GetEntry(LexiconDatabase.Lexicon lexicon)
    {
        if (entries == null) return null;
        foreach (var entry in entries)
        {
            if (entry.lexicon == lexicon)
            {
                SanitizeEntry(entry);
                return entry;
            }
        }
        return null;
    }

    private static void SanitizeEntry(LexiconEntry entry)
    {
        if (entry == null) return;

        entry.v0 = Mathf.Max(1f, entry.v0);
        entry.deltaS = Mathf.Max(1, entry.deltaS);
        entry.alpha = Mathf.Max(0f, entry.alpha);
        entry.vMax = Mathf.Max(entry.v0, entry.vMax);
        entry.vFast = Mathf.Max(entry.vMax, entry.vFast);
        entry.maxWordLength = Mathf.Max(7, entry.maxWordLength);
        entry.minGridColumns = Mathf.Max(7, entry.minGridColumns);
        entry.maxGridColumns = Mathf.Max(entry.minGridColumns, entry.maxGridColumns);
        entry.minCellSize = Mathf.Max(20f, entry.minCellSize);
        entry.tmpRectHorizontalPadding = Mathf.Max(0f, entry.tmpRectHorizontalPadding);
        entry.tmpRectVerticalPadding = Mathf.Max(0f, entry.tmpRectVerticalPadding);
        entry.minTMPRectSize = Mathf.Max(8f, entry.minTMPRectSize);
        entry.crossBlockTmpHorizontalPadding = Mathf.Max(0f, entry.crossBlockTmpHorizontalPadding);
        entry.crossBlockTmpVerticalPadding = Mathf.Max(0f, entry.crossBlockTmpVerticalPadding);
        entry.crossBlockMinTmpRectSize = Mathf.Max(8f, entry.crossBlockMinTmpRectSize);
        entry.crossBlockCompensationGamma = Mathf.Clamp(entry.crossBlockCompensationGamma, 0f, 3f);
        entry.crossBlockCompensationMin = Mathf.Max(0.5f, entry.crossBlockCompensationMin);
        entry.crossBlockCompensationMax = Mathf.Max(entry.crossBlockCompensationMin, entry.crossBlockCompensationMax);
        entry.wordBaseScore = Mathf.Max(1, entry.wordBaseScore);
        entry.comboBonusPerStreak = Mathf.Max(0, entry.comboBonusPerStreak);
        entry.comboBonusCap = Mathf.Max(0, entry.comboBonusCap);
        entry.levelTimeLimitSeconds = Mathf.Max(1f, entry.levelTimeLimitSeconds);
    }
}
