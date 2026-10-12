using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

// 증강 스킬 '수호 별': 캐릭터 주위를 도는 별이 닿는 적에게 피해를 준다 (항상 회전, 적/벽 통과).
// 스킬 버튼을 누르면 궤도가 크게 퍼졌다가 원래대로 돌아온다 (쿨타임은 스킬 데이터).
// Lv1~3: 별 1→2→3개, Lv4부터: 피해량 증가와 회전 속도 증가를 번갈아 강화.
public class GuardianStarAugmentSkill : AugmentSkillRuntime
{
    [Header("Orbit")]
    [SerializeField] private Transform starTemplate;  //프리팹 안의 별 하나 (복제해서 개수 늘림)
    [SerializeField] private int maxStars = 3;
    [SerializeField] private float orbitRadius = 1.6f;
    [SerializeField] private float orbitHeight = 0.5f;
    [SerializeField] private float baseRotationSpeed = 200f;  //초당 각도
    [SerializeField] private float rotationSpeedMultiplierPerUpgrade = 1.25f;
    [SerializeField] private float starSpinSpeed = 360f;

    [Header("Expand (스킬 버튼)")]
    [SerializeField] private float expandRadius = 3.6f;  //경기장 벽까지 절반 이상
    [SerializeField] private float expandOutDuration = 0.3f;
    [SerializeField] private float expandHoldDuration = 0.5f;
    [SerializeField] private float expandBackDuration = 0.4f;
    [SerializeField] private float expandSpeedMultiplier = 1.5f;

    [Header("Damage")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private float baseDamage = 6f;
    [SerializeField] private float damageIncreasePerUpgrade = 3f;
    [SerializeField] private float hitIntervalPerEnemy = 0.5f;  //같은 별이 같은 적을 다시 때리기까지

    [Header("Localization")]
    [SerializeField] private LocalizedString countUpgradeText;
    [SerializeField] private LocalizedString damageUpgradeText;
    [SerializeField] private LocalizedString speedUpgradeText;

    private readonly List<Transform> stars = new();
    private readonly List<Dictionary<int, float>> nextHitTimes = new();
    private readonly Collider[] hitBuffer = new Collider[16];
    private readonly List<Transform> needsTrailClear = new();

    private Transform player;
    private Camera mainCamera;
    private float angle;
    private float currentDamage;
    private float currentRotationSpeed;
    private float currentRadius;
    private float speedMultiplier = 1f;
    private Coroutine expandRoutine;

    public override bool IsBusy => expandRoutine != null;

    public override void Initialize(AugmentSkillData skillData, Sprite icon)
    {
        base.Initialize(skillData, icon);

        PlayerBallController controller = GetComponentInParent<PlayerBallController>();
        player = controller != null ? controller.transform : transform.parent;

        currentDamage = baseDamage;
        currentRotationSpeed = baseRotationSpeed;
        currentRadius = orbitRadius;
        speedMultiplier = 1f;

        starTemplate.gameObject.SetActive(false);
        SetStarCount(1);
    }

    protected override void OnLevelUp()
    {
        if (Level <= maxStars)
        {
            SetStarCount(Level);
            return;
        }

        if (IsDamageUpgrade(Level)) currentDamage += damageIncreasePerUpgrade;
        else currentRotationSpeed *= rotationSpeedMultiplierPerUpgrade;
    }

    private bool IsDamageUpgrade(int level)  //Lv4 피해, Lv5 속도, Lv6 피해 ...
    {
        return (level - maxStars) % 2 == 1;
    }

    private void SetStarCount(int count)
    {
        while (stars.Count < count)
        {
            Transform star = Instantiate(starTemplate, transform);
            star.gameObject.SetActive(true);
            star.name = "Star" + stars.Count;
            stars.Add(star);
            nextHitTimes.Add(new Dictionary<int, float>());
            needsTrailClear.Add(star);  //궤도 위치로 옮긴 뒤 꼬리를 지워야 중심에서 그어지는 선이 안 생김
        }
    }

    protected override void Update()  //별은 항상 돌고, 쿨타임은 전투 중에만 흐름 (버튼 사용은 base.TryUseSkill)
    {
        if (data == null || player == null) return;

        bool canDamage = CanProgressSkill();
        if (canDamage) cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        angle = Mathf.Repeat(angle + currentRotationSpeed * speedMultiplier * Time.deltaTime, 360f);

        if (mainCamera == null) mainCamera = Camera.main;

        for (int i = 0; i < stars.Count; i++)
        {
            float starAngle = (angle + 360f / stars.Count * i) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(starAngle), 0f, Mathf.Sin(starAngle)) * currentRadius;

            Transform star = stars[i];
            star.position = player.position + offset + Vector3.up * orbitHeight;

            if (mainCamera != null)  //항상 카메라를 보면서 빙글빙글
                star.rotation = mainCamera.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * starSpinSpeed);

            if (canDamage) HitEnemies(i, star.position);
        }

        if (needsTrailClear.Count > 0)
        {
            foreach (Transform star in needsTrailClear)
            {
                TrailRenderer trail = star.GetComponentInChildren<TrailRenderer>();
                if (trail != null) trail.Clear();
            }
            needsTrailClear.Clear();
        }
    }

    private void HitEnemies(int starIndex, Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, hitRadius, hitBuffer, enemyLayer, QueryTriggerInteraction.Ignore);
        Dictionary<int, float> hitTimes = nextHitTimes[starIndex];

        for (int i = 0; i < count; i++)
        {
            GameObject enemy = hitBuffer[i].attachedRigidbody != null ? hitBuffer[i].attachedRigidbody.gameObject : hitBuffer[i].gameObject;
            int id = enemy.GetInstanceID();

            if (hitTimes.TryGetValue(id, out float nextTime) && Time.time < nextTime) continue;

            hitTimes[id] = Time.time + hitIntervalPerEnemy;
            DamageManager.Instance?.ApplyFixedDamage(enemy, currentDamage, DamageTextType.Normal);
        }
    }

    protected override bool ActivateSkill()  //스킬 버튼: 궤도가 크게 퍼졌다가 돌아옴
    {
        if (expandRoutine != null || stars.Count == 0) return false;

        expandRoutine = StartCoroutine(ExpandRoutine());
        return true;
    }

    private void OnDisable()  //확장 도중 꺼져도 다음에 정상 궤도/버튼 사용 가능하게
    {
        expandRoutine = null;
        currentRadius = orbitRadius;
        speedMultiplier = 1f;
    }

    private IEnumerator ExpandRoutine()
    {
        yield return Tween(orbitRadius, expandRadius, 1f, expandSpeedMultiplier, expandOutDuration);
        yield return new WaitForSeconds(expandHoldDuration);
        yield return Tween(expandRadius, orbitRadius, expandSpeedMultiplier, 1f, expandBackDuration);

        currentRadius = orbitRadius;
        speedMultiplier = 1f;
        expandRoutine = null;
    }

    private IEnumerator Tween(float fromRadius, float toRadius, float fromSpeed, float toSpeed, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            currentRadius = Mathf.Lerp(fromRadius, toRadius, p);
            speedMultiplier = Mathf.Lerp(fromSpeed, toSpeed, p);
            yield return null;
        }

        currentRadius = toRadius;
        speedMultiplier = toSpeed;
    }

    public override string GetNextLevelDescription()
    {
        int nextLevel = Level + 1;

        if (nextLevel <= maxStars) return countUpgradeText.GetLocalizedString();

        return IsDamageUpgrade(nextLevel)
            ? damageUpgradeText.GetLocalizedString()
            : speedUpgradeText.GetLocalizedString();
    }
}
