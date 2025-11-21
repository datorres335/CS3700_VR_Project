using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Rng = Unity.Mathematics.Random;

/// <summary>
/// Spawns grabbable rigidbodies in zero gravity.
/// Objects start stationary and only move when colliding or grabbed/thrown.
/// </summary>
public class CosmeticJobsController : MonoBehaviour
{
    public enum ColliderType { Box, Sphere, Mesh }

    // Parallel job for calculating spawn data
    [BurstCompile]
    struct CalculateSpawnDataJob : IJobParallelFor
    {
        // Input parameters
        public bool uniformSpawn;
        public int gridSize;
        public float3 spawnCenter;
        public float3 spawnMin;
        public float3 spawnMax;
        public float baseScale;
        public float2 randomScaleRange;
        public float spacing;
        public Rng rngBase;

        // Output arrays
        [WriteOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<quaternion> rotations;
        [WriteOnly] public NativeArray<float> scales;

        public void Execute(int i)
        {
            if (uniformSpawn)
            {
                // Calculate grid position
                int xi = i % gridSize;
                int yi = (i / gridSize) % gridSize;
                int zi = i / (gridSize * gridSize);

                // Center the grid around spawn center
                float gridOffset = (gridSize - 1) * spacing * 0.5f;
                positions[i] = new float3(
                    spawnCenter.x + (xi * spacing) - gridOffset,
                    spawnCenter.y + (yi * spacing) - gridOffset,
                    spawnCenter.z + (zi * spacing) - gridOffset
                );

                // Uniform scale, no rotation
                scales[i] = baseScale;
                rotations[i] = quaternion.identity;
            }
            else
            {
                // Random spawning with unique RNG per object
                var rng = new Rng(rngBase.state + (uint)i);

                // Random position
                positions[i] = math.lerp(spawnMin, spawnMax, rng.NextFloat3());

                // Random scale
                scales[i] = baseScale * math.lerp(randomScaleRange.x, randomScaleRange.y, rng.NextFloat());

                // Random rotation
                rotations[i] = quaternion.Euler(
                    rng.NextFloat() * math.PI * 2f,
                    rng.NextFloat() * math.PI * 2f,
                    rng.NextFloat() * math.PI * 2f
                );
            }
        }
    }

    public static float LastJobMs { get; private set; }
    public static int LastJobCount { get; private set; }
    public static int WorkerCount => SystemInfo.processorCount;
    public static int LastUpdateFrame { get; private set; } = -1;

    [Header("Rendering (assign these)")]
    public Mesh mesh;
    public Material material;

    [Header("Counts & Area")]
    [Tooltip("Recommended: 50-200 for VR performance with physics")]
    public int count = 100;
    public Vector3 spawnMin = new(-3f, 1f, -3f);
    public Vector3 spawnMax = new(3f, 2f, 3f);

    [Header("Spawn Pattern")]
    [Tooltip("Uniform: Spawns in a cube grid. Random: Spawns at random positions.")]
    public bool uniformSpawn = true;
    [Tooltip("Padding between objects in uniform mode (multiplier of object scale)")]
    public float uniformPadding = 1.5f;

    [Header("Object Properties")]
    public float baseScale = 0.1f;
    public Vector2 randomScale = new(0.8f, 1.2f);
    public float mass = 1f;
    public float drag = 0.1f;
    public float angularDrag = 0.5f;

    [Header("Collision")]
    [Tooltip("Box: Best for brick/cube shapes. Mesh: Exact fit but more expensive.")]
    public ColliderType colliderType = ColliderType.Box;
    public PhysicsMaterial physicsMaterial;

    [Header("Seed")]
    public uint rngSeed = 123u;

    // Spawned objects
    List<GameObject> spawnedObjects = new List<GameObject>();
    bool initialized;

    public static void ClearStats()
    {
        LastJobMs = 0f;
        LastJobCount = 0;
        LastUpdateFrame = -1;
    }

    void Start()
    {
        if (!mesh || !material)
        {
            Debug.LogWarning("Assign mesh & material to CosmeticJobsController.");
            enabled = false;
            return;
        }

        SpawnObjects();
        initialized = true;
    }

