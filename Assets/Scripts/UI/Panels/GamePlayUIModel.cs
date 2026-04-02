using System.Collections.Generic;

public class GamePlayUIModel : BaseModel
{
    public class WordRowState
    {
        public string answer;
        public char[] displayChars;
        public bool[] holeOpen;
        public bool active;
    }

    public readonly List<WordEntry> levelWords = new List<WordEntry>();
    public readonly List<WordRowState> rows = new List<WordRowState>();

    public int nextWordIndex;
    public int completedWords;
    public int score;
    public bool levelFinished;
}
