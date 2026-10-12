using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// BreakableWall 프리팹의 조각 메시와 금 간 텍스처를 생성한다 (외부 에셋 없이 절차적으로).
// 벽 앞면을 충격 지점 기준 방사형 직선 + 보조 직선으로 잘라 볼록 다각형 조각을 만들고 두께만큼 돌출시킨다.
// 금 텍스처는 같은 절단선을 따라 그리므로 금 간 자리대로 부서진다. 패턴 Variant 여러 개를 만들어 벽마다 랜덤 선택.
public static class BreakableWallGenerator
{
    private const string PrefabPath = "Assets/Project/Prefabs/BreakableWall.prefab";
    private const string BodyMaterialPath = "Assets/Project/Prefabs/BreakableWall.mat";
    private const string OutputFolder = "Assets/Project/Prefabs/BreakableWall";
    private const string MeshAssetPath = OutputFolder + "/BreakableWallShards.asset";
    private const string CrackMaterialPath = OutputFolder + "/BreakableWallCrack.mat";

    private const float Width = 2.0f;
    private const float Height = 1.2f;
    private const float Thickness = 0.4f;
    private const int TextureWidth = 256;
    private const int TextureHeight = 154;  //Width:Height 비율
    private const int CrackStageCount = 3;

    private static readonly int[] VariantSeeds = { 11, 23, 37 };
    private static readonly float[] RadialRevealByStage = { 0.35f, 0.75f, 1f };  //단계별 방사형 금 길이 비율
    private static readonly float[] SecondaryRevealByStage = { 0f, 0.5f, 1f };  //단계별 보조 금 표시 비율

    private class CrackLine
    {
        public List<Vector2> points;  //지그재그 폴리라인
        public bool isRadial;
    }

    [MenuItem("Tools/BounceSquad/Generate Breakable Wall")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutputFolder);

        Material bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        Material crackMaterial = CreateOrUpdateCrackMaterial();

