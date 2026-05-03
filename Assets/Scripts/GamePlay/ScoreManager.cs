using System;

public class ScoreManager
{
    public const int InitialScore = 100;
    public const int CorrectBonus = 10;
    public const int WrongPenalty = 15;

    private int _score;
    private int _comboStreak;

    public int Score => _score;
    public int ComboStreak => _comboStreak;
    public bool IsFailed => _score <= 0;

    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged;
    public event Action<int, int, int> OnCorrectAwarded;

    public void Reset()
    {
        _score = InitialScore;
        _comboStreak = 0;
        OnScoreChanged?.Invoke(_score);
        OnComboChanged?.Invoke(_comboStreak);
    }

    public void AddCorrect()
    {
        AddCorrectWithCombo(CorrectBonus, 0, 0);
    }

    public int AddCorrectWithCombo(int baseScore, int comboBonusPerStreak, int comboBonusCap)
    {
        int safeBase = Math.Max(0, baseScore);
        int safeStep = Math.Max(0, comboBonusPerStreak);
        int safeCap = Math.Max(0, comboBonusCap);

        _comboStreak++;
        int bonus = Math.Min(Math.Max(0, _comboStreak - 1) * safeStep, safeCap);
        int gained = safeBase + bonus;

        _score += gained;
        OnCorrectAwarded?.Invoke(safeBase, bonus, gained);
        OnScoreChanged?.Invoke(_score);
        OnComboChanged?.Invoke(_comboStreak);
        return gained;
    }

    public void AddWrong()
    {
        _comboStreak = 0;
        _score -= WrongPenalty;
        OnScoreChanged?.Invoke(_score);
        OnComboChanged?.Invoke(_comboStreak);
    }

    public void ResetCombo()
    {
        if (_comboStreak == 0)
            return;
        _comboStreak = 0;
        OnComboChanged?.Invoke(_comboStreak);
    }

    public int GetStarRating()
    {
        if (_score >= 150) return 5;
        if (_score >= 120) return 4;
        if (_score >= 90) return 3;
        if (_score >= 60) return 2;
        return 1;
    }
}
