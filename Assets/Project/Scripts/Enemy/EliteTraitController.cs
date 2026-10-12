using DG.Tweening;
using UnityEngine;
using UnityEngine.Localization.Settings;

public enum EliteTrait
{
    None,
    Split,     //분열: 죽으면 일반 적 2마리 소환
    Blink,     //순간이동: 일정 간격으로 다른 위치로 이동
    Regen,     //재생: 한동안 안 맞으면 체력 회복
    Swift      //질주: 이동속도 증가
}

// 엘리트 방의 엘리트 적에게 무작위 특성을 붙인다 (EnemySpawner가 스폰 직후 Apply).
// 풀에서 재사용되므로 OnDisable에서 상태를 모두 정리하고, 특성이 없으면(None) 아무것도 하지 않는다.
public class EliteTraitController : MonoBehaviour, IEnemyDeathHook
{
    private const int SplitCount = 2;
    private const float BlinkInterval = 4f;
    private const float BlinkMinPlayerDistance = 2.5f;
    private const float RegenDelay = 2f;
    private const float RegenPercentPerSecond = 0.03f;
    private const float SwiftMultiplier = 1.5f;

    private static readonly EliteTrait[] RandomTraits = { EliteTrait.Split, EliteTrait.Blink, EliteTrait.Regen, EliteTrait.Swift };

    private EliteTrait trait;

    public bool HasTrait => trait != EliteTrait.None;  //전투 연출(엘리트 처치 시 더 크게)용
    private int roomNumber;
    private EnemyController enemy;
    private Health health;
    private Rigidbody rb;
    private EnemySpawner spawner;

    private float blinkTimer;
    private float lastDamagedTime;
    private float regenBuffer;
    private int lastHp;

    private bool isRegenerating;
    private EliteTraitPopup popup;
    private Vector3 baseScale = Vector3.one;
    private bool hasBaseScale;

    public static void ApplyRandom(EnemyController target, int roomNumber, EnemySpawner spawner)
    {
        if (target == null) return;

        EliteTraitController controller = target.GetComponent<EliteTraitController>();
        if (controller == null) controller = target.gameObject.AddComponent<EliteTraitController>();

        controller.Setup(RandomTraits[Random.Range(0, RandomTraits.Length)], roomNumber, spawner);
    }

    private void Setup(EliteTrait newTrait, int room, EnemySpawner enemySpawner)
    {
        if (!hasBaseScale) { baseScale = transform.localScale; hasBaseScale = true; }

        enemy = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        spawner = enemySpawner;

        trait = newTrait;
        roomNumber = room;
        blinkTimer = BlinkInterval;
        lastDamagedTime = Time.time;
        regenBuffer = 0f;
        isRegenerating = false;
        lastHp = health != null ? health.CurrentHp : 0;

        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnHealthChanged += HandleHealthChanged;
        }

        if (trait == EliteTrait.Swift && enemy != null) enemy.ApplyMoveSpeedMultiplier(SwiftMultiplier);  //Initialize가 매번 기본 속도로 되돌리므로 스폰마다 적용

        ShowPopup(transform, transform.position);  //등장 시 한 번
    }

    private void Update()
    {
        if (trait == EliteTrait.None || enemy == null || enemy.IsDead) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;

        if (trait == EliteTrait.Blink)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                blinkTimer = BlinkInterval;
                TryBlink();
            }
        }
        else if (trait == EliteTrait.Regen && health != null && health.CurrentHp < health.MaxHp && Time.time - lastDamagedTime >= RegenDelay)
        {
            if (!isRegenerating)
            {
                isRegenerating = true;
                ShowPopup(transform, transform.position);  //재생 시작할 때
            }

            regenBuffer += health.MaxHp * RegenPercentPerSecond * Time.deltaTime;

            if (regenBuffer >= 1f)
            {
                int amount = Mathf.FloorToInt(regenBuffer);
                regenBuffer -= amount;
                health.Heal(amount);
            }
        }
    }

    private void HandleHealthChanged(float currentHp, float maxHp)
    {
        if (currentHp < lastHp)  //피격 시 재생 지연
        {
            lastDamagedTime = Time.time;
            isRegenerating = false;
        }
        lastHp = Mathf.RoundToInt(currentHp);
    }

    private void TryBlink()
    {
        if (spawner == null || rb == null) return;

        GameObject player = GameObject.FindWithTag("Player");

        for (int i = 0; i < 10; i++)
        {
            Vector3 position = spawner.GetRandomSpawnPosition();
            position.y = rb.position.y;

            if (player != null && Vector3.Distance(position, player.transform.position) < BlinkMinPlayerDistance) continue;
            if (!spawner.IsValidSpawnPosition(position)) continue;

            transform.DOKill();
            transform.DOScale(baseScale * 0.2f, 0.12f).OnComplete(() =>
            {
                if (enemy == null || enemy.IsDead) return;

                rb.position = position;
                transform.position = position;
                transform.DOScale(baseScale, 0.15f).SetEase(Ease.OutBack);
                ShowPopup(transform, position);  //순간이동 직후
            });

            SoundManager.Instance?.PlaySFX(SoundId.EnemyShoot);
            return;
        }
    }

    public void OnBeforeEnemyDeath(EnemyController deadEnemy)  //방 클리어 판정 전에 분열 소환
    {
        if (trait != EliteTrait.Split || spawner == null) return;

        ShowPopup(null, transform.position);  //적은 곧 사라지므로 위치 고정

        for (int i = 0; i < SplitCount; i++)
        {
            spawner.SpawnNormalEnemyNear(transform.position, 0.8f, 2f, 1.5f, roomNumber);
        }
    }

    private void ShowPopup(Transform follow, Vector3 position)
    {
        if (trait == EliteTrait.None) return;

        if (popup == null) popup = EliteTraitPopup.Create();
        if (popup == null) return;

        popup.Show(GetTraitName(trait), GetTraitColor(trait), follow, position);
    }

    private static string GetTraitName(EliteTrait value)
    {
        string key = value switch
        {
            EliteTrait.Split => "elite_trait_split",
            EliteTrait.Blink => "elite_trait_blink",
            EliteTrait.Regen => "elite_trait_regen",
            EliteTrait.Swift => "elite_trait_swift",
            _ => null
        };

        if (key == null) return "";

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString("UI", key);
        return string.IsNullOrEmpty(localized) ? value.ToString() : localized;
    }

    private static Color GetTraitColor(EliteTrait value)
    {
        return value switch
        {
            EliteTrait.Split => new Color(0.95f, 0.45f, 0.1f),
            EliteTrait.Blink => new Color(0.62f, 0.3f, 0.95f),
            EliteTrait.Regen => new Color(0.2f, 0.72f, 0.3f),
            EliteTrait.Swift => new Color(0.15f, 0.58f, 0.95f),
            _ => Color.white
        };
    }

    private void OnDisable()  //풀 반환: 다음에 특성 없이 재사용될 수 있도록 정리
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;

        transform.DOKill();
        if (hasBaseScale) transform.localScale = baseScale;

        trait = EliteTrait.None;  //떠 있는 팝업은 스스로 사라진다 (분열 표시가 사망 직후에도 보이도록)
    }

    private void OnDestroy()
    {
        if (popup != null) Destroy(popup.gameObject);
    }
}