        // 이전에 생성한 메시 에셋은 새로 만든다 (프리팹이 참조를 새로 받음)
        AssetDatabase.DeleteAsset(MeshAssetPath);
        Mesh meshContainer = null;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
            }

            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(visual.GetChild(i).gameObject);
            }

            int wallLayer = root.layer;
            visual.gameObject.layer = wallLayer;

            Renderer body = CreateBody(visual, bodyMaterial, wallLayer);
            Renderer crackFront = CreateCrackQuad("CrackFront", visual, crackMaterial, wallLayer, false);
            Renderer crackBack = CreateCrackQuad("CrackBack", visual, crackMaterial, wallLayer, true);

            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = new Vector3(Width, Height, Thickness);
                box.center = new Vector3(0f, Height * 0.5f, 0f);
            }

            List<BreakableWall.FractureVariant> variants = new List<BreakableWall.FractureVariant>();

            for (int v = 0; v < VariantSeeds.Length; v++)
            {
                Random.State previousState = Random.state;
                Random.InitState(VariantSeeds[v]);

                List<List<Vector2>> polygons = Fracture(out List<CrackLine> cracks, out Vector2 impact);

                Random.state = previousState;

                GameObject shardRoot = new GameObject($"Shards_{v}");
                shardRoot.layer = wallLayer;
                shardRoot.transform.SetParent(visual, false);

                List<Transform> shards = new List<Transform>();
                for (int p = 0; p < polygons.Count; p++)
                {
                    string shardName = $"Shard_{v}_{p}";
                    Mesh mesh = BuildShardMesh(polygons[p], out Vector2 centroid);
                    mesh.name = shardName;

                    if (meshContainer == null)
                    {
                        meshContainer = mesh;
                        AssetDatabase.CreateAsset(mesh, MeshAssetPath);  //메인 에셋 이름이 파일명으로 바뀌므로 다시 지정
                        mesh.name = shardName;
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(mesh, meshContainer);
                    }

                    GameObject shard = new GameObject(shardName);
                    shard.layer = wallLayer;
                    shard.transform.SetParent(shardRoot.transform, false);
                    shard.transform.localPosition = new Vector3(centroid.x, centroid.y, 0f);
                    shard.AddComponent<MeshFilter>().sharedMesh = mesh;
                    SetupRenderer(shard.AddComponent<MeshRenderer>(), bodyMaterial);
                    shards.Add(shard.transform);
                }

                shardRoot.SetActive(false);

                Texture2D[] stages = new Texture2D[CrackStageCount];
                for (int s = 0; s < CrackStageCount; s++)
                {
                    stages[s] = SaveCrackTexture($"{OutputFolder}/WallCrack_V{v}_S{s + 1}.png", cracks, impact, s);
                }

                variants.Add(new BreakableWall.FractureVariant { shardRoot = shardRoot, shards = shards.ToArray(), crackStages = stages });
            }

            BreakableWall wall = root.GetComponent<BreakableWall>();
            SerializedObject so = new SerializedObject(wall);
            so.FindProperty("visualRoot").objectReferenceValue = visual;
            so.FindProperty("bodyRenderer").objectReferenceValue = body;

            SerializedProperty crackProp = so.FindProperty("crackRenderers");
            crackProp.arraySize = 2;
            crackProp.GetArrayElementAtIndex(0).objectReferenceValue = crackFront;
            crackProp.GetArrayElementAtIndex(1).objectReferenceValue = crackBack;

            SerializedProperty variantsProp = so.FindProperty("variants");
            variantsProp.arraySize = variants.Count;
            for (int v = 0; v < variants.Count; v++)
            {
                SerializedProperty element = variantsProp.GetArrayElementAtIndex(v);
                element.FindPropertyRelative("shardRoot").objectReferenceValue = variants[v].shardRoot;

                SerializedProperty shardsProp = element.FindPropertyRelative("shards");
                shardsProp.arraySize = variants[v].shards.Length;
                for (int i = 0; i < variants[v].shards.Length; i++) shardsProp.GetArrayElementAtIndex(i).objectReferenceValue = variants[v].shards[i];

                SerializedProperty stagesProp = element.FindPropertyRelative("crackStages");
                stagesProp.arraySize = variants[v].crackStages.Length;
                for (int s = 0; s < variants[v].crackStages.Length; s++) stagesProp.GetArrayElementAtIndex(s).objectReferenceValue = variants[v].crackStages[s];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BreakableWallGenerator] 생성 완료: 패턴 {VariantSeeds.Length}개, 금 단계 {CrackStageCount}개");
    }

    // ===== 조각 생성 =====

    private static List<List<Vector2>> Fracture(out List<CrackLine> cracks, out Vector2 impact)
    {
        float halfWidth = Width * 0.5f;
        List<List<Vector2>> polygons = new List<List<Vector2>>
        {
            new List<Vector2> { new Vector2(-halfWidth, 0f), new Vector2(halfWidth, 0f), new Vector2(halfWidth, Height), new Vector2(-halfWidth, Height) }
        };

        cracks = new List<CrackLine>();
        impact = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0.4f, 0.8f));

        // 1) 충격 지점을 지나는 방사형 절단 (모든 조각에 적용)
        int radialCount = 4;
        float baseAngle = Random.Range(0f, 180f);
        for (int i = 0; i < radialCount; i++)
        {
            float angle = (baseAngle + i * (180f / radialCount) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            List<List<Vector2>> next = new List<List<Vector2>>();
            foreach (List<Vector2> polygon in polygons)
            {
                if (SplitPolygon(polygon, impact, direction, out List<Vector2> left, out List<Vector2> right, out _, out _))
                {
                    next.Add(left);
                    next.Add(right);
                }
                else
                {
                    next.Add(polygon);
                }
            }
            polygons = next;

            // 금은 충격 지점에서 양쪽으로 뻗는 두 줄로 기록 (단계별로 바깥쪽까지 점점 드러남)
            if (ClipLineToRect(impact, direction, out Vector2 a, out Vector2 b))
            {
                cracks.Add(new CrackLine { points = Jagged(impact, a), isRadial = true });
                cracks.Add(new CrackLine { points = Jagged(impact, b), isRadial = true });
            }
        }

        // 2) 큰 조각을 몇 번 더 잘라 잔금 + 크기 다양화
        int secondaryCuts = 3;
        for (int i = 0; i < secondaryCuts; i++)
        {
            polygons.Sort((x, y) => Area(y).CompareTo(Area(x)));
            List<Vector2> target = polygons[0];
            Vector2 center = Centroid(target);
            float angle = Random.Range(0f, Mathf.PI);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            if (SplitPolygon(target, center, direction, out List<Vector2> left, out List<Vector2> right, out Vector2 segmentA, out Vector2 segmentB)
                && Area(left) > 0.03f && Area(right) > 0.03f)
            {
                polygons.RemoveAt(0);
                polygons.Add(left);
                polygons.Add(right);
                cracks.Add(new CrackLine { points = Jagged(segmentA, segmentB), isRadial = false });
            }
        }

        return polygons;
    }

    // 볼록 다각형을 직선(point, direction)으로 둘로 자른다
    private static bool SplitPolygon(List<Vector2> polygon, Vector2 point, Vector2 direction,
        out List<Vector2> left, out List<Vector2> right, out Vector2 cutA, out Vector2 cutB)
    {
        left = new List<Vector2>();
        right = new List<Vector2>();
        List<Vector2> cutPoints = new List<Vector2>();

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            float sideCurrent = Cross(direction, current - point);
            float sideNext = Cross(direction, next - point);

            if (sideCurrent >= 0f) left.Add(current);
            if (sideCurrent <= 0f) right.Add(current);

            if ((sideCurrent > 0f && sideNext < 0f) || (sideCurrent < 0f && sideNext > 0f))
            {
                float t = sideCurrent / (sideCurrent - sideNext);
                Vector2 intersection = Vector2.Lerp(current, next, t);
                left.Add(intersection);
                right.Add(intersection);
                cutPoints.Add(intersection);
            }
        }

        cutA = cutPoints.Count > 0 ? cutPoints[0] : Vector2.zero;
        cutB = cutPoints.Count > 1 ? cutPoints[1] : cutA;

        return cutPoints.Count == 2 && left.Count >= 3 && right.Count >= 3;
    }

    private static bool ClipLineToRect(Vector2 point, Vector2 direction, out Vector2 a, out Vector2 b)
    {
        float halfWidth = Width * 0.5f;
        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        ClipAxis(point.x, direction.x, -halfWidth, halfWidth, ref tMin, ref tMax);
        ClipAxis(point.y, direction.y, 0f, Height, ref tMin, ref tMax);

        a = point + direction * tMin;
        b = point + direction * tMax;
        return tMin < tMax;
    }

    private static void ClipAxis(float origin, float dir, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(dir) < 1e-5f) return;

        float t1 = (min - origin) / dir;
        float t2 = (max - origin) / dir;
        tMin = Mathf.Max(tMin, Mathf.Min(t1, t2));
        tMax = Mathf.Min(tMax, Mathf.Max(t1, t2));
    }

    private static List<Vector2> Jagged(Vector2 from, Vector2 to)  //실제 금처럼 약간 지그재그
    {
        List<Vector2> points = new List<Vector2> { from };
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / 0.07f));
        Vector2 perpendicular = new Vector2(-(to - from).y, (to - from).x).normalized;

        for (int i = 1; i < steps; i++)
        {
            points.Add(Vector2.Lerp(from, to, (float)i / steps) + perpendicular * Random.Range(-0.018f, 0.018f));
        }

        points.Add(to);
        return points;
    }

    private static Mesh BuildShardMesh(List<Vector2> polygon, out Vector2 centroid)
    {
        Vector2 center = Centroid(polygon);  //out 파라미터는 로컬 함수에서 못 쓰므로 복사
        centroid = center;
        if (SignedArea(polygon) < 0f) polygon.Reverse();  //CCW로 통일

        float halfThickness = Thickness * 0.5f;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        Vector3 ToLocal(Vector2 p, float z) => new Vector3(p.x - center.x, p.y - center.y, z);
        Vector2 ToUv(Vector2 p) => new Vector2((p.x + Width * 0.5f) / Width, p.y / Height);

        // 앞면(-Z), 뒷면(+Z)
        AddCap(polygon, -halfThickness, Vector3.back);
        AddCap(polygon, halfThickness, Vector3.forward);

        // 옆면 (깨진 단면)
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            Vector2 edge = b - a;
            Vector3 outward = new Vector3(edge.y, -edge.x, 0f).normalized;

            int start = vertices.Count;
            vertices.Add(ToLocal(a, -halfThickness));
            vertices.Add(ToLocal(b, -halfThickness));
            vertices.Add(ToLocal(b, halfThickness));
            vertices.Add(ToLocal(a, halfThickness));
            for (int k = 0; k < 4; k++) normals.Add(outward);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

            AddTriangle(start, start + 1, start + 2, outward);
            AddTriangle(start, start + 2, start + 3, outward);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;

        void AddCap(List<Vector2> poly, float z, Vector3 normal)
        {
            int start = vertices.Count;
            foreach (Vector2 p in poly)
            {
                vertices.Add(ToLocal(p, z));
                normals.Add(normal);
                uvs.Add(ToUv(p));
            }

            for (int i = 1; i < poly.Count - 1; i++) AddTriangle(start, start + i, start + i + 1, normal);
        }

        void AddTriangle(int i0, int i1, int i2, Vector3 desiredNormal)  //Unity 앞면 = cross(v1-v0, v2-v0) 방향
        {
            Vector3 faceNormal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);

            if (Vector3.Dot(faceNormal, desiredNormal) >= 0f)
            {
                triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            }
            else
            {
                triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
            }
        }
    }

    // ===== 금 텍스처 =====

    private static Texture2D SaveCrackTexture(string path, List<CrackLine> cracks, Vector2 impact, int stageIndex)
    {
        Color[] pixels = new Color[TextureWidth * TextureHeight];  //투명으로 초기화

        int secondaryTotal = 0;
        foreach (CrackLine crack in cracks) if (!crack.isRadial) secondaryTotal++;
        int secondaryVisible = Mathf.CeilToInt(secondaryTotal * SecondaryRevealByStage[stageIndex]);
        int secondaryIndex = 0;

        Color highlight = new Color(1f, 1f, 1f, 0.35f);
        Color dark = new Color(0.1f, 0.12f, 0.16f, 0.9f);
        float thickness = 1.1f + stageIndex * 0.35f;  //단계가 올라갈수록 금이 굵어짐

        foreach (CrackLine crack in cracks)
        {
            float reveal;
            if (crack.isRadial)
            {
                reveal = RadialRevealByStage[stageIndex];
            }
            else
            {
                reveal = secondaryIndex < secondaryVisible ? 1f : 0f;
                secondaryIndex++;
            }

            if (reveal <= 0f) continue;

            List<Vector2> visiblePoints = TrimPolyline(crack.points, reveal);
            DrawPolyline(pixels, visiblePoints, highlight, thickness + 0.6f, new Vector2(1f, -1f));
            DrawPolyline(pixels, visiblePoints, dark, thickness, Vector2.zero);
        }

        // 충격 지점 주변의 작은 파임
        DrawDisc(pixels, ToPixel(impact), 2.2f + stageIndex, dark);

        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static List<Vector2> TrimPolyline(List<Vector2> points, float ratio)  //앞쪽(충격 지점 쪽)부터 ratio 길이만큼
    {
        float total = 0f;
        for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);

        float target = total * ratio;
        List<Vector2> result = new List<Vector2> { points[0] };
        float walked = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            float segment = Vector2.Distance(points[i - 1], points[i]);
            if (walked + segment >= target)
            {
                result.Add(Vector2.Lerp(points[i - 1], points[i], (target - walked) / Mathf.Max(segment, 1e-5f)));
                break;
            }

            walked += segment;
            result.Add(points[i]);
        }

        return result;
    }

    private static void DrawPolyline(Color[] pixels, List<Vector2> points, Color color, float radius, Vector2 pixelOffset)
    {
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = ToPixel(points[i - 1]) + pixelOffset;
            Vector2 b = ToPixel(points[i]) + pixelOffset;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2f));

            for (int s = 0; s <= steps; s++)
            {
                // 끝으로 갈수록 가늘어지게
                float taper = Mathf.Lerp(1f, 0.6f, (i - 1 + (float)s / steps) / Mathf.Max(1, points.Count - 1));
                DrawDisc(pixels, Vector2.Lerp(a, b, (float)s / steps), radius * taper, color);
            }
        }
    }

    private static void DrawDisc(Color[] pixels, Vector2 center, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(TextureWidth - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(TextureHeight - 1, Mathf.CeilToInt(center.y + radius));

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float coverage = Mathf.Clamp01(radius - distance + 0.5f);
            if (coverage <= 0f) continue;

            int index = y * TextureWidth + x;
            Color existing = pixels[index];
            float alpha = color.a * coverage;

            // 위에 덮어 그리기 (알파 합성)
            float outAlpha = alpha + existing.a * (1f - alpha);
            if (outAlpha <= 0f) continue;

            Color blended = (color * alpha + existing * existing.a * (1f - alpha)) / outAlpha;
            blended.a = outAlpha;
            pixels[index] = blended;
        }
    }

    private static Vector2 ToPixel(Vector2 local)
    {
        return new Vector2((local.x + Width * 0.5f) / Width * (TextureWidth - 1), local.y / Height * (TextureHeight - 1));
    }

    // ===== 프리팹 구성 요소 =====

    private static Renderer CreateBody(Transform visual, Material material, int layer)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.name = "Body";
        body.layer = layer;
        body.transform.SetParent(visual, false);
        body.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
        body.transform.localScale = new Vector3(Width, Height, Thickness);

        MeshRenderer renderer = body.GetComponent<MeshRenderer>();
        SetupRenderer(renderer, material);
        return renderer;
    }

    private static Renderer CreateCrackQuad(string name, Transform visual, Material material, int layer, bool back)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = name;
        quad.layer = layer;
        quad.transform.SetParent(visual, false);

        float offset = Thickness * 0.5f + 0.004f;
        quad.transform.localPosition = new Vector3(0f, Height * 0.5f, back ? offset : -offset);
        quad.transform.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        quad.transform.localScale = new Vector3(back ? -Width : Width, Height, 1f);  //뒷면은 좌우 반전해 같은 자리에 금이 보이게

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        SetupRenderer(renderer, material);
        renderer.enabled = false;
        return renderer;
    }

    private static void SetupRenderer(MeshRenderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static Material CreateOrUpdateCrackMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CrackMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, CrackMaterialPath);
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetColor("_BaseColor", Color.white);
        // 큐를 올리면 Screen Space - Camera 캔버스(Transparent 큐) UI보다 나중에 그려져 증강/일시정지 창 위로 튀어나온다.
        // 몸체와 같은 큐로 두면 거리 정렬로 앞면 금은 몸체 다음, UI는 맨 마지막에 그려진다.
        material.SetFloat("_QueueOffset", 0f);  //URP가 머티리얼 검증 때 renderQueue를 Transparent + _QueueOffset으로 다시 계산함
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ===== 다각형 유틸 =====

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static float SignedArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++) area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        return area * 0.5f;
    }

    private static float Area(List<Vector2> polygon) => Mathf.Abs(SignedArea(polygon));

    private static Vector2 Centroid(List<Vector2> polygon)
    {
        Vector2 sum = Vector2.zero;
        foreach (Vector2 p in polygon) sum += p;
        return sum / polygon.Count;
    }
}
