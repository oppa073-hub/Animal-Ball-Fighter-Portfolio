using UnityEngine;

// 바닥과 평행한 원형 링 (LineRenderer). 퍼펙트 반사 타이밍 링 / 충전 링 / 충격파에 쓴다.
// 실행 중 생성하므로 에셋이 필요 없다.
public class JuiceRing
{
    private const int Segments = 48;

    private static Material sharedMaterial;

    private readonly LineRenderer line;
    private readonly Vector3[] points = new Vector3[Segments];

    // 충격파용: 실제 시간 기준으로 퍼지며 사라짐
    private float waveAge = -1f;
    private float waveDuration;
    private float waveStartRadius;
    private float waveEndRadius;
    private float waveStartWidth;
    private Vector3 waveCenter;
    private Color waveColor;

    public bool IsWaving => waveAge >= 0f;

    public JuiceRing(Transform parent, string name, Material fallbackMaterial)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Segments;
        line.numCapVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetMaterial(fallbackMaterial);
        line.enabled = false;
    }

    public void Show(Vector3 center, float radius, float width, Color color)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            points[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        line.SetPositions(points);
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.enabled = true;
    }

    public void Hide()
    {
        line.enabled = false;
    }

    public void PlayWave(Vector3 center, float startRadius, float endRadius, float startWidth, float duration, Color color)
    {
        waveCenter = center;
        waveStartRadius = startRadius;
        waveEndRadius = endRadius;
        waveStartWidth = startWidth;
        waveDuration = duration;
        waveColor = color;
        waveAge = 0f;
        UpdateWave();
    }

    public void UpdateWave()  //매 프레임 호출 (충격파 중일 때만 동작)
    {
        if (waveAge < 0f) return;

        float t = waveAge / waveDuration;
        if (t >= 1f)
        {
            waveAge = -1f;
            Hide();
            return;
        }

        float eased = 1f - (1f - t) * (1f - t);  //빠르게 퍼지다 느려짐
        Color color = waveColor;
        color.a *= 1f - t;

        Show(waveCenter, Mathf.Lerp(waveStartRadius, waveEndRadius, eased), Mathf.Lerp(waveStartWidth, waveStartWidth * 0.15f, t), color);
        waveAge += Time.unscaledDeltaTime;
    }

    private static Material GetMaterial(Material fallback)  //단색 반투명 (Sprites/Default는 항상 빌드에 포함됨)
    {
        if (sharedMaterial != null) return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return fallback;

        sharedMaterial = new Material(shader) { name = "JuiceRing (Runtime)" };
        return sharedMaterial;
    }
}
