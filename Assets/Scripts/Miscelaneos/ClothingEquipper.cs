using UnityEngine;
using System.Collections.Generic;

public class ClothingEquipper : MonoBehaviour
{
    public enum Slot { Up, Bottom, Shoes }

    [System.Serializable]
    public class BoneMapEntry
    {
        public string oldBone;
        public string newBone;
        public BoneMapEntry() { }
        public BoneMapEntry(string o, string n) { oldBone = o; newBone = n; }
    }

    [Header("Esqueleto")]
    public Transform rootBone;                 // se busca solo por nombre
    public string rootBoneName = "ORG-spine";
    public SkinnedMeshRenderer bodyRenderer;   // malla del cuerpo (Character); se busca solo

    [Header("Tabla de huesos (esqueleto viejo -> nuevo). Editable.")]
    public List<BoneMapEntry> boneTable = DefaultMap();

    [Header("Ropa inicial (opcional)")]
    public GameObject startUp;
    public GameObject startBottom;
    public GameObject startShoes;

    class Equipped { public GameObject holder; public List<Mesh> meshes = new List<Mesh>(); }

    readonly Dictionary<Slot, Equipped> equipped = new Dictionary<Slot, Equipped>();
    readonly Dictionary<string, Transform> boneByName = new Dictionary<string, Transform>();
    readonly Dictionary<Transform, Matrix4x4> restRel = new Dictionary<Transform, Matrix4x4>();
    readonly Dictionary<string, string> oldToNew = new Dictionary<string, string>();
    Matrix4x4 bodyToRoot;
    bool ready;

    static List<BoneMapEntry> DefaultMap()
    {
        var m = new List<BoneMapEntry>
        {
            new BoneMapEntry("Root_Bone",   "ORG-spine"),
            new BoneMapEntry("TorsoDown",   "ORG-spine"),
            new BoneMapEntry("TorsoMiddle", "ORG-spine.001"),
            new BoneMapEntry("TorsoUp",     "ORG-spine.002"),
            new BoneMapEntry("Head",        "ORG-spine.006"),
        };
        string[,] sides = { { "Left", "L" }, { "Right", "R" } };
        for (int i = 0; i < 2; i++)
        {
            string o = sides[i, 0], n = sides[i, 1];
            m.Add(new BoneMapEntry("ArmUp_" + o, "ORG-upper_arm." + n));
            m.Add(new BoneMapEntry("ArmDown_" + o, "ORG-forearm." + n));
            m.Add(new BoneMapEntry("HandStart_" + o, "ORG-hand." + n));
            m.Add(new BoneMapEntry("HandMiddle_" + o, "ORG-hand." + n));
            m.Add(new BoneMapEntry("HandFinish_" + o, "ORG-hand." + n));
            m.Add(new BoneMapEntry("ThumbStart_" + o, "ORG-thumb.01." + n));
            m.Add(new BoneMapEntry("ThumbEnd_" + o, "ORG-thumb.02." + n));
            m.Add(new BoneMapEntry("LegUp_" + o, "ORG-thigh." + n));
            m.Add(new BoneMapEntry("LegDown_" + o, "ORG-shin." + n));
            m.Add(new BoneMapEntry("KneeIK_" + o, "ORG-shin." + n));
            m.Add(new BoneMapEntry("HeelIK_" + o, "ORG-foot." + n));
            m.Add(new BoneMapEntry("FootStart_" + o, "ORG-foot." + n));
            m.Add(new BoneMapEntry("FootEnd_" + o, "ORG-toe." + n));
        }
        return m;
    }

    void Awake()
    {
        if (rootBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == rootBoneName) { rootBone = t; break; }
        }
        if (rootBone == null)
        {
            Debug.LogError($"[ClothingEquipper] No se encontró el hueso raíz '{rootBoneName}'.");
            return;
        }

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (bodyRenderer == null)
        {
            Debug.LogError("[ClothingEquipper] No se encontró la malla del cuerpo.");
            return;
        }

