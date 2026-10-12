using System;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

// 방마다 랜덤 배치되는 반투명 장애물 벽.
// 플레이어가 부딪힐 때마다 금 간 텍스처가 단계적으로 진해지고, 마지막 타격에 미리 쪼개둔 조각들이 바닥으로 흩어진 뒤 사라진다.
// 조각 메시와 금 텍스처는 BreakableWallGenerator(에디터 메뉴)가 같은 절단선으로 생성하므로 금 간 자리대로 갈라진다.
// 모바일 부담을 줄이려고 파편은 물리 없이 DOTween으로만 연출하고, 충돌체는 루트의 BoxCollider 하나만 쓴다.
public class BreakableWall : MonoBehaviour
{
    [Serializable]
    public class FractureVariant
    {
        public GameObject shardRoot;  //평소엔 비활성, 파괴 시 활성
        public Transform[] shards;
        public Texture2D[] crackStages;  //금 간 단계별 텍스처 (점점 진해짐)
    }

    [Header("Durability")]
    [SerializeField] private int maxHits = 4;
    [SerializeField] private float hitCooldown = 0.12f;  //한 번 부딪힐 때 접촉이 여러 번 들어오는 것 방지

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;  //흔들림 연출용 (루트를 흔들면 충돌체도 흔들림)
    [SerializeField] private Renderer bodyRenderer;  //부서지기 전 통짜 벽
    [SerializeField] private Renderer[] crackRenderers;  //앞/뒷면 금 오버레이
    [SerializeField] private FractureVariant[] variants;
    [SerializeField] private Color intactColor = new Color(0.62f, 0.78f, 0.9f, 0.55f);
    [SerializeField] private Color damagedColor = new Color(0.42f, 0.5f, 0.58f, 0.6f);

    [Header("Debris")]
    [SerializeField] private float debrisFlyDuration = 0.5f;
    [SerializeField] private float debrisLifetime = 2.5f;
    [SerializeField] private float debrisShrinkDuration = 0.35f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private BoxCollider wallCollider;
    private Vector3 baseColliderSize;
    private float lengthScale = 1f;  //벽 길이 배율 (루트를 스케일하면 회전하는 파편이 찌그러지므로 Visual과 콜라이더만 조절)
    private MaterialPropertyBlock bodyBlock;
    private MaterialPropertyBlock crackBlock;
    private Vector3[][] shardLocalPositions;
    private Quaternion[][] shardLocalRotations;
    private Vector3[][] shardLocalScales;

    private FractureVariant currentVariant;
    private int hitCount;
    private float lastHitTime;
    private bool isBroken;

    public bool IsBroken => isBroken;
    public float HalfLength => baseColliderSize.x * 0.5f * lengthScale;
    private Vector3 LengthVector => new Vector3(lengthScale, 1f, 1f);

    private int MaxHits
    {
        get
        {
#if UNITY_EDITOR
            int overrideHits = DebugTestSettings.WallHitsOverride;  //Tools > BounceSquad Debug
            if (overrideHits > 0) return overrideHits;
#endif
            return Mathf.Max(1, maxHits);
        }
    }

    private void Awake()
    {
        wallCollider = GetComponent<BoxCollider>();
        baseColliderSize = wallCollider != null ? wallCollider.size : Vector3.one;
        bodyBlock = new MaterialPropertyBlock();
        crackBlock = new MaterialPropertyBlock();

        shardLocalPositions = new Vector3[variants.Length][];
        shardLocalRotations = new Quaternion[variants.Length][];
        shardLocalScales = new Vector3[variants.Length][];

        for (int v = 0; v < variants.Length; v++)
        {
            Transform[] shards = variants[v].shards;
            shardLocalPositions[v] = new Vector3[shards.Length];
            shardLocalRotations[v] = new Quaternion[shards.Length];
            shardLocalScales[v] = new Vector3[shards.Length];

            for (int i = 0; i < shards.Length; i++)
            {
                shardLocalPositions[v][i] = shards[i].localPosition;
                shardLocalRotations[v][i] = shards[i].localRotation;
                shardLocalScales[v][i] = shards[i].localScale;
            }
        }
    }

    private void OnEnable()
    {
        ResetWall();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    // 풀에서 다시 꺼낼 때(OnEnable) 상태를 초기화하고 금 패턴을 새로 고른다.
    // 활성화 도중에는 계층 변경(SetParent)이 막히므로 조각 복원은 RestoreShards(풀 반환 직전)에서 한다.
    private void ResetWall()
    {
        KillTweens();

        hitCount = 0;
        isBroken = false;
        lastHitTime = -1f;

        if (wallCollider != null) wallCollider.enabled = true;
        if (bodyRenderer != null) bodyRenderer.enabled = true;
        ApplyLength();

        currentVariant = variants.Length > 0 ? variants[Random.Range(0, variants.Length)] : null;

        SetCrackStage(0);
        ApplyBodyColor(0f);
    }

    public void SetLength(float scale)  //RoomObstacleSpawner가 배치할 때 호출
    {
        lengthScale = Mathf.Max(0.1f, scale);
        ApplyLength();
    }

    private void ApplyLength()
    {
        if (visualRoot != null) visualRoot.localScale = LengthVector;
        if (wallCollider != null) wallCollider.size = new Vector3(baseColliderSize.x * lengthScale, baseColliderSize.y, baseColliderSize.z);
    }

    private void RestoreShards()  //흩어진 조각을 원래 자리로 되돌린다 (비활성화 전에 호출)
    {
        KillTweens();

        for (int v = 0; v < variants.Length; v++)
        {
            FractureVariant variant = variants[v];

            for (int i = 0; i < variant.shards.Length; i++)
            {
                Transform shard = variant.shards[i];
                shard.SetParent(variant.shardRoot.transform, false);
                shard.localPosition = shardLocalPositions[v][i];
                shard.localRotation = shardLocalRotations[v][i];
                shard.localScale = shardLocalScales[v][i];
                shard.gameObject.SetActive(true);
            }

            variant.shardRoot.SetActive(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;
        if (!collision.collider.CompareTag("Player")) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        lastHitTime = Time.time;
        hitCount++;

        if (hitCount >= MaxHits)
        {
            Break();
            return;
        }

        TakeHit();
    }

    private void TakeHit()
    {
        int stageCount = currentVariant != null ? currentVariant.crackStages.Length : 0;

        // 파괴 직전 타격에서 마지막 단계(가장 심한 금)가 되도록 균등 배분
        int stage = Mathf.Clamp(Mathf.CeilToInt((float)stageCount * hitCount / Mathf.Max(1, MaxHits - 1)), 1, stageCount);
        SetCrackStage(stage);
        ApplyBodyColor((float)hitCount / MaxHits);

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = LengthVector;
            visualRoot.DOPunchScale(new Vector3(0.06f, 0.1f, 0.06f), 0.2f, 8, 0.6f);
        }

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestSfx(SoundId.EnemyHit, priority: 1);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.EnemyHit);
        }
    }

