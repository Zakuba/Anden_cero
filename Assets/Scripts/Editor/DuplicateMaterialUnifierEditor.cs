using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DuplicateMaterialUnifierEditor : EditorWindow
{
    [MenuItem("Tools/Optimización/Unificar Materiales por Textura Base en Selección")]
    public static void UnifyMaterialsByTextureInSelected()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Sin selección",
                "Hacé clic en Pared izq, Pared der o ParteDeArriba en la Jerarquía antes de ejecutar esta herramienta.",
                "Entendido"
            );
            return;
        }

        // Agrupa materiales opacos que usan exactamente la misma textura base (_BaseMap o _MainTex) y el mismo Shader
        Dictionary<string, Material> textureToMasterMat = new Dictionary<string, Material>();

        int renderersModified = 0;
        int slotsReplaced = 0;

        foreach (GameObject root in selectedObjects)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

            foreach (MeshRenderer mr in renderers)
            {
                if (mr.gameObject == root && root.GetComponent<MultiMaterialMeshCombiner>() != null)
                    continue;

                Material[] mats = mr.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    Material currentMat = mats[i];
                    if (currentMat == null) continue;

                    Texture baseTex = null;
                    if (currentMat.HasProperty("_BaseMap"))
                        baseTex = currentMat.GetTexture("_BaseMap");
                    else if (currentMat.HasProperty("_MainTex"))
                        baseTex = currentMat.GetTexture("_MainTex");

                    // Clave única: Shader + Textura Base (o nombre limpio si no tiene textura)
                    string texKey = baseTex != null ? baseTex.GetEntityId().ToString() : currentMat.name.Replace(" (Instance)", "").Trim();
                    string key = $"{currentMat.shader.name}_{texKey}";

                    if (!textureToMasterMat.TryGetValue(key, out Material masterMat))
                    {
                        textureToMasterMat[key] = currentMat;
                        masterMat = currentMat;
                    }

                    if (currentMat != masterMat)
                    {
                        mats[i] = masterMat;
                        changed = true;
                        slotsReplaced++;
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(mr, "Unificar Materiales por Textura");
                    mr.sharedMaterials = mats;
                    EditorUtility.SetDirty(mr);
                    renderersModified++;
                }
            }
        }

        Debug.Log($"[Unificador por Textura] Renderers actualizados: {renderersModified} | Slots unificados: {slotsReplaced} | Materiales maestros resultantes: {textureToMasterMat.Count}");
    }
}