using System;

public class ScoreManager
{
    public const int InitialScore = 100;
    public const int CorrectBonus = 10;
    public const int WrongPenalty = 15;

    private int _score;

    public int Score => _score;
    public bool IsFailed => _score <= 0;

    public event Action<int> OnScoreChanged;

    public void Reset()
    {
        _score = InitialScore;
        OnScoreChanged?.Invoke(_score);
    }

    public void AddCorrect()
    {
        _score += CorrectBonus;
        OnScoreChanged?.Invoke(_score);
    }

    public void AddWrong()
    {
        _score -= WrongPenalty;
        OnScoreChanged?.Invoke(_score);
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
