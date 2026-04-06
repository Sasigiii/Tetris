public class GameOverUIModel : BaseModel
{
    public bool isCleared;
    public int finalScore;
    public int starRating;

    public static GameOverUIModel Pending { get; set; }
}
