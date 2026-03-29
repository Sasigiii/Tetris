using UnityEngine;
using SQLite;
using System.IO;
using System.Collections.Generic;

public class LexiconDatabase
{
    public enum Lexicon
    {
        ChuZhong,
        GaoZhong,
        CET4,
        CET6
    }

    private SQLiteConnection _db;

    public LexiconDatabase()
    {
        string dbName = "lexicon.db";
        string sourcePath = Path.Combine(Application.streamingAssetsPath, dbName);
        string dbPath;

#if UNITY_EDITOR
        dbPath = sourcePath;
#else
        dbPath = Path.Combine(Application.persistentDataPath, dbName);
        if (!File.Exists(dbPath))
            File.Copy(sourcePath, dbPath);
#endif

        _db = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
    }

    public List<WordEntry> GetAllWords(Lexicon lexicon)
    {
        string table = lexicon.ToString();
        return _db.Query<WordEntry>($"SELECT * FROM \"{table}\" ORDER BY wordRank");
    }

    public WordEntry GetWord(Lexicon lexicon, string headWord)
    {
        string table = lexicon.ToString();
        var results = _db.Query<WordEntry>(
            $"SELECT * FROM \"{table}\" WHERE headWord = ? LIMIT 1", headWord
        );
        return results.Count > 0 ? results[0] : null;
    }

    public WordEntry GetWordByRank(Lexicon lexicon, int rank)
    {
        string table = lexicon.ToString();
        var results = _db.Query<WordEntry>(
            $"SELECT * FROM \"{table}\" WHERE wordRank = ? LIMIT 1", rank
        );
        return results.Count > 0 ? results[0] : null;
    }

    public List<WordEntry> SearchWords(Lexicon lexicon, string keyword)
    {
        string table = lexicon.ToString();
        return _db.Query<WordEntry>(
            $"SELECT * FROM \"{table}\" WHERE headWord LIKE ? OR tranCn LIKE ? ORDER BY wordRank",
            $"%{keyword}%", $"%{keyword}%"
        );
    }

    public int GetWordCount(Lexicon lexicon)
    {
        string table = lexicon.ToString();
        return _db.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{table}\"");
    }

    public void Close()
    {
        _db?.Close();
        _db = null;
    }
}
