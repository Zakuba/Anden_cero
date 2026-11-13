using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MultiMaterialMeshCombiner : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Desactiva los MeshRenderer hijos para no perder sus Colliders ni Tags.")]
    [SerializeField] private bool disableChildRenderersOnly = true;

    [Tooltip("Combina automáticamente al iniciar si no hay una malla guardada asignada.")]
    [SerializeField] private bool combineOnStart = false;

    private void Start()
    {
        // Solo combina en Start si está marcado y no tiene ya un asset válido cargado
        if (combineOnStart && GetComponent<MeshFilter>().sharedMesh == null)
        {
            CombineMeshes();
        }
    }

    [ContextMenu("Combinar y Guardar Malla (.asset)")]
    public void CombineMeshes()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        Dictionary<Material, List<CombineInstance>> materialToMeshList = new Dictionary<Material, List<CombineInstance>>();

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.gameObject == gameObject) continue;
            if (mf.sharedMesh == null) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Material[] materials = mr.sharedMaterials;
            Mesh mesh = mf.sharedMesh;

            int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
            for (int i = 0; i < subMeshCount; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;

                if (!materialToMeshList.ContainsKey(mat))
                {
                    materialToMeshList[mat] = new List<CombineInstance>();
                }

                CombineInstance ci = new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = i,
                    transform = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                };

                materialToMeshList[mat].Add(ci);
            }

            if (disableChildRenderersOnly)
                mr.enabled = false;
            else
                mf.gameObject.SetActive(false);
        }

        if (materialToMeshList.Count == 0)
        {
            Debug.LogWarning("[MeshCombiner] No se encontraron mallas válidas para combinar.");
            return;
        }

        List<CombineInstance> finalCombineList = new List<CombineInstance>();
        List<Material> uniqueMaterials = new List<Material>();

        foreach (var entry in materialToMeshList)
        {
            Mesh subCombinedMesh = new Mesh();
            subCombinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            subCombinedMesh.CombineMeshes(entry.Value.ToArray(), true, true);

            finalCombineList.Add(new CombineInstance
            {
                mesh = subCombinedMesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
            uniqueMaterials.Add(entry.Key);
        }

        Mesh finalMesh = new Mesh();
        finalMesh.name = $"{gameObject.name}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}_Combined";
        finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        finalMesh.CombineMeshes(finalCombineList.ToArray(), false, false);

#if UNITY_EDITOR
        // Guardar la malla físicamente en el proyecto para que persista al dar Play y en builds
        if (!Application.isPlaying)
        {
            string folderPath = "Assets/CombinedMeshes";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "CombinedMeshes");
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{finalMesh.name}.asset");
            AssetDatabase.CreateAsset(finalMesh, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MeshCombiner] Malla guardada permanentemente en: {assetPath}");
        }
#endif

        GetComponent<MeshFilter>().sharedMesh = finalMesh;
        GetComponent<MeshRenderer>().sharedMaterials = uniqueMaterials.ToArray();
    }

    [ContextMenu("Restaurar Paredes Hijos")]
    public void RestoreChildren()
    {
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.gameObject == gameObject) continue;
            mr.gameObject.SetActive(true);
            mr.enabled = true;
        }
        GetComponent<MeshFilter>().sharedMesh = null;
        GetComponent<MeshRenderer>().sharedMaterials = new Material[0];
    }
}