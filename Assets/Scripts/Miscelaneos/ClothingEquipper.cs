using UnityEngine;
using System.Collections.Generic;

public class ClothingEquipper : MonoBehaviour
{
    public enum Slot { Up, Bottom, Shoes }

    public Transform fallbackRoot;       // Root_Bone del personaje

    [Header("Ropa inicial (opcional)")]
    public GameObject startUp;
    public GameObject startBottom;
    public GameObject startShoes;

    readonly Dictionary<Slot, GameObject> equipped = new Dictionary<Slot, GameObject>();

    void Start()
    {
        if (startUp) Equip(Slot.Up, startUp);
        if (startBottom) Equip(Slot.Bottom, startBottom);
        if (startShoes) Equip(Slot.Shoes, startShoes);
    }

    public void Equip(Slot slot, GameObject prefab)
    {
        Unequip(slot);
        if (prefab) equipped[slot] = EquipSkinned(prefab);
    }

    public void Unequip(Slot slot)
    {
        if (equipped.TryGetValue(slot, out var go) && go) Destroy(go);
        equipped.Remove(slot);
    }

    GameObject EquipSkinned(GameObject prefab)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>())
            bones[t.name] = t;

        var temp = Instantiate(prefab);
        var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>();

        var newBones = new Transform[smr.bones.Length];
        for (int i = 0; i < newBones.Length; i++)
            bones.TryGetValue(smr.bones[i].name, out newBones[i]);

        Transform root = fallbackRoot;
        if (smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out var r))
            root = r;

        smr.transform.SetParent(transform, false);
        smr.bones = newBones;
        smr.rootBone = root;

        Destroy(temp);   // descarta el esqueleto duplicado de la prenda
        return smr.gameObject;
    }
}