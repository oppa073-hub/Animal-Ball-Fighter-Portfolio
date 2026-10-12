using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;  //Android Build Support 모듈이 없는 에디터에서도 컴파일되도록 분리
#endif
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// McpPlayerBuildFilter로 MCP 어셈블리를 빌드에서 빼도, Unity가 만드는 RuntimeInitializeOnLoads.json에는
// MCP의 [RuntimeInitializeOnLoadMethod] 항목이 남는다. 앱 시작 시 없는 클래스를 찾지 않도록 그 항목을 지운다.
// Android: Gradle 프로젝트 생성 직후(APK/AAB 패키징 전), iOS: Xcode 프로젝트 생성 후
// 주의: Unity 업그레이드 후 이 json에 필드가 추가되면 JsonUtility 재작성 시 누락되므로 RuntimeInitializeEntry를 확인할 것
public class McpRuntimeInitializeCleaner : IPostprocessBuildWithReport
#if UNITY_ANDROID
    , IPostGenerateGradleAndroidProject
#endif
{
    private const string FileName = "RuntimeInitializeOnLoads.json";

    public int callbackOrder => 0;

#if UNITY_ANDROID
    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        CleanFile(Path.Combine(unityLibraryPath, "src", "main", "assets", "bin", "Data", FileName));
    }
#endif

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;

        CleanFile(Path.Combine(report.summary.outputPath, "Data", FileName));
    }

    [Serializable]
    private class RuntimeInitializeList
    {
        public List<RuntimeInitializeEntry> root = new List<RuntimeInitializeEntry>();
    }

    [Serializable]
    private class RuntimeInitializeEntry
    {
        public string assemblyName;
        public string nameSpace;
        public string className;
        public string methodName;
        public int loadTypes;
        public bool isUnityClass;
    }

    public static int CleanFile(string filePath)  //제거한 항목 수 반환
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[McpBuildFilter] {FileName}을 찾지 못함: {filePath}");
            return 0;
        }

        RuntimeInitializeList list = JsonUtility.FromJson<RuntimeInitializeList>(File.ReadAllText(filePath));
        if (list == null || list.root == null) return 0;

        int removedCount = list.root.RemoveAll(entry => McpPlayerBuildFilter.IsMcpAssembly(entry.assemblyName));
        if (removedCount == 0) return 0;

        File.WriteAllText(filePath, JsonUtility.ToJson(list));
        Debug.Log($"[McpBuildFilter] {FileName}에서 MCP 초기화 항목 {removedCount}개 제거");

        return removedCount;
    }
}
