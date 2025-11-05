using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Rng = Unity.Mathematics.Random;

public class CosmeticJobsController : MonoBehaviour
{
    [Header("Rendering (assign these)")]
    public Mesh mesh;
    public Material material; // URP Simple Lit/Unlit, Enable GPU Instancing ✅

    [Header("Counts & Area")]
    public int count = 2000;
    public Vector3 spawnMin = new(-3f, 1f, -3f);
    public Vector3 spawnMax = new(3f, 2f, 3f);

    [Header("Motion")]
    public float initialSpeed = 0.5f;   // random speed magnitude
    public float damping = 0.5f;        // per-second exponential damping
    public float baseScale = 1.0f;      // global size
    public Vector2 randomScale = new(0.8f, 1.2f);

    [Header("Seed")]
    public uint rngSeed = 123u;

    // Native buffers
    NativeArray<float3> pos;
    NativeArray<float3> vel;
    NativeArray<float4x4> matrices;

    // temp batch buffer for DrawMeshInstanced
    Matrix4x4[] batch = new Matrix4x4[1023];

    bool initialized;

    // -------------------- JOBS --------------------

    [BurstCompile]
    struct InitJob : IJobParallelFor
    {
        public float3 min, max;
        public float speedMag;
        public float baseScale;
        public float2 randScaleMinMax;
        public float uniformYRotation; // set 1 to keep upright else random yaw only
        public Rng rngBase;

        public NativeArray<float3> pos;
        public NativeArray<float3> vel;
        public NativeArray<float4x4> outMatrices;

        public void Execute(int i)
        {
            var rng = new Rng(rngBase.state + (uint)i);

            float3 p = math.lerp(min, max, rng.NextFloat3());
            float3 dir = math.normalize(rng.NextFloat3Direction());
            float spd = speedMag * rng.NextFloat(0.5f, 1.5f);
            float3 v = dir * spd;

            float s = baseScale * rng.NextFloat(randScaleMinMax.x, randScaleMinMax.y);

            quaternion r;
            if (uniformYRotation > 0.5f)
                r = quaternion.EulerXYZ(0, rng.NextFloat(0f, math.PI * 2f), 0);
            else
                r = quaternion.identity;

            pos[i] = p;
            vel[i] = v;
            outMatrices[i] = float4x4.TRS(p, r, new float3(s, s, s));
        }
    }

    [BurstCompile]
    struct IntegrateJob : IJobParallelFor
    {
        public float dt;
        public float damping; // per-second
        public NativeArray<float3> pos;
        public NativeArray<float3> vel;
        public NativeArray<float4x4> outMatrices;

        public void Execute(int i)
        {
            float3 v = vel[i] * math.exp(-damping * dt);
            float3 p = pos[i] + v * dt;

            vel[i] = v;
            pos[i] = p;

            outMatrices[i] = float4x4.TRS(p, quaternion.identity, 1f);
        }
    }

    // -------------------- LIFECYCLE --------------------

    void Start()
    {
        if (!mesh || !material)
        {
            Debug.LogWarning("Assign mesh & material to CosmeticJobsController.");
            enabled = false; return;
        }

        Allocate();
        InitializeParticles();
        initialized = true;
    }

    void Allocate()
    {
        DisposeIfAllocated();

        pos = new NativeArray<float3>(count, Allocator.Persistent);
        vel = new NativeArray<float3>(count, Allocator.Persistent);
        matrices = new NativeArray<float4x4>(count, Allocator.Persistent);
    }

    void InitializeParticles()
    {
        var job = new InitJob
        {
            min = spawnMin,
            max = spawnMax,
            speedMag = initialSpeed,
            baseScale = baseScale,
            randScaleMinMax = randomScale,
            uniformYRotation = 1f, // upright
            rngBase = new Rng(rngSeed == 0 ? 1u : rngSeed),
            pos = pos,
            vel = vel,
            outMatrices = matrices
        };

        var handle = job.Schedule(count, 64);
        handle.Complete();
    }

    void Update()
    {
        if (!initialized) return;

        // Parallel integrate
        var integrate = new IntegrateJob
        {
            dt = Time.deltaTime,
            damping = damping,
            pos = pos,
            vel = vel,
            outMatrices = matrices
        };
        var handle = integrate.Schedule(count, 128);
        handle.Complete();

        // Draw (main thread)
        DrawBatched();
    }

    void DrawBatched()
    {
        if (!material.enableInstancing) return;
        int i = 0;
        while (i < matrices.Length)
        {
            int n = math.min(1023, matrices.Length - i);
            // convert float4x4 -> Matrix4x4
            for (int j = 0; j < n; j++)
                batch[j] = ToUnity(matrices[i + j]);

            Graphics.DrawMeshInstanced(
                mesh, 0, material, batch, n,
                null, ShadowCastingMode.Off, false,
                0, null, LightProbeUsage.Off, null);

            i += n;
        }
    }

    static Matrix4x4 ToUnity(float4x4 m)
    {
        Matrix4x4 u = new Matrix4x4();
        u.m00 = m.c0.x; u.m10 = m.c0.y; u.m20 = m.c0.z; u.m30 = m.c0.w;
        u.m01 = m.c1.x; u.m11 = m.c1.y; u.m21 = m.c1.z; u.m31 = m.c1.w;
        u.m02 = m.c2.x; u.m12 = m.c2.y; u.m22 = m.c2.z; u.m32 = m.c2.w;
        u.m03 = m.c3.x; u.m13 = m.c3.y; u.m23 = m.c3.z; u.m33 = m.c3.w;
        return u;
    }

    void OnDestroy() => DisposeIfAllocated();

    void DisposeIfAllocated()
    {
        if (pos.IsCreated) pos.Dispose();
        if (vel.IsCreated) vel.Dispose();
        if (matrices.IsCreated) matrices.Dispose();
    }

    public void ReinitializeNow()
    {
        DisposeIfAllocated();  // free old NativeArrays if any
        Allocate();            // allocate using the current 'count'
        InitializeParticles(); // fill positions/velocities/matrices
    }

}
