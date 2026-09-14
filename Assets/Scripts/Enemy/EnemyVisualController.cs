using UnityEngine;

public class EnemyVisualController : MonoBehaviour
{
    [SerializeField] private GameObject[] models;
    [SerializeField] private EnemyAnimationController animationController;

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