public static class GameSessionData
{
    public static StageData SelectedStage { get; private set; }
    public static bool IsEndless { get; private set; }
    public static int EndlessRunGold { get; private set; }

    public static void SetStage(StageData stage)
    {
        SelectedStage = stage;
    }
    public static void SetEndless(bool isEndless)
    {
        IsEndless = isEndless;
    }
    public static void ResetEndlessRunGold()
    {
        EndlessRunGold = 0;
    }

    public static void AddEndlessRunGold(int amount)
    {
        EndlessRunGold += amount;
    }
}