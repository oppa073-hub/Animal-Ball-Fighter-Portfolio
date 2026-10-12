using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > BounceSquad > 무한 모드 맵 만들기
// 일반 스테이지 씬(StageData.isEndless == false)마다 루트 'Props'를 프리팹으로 저장하고 조명 색을 읽어
// Resources/EndlessMapSet에 채운다. 스테이지 씬 자체는 바꾸지 않는다 (열었던 씬은 저장하지 않고 닫음).
// 스테이지 씬의 Props를 고친 뒤에는 이 메뉴를 다시 실행하면 된다.
public static class EndlessMapBuilder
{
    private const string PrefabFolder = "Assets/Project/Prefabs/EndlessMaps";
    private const string SetPath = "Assets/Resources/EndlessMapSet.asset";
    private const string MapRootName = "Props";

    [MenuItem("Tools/BounceSquad/무한 모드 맵 만들기")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("무한 모드 맵 만들기", "플레이 중에는 실행할 수 없습니다.", "확인");
            return;
        }

        List<StageData> stages = new List<StageData>();
        foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
        {
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guid));
            if (stage != null && !stage.isEndless && !string.IsNullOrEmpty(stage.sceneName)) stages.Add(stage);
        }

        stages.Sort((a, b) => a.stageNumber.CompareTo(b.stageNumber));
        if (stages.Count == 0)
        {
            EditorUtility.DisplayDialog("무한 모드 맵 만들기", "일반 스테이지 StageData를 찾지 못했습니다.", "확인");
            return;
        }

        // 이미 열려 있는 스테이지 씬에 저장 안 한 변경이 있으면 그 내용이 그대로 맵에 담기므로 먼저 확인
        List<string> dirtyStages = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty && stages.Exists(stage => stage.sceneName == open.name)) dirtyStages.Add(open.name);
        }

        if (dirtyStages.Count > 0 && !EditorUtility.DisplayDialog("무한 모드 맵 만들기",
                $"저장하지 않은 변경이 있는 스테이지 씬이 열려 있습니다: {string.Join(", ", dirtyStages)}\n저장 안 한 내용까지 맵에 들어갑니다. 계속할까요?",
                "계속", "취소"))
        {
            return;
        }

        if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);

        EndlessMapSet set = AssetDatabase.LoadAssetAtPath<EndlessMapSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<EndlessMapSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }

        set.maps.Clear();
        List<string> report = new List<string>();

        try
        {
            for (int i = 0; i < stages.Count; i++)
            {
                StageData stage = stages[i];
                EditorUtility.DisplayProgressBar("무한 모드 맵 만들기", stage.sceneName, (float)i / stages.Count);

                string scenePath = FindScenePath(stage.sceneName);
                if (scenePath == null)
                {
                    report.Add($"✗ {stage.sceneName}: 씬 파일 없음");
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool openedHere = !scene.isLoaded;
                if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                try
                {
                    GameObject props = FindRoot(scene, MapRootName);
                    if (props == null)
                    {
                        report.Add($"✗ {stage.sceneName}: 루트 '{MapRootName}' 없음");
                        continue;
                    }

                    string prefabPath = $"{PrefabFolder}/{stage.sceneName}_Map.prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(props, prefabPath, out bool success);  //씬 오브젝트와 연결하지 않음
                    if (!success || prefab == null)
                    {
                        report.Add($"✗ {stage.sceneName}: 프리팹 저장 실패");
                        continue;
                    }

                    Light sun = FindDirectionalLight(scene);
                    set.maps.Add(new EndlessMapSet.MapEntry
                    {
                        stage = stage,
                        mapPrefab = prefab,
                        lightColor = sun != null ? sun.color : Color.white,
                        lightIntensity = sun != null ? sun.intensity : 1f,
                    });

                    report.Add($"✓ {stage.sceneName}");
                }
                finally
                {
                    if (openedHere) EditorSceneManager.CloseScene(scene, true);  //저장하지 않고 닫음
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);  //다른 작업 중인 에셋까지 저장하지 않도록 이것만
        AssetDatabase.Refresh();

        string message = string.Join("\n", report) + $"\n\n맵 {set.maps.Count}개를 {SetPath}에 저장했습니다.";
        Debug.Log("[EndlessMap] " + message.Replace("\n", " / "));
        EditorUtility.DisplayDialog("무한 모드 맵 만들기", message, "확인");
    }

    private static string FindScenePath(string sceneName)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        return null;
    }

    private static Light FindDirectionalLight(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional) return light;
            }
        }

        return null;
    }
}
