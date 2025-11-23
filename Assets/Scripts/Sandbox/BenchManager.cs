using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Three modes for your sandbox bench.
public enum BenchMode { RigidOnly, Fractured, Cosmetic }

/// Draws thousands of collider-less debris instances.
public class DebrisRenderer : MonoBehaviour
{
    public Mesh mesh;
    public Material material;
    public List<Matrix4x4> matrices = new();

    static readonly Matrix4x4[] BATCH = new Matrix4x4[1023];

    void LateUpdate()
    {
        if (!mesh || !material || matrices.Count == 0) return;
        if (!material.enableInstancing) return;

        int offset = 0;
        while (offset < matrices.Count)
        {
            int count = Mathf.Min(1023, matrices.Count - offset);
            matrices.CopyTo(offset, BATCH, 0, count);
            Graphics.DrawMeshInstanced(mesh, 0, material, BATCH, count,
                null, ShadowCastingMode.Off, false, 0, null,
                LightProbeUsage.Off, null);
            offset += count;
        }
    }

    public void Clear() => matrices.Clear();
}

/// Micro pool for fast spawn/clear of rigid objects.
public class SimplePool
{
    readonly GameObject prefab;
    readonly Transform parent;
    readonly Stack<GameObject> stack = new();

    public SimplePool(GameObject prefab, Transform parent, int prewarm = 0)
    {
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < prewarm; i++) stack.Push(Create());
    }

    GameObject Create()
    {
        var go = Object.Instantiate(prefab, parent);
        go.SetActive(false);
        return go;
    }

    public GameObject Get()
    {
        var go = stack.Count > 0 ? stack.Pop() : Create();
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
        stack.Push(go);
    }
}

/// The bench orchestrator: spawns/clears the chosen mode.
public class BenchManager : MonoBehaviour
{
    [Header("Mode & counts")]
    public BenchMode mode = BenchMode.Cosmetic;
    [Min(0)] public int rigidCount = 150;
    [Min(0)] public int fracturedCount = 8;
    [Min(0)] public int cosmeticCount = 2000;

    [Header("Spawn volume (local space)")]
    public Vector3 spawnMin = new(-3f, 0.8f, -3f);
    public Vector3 spawnMax = new(3f, 2.0f, 3f);

    [Header("Prefabs & assets")]
    public GameObject pooledRigidPrefab;   // e.g., PooledRigid.prefab
    public GameObject fracturedPrefab;     // e.g., FracturedSet.prefab
    public Mesh cosmeticMesh;              // e.g., DebrisMesh.fbx mesh (or a primitive mesh)
    public Material cosmeticMaterial;      // DebrisMat (Enable GPU Instancing ?)

    [Header("Cosmetic mode selection")]
    [Tooltip("Toggle between Parallel (multi-core) and Serial (single-core) execution for class demonstration")]
    public bool useParallelCosmetic = true;
    public CosmeticJobsController jobsController;    // drag the CosmeticJobs here
    public float cosmeticBaseScale = 1f;             // pass-through to jobs
    public Vector2 cosmeticRandomScale = new(0.8f, 1.2f);

    [Header("Parents (optional)")]
    public Transform rigidParent;
    public Transform fracturedParent;
    public Transform cosmeticParent;       // holder for DebrisRenderer

    SimplePool rigidPool;
    readonly List<GameObject> rigidsActive = new();
    readonly List<GameObject> fracturedActive = new();
    DebrisRenderer debrisRenderer;

    void Awake()
    {
        // Create holders so the Hierarchy stays tidy
        if (!rigidParent) rigidParent = new GameObject("Rigid_Parent").transform;
        if (!fracturedParent) fracturedParent = new GameObject("Fractured_Parent").transform;
        if (!cosmeticParent) cosmeticParent = new GameObject("Cosmetic_Parent").transform;

        rigidParent.SetParent(transform, false);
        fracturedParent.SetParent(transform, false);
        cosmeticParent.SetParent(transform, false);

        if (pooledRigidPrefab)
            rigidPool = new SimplePool(pooledRigidPrefab, rigidParent,
                                       prewarm: Mathf.Min(256, Mathf.Max(32, rigidCount)));

        // create the instanced drawer
        var drawer = new GameObject("DebrisRenderer");
        drawer.transform.SetParent(cosmeticParent, false);
        debrisRenderer = drawer.AddComponent<DebrisRenderer>();
        debrisRenderer.mesh = cosmeticMesh;
        debrisRenderer.material = cosmeticMaterial;
        debrisRenderer.gameObject.SetActive(!useParallelCosmetic);

        // if a jobs controller is assigned, start it disabled (we’ll enable only in Cosmetic)
        if (jobsController != null) jobsController.enabled = false;
    }

