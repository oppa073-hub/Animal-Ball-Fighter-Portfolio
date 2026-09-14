using UnityEngine;

public enum GameState
{
    Playing, RoomClear, GameOver, AugmentSelect, Ready, Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState currentState = GameState.Ready;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void GameOver()
    {
        if (currentState == GameState.GameOver) return;

        Debug.Log("게임오버");
        //Time.timeScale = 0f; //임시
        currentState = GameState.GameOver;
    }
    public void ResumeAfterRevive()
    {
        if (currentState != GameState.GameOver) return;

        currentState = GameState.Ready;
    }
}
