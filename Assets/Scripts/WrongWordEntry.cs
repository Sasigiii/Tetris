using SQLite;

public class WrongWordEntry
{
    [Indexed(Name = "idx_lexicon_headWord", Order = 1, Unique = true)]
    public string lexicon { get; set; }

    [Indexed(Name = "idx_lexicon_headWord", Order = 2, Unique = true)]
    public string headWord { get; set; }

    public string tranCn { get; set; }

    public int count { get; set; }
}
