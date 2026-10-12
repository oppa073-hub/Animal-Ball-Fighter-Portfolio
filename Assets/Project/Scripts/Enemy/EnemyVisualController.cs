using System.Collections.Generic;
using UnityEngine;

public class EnemyVisualController : MonoBehaviour
{
    [SerializeField] private GameObject[] models;
    [SerializeField] private EnemyAnimationController animationController;
    [SerializeField] private Color tintColor = Color.white;  //같은 모델을 쓰는 적 종류 구분용 (흰색이면 원래 색)

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 색을 바꿀 머티리얼 인스턴스와 원래 색 (풀에서 다른 종류로 다시 쓰일 때 원래 색 기준으로 다시 칠함)
    private readonly List<Material> tintMaterials = new List<Material>();
    private readonly List<Color> baseColors = new List<Color>();
    private readonly List<int> colorIds = new List<int>();
    private Color currentTint = Color.white;

    private void Awake()
    {
        if (tintColor != Color.white) SetTint(tintColor);
    }

    public void SetTint(Color tint)  //런타임 색 지정 (적 데이터의 tint 등). 흰색이면 원래 색
    {
        if (tint == currentTint && tintMaterials.Count > 0) return;
        if (tint == Color.white && tintMaterials.Count == 0) return;  //한 번도 칠하지 않았으면 인스턴스를 만들 필요 없음

        CacheMaterials();
        currentTint = tint;

        for (int i = 0; i < tintMaterials.Count; i++)
        {
            if (tintMaterials[i] != null) tintMaterials[i].SetColor(colorIds[i], baseColors[i] * tint);
        }
    }

    public void ResetTint() => SetTint(tintColor);  //프리팹 기본 색으로

    // 머티리얼 인스턴스에 색을 곱해 둔다 (피격 번쩍임은 sharedMaterials를 잠깐 바꿨다 되돌리므로 그대로 동작)
    private void CacheMaterials()
    {
        if (tintMaterials.Count > 0 || models == null) return;

        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] == null) continue;

            foreach (Renderer renderer in models[i].GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;

                foreach (Material material in renderer.materials)
                {
                    int id = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : -1;
                    if (id < 0) continue;

                    tintMaterials.Add(material);
                    baseColors.Add(material.GetColor(id));
                    colorIds.Add(id);
                }
            }
        }
    }

    private void OnDestroy()  //renderer.materials로 만든 인스턴스 정리
    {
        foreach (Material material in tintMaterials)
        {
            if (material != null) Destroy(material);
        }
    }

    public void SetRandomModel()
    {
        if (models == null || models.Length == 0) return;

        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] != null) models[i].SetActive(false);
        }

        int randomIndex = Random.Range(0, models.Length);

        GameObject selectedModel = models[randomIndex];

        if (selectedModel != null)
        {
            selectedModel.SetActive(true);

            Animator animator = selectedModel.GetComponent<Animator>();

            animationController?.SetAnimator(animator);
        }
    }
}
