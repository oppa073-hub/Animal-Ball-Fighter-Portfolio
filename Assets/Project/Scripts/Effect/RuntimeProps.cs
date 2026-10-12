using UnityEngine;

// 모델이 없을 때 쓰는 임시 소품 (기본 도형 + 단색). 특수 적 부착물, 맵 기믹 등에서 쓴다.
public static class RuntimeProps
{
    private static Material litMaterial;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static Transform Create(PrimitiveType type, Transform parent, Color color, bool keepCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());  //물리에 영향 없게 (한 프레임도 남지 않도록 즉시)
        if (parent != null) go.transform.SetParent(parent, false);

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = GetLitMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SetColor(renderer, color);

        return go.transform;
    }

    public static void SetColor(Renderer renderer, Color color, float emission = 0f)
    {
        if (renderer == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        if (emission > 0f) block.SetColor(EmissionColorId, color * emission);
        renderer.SetPropertyBlock(block);
    }

    private static Material GetLitMaterial()
    {
        if (litMaterial != null) return litMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        litMaterial = new Material(shader) { name = "RuntimeProp (Runtime)" };
        litMaterial.EnableKeyword("_EMISSION");  //발광(SetColor emission)용
        return litMaterial;
    }
}
