using UnityEngine;

public class StageDatabase : MonoBehaviour
{
    public static StageDatabase Instance { get; private set; }

    [SerializeField] private StageData[] normalStages;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public StageData GetRandomNormalStage()
    {
        if (normalStages == null || normalStages.Length == 0)
        {
            Debug.LogError("[StageDatabase] 일반 스테이지가 등록되지 않았습니다.");
            return null;
        }

        int randomIndex = Random.Range(0, normalStages.Length);

        return normalStages[randomIndex];
    }
}