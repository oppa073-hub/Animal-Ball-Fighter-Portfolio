using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 무한 모드: 보스 방까지 한 사이클을 끝내고 다음 방에 들어갈 때 맵(Props)과 조명 색만 다른 스테이지 것으로 바꾼다.
// 전투 진행(방 번호, 증강, 체력, 골드, 적)은 그대로이고, 씬도 다시 불러오지 않는다.
// 바닥/벽/생성 구역/카메라 배치는 모든 일반 스테이지가 같아서 Props만 갈아 끼워도 된다.
// 맵 목록은 Resources/EndlessMapSet (에디터 메뉴로 생성). 없으면 아무것도 하지 않는다.
public static class EndlessMapRotator
{
    private const string MapRootName = "Props";

    private static GameObject currentMap;
    private static int currentStageNumber = -1;
    private static readonly List<EndlessMapSet.MapEntry> bag = new List<EndlessMapSet.MapEntry>();

    // RoomManager.StartNextRoom에서 방 번호를 올린 직후 부른다
    public static void OnNextRoom(int newRoom)
    {
        if (!GameSessionData.IsEndless || RoomManager.Instance == null) return;
        if (newRoom <= 1 || !RoomManager.Instance.IsBossRoomNumber(newRoom - 1)) return;  //방금 끝난 방이 보스 방일 때만

        EndlessMapSet set = EndlessMapSet.Load();
        if (set == null || set.maps.Count < 2)
        {
            Debug.LogWarning("[EndlessMap] Resources/EndlessMapSet이 없거나 맵이 2개 미만이라 맵을 바꾸지 않습니다. (Tools > BounceSquad > 무한 모드 맵 만들기)");
            return;
        }

        if (currentMap == null) FindStartingMap();

        EndlessMapSet.MapEntry next = PickNext(set);
        if (next == null || next.mapPrefab == null) return;

        Apply(next);
    }

    // 씬에 처음 배치된 맵 (런 시작 때 고른 스테이지)
    private static void FindStartingMap()
    {
        currentStageNumber = GameSessionData.SelectedStage != null ? GameSessionData.SelectedStage.stageNumber : -1;
        bag.Clear();

        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == MapRootName)
            {
                currentMap = root;
                return;
            }
        }
    }

    // 섞은 주머니에서 하나씩 꺼낸다 (같은 맵 연속 금지, 모든 맵을 한 번씩 돈 뒤 다시 섞음)
    private static EndlessMapSet.MapEntry PickNext(EndlessMapSet set)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (bag.Count == 0)
            {
                foreach (EndlessMapSet.MapEntry entry in set.maps)
                {
                    if (entry != null && entry.mapPrefab != null) bag.Add(entry);
                }

                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
            }

            int index = bag.FindIndex(entry => StageNumber(entry) != currentStageNumber);
            if (index >= 0)
            {
                EndlessMapSet.MapEntry picked = bag[index];
                bag.RemoveAt(index);
                return picked;
            }

            bag.Clear();  //남은 게 지금 맵뿐이면 새로 섞기
        }

        return null;
    }

    private static int StageNumber(EndlessMapSet.MapEntry entry) => entry.stage != null ? entry.stage.stageNumber : -1;

    private static void Apply(EndlessMapSet.MapEntry next)
    {
        Vector3 position = currentMap != null ? currentMap.transform.position : next.mapPrefab.transform.position;
        Quaternion rotation = currentMap != null ? currentMap.transform.rotation : next.mapPrefab.transform.rotation;

        if (currentMap != null) Object.Destroy(currentMap);

        currentMap = Object.Instantiate(next.mapPrefab, position, rotation);
        currentMap.name = MapRootName;
        currentStageNumber = StageNumber(next);

        Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
        if (sun != null)
        {
            sun.color = next.lightColor;
            sun.intensity = next.lightIntensity;
        }

        PlayTransition(StageName(next.stage));
        GameAnalytics.Record("endless_map_changed", ("stage", currentStageNumber), ("room", RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 0));
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional) return light;
        }

        return null;
    }

    private static string StageName(StageData stage)
    {
        if (stage == null) return "";

        if (stage.localizedStageName != null && !stage.localizedStageName.IsEmpty)
        {
            try
            {
                string localized = stage.localizedStageName.GetLocalizedString();
                if (!string.IsNullOrEmpty(localized)) return localized;
            }
            catch (System.Exception)
            {
                //번역 표를 아직 못 읽었으면 기본 이름
            }
        }

        return stage.stageName;
    }

    // 맵을 바꾼 같은 프레임에 화면을 하얗게 덮었다가 걷어 내 바뀌는 순간이 보이지 않게 한다
    private static void PlayTransition(string stageName)
    {
        GameObject canvasObject = new GameObject("EndlessMapTransition");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;

        Image flash = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
        flash.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = flash.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        flash.color = new Color(1f, 0.98f, 0.92f, 1f);
        flash.raycastTarget = false;

        flash.DOFade(0f, 0.7f).SetDelay(0.15f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(canvasObject)
            .OnComplete(() => Object.Destroy(canvasObject));

        SoundManager.Instance?.PlaySFX(SoundId.AugmentSelect);
        HudBanner.Show(LobbyUiKit.Loc("endless_new_area", "새로운 지역"), stageName, 2f);
    }
}
