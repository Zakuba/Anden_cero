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

        public BoneMapEntry(string o, string n)
        {
            oldBone = o;
            newBone = n;
        }
    }

    [Header("Esqueleto")]
    public Transform rootBone;
    public string rootBoneName = "ORG-spine";
    public SkinnedMeshRenderer bodyRenderer;

    [Header("Tabla de huesos (esqueleto viejo -> nuevo). Editable.")]
    public List<BoneMapEntry> boneTable = DefaultMap();

    [Header("Ropa inicial (opcional)")]
    public GameObject startUp;
    public GameObject startBottom;
    public GameObject startShoes;

    [Header("Colores de ropa")]
    public Color colorUp = Color.white;
    public Color colorBottom = Color.white;
    public Color colorShoes = Color.white;

    [Header("Ajuste de ropa")]
    [Tooltip("Separación mínima entre la ropa y la piel, como fracción del tamaño del cuerpo " +
             "(0.015 = 1.5%). Empuja la ropa hacia afuera, siguiendo la normal del cuerpo, " +
             "hasta que quede al menos a esa distancia; también saca la ropa que quedó " +
             "adentro del cuerpo. Subila si todavía se ve piel. 0 = desactivado.")]
    [Range(0f, 0.08f)]
    public float clothingGapPercent = 0.015f;

    [Tooltip("Separación extra cerca de las articulaciones (hombros, codos, rodillas), " +
             "donde la piel se comprime al doblarse. 0 = igual en todo el cuerpo; " +
             "1 = hasta 1.5x en las zonas de más mezcla de huesos. Subilo si la piel asoma en los hombros.")]
    [Range(0f, 6f)]
    public float jointGapBoost = 1f;

    [Header("Ocultar cuerpo bajo la ropa")]
    [Tooltip("Oculta automáticamente la piel que queda cubierta por cada prenda. " +
             "Requiere Read/Write activado en la malla del cuerpo y de la ropa.")]
    public bool hideBodyUnderClothes = true;

    [Tooltip("Distancia ropa-cuerpo bajo la cual la piel se considera cubierta, " +
             "como fracción del tamaño del cuerpo (0.02 = 2%). " +
             "Subila si la piel sigue atravesando; bajala si desaparecen partes que deberían verse.")]
    [Range(0.005f, 0.08f)]
    public float coverDistancePercent = 0.02f;

    [Header("Pesos de la ropa")]
    [Tooltip("Copia a cada vértice de la ropa los pesos del vértice más cercano del cuerpo, " +
             "para que la ropa se deforme igual que la piel (hombros, codos). " +
             "Ignora la tabla de huesos. Requiere Read/Write en el cuerpo y en la ropa.")]
    public bool transferWeightsFromBody = true;

    class Equipped
    {
        public GameObject holder;
        public List<GameObject> extraHolders = new List<GameObject>();
        public List<Mesh> meshes = new List<Mesh>();
        public bool[] covered;   // vértices del cuerpo cubiertos por esta prenda
    }

    readonly Dictionary<Slot, Equipped> equipped =
        new Dictionary<Slot, Equipped>();

    readonly Dictionary<string, Transform> boneByName =
        new Dictionary<string, Transform>();

    readonly Dictionary<Transform, Matrix4x4> restRel =
        new Dictionary<Transform, Matrix4x4>();

    readonly Dictionary<string, string> oldToNew =
        new Dictionary<string, string>();

    Matrix4x4 bodyToRoot;

    // Cuerpo: malla original (asset) y copia modificable sin los triángulos ocultos
    Mesh bodyMeshOriginal;
    Mesh bodyMeshRuntime;
    Vector3[] bodyVerts;
    BoneWeight[] bodyWeights;
    Vector3[] bodyNormals;
    bool warnedBodyUnreadable;

    // Vértice del cuerpo más cercano a cada vértice de la ropa (por malla de ropa),
    // para no recalcular al volver a equiparla
    readonly Dictionary<Mesh, int[]> nearestCache =
        new Dictionary<Mesh, int[]>();

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

        string[,] sides =
        {
            { "Left", "L" },
            { "Right", "R" }
        };

        for (int i = 0; i < 2; i++)
        {
            string o = sides[i, 0];
            string n = sides[i, 1];

            m.Add(new BoneMapEntry(
                "ArmUp_" + o,
                "ORG-upper_arm." + n
            ));

            m.Add(new BoneMapEntry(
                "ArmDown_" + o,
                "ORG-forearm." + n
            ));

            m.Add(new BoneMapEntry(
                "HandStart_" + o,
                "ORG-hand." + n
            ));

            m.Add(new BoneMapEntry(
                "HandMiddle_" + o,
                "ORG-hand." + n
            ));

            m.Add(new BoneMapEntry(
                "HandFinish_" + o,
                "ORG-hand." + n
            ));

            m.Add(new BoneMapEntry(
                "ThumbStart_" + o,
                "ORG-thumb.01." + n
            ));

            m.Add(new BoneMapEntry(
                "ThumbEnd_" + o,
                "ORG-thumb.02." + n
            ));

            m.Add(new BoneMapEntry(
                "LegUp_" + o,
                "ORG-thigh." + n
            ));

            m.Add(new BoneMapEntry(
                "LegDown_" + o,
                "ORG-shin." + n
            ));

            m.Add(new BoneMapEntry(
                "KneeIK_" + o,
                "ORG-shin." + n
            ));

            m.Add(new BoneMapEntry(
                "HeelIK_" + o,
                "ORG-foot." + n
            ));

            m.Add(new BoneMapEntry(
                "FootStart_" + o,
                "ORG-foot." + n
            ));

            m.Add(new BoneMapEntry(
                "FootEnd_" + o,
                "ORG-toe." + n
            ));
        }

        return m;
    }

    void Awake()
    {
        if (rootBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == rootBoneName)
                {
                    rootBone = t;
                    break;
                }
            }
        }

        if (rootBone == null)
        {
            Debug.LogError(
                $"[ClothingEquipper] No se encontró el hueso raíz '{rootBoneName}'."
            );

            return;
        }

        if (bodyRenderer == null)
            bodyRenderer =
                FindBodyRenderer();

        if (bodyRenderer == null)
        {
            Debug.LogError(
                "[ClothingEquipper] No se encontró la malla del cuerpo."
            );

            return;
        }

        bodyMeshOriginal =
            bodyRenderer.sharedMesh;

        if (bodyMeshOriginal != null)
        {
            Debug.Log(
                $"[ClothingEquipper] Cuerpo: '{bodyRenderer.name}' " +
                $"({bodyMeshOriginal.vertexCount} vértices, " +
                $"{bodyRenderer.bones.Length} huesos, " +
                $"Read/Write: {(bodyMeshOriginal.isReadable ? "SI" : "NO")})."
            );
        }

        Matrix4x4 w2l =
            transform.worldToLocalMatrix;

        foreach (var t in rootBone.GetComponentsInChildren<Transform>(true))
        {
            boneByName[t.name] = t;

            restRel[t] =
                w2l * t.localToWorldMatrix;
        }

        bodyToRoot =
            w2l * bodyRenderer.transform.localToWorldMatrix;

        foreach (var e in boneTable)
        {
            if (!string.IsNullOrEmpty(e.oldBone))
            {
                oldToNew[e.oldBone] =
                    e.newBone;
            }
        }

        ready = true;
    }

    // El cuerpo es la malla con más vértices: el primer SkinnedMeshRenderer
    // que aparece en la jerarquía puede ser pelo, ojos, dientes, etc.
    SkinnedMeshRenderer FindBodyRenderer()
    {
        SkinnedMeshRenderer best = null;
        int bestCount = -1;

        foreach (
            var r in
            GetComponentsInChildren<SkinnedMeshRenderer>(true)
        )
        {
            if (r.sharedMesh == null)
                continue;

            if (r.sharedMesh.vertexCount > bestCount)
            {
                bestCount = r.sharedMesh.vertexCount;
                best = r;
            }
        }

        return best;
    }

    void Start()
    {
        if (startUp)
            Equip(Slot.Up, startUp);

        if (startBottom)
            Equip(Slot.Bottom, startBottom);

        if (startShoes)
            Equip(Slot.Shoes, startShoes);
    }

    void OnDestroy()
    {
        if (bodyMeshRuntime != null)
            Destroy(bodyMeshRuntime);
    }

    public void Equip(
        Slot slot,
        GameObject prefab
    )
    {
        Unequip(slot);

        if (!ready || prefab == null)
            return;

        equipped[slot] =
            EquipSkinned(prefab, slot);

        RebuildBody();
    }

    public void Unequip(Slot slot)
    {
        if (!equipped.TryGetValue(
            slot,
            out var e
        ))
            return;

        if (e.holder)
            Destroy(e.holder);

        foreach (var h in e.extraHolders)
        {
            if (h)
                Destroy(h);
        }

        foreach (var m in e.meshes)
        {
            if (m)
                Destroy(m);
        }

        equipped.Remove(slot);

        RebuildBody();
    }

    Transform Resolve(
        string oldName,
        out bool viaTable
    )
    {
        viaTable = false;

        if (string.IsNullOrEmpty(oldName))
            return null;

        if (boneByName.TryGetValue(
            oldName,
            out var direct
        ))
        {
            return direct;
        }

        if (
            oldToNew.TryGetValue(
                oldName,
                out var newName
            ) &&
            boneByName.TryGetValue(
                newName,
                out var mapped
            )
        )
        {
            viaTable = true;
            return mapped;
        }

        return null;
    }

    Equipped EquipSkinned(
        GameObject prefab,
        Slot slot
    )
    {
        var result =
            new Equipped();

        result.holder =
            new GameObject(
                "Clothing_" + slot
            );

        result.holder.transform.SetParent(
            transform,
            false
        );

        var temp =
            Instantiate(prefab);

        bool hideBody =
            hideBodyUnderClothes &&
            CanReadBody();

        if (hideBody)
        {
            result.covered =
                new bool[
                    bodyMeshOriginal.vertexCount
                ];
        }

        bool transfer =
            CanTransferWeights();

        int directCount = 0;
        int tableCount = 0;
        int missingCount = 0;
        int transferredCount = 0;

        foreach (
            var smr in
            temp.GetComponentsInChildren<
                SkinnedMeshRenderer
            >(true)
        )
        {
            if (smr.sharedMesh == null)
                continue;

            if (!smr.sharedMesh.isReadable)
            {
                Debug.LogWarning(
                    $"[ClothingEquipper] '{prefab.name}': la malla '{smr.sharedMesh.name}' " +
                    "no tiene Read/Write activado. Sin eso no se copian pesos, " +
                    "no se oculta piel ni se separa la ropa del cuerpo."
                );
            }

            // Se calcula con la malla original de la prenda, antes de modificarla
            if (hideBody && smr.sharedMesh.isReadable)
                MarkCovered(smr, result.covered);

            // La ropa pasa a usar los mismos huesos y bindposes que el cuerpo,
            // con los pesos del vértice más cercano del cuerpo.
            // No hace falta la tabla de huesos ni recalcular bindposes.
            if (transfer && smr.sharedMesh.isReadable)
            {
                // Los vértices de cada prenda pueden estar en un espacio local
                // distinto al del cuerpo. Primero se convierten al espacio local
                // del SkinnedMeshRenderer del cuerpo, antes de copiar sus pesos.
                Mesh sourceMesh = smr.sharedMesh;
                Vector3[] verticesBodySpace =
                    ConvertVerticesToBodySpace(smr, sourceMesh);

                var tmesh = Instantiate(sourceMesh);
                tmesh.name = sourceMesh.name + "_Runtime_" + slot;
                tmesh.vertices = verticesBodySpace;
                tmesh.boneWeights = GetTransferredWeights(tmesh);
                tmesh.bindposes = bodyMeshOriginal.bindposes;

                ApplyClearance(tmesh, tmesh);

                // Esta prenda necesita usar el mismo espacio de transformación
                // que la malla corporal. Se usa un holder separado para no alterar
                // las prendas que utilizan el mapeo de huesos tradicional.
                var transferHolder = new GameObject("ClothingTransfer_" + slot);
                transferHolder.transform.SetParent(bodyRenderer.transform, false);
                result.extraHolders.Add(transferHolder);

                smr.sharedMesh = tmesh;
                result.meshes.Add(tmesh);
                smr.updateWhenOffscreen = true;
                smr.transform.SetParent(transferHolder.transform, false);
                smr.transform.localPosition = Vector3.zero;
                smr.transform.localRotation = Quaternion.identity;
                smr.transform.localScale = Vector3.one;
                smr.bones = bodyRenderer.bones;
                smr.rootBone = bodyRenderer.rootBone != null
                    ? bodyRenderer.rootBone
                    : rootBone;

                AplicarColor(smr, ObtenerColor(slot));
                transferredCount++;
                continue;
            }

            var oldBones =
                smr.bones;

            var newBones =
                new Transform[
                    oldBones.Length
                ];

            bool needRebind = false;

            for (
                int i = 0;
                i < oldBones.Length;
                i++
            )
            {
                string oldName =
                    oldBones[i]
                    ? oldBones[i].name
                    : null;

                var found =
                    Resolve(
                        oldName,
                        out bool viaTable
                    );

                if (found == null)
                {
                    Debug.LogWarning(
                        $"[ClothingEquipper] '{prefab.name}': " +
                        $"hueso '{oldName}' sin equivalente. " +
                        "Se usa el hueso raíz."
                    );

                    found = rootBone;
                    viaTable = true;
                    missingCount++;
                }
                else if (viaTable)
                {
                    tableCount++;
                }
                else
                {
                    directCount++;
                }

                if (viaTable)
                    needRebind = true;

                newBones[i] =
                    found;
            }

            Transform newRoot = null;

            if (smr.rootBone != null)
            {
                newRoot =
                    Resolve(
                        smr.rootBone.name,
                        out _
                    );
            }

            if (newRoot == null)
                newRoot = rootBone;

            // Se instancia la malla si hay que re-bindear los huesos
            // o si hay que inflar la ropa (nunca se modifica el asset original)
            if (needRebind || clothingGapPercent > 0f)
            {
                var mesh =
                    Instantiate(
                        smr.sharedMesh
                    );

                if (needRebind)
                {
                    var bind =
                        new Matrix4x4[
                            newBones.Length
                        ];

                    for (
                        int i = 0;
                        i < newBones.Length;
                        i++
                    )
                    {
                        bind[i] =
                            restRel[
                                newBones[i]
                            ].inverse *
                            bodyToRoot;
                    }

                    mesh.bindposes =
                        bind;
                }

                ApplyClearance(mesh, smr.sharedMesh);

                smr.sharedMesh =
                    mesh;

                result.meshes.Add(mesh);

                smr.updateWhenOffscreen =
                    true;
            }

            smr.transform.SetParent(
                result.holder.transform,
                false
            );

            smr.bones =
                newBones;

            smr.rootBone =
                newRoot;

            AplicarColor(
                smr,
                ObtenerColor(slot)
            );
        }

        Destroy(temp);

        int coveredCount = 0;

        if (result.covered != null)
        {
            foreach (bool c in result.covered)
            {
                if (c)
                    coveredCount++;
            }
        }

        Debug.Log(
            $"[ClothingEquipper] '{prefab.name}': " +
            $"{directCount} huesos directos, " +
            $"{tableCount} por tabla, " +
            $"{missingCount} sin equivalente, " +
            $"{transferredCount} mallas con pesos copiados del cuerpo. " +
            $"Piel oculta: {coveredCount} vértices. " +
            $"Separación mínima: {BodySize() * clothingGapPercent:F4} " +
            $"(tamaño del cuerpo: {BodySize():F3})."
        );

        return result;
    }

    // Empuja la ropa hacia afuera para que quede, como mínimo, a cierta distancia
    // de la piel. La dirección es la normal del CUERPO en el punto más cercano
    // (no la de la ropa, que puede estar invertida o cancelarse en prendas de una sola capa).
    // Si un vértice de la ropa estaba dentro del cuerpo, también lo saca.
    void ApplyClearance(
        Mesh mesh,
        Mesh source
    )
    {
        if (clothingGapPercent <= 0f)
            return;

        if (!mesh.isReadable || !CanReadBody())
            return;

        EnsureBodyData();

        if (
            bodyNormals.Length != bodyVerts.Length ||
            bodyWeights.Length != bodyVerts.Length
        )
            return;

        int[] nearest =
            GetNearestBodyIndices(mesh);

        Vector3[] v =
            mesh.vertices;

        if (nearest.Length != v.Length)
            return;

        float gap =
            BodySize() * clothingGapPercent;

        for (int i = 0; i < v.Length; i++)
        {
            int j = nearest[i];
            Vector3 n = bodyNormals[j];

            // Cerca de las articulaciones la piel se mezcla entre varios huesos
            // y se comprime al doblarse: ahí se pide más separación
            BoneWeight w = bodyWeights[j];

            float maxW =
                Mathf.Max(
                    Mathf.Max(w.weight0, w.weight1),
                    Mathf.Max(w.weight2, w.weight3)
                );

            float gapHere =
                gap * (1f + jointGapBoost * (1f - maxW));

            // Distancia con signo a la superficie del cuerpo, medida sobre su normal
            float d =
                Vector3.Dot(v[i] - bodyVerts[j], n);

            if (d < gapHere)
                v[i] += n * (gapHere - d);
        }

        mesh.vertices = v;
        mesh.RecalculateBounds();
    }

    float BodySize()
    {
        Vector3 size =
            bodyMeshOriginal.bounds.size;

        return Mathf.Max(size.x, size.y, size.z);
    }

    // ------------------------------------------------------------------
    //  Ocultar el cuerpo cubierto por la ropa (arreglo B)
    // ------------------------------------------------------------------

    bool CanReadBody()
    {
        if (bodyMeshOriginal == null)
            return false;

        if (!bodyMeshOriginal.isReadable)
        {
            if (!warnedBodyUnreadable)
            {
                warnedBodyUnreadable = true;

                Debug.LogWarning(
                    "[ClothingEquipper] La malla del cuerpo no tiene Read/Write " +
                    "activado: no se puede ocultar la piel bajo la ropa."
                );
            }

            return false;
        }

        return true;
    }

    // Convierte los vértices de la malla de la ropa al espacio local del cuerpo.
    // Esto evita comparar coordenadas locales incompatibles entre dos mallas.
    Vector3[] ConvertVerticesToBodySpace(
        SkinnedMeshRenderer garmentRenderer,
        Mesh garmentMesh
    )
    {
        Vector3[] source = garmentMesh.vertices;
        Vector3[] converted = new Vector3[source.Length];

        Transform garmentTransform = garmentRenderer.transform;
        Transform bodyTransform = bodyRenderer.transform;

        for (int i = 0; i < source.Length; i++)
        {
            Vector3 world = garmentTransform.TransformPoint(source[i]);
            converted[i] = bodyTransform.InverseTransformPoint(world);
        }

        return converted;
    }

    // Marca los vértices del cuerpo que están cerca de la superficie de la prenda.
    // Asume que ropa y cuerpo comparten el mismo espacio de malla
    // (lo mismo que ya asume el cálculo de bindposes).
    void MarkCovered(
        SkinnedMeshRenderer garmentRenderer,
        bool[] covered
    )
    {
        if (bodyVerts == null)
            bodyVerts = bodyMeshOriginal.vertices;

        Vector3 size =
            bodyMeshOriginal.bounds.size;

        float dist =
            Mathf.Max(size.x, size.y, size.z) *
            coverDistancePercent;

        if (dist <= 0f)
            return;

        Mesh garment = garmentRenderer.sharedMesh;
        Vector3[] gv = ConvertVerticesToBodySpace(garmentRenderer, garment);
        int[] gt = garment.triangles;

        // Grilla espacial con puntos de la superficie de la prenda:
        // vértices, puntos medios de aristas y centros de triángulos
        var grid =
            new Dictionary<Vector3Int, List<Vector3>>();

        void Add(Vector3 p)
        {
            var cell = CellOf(p, dist);

            if (!grid.TryGetValue(cell, out var list))
                grid[cell] = list = new List<Vector3>();

            list.Add(p);
        }

        for (int i = 0; i < gv.Length; i++)
            Add(gv[i]);

        for (int t = 0; t < gt.Length; t += 3)
        {
            Vector3 p0 = gv[gt[t]];
            Vector3 p1 = gv[gt[t + 1]];
            Vector3 p2 = gv[gt[t + 2]];

            Add((p0 + p1) * 0.5f);
            Add((p1 + p2) * 0.5f);
            Add((p2 + p0) * 0.5f);
            Add((p0 + p1 + p2) / 3f);
        }

        float sq = dist * dist;

        for (int i = 0; i < bodyVerts.Length; i++)
        {
            if (covered[i])
                continue;

            Vector3 p = bodyVerts[i];

            if (AnyNear(grid, CellOf(p, dist), p, sq))
                covered[i] = true;
        }
    }

    static Vector3Int CellOf(
        Vector3 p,
        float size
    )
    {
        return new Vector3Int(
            Mathf.FloorToInt(p.x / size),
            Mathf.FloorToInt(p.y / size),
            Mathf.FloorToInt(p.z / size)
        );
    }

    static bool AnyNear(
        Dictionary<Vector3Int, List<Vector3>> grid,
        Vector3Int cell,
        Vector3 p,
        float sq
    )
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    var key =
                        new Vector3Int(
                            cell.x + x,
                            cell.y + y,
                            cell.z + z
                        );

                    if (!grid.TryGetValue(key, out var list))
                        continue;

                    for (int k = 0; k < list.Count; k++)
                    {
                        if ((list[k] - p).sqrMagnitude <= sq)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    // ------------------------------------------------------------------
    //  Transferencia de pesos del cuerpo a la ropa
    // ------------------------------------------------------------------

    void EnsureBodyData()
    {
        if (bodyVerts == null)
            bodyVerts = bodyMeshOriginal.vertices;

        if (bodyWeights == null)
            bodyWeights = bodyMeshOriginal.boneWeights;

        if (bodyNormals == null)
            bodyNormals = bodyMeshOriginal.normals;
    }

    bool CanTransferWeights()
    {
        if (!transferWeightsFromBody || !CanReadBody())
            return false;

        EnsureBodyData();

        bool ok =
            bodyWeights.Length == bodyVerts.Length &&
            bodyRenderer.bones.Length > 0 &&
            bodyRenderer.bones.Length == bodyMeshOriginal.bindposes.Length;

        if (!ok)
        {
            Debug.LogWarning(
                "[ClothingEquipper] No se pueden copiar los pesos del cuerpo: " +
                $"'{bodyRenderer.name}' tiene {bodyWeights.Length} pesos para {bodyVerts.Length} vértices, " +
                $"{bodyRenderer.bones.Length} huesos y {bodyMeshOriginal.bindposes.Length} bindposes. " +
                "¿Es esa la malla del cuerpo? Asignala a mano en 'Body Renderer'."
            );
        }

        return ok;
    }

    // Para cada vértice de la ropa, copia los pesos del vértice más cercano del cuerpo.
    // Los índices de hueso son los del arreglo de huesos del cuerpo.
    BoneWeight[] GetTransferredWeights(Mesh garment)
    {
        int[] nearest = GetNearestBodyIndices(garment);
        var result = new BoneWeight[nearest.Length];

        for (int i = 0; i < nearest.Length; i++)
            result[i] = bodyWeights[nearest[i]];

        return result;
    }

    // Para cada vértice de la ropa, índice del vértice más cercano del cuerpo
    int[] GetNearestBodyIndices(Mesh garment)
    {
        if (nearestCache.TryGetValue(garment, out var cached))
            return cached;

        EnsureBodyData();

        Vector3[] gv = garment.vertices;
        var result = new int[gv.Length];

        Vector3 size = bodyMeshOriginal.bounds.size;
        float cell = Mathf.Max(size.x, size.y, size.z) * 0.03f;

        var grid = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < bodyVerts.Length; i++)
        {
            var key = CellOf(bodyVerts[i], cell);

            if (!grid.TryGetValue(key, out var list))
                grid[key] = list = new List<int>();

            list.Add(i);
        }

        for (int i = 0; i < gv.Length; i++)
            result[i] = NearestBodyVertex(gv[i], grid, cell);

        nearestCache[garment] = result;
        return result;
    }

    int NearestBodyVertex(
        Vector3 p,
        Dictionary<Vector3Int, List<int>> grid,
        float cell
    )
    {
        Vector3Int c = CellOf(p, cell);

        int best = -1;
        float bestSq = float.MaxValue;

        for (int r = 0; r <= 12; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    for (int z = -r; z <= r; z++)
                    {
                        // solo la capa exterior de este radio
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != r)
                            continue;

                        var key =
                            new Vector3Int(
                                c.x + x,
                                c.y + y,
                                c.z + z
                            );

                        if (!grid.TryGetValue(key, out var list))
                            continue;

                        for (int k = 0; k < list.Count; k++)
                        {
                            float sq =
                                (bodyVerts[list[k]] - p).sqrMagnitude;

                            if (sq < bestSq)
                            {
                                bestSq = sq;
                                best = list[k];
                            }
                        }
                    }
                }
            }

            // Lo que queda sin visitar está al menos a r * cell de distancia
            float reach = r * cell;

            if (best >= 0 && bestSq <= reach * reach)
                break;
        }

        if (best >= 0)
            return best;

        // Punto muy lejos del cuerpo: búsqueda completa
        for (int i = 0; i < bodyVerts.Length; i++)
        {
            float sq = (bodyVerts[i] - p).sqrMagnitude;

            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        return best;
    }

    // Reconstruye la malla del cuerpo sin los triángulos cuyos tres vértices
    // están cubiertos por alguna de las prendas equipadas.
    void RebuildBody()
    {
        if (bodyRenderer == null || bodyMeshOriginal == null)
            return;

        bool[] union = null;

        foreach (var e in equipped.Values)
        {
            if (e.covered == null)
                continue;

            if (union == null)
                union = new bool[e.covered.Length];

            for (int i = 0; i < union.Length; i++)
                union[i] |= e.covered[i];
        }

        // Nada que ocultar: volver a la malla original
        if (union == null)
        {
            bodyRenderer.sharedMesh = bodyMeshOriginal;

            if (bodyMeshRuntime != null)
            {
                Destroy(bodyMeshRuntime);
                bodyMeshRuntime = null;
            }

            return;
        }

        if (bodyMeshRuntime == null)
            bodyMeshRuntime = Instantiate(bodyMeshOriginal);

        for (int s = 0; s < bodyMeshOriginal.subMeshCount; s++)
        {
            int[] tris = bodyMeshOriginal.GetTriangles(s);
            var keep = new List<int>(tris.Length);

            for (int t = 0; t < tris.Length; t += 3)
            {
                if (
                    union[tris[t]] &&
                    union[tris[t + 1]] &&
                    union[tris[t + 2]]
                )
                    continue;

                keep.Add(tris[t]);
                keep.Add(tris[t + 1]);
                keep.Add(tris[t + 2]);
            }

            bodyMeshRuntime.SetTriangles(keep, s, false);
        }

        bodyRenderer.sharedMesh = bodyMeshRuntime;
    }

    // ------------------------------------------------------------------

    Color ObtenerColor(Slot slot)
    {
        switch (slot)
        {
            case Slot.Up:
                return colorUp;

            case Slot.Bottom:
                return colorBottom;

            case Slot.Shoes:
                return colorShoes;

            default:
                return Color.white;
        }
    }

    void AplicarColor(
        SkinnedMeshRenderer smr,
        Color color
    )
    {
        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        smr.GetPropertyBlock(block);

        // URP / Standard
        block.SetColor(
            "_BaseColor",
            color
        );

        // Por compatibilidad con shaders
        // que utilizan "_Color"
        block.SetColor(
            "_Color",
            color
        );

        smr.SetPropertyBlock(block);
    }
}