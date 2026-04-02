public static class GameContext
{
    public static LexiconDatabase.Lexicon CurrentLexicon { get; set; }
    public static int CurrentLevel { get; set; }

    private static LexiconDatabase _db;

    public static LexiconDatabase Database
    {
        get
        {
            if (_db == null)
                _db = new LexiconDatabase();
            return _db;
        }
    }

    public static void Shutdown()
    {
        _db?.Close();
        _db = null;
    }
}
