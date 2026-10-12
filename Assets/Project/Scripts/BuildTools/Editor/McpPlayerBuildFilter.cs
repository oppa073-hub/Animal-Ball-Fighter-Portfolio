using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Unity-MCP(com.ivanmurzak.unity.mcp)는 에디터 개발용 도구다.
// 패키지가 모든 빌드 타깃에 UNITY_MCP_READY define을 넣고(도메인 리로드마다 다시 추가), NuGet DLL을 Any Platform으로 설정하므로
// 그대로 두면 런타임 어셈블리와 DLL이 플레이어 빌드에 포함되고 시작 시 RuntimeInitializeOnLoadMethod가 실행된다.
// 플레이어 빌드에 들어갈 어셈블리 목록에서 MCP 관련 어셈블리를 모두 제거한다.
public class McpPlayerBuildFilter : IFilterBuildAssemblies
{
    private const string NuGetPluginFolder = "Assets/Plugins/NuGet";

    private static readonly string[] McpAssemblyPrefixes =
    {
        "com.IvanMurzak.",
        "Extensions.Unity.PlayerPrefsEx",  //MCP 패키지 전용 의존성
    };

    public int callbackOrder => 0;

    public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
    {
        HashSet<string> nugetAssemblyNames = GetNuGetAssemblyNames();
        List<string> result = new List<string>(assemblies.Length);
        List<string> removed = new List<string>();

        foreach (string assemblyPath in assemblies)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            if (IsMcpAssembly(assemblyName, nugetAssemblyNames))
            {
                removed.Add(assemblyName);
                continue;
            }

            result.Add(assemblyPath);
        }

        Debug.Log($"[McpBuildFilter] 플레이어 빌드에서 MCP 어셈블리 {removed.Count}개 제외: {string.Join(", ", removed)}");

        return result.ToArray();
    }

    public static bool IsMcpAssembly(string assemblyName)
    {
        return IsMcpAssembly(assemblyName, GetNuGetAssemblyNames());
    }

    private static bool IsMcpAssembly(string assemblyName, HashSet<string> nugetAssemblyNames)
    {
        foreach (string prefix in McpAssemblyPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return nugetAssemblyNames.Contains(assemblyName);
    }

    // Assets/Plugins/NuGet은 MCP 패키지가 복원하는 DLL 폴더 (gitignore 대상)
    // 다른 패키지/SDK 플러그인에 같은 이름의 DLL이 있으면 그쪽이 쓰는 것일 수 있으므로 제외 대상에서 뺀다
    private static HashSet<string> GetNuGetAssemblyNames()
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        string fullPath = Path.GetFullPath(NuGetPluginFolder);
        if (!Directory.Exists(fullPath)) return names;

        foreach (string dllPath in Directory.GetFiles(fullPath, "*.dll", SearchOption.AllDirectories))
        {
            names.Add(Path.GetFileNameWithoutExtension(dllPath));
        }

        foreach (PluginImporter importer in PluginImporter.GetAllImporters())
        {
            string assetPath = importer.assetPath.Replace('\\', '/');
            if (assetPath.StartsWith(NuGetPluginFolder + "/", StringComparison.OrdinalIgnoreCase)) continue;
            if (!assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;

            names.Remove(Path.GetFileNameWithoutExtension(assetPath));
        }

        return names;
    }
}
