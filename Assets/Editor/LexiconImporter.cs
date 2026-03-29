using UnityEngine;
using UnityEditor;
using SQLite;
using System.IO;
using System.Collections.Generic;

public static class LexiconImporter
{
    private static readonly Dictionary<string, string> FileToTable = new Dictionary<string, string>
    {
        { "ChuZhongluan_2.json", "ChuZhong" },
        { "GaoZhongluan_2.json", "GaoZhong" },
        { "CET4luan_1.json",     "CET4" },
        { "CET6luan_1.json",     "CET6" }
    };

    [MenuItem("Tools/Import Lexicon to SQLite")]
    public static void ImportAll()
    {
        string dbPath = Path.Combine(Application.streamingAssetsPath, "lexicon.db");

        if (!Directory.Exists(Application.streamingAssetsPath))
            Directory.CreateDirectory(Application.streamingAssetsPath);

        if (File.Exists(dbPath))
            File.Delete(dbPath);

        var db = new SQLiteConnection(dbPath);

        int totalWords = 0;

        foreach (var pair in FileToTable)
        {
            string jsonPath = Path.Combine(Application.dataPath, "Lexicon", pair.Key);
            if (!File.Exists(jsonPath))
            {
                Debug.LogWarning($"[LexiconImporter] File not found: {jsonPath}");
                continue;
            }

            string tableName = pair.Value;
            db.Execute($"CREATE TABLE IF NOT EXISTS \"{tableName}\" (wordRank INTEGER PRIMARY KEY, headWord TEXT, tranCn TEXT)");

            string json = File.ReadAllText(jsonPath);
            var items = JsonHelper.FromJsonArray(json);

            db.BeginTransaction();
            try
            {
                foreach (var item in items)
                {
                    string tranCn = ExtractTranCn(item);
                    db.Execute(
                        $"INSERT OR REPLACE INTO \"{tableName}\" (wordRank, headWord, tranCn) VALUES (?, ?, ?)",
                        item.wordRank, item.headWord, tranCn
                    );
                }
                db.Commit();
            }
            catch
            {
                db.Rollback();
                throw;
            }

            Debug.Log($"[LexiconImporter] {tableName}: {items.Length} words imported");
            totalWords += items.Length;
        }

        db.Close();
        AssetDatabase.Refresh();
        Debug.Log($"[LexiconImporter] Done! Total {totalWords} words -> {dbPath}");
    }

    private static string ExtractTranCn(RawWordItem item)
    {
        if (item.content?.word?.content?.trans == null)
            return "";

        var parts = new List<string>();
        foreach (var t in item.content.word.content.trans)
        {
            string pos = t.pos ?? "";
            string cn = t.tranCn ?? "";
            if (!string.IsNullOrEmpty(cn))
                parts.Add(string.IsNullOrEmpty(pos) ? cn : $"{pos}.{cn}");
        }
        return string.Join("; ", parts);
    }

    #region JSON Data Classes

    [System.Serializable]
    private class RawWordItem
    {
        public int wordRank;
        public string headWord;
        public string bookId;
        public RawContent content;
    }

    [System.Serializable]
    private class RawContent
    {
        public RawWord word;
    }

    [System.Serializable]
    private class RawWord
    {
        public string wordHead;
        public string wordId;
        public RawWordContent content;
    }

    [System.Serializable]
    private class RawWordContent
    {
        public RawTrans[] trans;
    }

    [System.Serializable]
    private class RawTrans
    {
        public string tranCn;
        public string pos;
    }

    private static class JsonHelper
    {
        public static RawWordItem[] FromJsonArray(string json)
        {
            string wrapped = "{\"items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<Wrapper>(wrapped);
            return wrapper.items;
        }

        [System.Serializable]
        private class Wrapper
        {
            public RawWordItem[] items;
        }
    }

    #endregion
}
