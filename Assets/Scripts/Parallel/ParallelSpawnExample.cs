using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ParallelSpawnExample : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject prefab;              // what to instantiate
    public int count = 200;
    public Vector3 min = new(-3f, 1f, -3f);
    public Vector3 max = new(3f, 2f, 3f);
    public float uniformScale = 1f;

    [Header("Seed")]
    public uint rngSeed = 12345;           // change for different patterns

    [BurstCompile]
    public struct SpawnXformsJob : IJobParallelFor
    {
        public float3 min, max;
        [WriteOnly] public NativeArray<float4x4> outLocalToWorld;
        public Unity.Mathematics.Random rngBase;

        public void Execute(int i)
        {
            // each index gets its own RNG stream
            var rng = new Unity.Mathematics.Random(rngBase.state + (uint)i);

            float3 p = math.lerp(min, max, rng.NextFloat3());
            quaternion r = quaternion.identity;
            float s = 1f;

            outLocalToWorld[i] = float4x4.TRS(p, r, new float3(s, s, s));
        }
    }

    [ContextMenu("Spawn Now")]
    public void SpawnNow()
    {
        if (!prefab) { Debug.LogWarning("Assign a prefab."); return; }

        var mats = new NativeArray<float4x4>(count, Allocator.TempJob);
        var job = new SpawnXformsJob
        {
            min = min,
            max = max,
            outLocalToWorld = mats,
            rngBase = new Unity.Mathematics.Random(rngSeed == 0 ? 1u : rngSeed)
        };

        JobHandle handle = job.Schedule(count, 64); // 64 = batch size
        handle.Complete();                          // wait for transforms

        // Main thread: instantiate using the results
        for (int i = 0; i < count; i++)
        {
            var m = mats[i];
            var go = Instantiate(prefab);
            // extract TRS
            float3 p = m.c3.xyz;
            go.transform.SetPositionAndRotation((Vector3)p, Quaternion.identity);
            go.transform.localScale = Vector3.one * uniformScale;
        }

        mats.Dispose();
    }

    // quick demo: auto spawn on Start
    void Start()
    {
        // comment out if you want to call via context menu or another script
        SpawnNow();
    }
}