    void SpawnObjects()
    {
        ClearObjects();

        var t0 = System.Diagnostics.Stopwatch.StartNew();

        // Calculate grid dimensions for uniform spawning
        int gridSize = Mathf.CeilToInt(Mathf.Pow(count, 1f / 3f));
        float3 center = new float3((spawnMin + spawnMax) * 0.5f);
        float avgScale = baseScale * (randomScale.x + randomScale.y) * 0.5f;
        float spacing = avgScale * uniformPadding;

        // Allocate NativeArrays for parallel job results
        var positions = new NativeArray<float3>(count, Allocator.TempJob);
        var rotations = new NativeArray<quaternion>(count, Allocator.TempJob);
        var scales = new NativeArray<float>(count, Allocator.TempJob);

        // Set up parallel job
        var job = new CalculateSpawnDataJob
        {
            uniformSpawn = uniformSpawn,
            gridSize = gridSize,
            spawnCenter = center,
            spawnMin = spawnMin,
            spawnMax = spawnMax,
            baseScale = baseScale,
            randomScaleRange = new float2(randomScale.x, randomScale.y),
            spacing = spacing,
            rngBase = new Rng(rngSeed == 0 ? 1u : rngSeed),
            positions = positions,
            rotations = rotations,
            scales = scales
        };

        // Schedule parallel job (batch size 64 for good load balancing)
        JobHandle handle = job.Schedule(count, 64);
        handle.Complete(); // Wait for all threads to finish

        t0.Stop();
        LastJobMs = (float)t0.Elapsed.TotalMilliseconds;

        // Now create GameObjects using the calculated data (main thread only)
        var t1 = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            Vector3 position = positions[i];
            Quaternion rotation = rotations[i];
            float scale = scales[i];

            // Create GameObject
            GameObject obj = new GameObject($"GrabbableObject_{i}");
            obj.transform.parent = transform;
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = Vector3.one * scale;

            // Add mesh rendering
            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.material = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // Add collider based on selected type
            Collider collider = null;
            switch (colliderType)
            {
                case ColliderType.Box:
                    // BoxCollider automatically sizes to mesh bounds
                    var boxCol = obj.AddComponent<BoxCollider>();
                    boxCol.center = mesh.bounds.center;
                    boxCol.size = mesh.bounds.size;
                    collider = boxCol;
                    break;

                case ColliderType.Sphere:
                    // SphereCollider uses mesh bounds to determine radius
                    var sphereCol = obj.AddComponent<SphereCollider>();
                    sphereCol.center = mesh.bounds.center;
                    sphereCol.radius = mesh.bounds.extents.magnitude * 0.5f;
                    collider = sphereCol;
                    break;

                case ColliderType.Mesh:
                    // MeshCollider for exact collision (must be convex for rigidbody)
                    var meshCol = obj.AddComponent<MeshCollider>();
                    meshCol.sharedMesh = mesh;
                    meshCol.convex = true; // Required for rigidbody interaction
                    collider = meshCol;
                    break;
            }

            if (collider != null && physicsMaterial != null)
                collider.material = physicsMaterial;

            // Add rigidbody with zero gravity
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false; // Zero gravity - space simulation
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.linearVelocity = Vector3.zero; // Start stationary
            rb.angularVelocity = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Add Meta XR Interaction SDK grab components (matching PooledRigid.prefab setup)

            // 1. Grabbable - main grab handler with rigidbody reference
            var grabbable = obj.AddComponent<Oculus.Interaction.Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);

            // 2. HandGrabInteractable - for hand tracking grab
            var handGrabInteractable = obj.AddComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
            handGrabInteractable.InjectRigidbody(rb);
            handGrabInteractable.InjectOptionalPointableElement(grabbable);

            // 3. GrabInteractable - for controller grab
            var grabInteractable = obj.AddComponent<Oculus.Interaction.GrabInteractable>();
            grabInteractable.InjectRigidbody(rb);
            grabInteractable.InjectOptionalPointableElement(grabbable);

            spawnedObjects.Add(obj);
        }

        t1.Stop();

        // Dispose NativeArrays
        positions.Dispose();
        rotations.Dispose();
        scales.Dispose();

        // Update stats
        LastJobCount = count;
        LastUpdateFrame = Time.frameCount;

        float gameObjectMs = (float)t1.Elapsed.TotalMilliseconds;
        float totalMs = LastJobMs + gameObjectMs;

        Debug.Log($"[CosmeticJobsController] Parallel job: {LastJobMs:F3} ms | GameObject creation: {gameObjectMs:F3} ms | Total: {totalMs:F3} ms | Workers: {WorkerCount}");
    }

    void ClearObjects()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    void Update()
    {
        if (!initialized) return;

        LastUpdateFrame = Time.frameCount;
        LastJobCount = spawnedObjects.Count;

        // Log stats periodically
        if (Time.frameCount % 60 == 0)
        {
            int activeCount = 0;
            foreach (var obj in spawnedObjects)
            {
                if (obj != null && obj.activeInHierarchy)
                    activeCount++;
            }
            Debug.Log($"[CosmeticJobsController] Active objects: {activeCount}  |  frame={(Time.deltaTime * 1000f):F3} ms");
        }
    }

    void OnDisable()
    {
        ClearStats();
    }

    void OnDestroy()
    {
        ClearObjects();
    }

    public void ReinitializeNow()
    {
        ClearObjects();
        SpawnObjects();
    }

    /// <summary>
    /// Apply an impulse to all objects (useful for testing collisions)
    /// </summary>
    public void ApplyImpulseToAll(Vector3 impulse)
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddForce(impulse, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// Reset all objects to stationary
    /// </summary>
    public void ResetAllVelocities()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
