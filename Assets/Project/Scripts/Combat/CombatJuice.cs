using System.Collections.Generic;
using UnityEngine;

// 전투를 정신없고 볼거리 많게 만드는 연출 모음 (씬 로컬, RoomManager.Start에서 생성).
// - 처치: 별 조각 폭발, 적이 맞은 방향으로 날아감, 히트스톱, 화면 흔들림, 코인 흩뿌림(연출만, 골드 계산은 그대로), 바닥 그을음
// - 연속 처치(DOUBLE/TRIPLE...)와 콤보 단계 문구, 피버(콤보 10+) 중 화면 가장자리 빛 + 공 꼬리 색 변화
// - 맞은 적: 하얗게 번쩍 + 말랑하게 찌그러짐 + 충돌 지점 섬광/불꽃 (크리티컬은 더 크게, 아주 짧은 멈칫)
// - 퍼펙트 반사: 벽 접근 타이밍 링, 성공 시 금색 섬광/충격파/PERFECT 문구, 충전 중 금색 꼬리와 링, 충전 타격은 더 크게
// 게임 규칙(피해, 골드, 방 진행)은 건드리지 않는다.
public class CombatJuice : MonoBehaviour
{
    private const float MultiKillWindow = 0.8f;
    private const int MaxCoins = 80;
    private const int MaxDecals = 40;
    private const float CoinScatterTime = 0.55f;
    private const float CoinSfxInterval = 0.06f;

