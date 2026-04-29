using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FBXExtractorWindow : EditorWindow
{
    private Object _fbxAsset;
    private DefaultAsset _meshesFolder;
    private DefaultAsset _materialsFolder;

    [MenuItem("Tools/FBX Extractor")]
    public static void ShowWindow()
    {
        GetWindow<FBXExtractorWindow>("FBX Extractor");
    }

    private void OnGUI()
    {
        GUILayout.Label("FBX and Folders", EditorStyles.boldLabel);

        _fbxAsset = EditorGUILayout.ObjectField("FBX File", _fbxAsset, typeof(Object), false);
        if (_fbxAsset != null && !_fbxAsset.GetType().IsSubclassOf(typeof(AssetImporter)))
        {
            string path = AssetDatabase.GetAssetPath(_fbxAsset);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                _fbxAsset = null;
        }

        EditorGUILayout.Space();

        _meshesFolder = (DefaultAsset)EditorGUILayout.ObjectField("Meshes Folder", _meshesFolder, typeof(DefaultAsset), false);
        if (_meshesFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_meshesFolder)))
            _meshesFolder = null;

        _materialsFolder = (DefaultAsset)EditorGUILayout.ObjectField("Materials Folder", _materialsFolder, typeof(DefaultAsset), false);
        if (_materialsFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_materialsFolder)))
            _materialsFolder = null;

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(_fbxAsset == null || _meshesFolder == null || _materialsFolder == null);
        if (GUILayout.Button("Extract Meshes, Materials and Textures"))
        {
            ExtractAll();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void ExtractAll()
    {
        string fbxPath = AssetDatabase.GetAssetPath(_fbxAsset);
        string meshesDir = AssetDatabase.GetAssetPath(_meshesFolder);
        string materialsDir = AssetDatabase.GetAssetPath(_materialsFolder);

        EnsureDirectoryExists(meshesDir);
        EnsureDirectoryExists(materialsDir);

        ExtractMeshes(fbxPath, meshesDir);

        ExtractMaterialsAndTextures(fbxPath, materialsDir);

        AssetDatabase.Refresh();
        Debug.Log($"FBX extraction completed.\nMeshes → {meshesDir}\nMaterials → {materialsDir}");
    }

    private void ExtractMeshes(string fbxPath, string targetDir)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        List<Mesh> meshes = new List<Mesh>();
        foreach (var obj in assets)
        {
            if (obj is Mesh mesh && !mesh.name.Contains("__preview") && !string.IsNullOrEmpty(mesh.name))
                meshes.Add(mesh);
        }

        if (meshes.Count == 0)
        {
            Debug.LogWarning("No meshes found in the FBX.");
            return;
        }

        int saved = 0;
        foreach (var mesh in meshes)
        {
            Mesh copy = Object.Instantiate(mesh);
            string safeName = SanitizeFileName(mesh.name);
            string assetPath = Path.Combine(targetDir, $"{safeName}.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(copy, assetPath);
            saved++;
        }
        Debug.Log($"Extracted {saved} meshes to {targetDir}");
    }

    private void ExtractMaterialsAndTextures(string fbxPath, string materialsTargetDir)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        List<Material> materialsToExtract = new List<Material>();
        
        foreach (var obj in subAssets)
        {
            if (obj is Material mat && mat != null)
            {
                if (!mat.name.StartsWith("Default-Material") && !mat.name.StartsWith("Unity-Runtime"))
                    materialsToExtract.Add(mat);
            }
        }

        if (materialsToExtract.Count == 0)
        {
            Debug.LogWarning("No materials found inside the FBX.");
            return;
        }

        string mainAssetPath = fbxPath;
        try
        {
            AssetDatabase.StartAssetEditing();
            
            foreach (Material mat in materialsToExtract)
            {
                string sanitizedName = SanitizeFileName(mat.name);
                string newMaterialPath = Path.Combine(materialsTargetDir, $"{sanitizedName}.mat");
                newMaterialPath = AssetDatabase.GenerateUniqueAssetPath(newMaterialPath);
             
                string error = AssetDatabase.ExtractAsset(mat, newMaterialPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"Failed to extract material {mat.name}: {error}");
                else
                    Debug.Log($"Extracted material: {newMaterialPath}");
            }
          
            AssetDatabase.WriteImportSettingsIfDirty(mainAssetPath);
            AssetDatabase.ImportAsset(mainAssetPath, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path);
            string newFolder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectoryExists(parent);
            AssetDatabase.CreateFolder(parent, newFolder);
        }
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}