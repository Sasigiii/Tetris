using SQLite;

public class WordEntry
{
    [PrimaryKey]
    public int wordRank { get; set; }

    public string headWord { get; set; }

    public string tranCn { get; set; }
}
