using System.Collections.Generic;
using UnityEngine;

// 일반 MeshRenderer로 된 모델(플레이어 동물 캐릭터)용 잔상. 로봇 보스의 SandevistanEffect(스킨드 메시 전용)와 같은 머티리얼/색 그라디언트를 쓴다.
// 시간 감속 중에도 일정하게 보이도록 실제 시간(unscaled) 기준으로 생성/소멸하고, 잔상 오브젝트는 재사용한다.
public class MeshEchoTrail : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private class EchoPiece
    {
        public GameObject root;
        public List<MeshFilter> filters = new List<MeshFilter>();
        public List<MeshRenderer> renderers = new List<MeshRenderer>();
        public float remain;
        public Color color;
    }

    private Transform modelRoot;
    private Material echoMaterial;
    private Gradient gradient;
    private float colorCycleSpeed = 1f;
    private float echoInterval = 0.04f;
    private float echoLifeTime = 0.3f;
    private float minSpeed = 1f;
    private Rigidbody targetRigidbody;

    private bool isEmitting;
    private float emitTimer;
    private MaterialPropertyBlock propertyBlock;

    private readonly List<EchoPiece> activeEchoes = new List<EchoPiece>();
    private readonly Stack<EchoPiece> pool = new Stack<EchoPiece>();
    private readonly List<MeshFilter> sourceFilters = new List<MeshFilter>();

    public void Setup(Transform model, Rigidbody body, Material material, Gradient colorGradient, float interval, float lifeTime, float minimumSpeed)
    {
        modelRoot = model;
        targetRigidbody = body;
        echoMaterial = material;
        gradient = colorGradient;
        echoInterval = Mathf.Max(0.01f, interval);
        echoLifeTime = Mathf.Max(0.05f, lifeTime);
        minSpeed = minimumSpeed;
    }

    public void StartEcho()
    {
        isEmitting = true;
        emitTimer = 0f;
    }

    public void StopEcho()
    {
        isEmitting = false;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (Time.timeScale <= 0f) deltaTime = 0f;  //일시정지 중에는 멈춤

        if (isEmitting && modelRoot != null && echoMaterial != null)
        {
            emitTimer -= deltaTime;

            if (emitTimer <= 0f)
            {
                emitTimer = echoInterval;
                TryCreateEcho();
            }
        }

        for (int i = activeEchoes.Count - 1; i >= 0; i--)
        {
            EchoPiece echo = activeEchoes[i];
            echo.remain -= deltaTime;

            if (echo.remain <= 0f)
            {
                echo.root.SetActive(false);
                activeEchoes.RemoveAt(i);
                pool.Push(echo);
                continue;
            }

            Color color = echo.color;
            color.a *= echo.remain / echoLifeTime;  //점점 흐려짐
            SetColor(echo, color);
        }
    }

    private void TryCreateEcho()
    {
        if (targetRigidbody != null)
        {
            Vector3 velocity = targetRigidbody.linearVelocity;
            velocity.y = 0f;
            if (velocity.magnitude < minSpeed) return;
        }

        sourceFilters.Clear();
        modelRoot.GetComponentsInChildren(false, sourceFilters);
        if (sourceFilters.Count == 0) return;

        EchoPiece echo = pool.Count > 0 ? pool.Pop() : CreatePiece();

        while (echo.filters.Count < sourceFilters.Count) AddPart(echo);

        for (int i = 0; i < echo.filters.Count; i++)
        {
            bool used = i < sourceFilters.Count;
            echo.filters[i].gameObject.SetActive(used);
            if (!used) continue;

            MeshFilter source = sourceFilters[i];
            Transform part = echo.filters[i].transform;

            part.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            part.localScale = source.transform.lossyScale;
            echo.filters[i].sharedMesh = source.sharedMesh;

            int subMeshCount = source.sharedMesh != null ? Mathf.Max(1, source.sharedMesh.subMeshCount) : 1;
            if (echo.renderers[i].sharedMaterials.Length != subMeshCount)
            {
                Material[] materials = new Material[subMeshCount];
                for (int m = 0; m < subMeshCount; m++) materials[m] = echoMaterial;
                echo.renderers[i].sharedMaterials = materials;
            }
        }

        echo.color = gradient != null ? gradient.Evaluate(Mathf.Repeat(Time.unscaledTime * colorCycleSpeed, 1f)) : Color.cyan;
        echo.remain = echoLifeTime;
        SetColor(echo, echo.color);

        echo.root.SetActive(true);
        activeEchoes.Add(echo);
    }

    private EchoPiece CreatePiece()
    {
        EchoPiece echo = new EchoPiece { root = new GameObject("PlayerEcho") };
        echo.root.SetActive(false);
        return echo;
    }

    private void AddPart(EchoPiece echo)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(echo.root.transform, false);

        MeshRenderer meshRenderer = part.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        echo.filters.Add(part.AddComponent<MeshFilter>());
        echo.renderers.Add(meshRenderer);
    }

    private void SetColor(EchoPiece echo, Color color)
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor(BaseColorId, color);
        for (int i = 0; i < echo.renderers.Count; i++) echo.renderers[i].SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        isEmitting = false;

        for (int i = 0; i < activeEchoes.Count; i++)
        {
            if (activeEchoes[i].root != null) activeEchoes[i].root.SetActive(false);
            pool.Push(activeEchoes[i]);
        }

        activeEchoes.Clear();
    }

    private void OnDestroy()
    {
        foreach (EchoPiece echo in pool)
        {
            if (echo.root != null) Destroy(echo.root);
        }
    }
}
