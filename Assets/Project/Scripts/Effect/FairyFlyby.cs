using System.Collections;
using UnityEngine;

public enum FairyMode
{
    Flyby,  //가끔 화면을 가로질러 지나감 (로비)
    Hover   //제자리에서 둥둥 떠다님 (스테이지 배경)
}

// 작은 요정 무리(빛 + 반짝이 꼬리) 연출. 게임 로직과 무관.
// 자식 "Fairy*" 오브젝트들이 무리의 각 요정이며, 서로 조금씩 따로 흔들린다.
public class FairyFlyby : MonoBehaviour
{
    [SerializeField] private FairyMode mode = FairyMode.Flyby;

    [Header("Flyby")]
    [SerializeField] private Vector3 areaCenter = new Vector3(0f, 1.8f, -1.5f);
    [SerializeField] private Vector3 areaSize = new Vector3(9.5f, 1f, 16f);
    [SerializeField] private Vector2 intervalRange = new Vector2(10f, 20f);
    [SerializeField] private Vector2 flightDurationRange = new Vector2(2.2f, 3.6f);
    [SerializeField] private float waveAmplitude = 0.8f;
    [SerializeField] private float waveCycles = 1.5f;
    [SerializeField] private float firstDelay = 4f;

    [Header("Members")]
    [SerializeField] private float memberBobAmplitude = 0.25f;
    [SerializeField] private float memberBobSpeed = 1.3f;

    private Transform[] members;
    private Vector3[] memberBase;
    private ParticleSystem[] systems;
    private float seed;

    private void Awake()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform) if (child.name.StartsWith("Fairy")) list.Add(child);
        members = list.ToArray();

        memberBase = new Vector3[members.Length];
        for (int i = 0; i < members.Length; i++) memberBase[i] = members[i].localPosition;

        systems = GetComponentsInChildren<ParticleSystem>(true);
        seed = Random.Range(0f, 100f);
    }

    private void OnEnable()
    {
        if (mode == FairyMode.Hover)
        {
            SetEmitting(true);
            return;
        }

        SetEmitting(false);
        StartCoroutine(FlyLoop());
    }

    private void Update()  //무리 안에서 각자 살랑살랑
    {
        float time = Time.time * memberBobSpeed + seed;

        for (int i = 0; i < members.Length; i++)
        {
            Vector3 bob = new Vector3(
                Mathf.Sin(time + i * 2.1f),
                Mathf.Sin(time * 1.3f + i * 1.7f) * 0.6f,
                Mathf.Cos(time * 0.9f + i * 2.9f));

            members[i].localPosition = memberBase[i] + bob * memberBobAmplitude;
        }
    }

    private IEnumerator FlyLoop()
    {
        yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            yield return Fly();
            yield return new WaitForSeconds(Random.Range(intervalRange.x, intervalRange.y));
        }
    }

    private IEnumerator Fly()
    {
        bool leftToRight = Random.value < 0.5f;
        float halfX = areaSize.x * 0.5f + 1.5f;  //화면 밖에서 들어와 밖으로 나가도록 여유
        Vector3 start = areaCenter + new Vector3(leftToRight ? -halfX : halfX, Random.Range(-0.5f, 0.5f) * areaSize.y, Random.Range(-0.5f, 0.5f) * areaSize.z);
        Vector3 end = areaCenter + new Vector3(leftToRight ? halfX : -halfX, Random.Range(-0.5f, 0.5f) * areaSize.y, Random.Range(-0.5f, 0.5f) * areaSize.z);

        Vector3 direction = (end - start).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        float duration = Random.Range(flightDurationRange.x, flightDurationRange.y);
        float phase = Random.Range(0f, Mathf.PI * 2f);

        transform.position = start;
        SetEmitting(true);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float wave = Mathf.Sin(p * Mathf.PI * 2f * waveCycles + phase) * waveAmplitude;
            float bob = Mathf.Sin(p * Mathf.PI * 4f * waveCycles + phase) * waveAmplitude * 0.35f;
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, p)) + side * wave + Vector3.up * bob;
            yield return null;
        }

        SetEmitting(false);  //꼬리 입자는 남아서 자연스럽게 사라짐
    }

    private void SetEmitting(bool emitting)
    {
        foreach (ParticleSystem system in systems)
        {
            if (system == null) continue;

            if (emitting) system.Play(false);
            else system.Stop(false, system.main.simulationSpace == ParticleSystemSimulationSpace.Local
                ? ParticleSystemStopBehavior.StopEmittingAndClear   //본체 빛은 바로 사라짐
                : ParticleSystemStopBehavior.StopEmitting);         //꼬리는 남은 입자가 자연스럽게 사라짐
        }
    }
}
