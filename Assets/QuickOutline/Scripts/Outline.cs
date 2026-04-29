using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public enum Mode
    {
        OutlineAll, OutlineVisible, OutlineHidden, OutlineAndSilhouette, SilhouetteOnly
    }

    [SerializeField] private Mode outlineMode = Mode.OutlineAll;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 2f;

    private Renderer[] currentRenderers;
    private Material outlineMaskMaterial;
    private Material outlineFillMaterial;
    private bool needsUpdate;

    private void Awake()
    {
        outlineMaskMaterial = Instantiate(Resources.Load<Material>("Materials/OutlineMask"));
        outlineFillMaterial = Instantiate(Resources.Load<Material>("Materials/OutlineFill"));
        outlineMaskMaterial.name = "OutlineMask (Instance)";
        outlineFillMaterial.name = "OutlineFill (Instance)";
    }

    private void OnDestroy()
    {
        Destroy(outlineMaskMaterial);
        Destroy(outlineFillMaterial);
    }

    private void FixedUpdate()
    {
        if (needsUpdate)
        {
            needsUpdate = false;
            UpdateMaterialProperties();
        }
    }

    public void SetTarget(GameObject target)
    {
        // Удаляем outline со старых рендереров
        if (currentRenderers != null)
        {
            foreach (var renderer in currentRenderers)
            {
                var materials = renderer.sharedMaterials.ToList();
                materials.Remove(outlineMaskMaterial);
                materials.Remove(outlineFillMaterial);
                renderer.materials = materials.ToArray();
            }
        }

        // Находим новые рендереры
        if (target == null)
        {
            currentRenderers = null;
            return;
        }

        currentRenderers = target.GetComponentsInChildren<Renderer>();
        foreach (var renderer in currentRenderers)
        {
            var materials = renderer.sharedMaterials.ToList();
            materials.Add(outlineMaskMaterial);
            materials.Add(outlineFillMaterial);
            renderer.materials = materials.ToArray();
        }

        // Пересчитываем сглаженные нормали для новых мешей
        foreach (var renderer in currentRenderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                StoreSmoothNormals(meshFilter.sharedMesh);
        }
        needsUpdate = true;
    }

    private void StoreSmoothNormals(Mesh mesh)
    {
        // var groups = mesh.vertices.Select((v, i) => new { v, i }).GroupBy(x => x.v);
        // var smoothNormals = new List<Vector3>(mesh.normals);
        // foreach (var group in groups)
        // {
        //     if (group.Count() == 1) continue;
        //     var avg = Vector3.zero;
        //     foreach (var item in group) avg += smoothNormals[item.i];
        //     avg.Normalize();
        //     foreach (var item in group) smoothNormals[item.i] = avg;
        // }
        // mesh.SetUVs(3, smoothNormals);
    }

    public void SetColor(Color color)
    {
        outlineColor = color;
        needsUpdate = true;
    }

    private void UpdateMaterialProperties()
    {
        if (outlineFillMaterial == null) return;
        outlineFillMaterial.SetColor("_OutlineColor", outlineColor);
        switch (outlineMode)
        {
            case Mode.OutlineAll:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;
            case Mode.OutlineVisible:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;
            case Mode.OutlineHidden:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;
            case Mode.OutlineAndSilhouette:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;
            case Mode.SilhouetteOnly:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                break;
        }
    }
}