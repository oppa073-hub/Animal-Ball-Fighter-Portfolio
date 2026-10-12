using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 도감 초상화: 적 프리팹을 화면 밖 먼 곳에 '그림만' 복제해 전용 카메라로 RenderTexture에 한 장씩 찍는다.
// - 복제는 꺼진 부모 아래에서 만들어 적 스크립트(Awake 등)가 돌기 전에 스크립트/물리를 모두 지운다.
// - 같은 모델을 색으로 구분하는 적은 tint를, 아직 못 만난 적은 검은 실루엣을 입힌다.
// - 한 프레임에 한 마리씩 찍고, 다 찍으면 카메라를 끈다. 창을 닫으면 RenderTexture까지 정리한다.
public class CodexPortraitStudio : MonoBehaviour
{
    private const int PortraitLayer = 31;  //로비 메인/미리보기 카메라가 그리지 않는 레이어
    private const int TextureSize = 384;
    private static readonly Vector3 StudioOrigin = new Vector3(0f, -3000f, 0f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private class Job
    {
        public GameObject prefab;
        public Color tint;
        public bool silhouette;
        public float yaw;
        public RenderTexture target;
    }

    private readonly Queue<Job> jobs = new Queue<Job>();
    private readonly List<RenderTexture> textures = new List<RenderTexture>();
    private Camera studioCamera;
    private Coroutine worker;

    // 캔버스 아래에 두면 캔버스 배율이 모델에 곱해지므로 씬 최상위에 만든다. 만든 쪽(도감 창)이 닫힐 때 Destroy한다.
    public static CodexPortraitStudio Create()
    {
        GameObject go = new GameObject("CodexPortraitStudio");
        return go.AddComponent<CodexPortraitStudio>();
    }

    // 바로 빈 텍스처를 돌려주고, 차례가 되면 그 텍스처에 그린다
    public RenderTexture Request(GameObject prefab, Color tint, bool silhouette, float yaw = -25f)
    {
        RenderTexture texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32) { name = "CodexPortrait", antiAliasing = 2 };
        texture.Create();
        textures.Add(texture);

        if (prefab != null)
        {
            jobs.Enqueue(new Job { prefab = prefab, tint = tint, silhouette = silhouette, yaw = yaw, target = texture });
            if (worker == null) worker = StartCoroutine(Work());
        }

        return texture;
    }

    private void EnsureCamera()
    {
        if (studioCamera != null) return;

        GameObject cameraObject = new GameObject("CodexPortraitCamera");
        cameraObject.transform.SetParent(transform, false);
        studioCamera = cameraObject.AddComponent<Camera>();
        studioCamera.clearFlags = CameraClearFlags.SolidColor;
        studioCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        studioCamera.cullingMask = 1 << PortraitLayer;
        studioCamera.fieldOfView = 26f;
        studioCamera.nearClipPlane = 0.05f;
        studioCamera.farClipPlane = 200f;
        studioCamera.depth = -50f;
        studioCamera.allowHDR = false;
        studioCamera.allowMSAA = true;
        studioCamera.enabled = false;
    }

    private IEnumerator Work()
    {
        EnsureCamera();

        while (jobs.Count > 0)
        {
            Job job = jobs.Dequeue();
            if (job.target == null) continue;

            GameObject holder = new GameObject("PortraitHolder");
            holder.transform.SetParent(transform, false);
            holder.transform.position = StudioOrigin;
            holder.SetActive(false);

            GameObject model = BuildVisualCopy(job.prefab, holder.transform);
            if (model == null)
            {
                Destroy(holder);
                continue;
            }

            model.transform.localRotation = Quaternion.Euler(0f, job.yaw, 0f);
            ApplyColor(model, job.tint, job.silhouette);
            holder.SetActive(true);

            yield return null;  //애니메이터가 기본 자세를 잡을 때까지
            yield return null;

            if (holder == null || job.target == null) continue;

            if (FrameCamera(model))
            {
                studioCamera.targetTexture = job.target;
                studioCamera.enabled = true;
                yield return null;  //이 프레임 끝에 그려짐
                studioCamera.enabled = false;
                studioCamera.targetTexture = null;
            }

            if (holder != null) Destroy(holder);
        }

        worker = null;
    }

    // 프리팹을 꺼진 부모 아래 복제한 뒤 그림 외의 것은 모두 지운다 (스크립트가 한 번도 실행되지 않음)
    private static GameObject BuildVisualCopy(GameObject prefab, Transform holder)
    {
        GameObject copy = Instantiate(prefab, holder);
        copy.name = prefab.name + "_Portrait";
        copy.transform.localPosition = Vector3.zero;

        MonoBehaviour[] behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            if (behaviours[i] != null) DestroyImmediate(behaviours[i]);
        }

        foreach (Joint joint in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(joint);
        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
        foreach (AudioSource source in copy.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(source);
        foreach (ParticleSystem particle in copy.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
        foreach (Canvas canvas in copy.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);

        // 몸 모델만 보이게 (경고선/잔상/그림자 스프라이트 등은 끔)
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) renderer.enabled = false;
        }

        // 랜덤 모델 중 하나를 고르는 적은 첫 번째 모델만 켬
        foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "ModelRoot" || child.childCount <= 1) continue;
            for (int i = 0; i < child.childCount; i++) child.GetChild(i).gameObject.SetActive(i == 0);
        }

        foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }

        SetLayer(copy.transform, PortraitLayer);
        return copy;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
    }

    private static void ApplyColor(GameObject model, Color tint, bool silhouette)
    {
        if (!silhouette && (tint == Color.white || tint.a <= 0f)) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                int id = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : -1;
                if (id < 0) continue;

                Color color = silhouette ? new Color(0.03f, 0.05f, 0.1f, 1f) : material.GetColor(id) * tint;
                renderer.GetPropertyBlock(block, i);
                block.SetColor(id, color);
                if (silhouette) block.SetColor("_EmissionColor", Color.black);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    // 보이는 몸 전체가 꽉 차게 카메라를 둔다. 적 모델은 +Z를 보고 있으므로 +Z 쪽에서 앞모습을 찍는다
    private bool FrameCamera(GameObject model)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds) return false;

        float radius = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.9f);
        float distance = radius / Mathf.Tan(studioCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        Vector3 center = bounds.center;
        Vector3 cameraPosition = center + new Vector3(0f, radius * 0.25f, distance);

        studioCamera.transform.position = cameraPosition;
        studioCamera.transform.LookAt(center);
        studioCamera.farClipPlane = distance + radius * 4f;
        return true;
    }

    private void OnDestroy()
    {
        foreach (RenderTexture texture in textures)
        {
            if (texture == null) continue;
            texture.Release();
            Destroy(texture);
        }

        textures.Clear();
    }
}