    private void Break()
    {
        isBroken = true;

        if (wallCollider != null) wallCollider.enabled = false;
        if (bodyRenderer != null) bodyRenderer.enabled = false;
        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = LengthVector;
        }
        SetCrackStage(0);

        if (currentVariant != null)
        {
            currentVariant.shardRoot.SetActive(true);

            foreach (Transform shard in currentVariant.shards)
            {
                LaunchShard(shard);
            }
        }

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.RequestImpact(SoundId.RobotMissileImpact, 0.12f, 0.12f, priority: 2);
        }
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.RobotMissileImpact);
        }

        DOVirtual.DelayedCall(debrisFlyDuration + debrisLifetime + debrisShrinkDuration + 0.05f, ReturnToPool, false)
            .SetTarget(this);
    }

    private void LaunchShard(Transform shard)  //조각을 바깥쪽으로 튕겨 바닥에 떨어뜨린다 (물리 없이 트윈만 사용)
    {
        Vector3 startPosition = shard.position;

        // 벽 중심에서 조각 방향 + 벽 앞/뒤 중 한쪽으로 퍼지게
        Vector3 fromCenter = startPosition - (transform.position + Vector3.up * 0.6f);
        fromCenter.y = 0f;
        Vector3 sideways = transform.forward * (Random.value < 0.5f ? -1f : 1f);
        Vector3 direction = (fromCenter.normalized * 0.6f + sideways).normalized;

        float distance = Random.Range(0.4f, 1.1f);
        float landingHalfHeight = baseColliderSize.z * 0.5f;  //조각이 눕으면 벽 두께 방향이 위를 향함
        Vector3 landPosition = new Vector3(
            startPosition.x + direction.x * distance,
            transform.position.y + landingHalfHeight,
            startPosition.z + direction.z * distance);

        shard.SetParent(transform, true);  //visualRoot 흔들림과 분리
        shard.DOKill();  //조각은 MaterialPropertyBlock 없이 공유 머티리얼 그대로 (SRP Batcher 유지)

        Sequence sequence = DOTween.Sequence().SetTarget(shard);
        sequence.Append(shard.DOJump(landPosition, Random.Range(0.25f, 0.55f), 1, debrisFlyDuration).SetEase(Ease.OutQuad));
        sequence.Join(shard.DORotate(new Vector3(Random.Range(60f, 100f) * (Random.value < 0.5f ? -1f : 1f), Random.Range(-40f, 40f), Random.Range(-30f, 30f)), debrisFlyDuration, RotateMode.LocalAxisAdd));
        sequence.AppendInterval(debrisLifetime);
        sequence.Append(shard.DOScale(Vector3.zero, debrisShrinkDuration));
        sequence.OnComplete(() => shard.gameObject.SetActive(false));
    }

    private void SetCrackStage(int stage)  //0이면 금 없음
    {
        bool visible = stage > 0 && currentVariant != null && stage <= currentVariant.crackStages.Length;

        if (visible) crackBlock.SetTexture(BaseMapId, currentVariant.crackStages[stage - 1]);

        foreach (Renderer crackRenderer in crackRenderers)
        {
            if (crackRenderer == null) continue;

            crackRenderer.enabled = visible;
            if (visible) crackRenderer.SetPropertyBlock(crackBlock);
        }
    }

    private void ApplyBodyColor(float damageRatio)
    {
        if (bodyRenderer == null) return;

        bodyBlock.SetColor(BaseColorId, Color.Lerp(intactColor, damagedColor, damageRatio));
        bodyRenderer.SetPropertyBlock(bodyBlock);
    }

    private void KillTweens()
    {
        DOTween.Kill(this);
        if (visualRoot != null) visualRoot.DOKill();

        if (variants == null) return;

        foreach (FractureVariant variant in variants)
        {
            foreach (Transform shard in variant.shards)
            {
                if (shard == null) continue;
                DOTween.Kill(shard);
                shard.DOKill();
            }
        }
    }

    public void ReturnToPool()
    {
        RestoreShards();

        PooledObject pooledObject = GetComponent<PooledObject>();

        if (pooledObject != null)
        {
            pooledObject.ReturnToPool();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
