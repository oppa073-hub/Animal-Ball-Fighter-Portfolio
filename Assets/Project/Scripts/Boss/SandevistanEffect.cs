using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class SandevistanEffect : MonoBehaviour
{
    [SerializeField] private GameObject echoPrefab;
    [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] Material echoMaterial;
    int mainTexId = Shader.PropertyToID("_BaseColor");

    [SerializeField] Gradient gradient;
    [SerializeField] float colorCycleSpeed = 1f;

    [Header("Echo Settings")]
    [SerializeField] private float echoInterval = 0.04f;
    [SerializeField] private float echoDuration = 0.3f;

    private WaitForSecondsRealtime echoWait;  //시간 감속 중에도 실제 시간 기준으로 일정하게

    Coroutine echoRoutine;

    // 잔상은 초당 25개씩 생기므로 오브젝트/메시를 매번 만들지 않고 재사용 (모바일 GC·Instantiate 부담 방지)
    private class EchoPiece
    {
        public GameObject root;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Mesh mesh;
        public float remain;  //실제 시간 기준 남은 시간 (일시정지 중에는 줄지 않음)
    }

    private readonly List<EchoPiece> activeEchoes = new List<EchoPiece>();
    private readonly Stack<EchoPiece> pool = new Stack<EchoPiece>();
    private MaterialPropertyBlock propertyBlock;
    private Material[] echoMaterials;

    public void CreateEcho(float echoDuration, Rigidbody rb, float minEchoSpeed)
    {
        if (!skinnedMeshRenderer) return;
        if (!echoPrefab) return;

        // 현재 포즈를 복사
        if (rb == null) return;

        if (rb.linearVelocity.magnitude < minEchoSpeed) return;

        EchoPiece echo = GetEcho();
        if (echo == null) return;

        skinnedMeshRenderer.BakeMesh(echo.mesh);

        Transform source = skinnedMeshRenderer.transform;
        echo.root.transform.SetPositionAndRotation(source.position, source.rotation);
        echo.root.transform.localScale = source.lossyScale;

        // 모든 SubMesh에 동일한 머티리얼 적용
        int subMeshCount = echo.mesh.subMeshCount;
        if (echoMaterials == null || echoMaterials.Length != subMeshCount)
        {
            echoMaterials = new Material[subMeshCount];
            for (int i = 0; i < subMeshCount; i++) echoMaterials[i] = echoMaterial;
        }
        echo.renderer.sharedMaterials = echoMaterials;

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        float t = Mathf.Repeat(Time.unscaledTime * colorCycleSpeed, 1f);
        propertyBlock.SetColor(mainTexId, gradient.Evaluate(t));
        for (int i = 0; i < subMeshCount; ++i) echo.renderer.SetPropertyBlock(propertyBlock, i);

        echo.remain = echoDuration;
        echo.root.SetActive(true);
        activeEchoes.Add(echo);
    }

    private EchoPiece GetEcho()
    {
        if (pool.Count > 0) return pool.Pop();

        GameObject root = Instantiate(echoPrefab);
        root.SetActive(false);

        EchoPiece echo = new EchoPiece
        {
            root = root,
            filter = root.GetComponent<MeshFilter>(),
            renderer = root.GetComponent<MeshRenderer>(),
            mesh = new Mesh { name = "SandevistanEcho (Runtime)" }
        };

        if (echo.filter == null || echo.renderer == null)
        {
            Destroy(root);
            Destroy(echo.mesh);
            return null;
        }

        echo.mesh.MarkDynamic();
        echo.filter.sharedMesh = echo.mesh;
        return echo;
    }

    private void Update()
    {
        if (activeEchoes.Count == 0) return;

        if (Time.timeScale <= 0f) return;  //일시정지 중에는 잔상도 그대로 멈춤

        float deltaTime = Time.unscaledDeltaTime;

        for (int i = activeEchoes.Count - 1; i >= 0; i--)
        {
            EchoPiece echo = activeEchoes[i];
            echo.remain -= deltaTime;
            if (echo.remain > 0f) continue;

            echo.root.SetActive(false);
            activeEchoes.RemoveAt(i);
            pool.Push(echo);
        }
    }

    private void OnDisable()  //보스가 꺼지면 남은 잔상도 숨김
    {
        for (int i = 0; i < activeEchoes.Count; i++)
        {
            if (activeEchoes[i].root != null) activeEchoes[i].root.SetActive(false);
            pool.Push(activeEchoes[i]);
        }
        activeEchoes.Clear();
        echoRoutine = null;  //비활성화 시 코루틴은 자동으로 멈춤
    }

    private void OnDestroy()
    {
        foreach (EchoPiece echo in pool)
        {
            if (echo.root != null) Destroy(echo.root);
            if (echo.mesh != null) Destroy(echo.mesh);
        }
        pool.Clear();
    }

    public void StartEcho(Rigidbody rb, float minEchoSpeed)
    {
        if (echoRoutine != null) return;

        echoWait = new WaitForSecondsRealtime(echoInterval);
        echoRoutine = StartCoroutine(EchoLoop(rb, minEchoSpeed));
    }

    public void StopEchoLoop()
    {
        if (echoRoutine != null)
        {
            StopCoroutine(echoRoutine);
            echoRoutine = null;
        }
    }
    private IEnumerator EchoLoop(Rigidbody rb, float minEchoSpeed)
    {
        while (true)
        {
            CreateEcho(echoDuration, rb, minEchoSpeed);

            yield return echoWait;
        }
    }

}
