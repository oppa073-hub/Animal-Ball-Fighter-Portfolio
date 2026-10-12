using UnityEngine;

// 바닥에 그리는 마법진 (바깥 고리 + 안쪽 고리 + 육망성). 마녀 보스 패턴들이 같이 쓴다.
// 빛 머티리얼(BossGlowBeam)로 LineRenderer를 만들고, 호출하는 쪽이 매 프레임 위치/반지름/회전/투명도를 넘긴다.
public class MagicCircleVisual
{
    private const int StarPoints = 6;

    private readonly Transform root;
    private readonly LineRenderer outerRing;
    private readonly LineRenderer innerRing;
    private readonly LineRenderer starA;
    private readonly LineRenderer starB;

    public MagicCircleVisual(Transform parent, string name, Material material)
    {
        root = new GameObject(name).transform;
        root.SetParent(parent, false);

        outerRing = BossGlowVisual.CreateRing(root, "Outer", material, 72);
        innerRing = BossGlowVisual.CreateRing(root, "Inner", material, 60);

        // 육망성 = 삼각형 두 개
        starA = BossGlowVisual.CreateRing(root, "StarA", material, 3);
        starB = BossGlowVisual.CreateRing(root, "StarB", material, 3);
    }

    public void Show(Vector3 center, float radius, float rotationDegrees, Color color)
    {
        center.y = 0.07f;

        SetEnabled(true);

        BossGlowVisual.SetRing(outerRing, center, radius);
        BossGlowVisual.SetRing(innerRing, center, radius * 0.78f);
        SetTriangle(starA, center, radius * 0.76f, rotationDegrees);
        SetTriangle(starB, center, radius * 0.76f, rotationDegrees + 60f);

        // 밝은 바닥(풀밭 등)에서도 보이도록 선을 굵게
        BossGlowVisual.SetColor(outerRing, color);
        BossGlowVisual.SetWidth(outerRing, Mathf.Max(0.1f, radius * 0.11f));

        Color inner = color;
        inner.a *= 0.8f;
        BossGlowVisual.SetColor(innerRing, inner);
        BossGlowVisual.SetWidth(innerRing, Mathf.Max(0.06f, radius * 0.055f));

        Color star = color;
        star.a *= 0.9f;
        BossGlowVisual.SetColor(starA, star);
        BossGlowVisual.SetColor(starB, star);
        BossGlowVisual.SetWidth(starA, Mathf.Max(0.06f, radius * 0.065f));
        BossGlowVisual.SetWidth(starB, Mathf.Max(0.06f, radius * 0.065f));
    }

    public void Hide()
    {
        SetEnabled(false);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root.gameObject);
    }

    private void SetEnabled(bool enabled)
    {
        if (outerRing == null) return;

        outerRing.enabled = enabled;
        innerRing.enabled = enabled;
        starA.enabled = enabled;
        starB.enabled = enabled;
    }

    private static void SetTriangle(LineRenderer line, Vector3 center, float radius, float rotationDegrees)
    {
        for (int i = 0; i < 3; i++)
        {
            float angle = (rotationDegrees + i * 120f) * Mathf.Deg2Rad;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }
}