        // Pose de reposo del personaje (antes de que el Animator la mueva)
        Matrix4x4 w2l = transform.worldToLocalMatrix;
        foreach (var t in rootBone.GetComponentsInChildren<Transform>(true))
        {
            boneByName[t.name] = t;
            restRel[t] = w2l * t.localToWorldMatrix;
        }
        bodyToRoot = w2l * bodyRenderer.transform.localToWorldMatrix;

        foreach (var e in boneTable)
            if (!string.IsNullOrEmpty(e.oldBone)) oldToNew[e.oldBone] = e.newBone;

        ready = true;
    }

    void Start()
    {
        if (startUp) Equip(Slot.Up, startUp);
        if (startBottom) Equip(Slot.Bottom, startBottom);
        if (startShoes) Equip(Slot.Shoes, startShoes);
    }

    public void Equip(Slot slot, GameObject prefab)
    {
        Unequip(slot);
        if (!ready || prefab == null) return;
        equipped[slot] = EquipSkinned(prefab, slot);
    }

    public void Unequip(Slot slot)
    {
        if (!equipped.TryGetValue(slot, out var e)) return;
        if (e.holder) Destroy(e.holder);
        foreach (var m in e.meshes) if (m) Destroy(m);
        equipped.Remove(slot);
    }

    Transform Resolve(string oldName, out bool viaTable)
    {
        viaTable = false;
        if (string.IsNullOrEmpty(oldName)) return null;
        if (boneByName.TryGetValue(oldName, out var direct)) return direct;
        if (oldToNew.TryGetValue(oldName, out var newName) &&
            boneByName.TryGetValue(newName, out var mapped))
        {
            viaTable = true;
            return mapped;
        }
        return null;
    }

    Equipped EquipSkinned(GameObject prefab, Slot slot)
    {
        var result = new Equipped();
        result.holder = new GameObject("Clothing_" + slot);
        result.holder.transform.SetParent(transform, false);

        var temp = Instantiate(prefab);
        int directCount = 0, tableCount = 0, missingCount = 0;

        foreach (var smr in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;

            var oldBones = smr.bones;
            var newBones = new Transform[oldBones.Length];
            bool needRebind = false;

            for (int i = 0; i < oldBones.Length; i++)
            {
                string oldName = oldBones[i] ? oldBones[i].name : null;
                var found = Resolve(oldName, out bool viaTable);

                if (found == null)
                {
                    Debug.LogWarning($"[ClothingEquipper] '{prefab.name}': hueso '{oldName}' sin equivalente. Se usa el hueso raíz.");
                    found = rootBone;
                    viaTable = true;
                    missingCount++;
                }
                else if (viaTable) tableCount++;
                else directCount++;

                if (viaTable) needRebind = true;
                newBones[i] = found;
            }

            Transform newRoot = null;
            if (smr.rootBone != null) newRoot = Resolve(smr.rootBone.name, out _);
            if (newRoot == null) newRoot = rootBone;

            if (needRebind)
            {
                // Recalcula los bindposes para que la prenda calce sobre el cuerpo en reposo
                var mesh = Instantiate(smr.sharedMesh);
                var bind = new Matrix4x4[newBones.Length];
                for (int i = 0; i < newBones.Length; i++)
                    bind[i] = restRel[newBones[i]].inverse * bodyToRoot;
                mesh.bindposes = bind;
                smr.sharedMesh = mesh;
                result.meshes.Add(mesh);
                smr.updateWhenOffscreen = true;   // los bounds originales ya no sirven
            }

            smr.transform.SetParent(result.holder.transform, false);
            smr.bones = newBones;
            smr.rootBone = newRoot;
        }

        Destroy(temp);   // descarta el esqueleto viejo de la prenda
        Debug.Log($"[ClothingEquipper] '{prefab.name}': {directCount} huesos directos, {tableCount} por tabla, {missingCount} sin equivalente.");
        return result;
    }
}