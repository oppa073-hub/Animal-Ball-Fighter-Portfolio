using UnityEngine;
using System.Linq;
using System.Collections;
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

    private WaitForSeconds echoWait;

    Coroutine echoRoutine;

    public void CreateEcho(float echoDuration, Rigidbody rb, float minEchoSpeed)
    {
        if (!skinnedMeshRenderer) return;
        if (!echoPrefab) return;

        // 현재 포즈를 복사
        if (rb == null) return;

        if (rb.linearVelocity.magnitude < minEchoSpeed) return;

        Mesh bakedMesh = new Mesh();

        skinnedMeshRenderer.BakeMesh(bakedMesh);

        // 오브젝트 생성
        var echo = Instantiate(echoPrefab);
        echo.transform.position = skinnedMeshRenderer.transform.position;
        echo.transform.rotation = skinnedMeshRenderer.transform.rotation;
        echo.transform.localScale = skinnedMeshRenderer.transform.lossyScale;

        // 메시와 머티리얼 세팅
        var mf = echo.GetComponent<MeshFilter>();
        var mr = echo.GetComponent<MeshRenderer>();

        mf.sharedMesh = bakedMesh;

        int subMeshCount = bakedMesh.subMeshCount;
        // 모든 SubMesh에 동일한 머티리얼 적용
        var materials = Enumerable.Repeat(echoMaterial, subMeshCount).ToArray();
        mr.sharedMaterials = materials;

        var mpb = new MaterialPropertyBlock();
        float t = Mathf.Repeat(Time.time * colorCycleSpeed, 1f);
        Color currentColor = gradient.Evaluate(t);
        mpb.SetColor(mainTexId, currentColor);
        for (int i = 0; i < subMeshCount; ++i) mr.SetPropertyBlock(mpb, i);

        Destroy(echo, echoDuration);
        Destroy(bakedMesh, echoDuration);
    }

    public void StartEcho(Rigidbody rb, float minEchoSpeed)
    {
        if (echoRoutine != null) return;

        echoWait = new WaitForSeconds(echoInterval);
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
