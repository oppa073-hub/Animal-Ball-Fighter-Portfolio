using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Stage")]
    [SerializeField] private StageController stageController;

    private bool isPaused;
    private GameState stateBeforePause;

    private void Awake()
    {
        // 이전 씬에서 혹시 일시정지 값이 남았을 경우를 대비
        Time.timeScale = 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (stageController == null)
        {
            stageController = FindFirstObjectByType<StageController>();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        if (GameManager.Instance == null) return;

        GameState currentState = GameManager.Instance.currentState;

        // 전투 중 또는 발사 준비 상태에서만 일시정지 가능
        if (currentState != GameState.Playing && currentState != GameState.Ready)
        {
            return;
        }

        stateBeforePause = currentState;
        isPaused = true;

        GameManager.Instance.currentState = GameState.Paused;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentState = stateBeforePause;
        }

        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void ReturnToLobby()
    {
        // 로비까지 멈춘 상태가 이어지지 않도록 먼저 복구
        Time.timeScale = 1f;
        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (stageController == null)
        {
            Debug.LogError("[Pause] StageController가 연결되지 않았습니다.");
            return;
        }

        stageController.ReturnToLobby();
    }

    private void OnDestroy()
    {
        // 일시정지 상태에서 씬이 종료돼도 반드시 복구
        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }
}