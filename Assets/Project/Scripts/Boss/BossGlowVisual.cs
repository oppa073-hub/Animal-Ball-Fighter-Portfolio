using UnityEngine;
using UnityEngine.Rendering;

// 보스 패턴의 빛 연출(레이저 빔, 조준선, 충격파 고리, 빛 구슬)을 코드로 만드는 도우미.
// 머티리얼은 가산(Additive) 파티클 머티리얼(BossGlowBeam / BossGlowOrb)을 패턴 인스펙터에서 넘겨받는다.
public static class BossGlowVisual
{
    public static LineRenderer CreateLine(Transform parent, string name, Material material, int capVertices = 4)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.numCapVertices = capVertices;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    public static LineRenderer CreateRing(Transform parent, string name, Material material, int segments = 64)
    {
        LineRenderer ring = CreateLine(parent, name, material, 0);
        ring.loop = true;
        ring.positionCount = segments;
        ring.alignment = LineAlignment.TransformZ;  //바닥에 눕힌 고리
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return ring;
    }

    public static void SetRing(LineRenderer ring, Vector3 center, float radius)
    {
        int count = ring.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    public static void SetColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    public static void SetWidth(LineRenderer line, float width)
    {
        line.startWidth = width;
        line.endWidth = width;
    }

    // 카메라를 바라보는 둥근 빛 (차지 구슬, 발사 섬광)
    public static Transform CreateOrb(Transform parent, string name, Material material, out Renderer renderer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);

        renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    public static void FaceCamera(Transform orb)
    {
        Camera camera = Camera.main;
        if (camera != null) orb.rotation = camera.transform.rotation;
    }

    public static void SetOrbColor(Renderer renderer, MaterialPropertyBlock block, Color color)
    {
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
    }
}