    void Start() => RunMode(mode);

    // Call this to switch modes (e.g., from a button or a key)
    public void RunMode(BenchMode newMode)
    {
        mode = newMode;

        // Turn off both cosmetic paths first (avoid double rendering)
        if (debrisRenderer) debrisRenderer.gameObject.SetActive(false);
        if (jobsController) jobsController.enabled = false;

        CosmeticJobsController.ClearStats();

        ClearAll();

        switch (mode)
        {
            case BenchMode.RigidOnly:
                SpawnRigidOnly();
                break;

            case BenchMode.Fractured:
                SpawnFractured();
                break;

            case BenchMode.Cosmetic:
                // Always use jobsController for cosmetic mode (creates grabbable GameObjects)
                if (jobsController != null)
                {
                    // Configure the jobs controller
                    jobsController.count = Mathf.Max(0, cosmeticCount);
                    jobsController.spawnMin = spawnMin;
                    jobsController.spawnMax = spawnMax;
                    jobsController.baseScale = cosmeticBaseScale;
                    jobsController.randomScale = cosmeticRandomScale;

                    // Set execution mode: Parallel (multi-core) or Serial (single-core)
                    jobsController.useParallelProcessing = useParallelCosmetic;

                    // Make sure it uses the same mesh/material you assigned to BenchManager
                    if (cosmeticMesh) jobsController.mesh = cosmeticMesh;
                    if (cosmeticMaterial) jobsController.material = cosmeticMaterial;

                    jobsController.enabled = true;

                    // Force rebuild with new settings
                    jobsController.ReinitializeNow();
                }
                else
                {
                    Debug.LogWarning("BenchManager: CosmeticJobsController not assigned. Cannot spawn cosmetic objects.");
                }
                break;
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < rigidsActive.Count; i++) rigidPool?.Release(rigidsActive[i]);
        rigidsActive.Clear();

        for (int i = 0; i < fracturedActive.Count; i++) if (fracturedActive[i]) Destroy(fracturedActive[i]);
        fracturedActive.Clear();

        debrisRenderer?.Clear();
    }

    void SpawnRigidOnly()
    {
        if (rigidPool == null)
        {
            Debug.LogWarning("BenchManager: PooledRigid Prefab not set.");
            return;
        }

        for (int i = 0; i < rigidCount; i++)
        {
            var go = rigidPool.Get();
            go.transform.SetPositionAndRotation(RandomPos(), Random.rotation);

            var rb = go.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.sleepThreshold = 0.005f;
                rb.linearDamping = 0f;
                rb.angularDamping = 0.05f;
            }
            rigidsActive.Add(go);
        }
    }

    void SpawnFractured()
    {
        if (!fracturedPrefab)
        {
            Debug.LogWarning("BenchManager: Fractured Prefab not set.");
            return;
        }

        for (int i = 0; i < fracturedCount; i++)
        {
            var p = Instantiate(fracturedPrefab, RandomPos(), Random.rotation, fracturedParent);
            // optional stabilizers
            foreach (var rb in p.GetComponentsInChildren<Rigidbody>())
            {
                rb.sleepThreshold = 0.005f;
                // Optionally anchor a few shards:
                // if (Random.value < 0.1f) rb.isKinematic = true;
            }
            fracturedActive.Add(p);
        }
    }

    void SpawnCosmetic()
    {
        if (!debrisRenderer || !debrisRenderer.material || !debrisRenderer.mesh)
        {
            Debug.LogWarning("BenchManager: Cosmetic mesh/material not set.");
            return;
        }

        for (int i = 0; i < cosmeticCount; i++)
        {
            var pos = RandomPos();
            var rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var s = Vector3.one * Random.Range(0.6f, 1.2f);
            debrisRenderer.matrices.Add(Matrix4x4.TRS(pos, rot, s));
        }
    }

    Vector3 RandomPos()
    {
        return transform.TransformPoint(new Vector3(
            Random.Range(spawnMin.x, spawnMax.x),
            Random.Range(spawnMin.y, spawnMax.y),
            Random.Range(spawnMin.z, spawnMax.z)
        ));
    }

    // Draw the spawn volume in the editor so you can see where things appear
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        var center = (spawnMin + spawnMax) * 0.5f;
        var size = (spawnMax - spawnMin);
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(center, size);
        Gizmos.matrix = m;
    }
}
