using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


public class CosmeticJobsController : MonoBehaviour
{
    public enum ColliderType { Box, Sphere, Mesh }

    [BurstCompile]
    struct CalculateSpawnDataJob : IJobParallelFor
    {
        public int gridSize;
        public float3 spawnCenter;
        public float baseScale;
        public float spacing;

        [WriteOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<quaternion> rotations;
        [WriteOnly] public NativeArray<float> scales;

        public void Execute(int i)
        {
            int xi = i % gridSize;
            int yi = (i / gridSize) % gridSize;
            int zi = i / (gridSize * gridSize);

            float gridOffset = (gridSize - 1) * spacing * 0.5f;
            positions[i] = new float3(
                spawnCenter.x + (xi * spacing) - gridOffset,
                spawnCenter.y + (yi * spacing) - gridOffset,
                spawnCenter.z + (zi * spacing) - gridOffset
            );

            scales[i] = baseScale;
            rotations[i] = quaternion.identity;
        }
    }

    public static float LastCalculationMs { get; private set; }     // Time to calculate spawn data (serial or parallel)
    public static float LastGameObjectMs { get; private set; }      // Time to create GameObjects (always serial)
    public static float LastTotalMs { get; private set; }           // Total spawn time
    public static bool IsUsingParallel { get; private set; }        // Current execution mode
    public static float SerialCalculationMs { get; private set; }   // Last serial calculation time
    public static float ParallelCalculationMs { get; private set; } // Last parallel calculation time

    public static float LastJobMs => LastCalculationMs;
    public static int LastJobCount { get; private set; }
    public static int WorkerCount => SystemInfo.processorCount;
    public static int LastUpdateFrame { get; private set; } = -1;

    [Header("Rendering (assign these)")]
    public Mesh mesh;
    public Material material;

    [Header("Execution Mode")]
    [Tooltip("Toggle between parallel (multi-core) and serial (single-core) execution for class demonstration")]
    public bool useParallelProcessing = true;

    [Header("Counts & Area")]
    [Tooltip("Recommended: 50-200 for VR performance with physics")]
    public int count = 100;
    public Vector3 spawnMin = new(-3f, 1f, -3f);
    public Vector3 spawnMax = new(3f, 2f, 3f);

    [Header("Spawn Pattern")]
    [Tooltip("Padding between objects in uniform grid (multiplier of object scale)")]
    public float uniformPadding = 1.5f;

    [Header("Object Properties")]
    public float baseScale = 0.1f;
    public float mass = 1f;
    public float drag = 0.1f;
    public float angularDrag = 0.5f;

    [Header("Collision")]
    [Tooltip("Box: Best for brick/cube shapes. Mesh: Exact fit but more expensive.")]
    public ColliderType colliderType = ColliderType.Box;
    public PhysicsMaterial physicsMaterial;

    List<GameObject> spawnedObjects = new List<GameObject>();
    bool initialized;

    public static void ClearStats()
    {
        LastCalculationMs = 0f;
        LastGameObjectMs = 0f;
        LastTotalMs = 0f;
        LastJobCount = 0;
        LastUpdateFrame = -1;
        IsUsingParallel = false;
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

    void CalculateSpawnDataSerial(
        NativeArray<float3> positions,
        NativeArray<quaternion> rotations,
        NativeArray<float> scales,
        int gridSize,
        float3 spawnCenter,
        float baseScale,
        float spacing)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            // Calculate grid position
            int xi = i % gridSize;
            int yi = (i / gridSize) % gridSize;
            int zi = i / (gridSize * gridSize);

            float gridOffset = (gridSize - 1) * spacing * 0.5f;
            positions[i] = new float3(
                spawnCenter.x + (xi * spacing) - gridOffset,
                spawnCenter.y + (yi * spacing) - gridOffset,
                spawnCenter.z + (zi * spacing) - gridOffset
            );

            scales[i] = baseScale;
            rotations[i] = quaternion.identity;
        }
    }

