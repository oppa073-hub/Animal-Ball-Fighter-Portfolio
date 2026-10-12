using System.Collections.Generic;
using UnityEngine;

public class BossController : MonoBehaviour
{
    private BossPattern[] patterns;
    private readonly List<BossPattern> usablePatterns = new List<BossPattern>();

    private Health health;

    private bool isUsingPattern;
    private bool isGameOverHandled;
    private bool isDeathHandled;

    private int currentPhase = 1;

    // 2페이즈(분노): 체력 70% 이하부터 패턴 쿨타임 감소 + 연출(CombatJuice)
    [SerializeField] private float ragePatternCooldownMultiplier = 0.65f;
    public static event System.Action<Transform, int> OnPhaseChanged;
    public float CooldownMultiplier => currentPhase >= 2 ? ragePatternCooldownMultiplier : 1f;
    public int CurrentPhase => currentPhase;

    private void Awake()
    {
        patterns = GetComponents<BossPattern>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        // 오브젝트 풀에서 보스를 다시 꺼냈을 때
        // 이전 전투의 상태가 남지 않도록 초기화
        isUsingPattern = false;
        isGameOverHandled = false;
        isDeathHandled = false;
        currentPhase = 1;
    }

    private void OnDisable()
    {
        // 보스가 풀로 돌아갈 때 실행 중인 패턴 정리
        CancelAllPatterns();

        isUsingPattern = false;
        isGameOverHandled = false;
        isDeathHandled = false;
        currentPhase = 1;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (health == null) return;

        // 보스가 죽은 뒤 사망 애니메이션이 재생되는 동안
        // 새로운 패턴이 시작되는 것을 방지
        if (health.CurrentHp <= 0)
        {
            if (!isDeathHandled)
            {
                CancelAllPatterns();
                isDeathHandled = true;
            }

            return;
        }

        isDeathHandled = false;

        // 플레이어가 죽었을 때 보스 패턴 정지
        if (GameManager.Instance.currentState == GameState.GameOver)
        {
            if (!isGameOverHandled)
            {
                CancelAllPatterns();
                isGameOverHandled = true;
            }

            return;
        }

        // 광고 부활 후 두 번째 사망에서도
        // 다시 패턴을 취소할 수 있도록 초기화
        isGameOverHandled = false;

        if (GameManager.Instance.currentState != GameState.Playing)
        {
            return;
        }

        int phase = health.HpRatio > 0.7f ? 1 : 2;
        if (phase > currentPhase) OnPhaseChanged?.Invoke(transform, phase);  //분노 진입 (한 번만, 패턴 중에도 바로)
        currentPhase = Mathf.Max(currentPhase, phase);  //회복돼도 다시 1페이즈로 돌아가지 않음

        if (isUsingPattern) return;

        TryUsePattern();
    }

    public void TryUsePattern()
    {
        if (patterns == null || patterns.Length == 0) return;

        usablePatterns.Clear();

        for (int i = 0; i < patterns.Length; i++)
        {
            BossPattern pattern = patterns[i];

            if (pattern == null) continue;
            if (pattern.requiredPhase > currentPhase) continue;
            if (!pattern.CanUse()) continue;

            usablePatterns.Add(pattern);
        }

        if (usablePatterns.Count == 0) return;

        int randomIndex = Random.Range(0, usablePatterns.Count);
        BossPattern selectedPattern = usablePatterns[randomIndex];

        selectedPattern.RecordUseTime();

        isUsingPattern = true;

        selectedPattern.UsePattern();
    }

    public void EndPattern()
    {
        isUsingPattern = false;
    }

    private void CancelAllPatterns()
    {
        if (patterns == null) return;

        for (int i = 0; i < patterns.Length; i++)
        {
            patterns[i]?.CancelPattern();
        }

        isUsingPattern = false;
    }
}