    private static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.85f, 0.25f), new Color(1f, 0.45f, 0.35f), new Color(0.45f, 0.85f, 1f),
        new Color(0.6f, 1f, 0.5f), new Color(1f, 0.55f, 0.95f), Color.white
    };

    private static readonly Color[] FeverColors =
    {
        Color.white, new Color(1f, 0.75f, 0.2f), new Color(1f, 0.35f, 0.2f), new Color(1f, 0.3f, 0.9f)
    };

    private static CombatJuice instance;

    private CombatUiStyle style;
    private PlayerBallController player;
    private Rigidbody playerRb;
    private Camera mainCamera;

    private ParticleSystem burst;
    private ParticleSystem hitGlow;    //충돌 지점 섬광
    private ParticleSystem hitSparks;  //충돌 지점에서 튀는 길쭉한 불꽃
    private TrailRenderer trail;
    private CombatCalloutUI callouts;

    private static readonly Color PerfectGold = new Color(1f, 0.82f, 0.2f);
    private PlayerPerfectBounce perfectBounce;
    private JuiceRing approachRing;  //벽에 가까워지면 줄어드는 타이밍 링
    private JuiceRing chargeRing;    //퍼펙트 충전 중 공 주위 링
    private readonly JuiceRing[] waveRings = new JuiceRing[6];  //퍼펙트/폭발 충격파 (돌려 씀)
    private int waveCursor;

    private class Coin
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float age;
        public float floorY;
        public bool active;
    }

    private readonly List<Coin> coins = new List<Coin>();
    private float nextCoinSfxTime;

    private Sprite decalSprite;
    private readonly List<SpriteRenderer> decals = new List<SpriteRenderer>();
    private int decalCursor;
    private float decalFadeTimer = -1f;

    private int multiKillCount;
    private float lastKillTime = -999f;
    private int feverTier;
    private int lastMilestone;

    public static void Create()
    {
        if (instance != null) return;
        instance = new GameObject("CombatJuice").AddComponent<CombatJuice>();
    }

    private void Awake()
    {
        style = CombatUiStyle.Get();
        mainCamera = Camera.main;
        player = FindAnyObjectByType<PlayerBallController>();
        if (player != null) playerRb = player.GetComponent<Rigidbody>();
        if (player != null) perfectBounce = player.GetComponent<PlayerPerfectBounce>();

        CreateBurstParticles();
        CreateHitParticles();
        CreateTrail();
        CreateRings();
        decalSprite = CreateDecalSprite();
        callouts = new CombatCalloutUI();

        GameEvents.EnemyKilled += HandleEnemyKilled;
        GameEvents.ComboChanged += HandleComboChanged;
        GameEvents.RoomCleared += HandleRoomCleared;
        PlayerBallController.OnPlayerHitEnemy += HandlePlayerHitEnemy;
        PlayerBallController.OnPlayerHitWall += HandlePlayerHitWall;
        PlayerPerfectBounce.OnPerfect += HandlePerfectBounce;
        PlayerPerfectBounce.OnMiss += HandlePerfectMiss;
        AreaDamage.OnExplosion += HandleExplosion;
        LegendaryEffects.OnThunder += HandleLegendaryThunder;
        RunFlow.OnTrickShot += HandleTrickShot;
        PlayerBallController.OnShieldBlocked += HandleShieldBlocked;
        PlayerBallController.OnSpikeHurt += HandleSpikeHurt;
        EnemyRoleController.OnHealPulse += HandleHealPulse;
        BossController.OnPhaseChanged += HandleBossPhaseChanged;
        RoomGimmickEvents.OnBumper += HandleBumper;
        RoomGimmickEvents.OnPortal += HandlePortal;
    }

    private void OnDestroy()
    {
        GameEvents.EnemyKilled -= HandleEnemyKilled;
        GameEvents.ComboChanged -= HandleComboChanged;
        GameEvents.RoomCleared -= HandleRoomCleared;
        PlayerBallController.OnPlayerHitEnemy -= HandlePlayerHitEnemy;
        PlayerBallController.OnPlayerHitWall -= HandlePlayerHitWall;
        PlayerPerfectBounce.OnPerfect -= HandlePerfectBounce;
        PlayerPerfectBounce.OnMiss -= HandlePerfectMiss;
        AreaDamage.OnExplosion -= HandleExplosion;
        LegendaryEffects.OnThunder -= HandleLegendaryThunder;
        RunFlow.OnTrickShot -= HandleTrickShot;
        PlayerBallController.OnShieldBlocked -= HandleShieldBlocked;
        PlayerBallController.OnSpikeHurt -= HandleSpikeHurt;
        EnemyRoleController.OnHealPulse -= HandleHealPulse;
        BossController.OnPhaseChanged -= HandleBossPhaseChanged;
        RoomGimmickEvents.OnBumper -= HandleBumper;
        RoomGimmickEvents.OnPortal -= HandlePortal;

        callouts?.Dispose();
        if (instance == this) instance = null;
    }

    // ───────── 이벤트 ─────────

    private void HandleEnemyKilled(EnemyController enemy, bool isBoss)
    {
        if (enemy == null) return;

        Vector3 position = enemy.transform.position;
        bool isElite = !isBoss && enemy.TryGetComponent(out EliteTraitController elite) && elite.HasTrait;
        float floorY = GetFloorY(position);

        EmitBurst(position + Vector3.up * 0.6f, isBoss ? 36 : isElite ? 16 : 9, isBoss ? 1.2f : 0.75f);
        SpawnCoins(position + Vector3.up * 0.6f, floorY, isBoss ? 8 : isElite ? 3 : 2);
        SpawnDecal(new Vector3(position.x, floorY + 0.03f, position.z), isBoss ? 2.4f : isElite ? 1.5f : 1.1f);

        if (!isBoss)
        {
            Vector3 away = player != null ? position - player.transform.position : Vector3.zero;
            GetJuiceState(enemy.gameObject).FlyAway(away);
        }

        multiKillCount = Time.time - lastKillTime <= MultiKillWindow ? multiKillCount + 1 : 1;
        lastKillTime = Time.time;

        if (multiKillCount >= 2)
        {
            callouts.ShowMultiKill(multiKillCount);
            SoundManager.Instance?.PlaySFX(SoundId.MultiKill);
        }

        HitStop.Play(isBoss ? 0.1f : multiKillCount >= 3 ? 0.06f : 0.035f);

        float shake = isBoss ? 0.35f : 0.06f + 0.025f * Mathf.Min(multiKillCount, 4);
        if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestShake(0.14f, shake);
        else CameraShake.Instance?.Shake(0.14f, shake);
    }

    private void HandlePlayerHitEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        bool critical = PlayerBallController.LastHitWasCritical;
        bool perfect = PlayerPerfectBounce.LastHitWasPerfect;

        EnemyJuiceState state = GetJuiceState(enemy);
        state.Flash(style != null ? style.hitFlashMaterial : null, critical ? 0.09f : 0.065f);
        state.Squash(critical ? 0.45f : 0.32f);

        Vector3 enemyPosition = enemy.transform.position;
        Vector3 away = player != null ? enemyPosition - player.transform.position : Vector3.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

        Vector3 contact = enemyPosition - away * 0.45f + Vector3.up * 0.55f;  //공과 적이 닿은 쪽
        EmitHitImpact(contact, away, critical || perfect);

        if (perfect)  //퍼펙트 충전 타격: 금색 섬광 + 충격파를 한 번 더
        {
            EmitPerfectFlash(contact, 1.3f);
            PlayWave(new Vector3(enemyPosition.x, contact.y, enemyPosition.z), 0.4f, 2.2f, PerfectGold);
            HitStop.Play(0.05f);
        }
        else if (critical) HitStop.Play(0.03f);
    }

    private void HandlePerfectBounce(Vector3 position, int chain)
    {
        float power = 1f + 0.15f * Mathf.Min(chain - 1, 4);

        EmitPerfectFlash(position + Vector3.up * 0.3f, power);
        EmitBurst(position + Vector3.up * 0.3f, 6 + 2 * Mathf.Min(chain, 5), 0.6f);
        PlayWave(position, 0.5f, 2.6f * power, PerfectGold);
        if (chain >= 2) PlayWave(position, 0.3f, 1.6f * power, Color.white);

        callouts.ShowPerfect(position, chain);
        HitStop.Play(0.045f);

        if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestImpact(SoundId.PerfectBounce, 0.1f, 0.07f, priority: 2);
        else
        {
            SoundManager.Instance?.PlaySFX(SoundId.PerfectBounce);
            CameraShake.Instance?.Shake(0.1f, 0.07f);
        }
    }

    // ───────── 전설 증강 연출 ─────────

    private static readonly Color ThunderColor = new Color(0.7f, 0.9f, 1f);

    // ───────── 트릭샷 / 특수 적 / 보스 페이즈 연출 ─────────

    private static readonly Color TrickColor = new Color(0.45f, 0.95f, 1f);
    private static readonly Color ShieldColor = new Color(0.55f, 0.8f, 1f);
    private static readonly Color SpikeColor = new Color(0.85f, 0.4f, 1f);
    private static readonly Color HealColor = new Color(0.4f, 1f, 0.5f);
    private static readonly Color RageColor = new Color(1f, 0.25f, 0.2f);

    private void HandleTrickShot(string text, int gold, Vector3 position)
    {
        callouts.ShowTrick(text, gold, position);
        EmitBurst(position + Vector3.up * 0.4f, 10, 0.7f);
        PlayWave(position, 0.4f, 2.4f, TrickColor);
        HitStop.Play(0.04f);
        SoundManager.Instance?.PlaySFX(SoundId.MultiKill);
    }

    private void HandleShieldBlocked(Vector3 position)  //방패에 막힘: 파란 불꽃 + BLOCK
    {
        EmitColoredSparks(position, ShieldColor, 10);
        callouts.ShowSmall(RunPopupUI.Loc("callout_block", "BLOCK"), ShieldColor, position);
    }

    private void HandleSpikeHurt(Vector3 position)  //가시에 찔림
    {
        EmitColoredSparks(position + Vector3.up * 0.3f, SpikeColor, 8);
        callouts.ShowSmall(RunPopupUI.Loc("callout_spike", "OUCH!"), SpikeColor, position);
    }

    private void HandleHealPulse(Vector3 position, float radius)  //치유사: 초록 링이 퍼짐
    {
        PlayWave(position, 0.3f, radius, new Color(HealColor.r, HealColor.g, HealColor.b, 0.7f));
    }

    private void HandleBossPhaseChanged(Transform boss, int phase)  //보스 분노: 붉은 충격파 두 번 + 흔들림 + 배너
    {
        if (boss == null || phase < 2) return;

        Vector3 position = boss.position;
        PlayWave(position, 0.5f, 4.5f, RageColor);
        PlayWave(position, 0.3f, 3f, new Color(1f, 0.7f, 0.3f));
        EmitColoredSparks(position + Vector3.up * 0.8f, RageColor, 20);
        HitStop.Play(0.08f);

        if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestShake(0.35f, 0.25f);
        else CameraShake.Instance?.Shake(0.35f, 0.25f);

        HudBanner.Show(RunPopupUI.Loc("boss_rage_title", "WARNING"), RunPopupUI.Loc("boss_rage_name", "The boss is enraged!"));
    }

    private void HandleBumper(Vector3 position)  //범퍼: 초록 링
    {
        PlayWave(position, 0.6f, 2f, new Color(0.3f, 1f, 0.65f));
        EmitColoredSparks(position + Vector3.up * 0.5f, new Color(0.3f, 1f, 0.65f), 6);
    }

    private void HandlePortal(Vector3 from, Vector3 to)  //포털: 들어간 곳은 오므라들고 나온 곳은 퍼짐
    {
        PlayWave(from, 1.6f, 0.3f, new Color(0.3f, 0.85f, 1f));
        PlayWave(to, 0.3f, 1.8f, new Color(1f, 0.6f, 0.2f));
        EmitColoredSparks(to + Vector3.up * 0.3f, new Color(0.6f, 0.8f, 1f), 10);
    }

    private void EmitColoredSparks(Vector3 position, Color color, int count)
    {
        if (hitGlow == null) return;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { position = position, velocity = Vector3.zero, startSize = 1.6f, startColor = color };
        hitGlow.Emit(emit, 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            direction.y = Random.Range(0.1f, 0.6f);
            emit.velocity = direction.normalized * Random.Range(8f, 14f);
            emit.startSize = Random.Range(0.12f, 0.2f);
            emit.startColor = Random.value < 0.4f ? Color.white : color;
            hitSparks.Emit(emit, 1);
        }
    }

    private void HandleExplosion(Vector3 position, float radius, Color color)  //범위 피해(AreaDamage): 섬광 + 퍼지는 링 (반경에 비례)
    {
        if (hitGlow != null)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { position = position + Vector3.up * 0.4f, velocity = Vector3.zero };
            emit.startSize = radius * 1.6f;
            emit.startColor = color;
            hitGlow.Emit(emit, 1);
            emit.startSize = radius * 0.7f;
            emit.startColor = new Color(1f, 0.95f, 0.8f);
            hitGlow.Emit(emit, 1);
        }

        EmitBurst(position + Vector3.up * 0.3f, Mathf.RoundToInt(3 + radius * 2f), 0.4f + radius * 0.1f);
        PlayWave(position, 0.2f, radius, color);

        float shake = Mathf.Clamp(radius * 0.025f, 0.04f, 0.12f);
        if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestShake(0.1f, shake);
    }

    private void HandleLegendaryThunder(Vector3 position)  //하늘에서 지그재그 번개
    {
        LineRenderer bolt = GetBolt();
        if (bolt == null) return;

        Vector3 top = position + new Vector3(Random.Range(-1f, 1f), 9f, Random.Range(-1f, 1f));
        int count = bolt.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = i / (count - 1f);
            Vector3 point = Vector3.Lerp(top, position, t);
            if (i > 0 && i < count - 1) point += new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f));
            bolt.SetPosition(i, point);
        }

        bolt.enabled = true;
        boltAges[bolts.IndexOf(bolt)] = 0f;

        EmitPerfectFlash(position + Vector3.up * 0.4f, 0.7f);
        if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.RequestShake(0.1f, 0.06f);
    }

    private const float BoltDuration = 0.18f;
    private readonly List<LineRenderer> bolts = new List<LineRenderer>();
    private readonly List<float> boltAges = new List<float>();

    private LineRenderer GetBolt()
    {
        for (int i = 0; i < bolts.Count; i++) if (!bolts[i].enabled) return bolts[i];
        if (bolts.Count >= 4) return bolts[0];

        GameObject go = new GameObject("ThunderBolt");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 8;
        line.widthMultiplier = 0.22f;
        line.numCornerVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        line.sharedMaterial = shader != null ? new Material(shader) : (style != null ? style.trailMaterial : null);
        line.enabled = false;

        bolts.Add(line);
        boltAges.Add(BoltDuration);
        return line;
    }

    private void UpdateBolts()
    {
        for (int i = 0; i < bolts.Count; i++)
        {
            if (!bolts[i].enabled) continue;

            boltAges[i] += Time.unscaledDeltaTime;
            float t = boltAges[i] / BoltDuration;
            if (t >= 1f)
            {
                bolts[i].enabled = false;
                continue;
            }

            Color color = ThunderColor;
            color.a = 1f - t;
            bolts[i].startColor = color;
            bolts[i].endColor = new Color(1f, 1f, 1f, 1f - t);
        }
    }

    private void HandlePerfectMiss(Vector3 position)
    {
        callouts.ShowMiss(position);
        PlayWave(position, 0.9f, 0.5f, new Color(0.7f, 0.75f, 0.85f, 0.5f));  //안쪽으로 오므라드는 회색 링
    }

    private void HandlePlayerHitWall()
    {
        if (player == null) return;
        EmitBurst(player.transform.position + Vector3.up * 0.3f, 3 + feverTier * 2, 0.45f);
    }

    private void HandleComboChanged(int combo)
    {
        int newTier = combo >= 30 ? 3 : combo >= 20 ? 2 : combo >= 10 ? 1 : 0;

        if (newTier != feverTier)
        {
            feverTier = newTier;
            callouts.SetFever(feverTier, FeverColors[feverTier]);
        }

        if (combo == 0)
        {
            lastMilestone = 0;
            return;
        }

        int milestone = combo / 10 * 10;
        if (milestone >= 10 && milestone > lastMilestone)
        {
            lastMilestone = milestone;
            callouts.ShowMilestone(milestone, FeverColors[Mathf.Max(1, feverTier)]);
            SoundManager.Instance?.PlaySFX(SoundId.ComboMilestone);
        }
    }

    private void HandleRoomCleared(int roomNumber, bool isEndless)
    {
        decalFadeTimer = 0.8f;  //다음 방 전에 그을음을 지움
    }

    private static EnemyJuiceState GetJuiceState(GameObject enemy)
    {
        EnemyJuiceState state = enemy.GetComponent<EnemyJuiceState>();
        return state != null ? state : enemy.AddComponent<EnemyJuiceState>();
    }

    // ───────── 매 프레임 ─────────

    private void Update()
    {
        UpdateCoins();
        UpdateDecalFade();
        UpdateTrail();
        UpdateRings();
        UpdateBolts();
        callouts.Update();
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    // ───────── 별 조각 폭발 ─────────

    private void CreateBurstParticles()
    {
        GameObject go = new GameObject("KillBurst");
        go.transform.SetParent(transform, false);

        burst = go.AddComponent<ParticleSystem>();
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = burst.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = 0f;  //속도는 Emit에서 직접 지정
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.4f;
        main.maxParticles = 800;

        ParticleSystem.EmissionModule emission = burst.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = burst.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = burst.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = burst.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystem.RotationOverLifetimeModule rotation = burst.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (style != null && style.burstParticleMaterial != null) renderer.sharedMaterial = style.burstParticleMaterial;

        burst.Play();
    }

    private void EmitBurst(Vector3 position, int count, float power)
    {
        if (burst == null || count <= 0) return;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) + 0.35f;

            emit.position = position;
            emit.velocity = direction.normalized * Random.Range(4f, 9f) * power;
            emit.startColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
            burst.Emit(emit, 1);
        }
    }

    // ───────── 피격 섬광 / 불꽃 ─────────

    private void CreateHitParticles()
    {
        Material glowMaterial = style != null ? style.trailMaterial : null;  //부드러운 원형 빛

        hitGlow = CreateSimpleParticles("HitGlow", ParticleSystemRenderMode.Billboard, glowMaterial, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        ParticleSystem.MainModule glowMain = hitGlow.main;
        glowMain.startLifetime = 0.1f;

        hitSparks = CreateSimpleParticles("HitSparks", ParticleSystemRenderMode.Stretch, glowMaterial, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        ParticleSystem.MainModule sparkMain = hitSparks.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);

        ParticleSystemRenderer sparkRenderer = hitSparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.velocityScale = 0.045f;
        sparkRenderer.lengthScale = 1f;
    }

    private ParticleSystem CreateSimpleParticles(string name, ParticleSystemRenderMode renderMode, Material material, AnimationCurve sizeCurve)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        if (material != null) renderer.sharedMaterial = material;

        system.Play();
        return system;
    }

    private void EmitHitImpact(Vector3 position, Vector3 away, bool critical)
    {
        if (hitGlow == null) return;

        Color tint = critical ? new Color(1f, 0.85f, 0.3f) : Color.white;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { position = position, velocity = Vector3.zero };

        emit.startSize = critical ? 2.6f : 1.8f;  //큰 섬광 1개
        emit.startColor = tint;
        hitGlow.Emit(emit, 1);

        emit.startSize = critical ? 1.1f : 0.8f;  //가운데 더 밝은 심
        emit.startColor = Color.white;
        hitGlow.Emit(emit, 1);

        int sparkCount = critical ? 12 : 7;
        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away;  //맞은 방향으로 부채꼴
            direction.y = Random.Range(0.1f, 0.6f);

            emit.velocity = direction.normalized * Random.Range(9f, 16f) * (critical ? 1.25f : 1f);
            emit.startSize = Random.Range(0.12f, 0.2f);
            emit.startColor = Random.value < 0.5f ? Color.white : tint == Color.white ? new Color(1f, 0.95f, 0.7f) : tint;
            hitSparks.Emit(emit, 1);
        }
    }

    // ───────── 코인 ─────────

    private void SpawnCoins(Vector3 position, float floorY, int count)
    {
        if (style == null || style.coinSprite == null) return;

        for (int i = 0; i < count; i++)
        {
            Coin coin = GetFreeCoin();
            if (coin == null) return;

            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f);
            coin.velocity = new Vector3(flat.x, Random.Range(5f, 8f), flat.y);
            coin.transform.position = position;
            coin.floorY = floorY;
            coin.age = -Random.Range(0f, 0.08f);  //조금씩 엇갈려 튀어나옴
            coin.active = true;
            coin.renderer.color = Color.white;
            coin.transform.gameObject.SetActive(true);
        }
    }

    private Coin GetFreeCoin()
    {
        foreach (Coin coin in coins)
        {
            if (!coin.active) return coin;
        }

        if (coins.Count >= MaxCoins) return null;

        GameObject go = new GameObject("Coin");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * 0.45f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = style.coinSprite;
        renderer.sortingOrder = 5;

        Coin newCoin = new Coin { transform = go.transform, renderer = renderer };
        coins.Add(newCoin);
        return newCoin;
    }

    private void UpdateCoins()
    {
        float dt = Time.deltaTime;
        Vector3 target = player != null ? player.transform.position + Vector3.up * 0.5f : Vector3.zero;
        Quaternion facing = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;

        foreach (Coin coin in coins)
        {
            if (!coin.active) continue;

            coin.age += dt;
            Transform t = coin.transform;

            if (coin.age < CoinScatterTime || player == null)
            {
                coin.velocity.y -= 22f * dt;
                Vector3 position = t.position + coin.velocity * dt;

                if (position.y < coin.floorY + 0.2f)  //바닥에서 통통 튐
                {
                    position.y = coin.floorY + 0.2f;
                    coin.velocity.y = Mathf.Abs(coin.velocity.y) * 0.45f;
                    coin.velocity.x *= 0.7f;
                    coin.velocity.z *= 0.7f;
                }

                t.position = position;

                if (player == null && coin.age > 1.2f) coin.active = false;
            }
            else  //플레이어에게 빨려감
            {
                float speed = 8f + (coin.age - CoinScatterTime) * 40f;
                t.position = Vector3.MoveTowards(t.position, target, speed * dt);

                if ((t.position - target).sqrMagnitude < 0.25f)
                {
                    coin.active = false;
                    PlayCoinSfx();
                }
            }

            if (!coin.active)
            {
                t.gameObject.SetActive(false);
                continue;
            }

            t.rotation = facing;
            float flip = Mathf.Abs(Mathf.Cos(coin.age * 12f));  //동전이 도는 느낌
            t.localScale = new Vector3(0.45f * Mathf.Max(0.15f, flip), 0.45f, 0.45f);
        }
    }

    private void PlayCoinSfx()
    {
        if (Time.unscaledTime < nextCoinSfxTime) return;

        nextCoinSfxTime = Time.unscaledTime + CoinSfxInterval;
        SoundManager.Instance?.PlaySFX(SoundId.CoinCollect);
    }

    // ───────── 바닥 그을음 ─────────

    private float GetFloorY(Vector3 position)
    {
        int mask = ~LayerMask.GetMask("Enemy", "Player");

        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f, mask, QueryTriggerInteraction.Ignore)) return hit.point.y;

        return position.y - 0.4f;
    }

    private void SpawnDecal(Vector3 position, float size)
    {
        if (decalSprite == null) return;

        SpriteRenderer decal;

        if (decals.Count < MaxDecals)
        {
            GameObject go = new GameObject("ScorchDecal");
            go.transform.SetParent(transform, false);
            decal = go.AddComponent<SpriteRenderer>();
            decal.sprite = decalSprite;
            decal.sortingOrder = -5;
            decals.Add(decal);
        }
        else
        {
            decal = decals[decalCursor];  //가장 오래된 것부터 재사용
            decalCursor = (decalCursor + 1) % MaxDecals;
        }

        decal.gameObject.SetActive(true);
        decal.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        decal.transform.localScale = Vector3.one * size * Random.Range(0.85f, 1.15f);
        decal.color = new Color(0.12f, 0.08f, 0.14f, 0.26f);

        decalFadeTimer = -1f;
    }

    private void UpdateDecalFade()
    {
        if (decalFadeTimer < 0f) return;

        decalFadeTimer -= Time.unscaledDeltaTime;
        float alpha = Mathf.Clamp01(decalFadeTimer / 0.8f) * 0.26f;

        foreach (SpriteRenderer decal in decals)
        {
            if (!decal.gameObject.activeSelf) continue;

            Color c = decal.color;
            c.a = Mathf.Min(c.a, alpha);
            decal.color = c;

            if (decalFadeTimer <= 0f) decal.gameObject.SetActive(false);
        }

        if (decalFadeTimer <= 0f) decalFadeTimer = -1f;
    }

    private static Sprite CreateDecalSprite()  //가장자리가 들쭉날쭉한 얼룩 + 주변 작은 방울
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        float[] lobes = new float[7];
        for (int i = 0; i < lobes.Length; i++) lobes[i] = Random.Range(0f, Mathf.PI * 2f);

        Vector2[] drops = new Vector2[6];
        for (int i = 0; i < drops.Length; i++) drops[i] = Random.insideUnitCircle.normalized * Random.Range(0.6f, 0.85f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x - center) / center, (y - center) / center);
                float angle = Mathf.Atan2(p.y, p.x);

                float radius = 0.5f;
                for (int i = 0; i < lobes.Length; i++) radius += 0.045f * Mathf.Sin(angle * (i + 2) + lobes[i]);

                float alpha = Mathf.Clamp01((radius - p.magnitude) / 0.12f);

                foreach (Vector2 drop in drops) alpha = Mathf.Max(alpha, Mathf.Clamp01((0.07f - (p - drop).magnitude) / 0.03f));

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ───────── 공 꼬리 ─────────

    private void CreateTrail()
    {
        if (player == null || style == null || style.trailMaterial == null) return;

        GameObject go = new GameObject("JuiceTrail");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = Vector3.up * 0.3f;

        trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = style.trailMaterial;
        trail.time = 0.2f;
        trail.minVertexDistance = 0.08f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;
    }

    private void UpdateTrail()
    {
        if (trail == null || playerRb == null) return;

        Vector3 velocity = playerRb.linearVelocity;
        velocity.y = 0f;

        bool playing = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing;
        bool fast = velocity.magnitude > 7f;
        trail.emitting = playing && (fast || feverTier > 0);

        bool charged = perfectBounce != null && perfectBounce.IsCharged;  //퍼펙트 충전 중엔 금색 굵은 꼬리
        if (charged) trail.emitting = playing;

        Color color = charged ? PerfectGold : FeverColors[feverTier];
        float alpha = charged ? 0.95f : feverTier > 0 ? 0.85f : 0.35f;
        trail.startColor = new Color(color.r, color.g, color.b, alpha);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.widthMultiplier = charged ? 0.6f + 0.12f * feverTier : feverTier > 0 ? 0.45f + 0.12f * feverTier : 0.3f;
        trail.time = (charged ? 0.3f : 0.2f) + 0.07f * feverTier;
    }

    // ───────── 퍼펙트 반사 ─────────

    private void CreateRings()
    {
        Material fallback = style != null ? style.trailMaterial : null;

        approachRing = new JuiceRing(transform, "PerfectApproachRing", fallback);
        chargeRing = new JuiceRing(transform, "PerfectChargeRing", fallback);
        for (int i = 0; i < waveRings.Length; i++) waveRings[i] = new JuiceRing(transform, "PerfectWave" + i, fallback);
    }

    private void UpdateRings()
    {
        for (int i = 0; i < waveRings.Length; i++) waveRings[i].UpdateWave();

        if (player == null || perfectBounce == null)
        {
            approachRing.Hide();
            chargeRing.Hide();
            return;
        }

        Vector3 center = player.transform.position;
        float timeToWall = perfectBounce.TimeToWall;

        if (timeToWall >= 0f && timeToWall <= perfectBounce.ApproachTime)  //벽에 닿는 순간 공 크기로 줄어듦
        {
            float t = timeToWall / perfectBounce.ApproachTime;
            bool inWindow = timeToWall <= perfectBounce.EarlyWindow;

            Color color = inWindow ? PerfectGold : Color.white;
            color.a = Mathf.Lerp(0.85f, 0.15f, t);
            approachRing.Show(center, Mathf.Lerp(0.55f, 1.9f, t), inWindow ? 0.12f : 0.07f, color);
        }
        else approachRing.Hide();

        if (perfectBounce.IsCharged)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 12f);
            Color color = PerfectGold;
            color.a = 0.65f + 0.25f * pulse;
            chargeRing.Show(center, 0.75f + 0.07f * pulse, 0.1f, color);
        }
        else chargeRing.Hide();
    }

    private void PlayWave(Vector3 center, float startRadius, float endRadius, Color color)
    {
        JuiceRing ring = waveRings[waveCursor];
        waveCursor = (waveCursor + 1) % waveRings.Length;
        ring.PlayWave(center, startRadius, endRadius, 0.3f, 0.35f, color);
    }

    private void EmitPerfectFlash(Vector3 position, float power)  //금색 큰 섬광 + 사방으로 튀는 불꽃
    {
        if (hitGlow == null) return;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { position = position, velocity = Vector3.zero };

        emit.startSize = 3.4f * power;
        emit.startColor = PerfectGold;
        hitGlow.Emit(emit, 1);

        emit.startSize = 1.5f * power;
        emit.startColor = Color.white;
        hitGlow.Emit(emit, 1);

        int sparkCount = Mathf.RoundToInt(16 * power);
        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 360f / sparkCount + Random.Range(-8f, 8f), 0f) * Vector3.forward;
            direction.y = Random.Range(0.05f, 0.4f);

            emit.velocity = direction.normalized * Random.Range(11f, 18f) * power;
            emit.startSize = Random.Range(0.14f, 0.24f);
            emit.startColor = Random.value < 0.4f ? Color.white : PerfectGold;
            hitSparks.Emit(emit, 1);
        }
    }
}
