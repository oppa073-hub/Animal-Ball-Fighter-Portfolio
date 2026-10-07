using UnityEngine;

public class LobbyRewardedAdButton : MonoBehaviour
{
    public void ShowGoldAd()
    {
        if (AdManager.Instance == null)
        {
            Debug.LogError("[Ads] AdManager가 없습니다.");
            return;
        }

        AdManager.Instance.ShowGoldRewardedAd();
    }
}