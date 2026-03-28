using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class VertexPaintTextureArrayBaker : EditorWindow
{
    private const string DefaultOutputDirectory = "Assets/P2/Shadergraph/Textures";

    private Material targetMaterial;
    private List<Texture2D> albedoSlices = new List<Texture2D>();
    private List<Texture2D> normalSlices = new List<Texture2D>();
    private List<Texture2D> maohsSlices = new List<Texture2D>();

    private int outputSize = 1024;
    private bool generateMipmaps = true;
    private string outputDirectory = DefaultOutputDirectory;

    [MenuItem("Tools/P2/VertexPaint Texture Array Baker")]
    private static void OpenWindow()
    {
        var window = GetWindow<VertexPaintTextureArrayBaker>("VertexPaint Array Baker");
        window.minSize = new Vector2(520f, 520f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        targetMaterial = (Material)EditorGUILayout.ObjectField("VertexPaint Material", targetMaterial, typeof(Material), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load A/B From Material"))
            {
                LoadFromMaterialAB();
            }

            if (GUILayout.Button("Clear Lists"))
            {
                albedoSlices.Clear();
                normalSlices.Clear();
                maohsSlices.Clear();
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Source Slices", EditorStyles.boldLabel);
        DrawTextureList("Albedo Slices", albedoSlices);
        DrawTextureList("Normal Slices", normalSlices);
        DrawTextureList("MAOHS Slices", maohsSlices);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputSize = EditorGUILayout.IntPopup("Array Size", outputSize, new[] { "256", "512", "1024", "2048", "4096" }, new[] { 256, 512, 1024, 2048, 4096 });
        generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", generateMipmaps);
        outputDirectory = EditorGUILayout.TextField("Output Folder", outputDirectory);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Bake Arrays And Assign To Material", GUILayout.Height(34)))
        {
            BakeAndAssign();
        }

        EditorGUILayout.HelpBox(
            "Ordena las tres listas con el mismo índice por set. Ejemplo: índice 0 = Set A, índice 1 = Set B.\n" +
            "El script crea/actualiza: Albedo_Array.asset, Normal_Array.asset, MAOHS_Array.asset y los asigna al material.",
            MessageType.Info);
    }

    private void DrawTextureList(string title, List<Texture2D> list)
    {
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

        int removeIndex = -1;
        for (int i = 0; i < list.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                list[i] = (Texture2D)EditorGUILayout.ObjectField($"{title} [{i}]", list[i], typeof(Texture2D), false);
                if (GUILayout.Button("-", GUILayout.Width(26)))
                {
                    removeIndex = i;
                }
            }
        }

        if (removeIndex >= 0)
        {
            list.RemoveAt(removeIndex);
        }

        if (GUILayout.Button($"Add {title}"))
        {
            list.Add(null);
        }
    }

    private void LoadFromMaterialAB()
    {
        if (targetMaterial == null)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "Asigna primero un material.", "OK");
            return;
        }

        albedoSlices.Clear();
        normalSlices.Clear();
        maohsSlices.Clear();

        TryAddTexture(targetMaterial, "_A_Abledo", albedoSlices);
        TryAddTexture(targetMaterial, "_A_Albedo", albedoSlices);
        TryAddTexture(targetMaterial, "_B_Albedo", albedoSlices);

        TryAddTexture(targetMaterial, "_A_Normal", normalSlices);
        TryAddTexture(targetMaterial, "_B_Normal", normalSlices);

        TryAddTexture(targetMaterial, "_A_MAOHS", maohsSlices);
        TryAddTexture(targetMaterial, "_B_MAOHS", maohsSlices);

        if (albedoSlices.Count > 0 && outputSize <= 0)
        {
            outputSize = albedoSlices[0].width;
        }

        Repaint();
    }

    private static void TryAddTexture(Material mat, string propertyName, List<Texture2D> destination)
    {
        if (!mat.HasProperty(propertyName))
        {
            return;
        }

        var tex = mat.GetTexture(propertyName) as Texture2D;
        if (tex == null)
        {
            return;
        }

        if (!destination.Contains(tex))
        {
            destination.Add(tex);
        }
    }

    private void BakeAndAssign()
    {
        if (targetMaterial == null)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "Asigna un material objetivo.", "OK");
            return;
        }

        if (outputSize <= 0)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "El tamaño de salida debe ser mayor que 0.", "OK");
            return;
        }

        if (!ValidateSliceLists())
        {
            return;
        }

        EnsureDirectoryExists(outputDirectory);

        var albedoArray = BuildArray(albedoSlices, outputSize, false, generateMipmaps);
        var normalArray = BuildArray(normalSlices, outputSize, true, generateMipmaps);
        var maohsArray = BuildArray(maohsSlices, outputSize, true, generateMipmaps);

        if (albedoArray == null || normalArray == null || maohsArray == null)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "No se pudieron crear los arrays. Revisa la consola.", "OK");
            return;
        }

        SaveArrayAsset(albedoArray, $"{outputDirectory}/Albedo_Array.asset");
        SaveArrayAsset(normalArray, $"{outputDirectory}/Normal_Array.asset");
        SaveArrayAsset(maohsArray, $"{outputDirectory}/MAOHS_Array.asset");

        AssignArray(targetMaterial, "_Albedo_Array", albedoArray);
        AssignArray(targetMaterial, "_Normal_Array", normalArray);
        AssignArray(targetMaterial, "_MAOHS_Array", maohsArray);

        if (targetMaterial.HasProperty("_Index_A"))
        {
            targetMaterial.SetFloat("_Index_A", 0f);
        }

        if (targetMaterial.HasProperty("_Index_B"))
        {
            targetMaterial.SetFloat("_Index_B", Mathf.Min(1, albedoSlices.Count - 1));
        }

        EditorUtility.SetDirty(targetMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("VertexPaint Array Baker", "Arrays generados y asignados correctamente.", "OK");
    }

    private bool ValidateSliceLists()
    {
        if (albedoSlices.Count == 0 || normalSlices.Count == 0 || maohsSlices.Count == 0)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "Las listas no pueden estar vacías.", "OK");
            return false;
        }

        if (albedoSlices.Count != normalSlices.Count || albedoSlices.Count != maohsSlices.Count)
        {
            EditorUtility.DisplayDialog("VertexPaint Array Baker", "Albedo, Normal y MAOHS deben tener el mismo número de slices.", "OK");
            return false;
        }

        for (int i = 0; i < albedoSlices.Count; i++)
        {
            if (albedoSlices[i] == null || normalSlices[i] == null || maohsSlices[i] == null)
            {
                EditorUtility.DisplayDialog("VertexPaint Array Baker", "Hay elementos nulos en las listas.", "OK");
                return false;
            }
        }

        return true;
    }

    private static Texture2DArray BuildArray(IReadOnlyList<Texture2D> slices, int size, bool linear, bool mipmaps)
    {
        if (slices == null || slices.Count == 0)
        {
            return null;
        }

        var array = new Texture2DArray(size, size, slices.Count, TextureFormat.RGBA32, mipmaps, linear)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };

        for (int i = 0; i < slices.Count; i++)
        {
            Texture2D converted = ConvertToReadableTexture(slices[i], size, size, linear);
            if (converted == null)
            {
                return null;
            }

            array.SetPixels(converted.GetPixels(), i, 0);
            Object.DestroyImmediate(converted);
        }

        array.Apply(mipmaps, false);
        return array;
    }

    private static Texture2D ConvertToReadableTexture(Texture2D source, int width, int height, bool linear)
    {
        if (source == null)
        {
            return null;
        }

        var previousActive = RenderTexture.active;
        RenderTextureReadWrite rw = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, rw);

        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        readable.Apply(false, false);

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }

    private static void SaveArrayAsset(Texture2DArray array, string path)
    {
        string normalizedPath = path.Replace("\\", "/");

        if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(normalizedPath) != null)
        {
            AssetDatabase.DeleteAsset(normalizedPath);
        }

        AssetDatabase.CreateAsset(array, normalizedPath);
    }

    private static void AssignArray(Material material, string propertyName, Texture2DArray array)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, array);
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        string normalized = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string[] parts = normalized.Split('/');
        if (parts.Length == 0)
        {
            return;
        }

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