    void SpawnObjects()
    {
        ClearObjects();

        var totalTimer = System.Diagnostics.Stopwatch.StartNew();
        var calculationTimer = System.Diagnostics.Stopwatch.StartNew();

        int gridSize = Mathf.CeilToInt(Mathf.Pow(count, 1f / 3f));
        float3 center = new float3((spawnMin + spawnMax) * 0.5f);
        float spacing = baseScale * uniformPadding;

        var positions = new NativeArray<float3>(count, Allocator.TempJob);
        var rotations = new NativeArray<quaternion>(count, Allocator.TempJob);
        var scales = new NativeArray<float>(count, Allocator.TempJob);

        if (useParallelProcessing)
        {
            var job = new CalculateSpawnDataJob
            {
                gridSize = gridSize,
                spawnCenter = center,
                baseScale = baseScale,
                spacing = spacing,
                positions = positions,
                rotations = rotations,
                scales = scales
            };

            JobHandle handle = job.Schedule(count, 64); // batch size 64 for good load balancing across cores
            handle.Complete(); // Wait for all worker threads to finish

            calculationTimer.Stop();
            LastCalculationMs = (float)calculationTimer.Elapsed.TotalMilliseconds;
            ParallelCalculationMs = LastCalculationMs;
            IsUsingParallel = true;
        }
        else // SERIAL PATH
        {
            CalculateSpawnDataSerial(
                positions, rotations, scales,
                gridSize, center, baseScale, spacing
            );

            calculationTimer.Stop();
            LastCalculationMs = (float)calculationTimer.Elapsed.TotalMilliseconds;
            SerialCalculationMs = LastCalculationMs;
            IsUsingParallel = false;
        }

        var gameObjectTimer = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            Vector3 position = positions[i];
            Quaternion rotation = rotations[i];
            float scale = scales[i];

            GameObject obj = new GameObject($"GrabbableObject_{i}");
            obj.transform.parent = transform;
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = Vector3.one * scale;

            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.material = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Collider collider = null;
            switch (colliderType)
            {
                case ColliderType.Box:
                    var boxCol = obj.AddComponent<BoxCollider>();
                    boxCol.center = mesh.bounds.center;
                    boxCol.size = mesh.bounds.size;
                    collider = boxCol;
                    break;

                case ColliderType.Sphere:
                    var sphereCol = obj.AddComponent<SphereCollider>();
                    sphereCol.center = mesh.bounds.center;
                    sphereCol.radius = mesh.bounds.extents.magnitude * 0.5f;
                    collider = sphereCol;
                    break;

                case ColliderType.Mesh:
                    var meshCol = obj.AddComponent<MeshCollider>();
                    meshCol.sharedMesh = mesh;
                    meshCol.convex = true;
                    collider = meshCol;
                    break;
            }

            if (collider != null && physicsMaterial != null)
                collider.material = physicsMaterial;

            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grabbable = obj.AddComponent<Oculus.Interaction.Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);

            var handGrabInteractable = obj.AddComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
            handGrabInteractable.InjectRigidbody(rb);
            handGrabInteractable.InjectOptionalPointableElement(grabbable);

            var grabInteractable = obj.AddComponent<Oculus.Interaction.GrabInteractable>();
            grabInteractable.InjectRigidbody(rb);
            grabInteractable.InjectOptionalPointableElement(grabbable);

            spawnedObjects.Add(obj);
        }

        gameObjectTimer.Stop();
        totalTimer.Stop();

        positions.Dispose();
        rotations.Dispose();
        scales.Dispose();

        LastGameObjectMs = (float)gameObjectTimer.Elapsed.TotalMilliseconds;
        LastTotalMs = (float)totalTimer.Elapsed.TotalMilliseconds;
        LastJobCount = count;
        LastUpdateFrame = Time.frameCount;
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
