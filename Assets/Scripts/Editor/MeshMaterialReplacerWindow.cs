using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class MeshMaterialReplacerWindow : EditorWindow
{
    private GameObject _targetObject;
    private DefaultAsset _meshesFolder;
    private DefaultAsset _materialsFolder;

    private Dictionary<string, string> _meshPathCache; 
    private Dictionary<string, string> _materialPathCache; 

    [MenuItem("Tools/Mesh & Material Replacer")]
    public static void ShowWindow()
    {
        GetWindow<MeshMaterialReplacerWindow>("Mesh/Material Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Drag & Drop Areas", EditorStyles.boldLabel);

        _targetObject = (GameObject)EditorGUILayout.ObjectField(
            "Target Object", _targetObject, typeof(GameObject), true);

        EditorGUILayout.BeginHorizontal();
        _meshesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Meshes Folder", _meshesFolder, typeof(DefaultAsset), false);
        if (_meshesFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_meshesFolder)))
            _meshesFolder = null;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _materialsFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Materials Folder", _materialsFolder, typeof(DefaultAsset), false);
        if (_materialsFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_materialsFolder)))
            _materialsFolder = null;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(_targetObject == null || _meshesFolder == null);
        if (GUILayout.Button("Replace Meshes (exact name match)"))
            ReplaceMeshesInHierarchy();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(_targetObject == null || _materialsFolder == null);
        if (GUILayout.Button("Replace Materials (exact name match)"))
            ReplaceMaterialsInHierarchy();
        EditorGUI.EndDisabledGroup();
    }
  
    // Mesh replacement
   
    private void ReplaceMeshesInHierarchy()
    {
        if (_targetObject == null)
        {
            Debug.LogWarning("No target object assigned.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(_meshesFolder);
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogWarning("Invalid meshes folder.");
            return;
        }

        BuildMeshCache(folderPath);

        if (_meshPathCache == null || _meshPathCache.Count == 0)
        {
            Debug.LogWarning("No meshes found in the selected folder.");
            return;
        }

        var meshFilters = _targetObject.GetComponentsInChildren<MeshFilter>(true);
    
        int replaced = 0;

        EditorUtility.DisplayProgressBar("Replace Meshes", "Processing...", 0f);
        try
        {
            // MeshFilter
            for (int i = 0; i < meshFilters.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Replace Meshes",
                    $"Replacing meshes... ({i + 1})",
                    (float)i /meshFilters.Length);

                MeshFilter mf = meshFilters[i];
                if (mf.sharedMesh == null) continue;

                string currentMeshName = mf.sharedMesh.name;
                if (_meshPathCache.TryGetValue(currentMeshName, out string newMeshPath))
                {
                    Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(newMeshPath);
                    if (newMesh != null)
                    {
                        Undo.RecordObject(mf, "Replace Mesh");
                        mf.sharedMesh = newMesh;
                        EditorUtility.SetDirty(mf);
                        replaced++;
                    }
                }
            }

           
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Mesh replacement finished. Replaced {replaced}  mesh references.");
    }

    private void BuildMeshCache(string folderPath)
    {
        _meshPathCache = new Dictionary<string, string>();
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string meshName = Path.GetFileNameWithoutExtension(assetPath);
            if (!_meshPathCache.ContainsKey(meshName))
                _meshPathCache.Add(meshName, assetPath);
            else
                Debug.LogWarning($"Duplicate mesh name '{meshName}' found. Only the first one will be used.");
        }
    }
 
    // Material replacement
    private void ReplaceMaterialsInHierarchy()
    {
        if (_targetObject == null)
        {
            Debug.LogWarning("No target object assigned.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(_materialsFolder);
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogWarning("Invalid materials folder.");
            return;
        }

        BuildMaterialCache(folderPath);

        if (_materialPathCache == null || _materialPathCache.Count == 0)
        {
            Debug.LogWarning("No materials found in the selected folder.");
            return;
        }

        // Find all renderers (MeshRenderer, SkinnedMeshRenderer)
        var renderers = _targetObject.GetComponentsInChildren<Renderer>(true);
        int totalMaterialSlots = 0;
        int replacedSlots = 0;

        // First pass: count total material slots
        foreach (var rend in renderers)
            totalMaterialSlots += rend.sharedMaterials.Length;

        int processedSlots = 0;

        EditorUtility.DisplayProgressBar("Replace Materials", "Processing...", 0f);
        try
        {
            foreach (var rend in renderers)
            {
                Material[] oldMaterials = rend.sharedMaterials;
                bool changed = false;
                Material[] newMaterials = new Material[oldMaterials.Length];

                for (int i = 0; i < oldMaterials.Length; i++)
                {
                    processedSlots++;
                    EditorUtility.DisplayProgressBar("Replace Materials",
                        $"Processing material slots... ({processedSlots}/{totalMaterialSlots})",
                        (float)processedSlots / totalMaterialSlots);

                    Material oldMat = oldMaterials[i];
                    if (oldMat == null) continue;

                    string matName = oldMat.name;
                    if (_materialPathCache.TryGetValue(matName, out string newMatPath))
                    {
                        Material newMat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
                        if (newMat != null)
                        {
                            newMaterials[i] = newMat;
                            changed = true;
                            replacedSlots++;
                            continue;
                        }
                    }
                    newMaterials[i] = oldMat;
                }

                if (changed)
                {
                    Undo.RecordObject(rend, "Replace Materials");
                    rend.sharedMaterials = newMaterials;
                    EditorUtility.SetDirty(rend);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Material replacement finished. Replaced {replacedSlots} of {totalMaterialSlots} material slots.");
    }

    private void BuildMaterialCache(string folderPath)
    {
        _materialPathCache = new Dictionary<string, string>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string matName = Path.GetFileNameWithoutExtension(assetPath);
            if (!_materialPathCache.ContainsKey(matName))
                _materialPathCache.Add(matName, assetPath);
            else
                Debug.LogWarning($"Duplicate material name '{matName}' found. Only the first one will be used.");
        }
    }
}