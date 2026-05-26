using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;

public static class WrongWordManager
{
    private static SQLiteConnection _db;

    public static void Init()
    {
        string dbPath = Path.Combine(Application.persistentDataPath, "wrong_words.db");
        _db = new SQLiteConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
        _db.CreateTable<WrongWordEntry>();
    }

    public static void Shutdown()
    {
        _db?.Close();
        _db = null;
    }

    public static void RecordWrong(LexiconDatabase.Lexicon lexicon, string headWord, string tranCn)
    {
        if (_db == null) return;

        string lex = lexicon.ToString();
        var existing = _db.Query<WrongWordEntry>(
            "SELECT * FROM \"WrongWordEntry\" WHERE lexicon = ? AND headWord = ? LIMIT 1",
            lex, headWord);

        if (existing.Count > 0)
        {
            var entry = existing[0];
            entry.count += 1;
            entry.tranCn = tranCn;
            _db.Execute(
                "UPDATE \"WrongWordEntry\" SET count = ?, tranCn = ? WHERE lexicon = ? AND headWord = ?",
                entry.count, entry.tranCn, lex, headWord);
        }
        else
        {
            _db.Insert(new WrongWordEntry
            {
                lexicon = lex,
                headWord = headWord,
                tranCn = tranCn,
                count = 1
            });
        }
    }

    public static List<WrongWordEntry> GetWrongWords(LexiconDatabase.Lexicon lexicon)
    {
        if (_db == null) return new List<WrongWordEntry>();

        string lex = lexicon.ToString();
        return _db.Query<WrongWordEntry>(
            "SELECT * FROM \"WrongWordEntry\" WHERE lexicon = ? ORDER BY count DESC",
            lex);
    }
}
