# CosmeticJobsController - Parallel Processing Documentation

## Overview

`CosmeticJobsController.cs` is a Unity script that demonstrates **true parallel processing** using Unity's Job System and Burst Compiler. It manages thousands of cosmetic particles with physics-based movement, distributing the computational work across multiple CPU cores.

---

## Parallel Processing Confirmation

**Yes, this script utilizes parallel processing.** Here's the evidence:

1. **`IJobParallelFor` Interface** - Both jobs implement this interface, which automatically distributes work across worker threads
2. **Burst Compilation** - `[BurstCompile]` attribute enables high-performance native code generation
3. **Batch Scheduling** - Jobs are scheduled with batch sizes (64 and 128) for optimal thread distribution

---

## Script Flow

### 1. Initialization Phase (`Start()`)

```
Start()
  ├── Validate mesh & material
  ├── Allocate() → Create NativeArrays for pos, vel, matrices
  └── InitializeParticles() → Schedule InitJob (PARALLEL)
```

### 2. Per-Frame Update Phase (`Update()`)

```
Update()
  ├── Start timing
  ├── Schedule IntegrateJob (PARALLEL)
  ├── Complete job & record stats
  └── DrawBatched() → Render all particles (MAIN THREAD)
```

### 3. Cleanup Phase (`OnDestroy()`)

```
OnDestroy()
  └── DisposeIfAllocated() → Free all NativeArrays
```

---

## Parallel Jobs Explained

### InitJob (Particle Initialization)

**Purpose:** Initialize all particle positions, velocities, and transformation matrices in parallel.

```csharp
[BurstCompile]
struct InitJob : IJobParallelFor
{
    public void Execute(int i)
    {
        // Each thread handles different particles simultaneously
        // Thread 1: particles 0-63
        // Thread 2: particles 64-127
        // Thread N: particles ...

        var rng = new Rng(rngBase.state + (uint)i);  // Unique RNG per particle
        float3 p = math.lerp(min, max, rng.NextFloat3());  // Random position
        float3 v = dir * spd;  // Random velocity

        pos[i] = p;
        vel[i] = v;
        outMatrices[i] = float4x4.TRS(p, r, scale);
    }
}
```

**Scheduling:**
```csharp
job.Schedule(count, 64);  // count=2000, batchSize=64
// Creates ~31 batches distributed across worker threads
```

### IntegrateJob (Physics Update)

**Purpose:** Update all particle positions based on velocity and damping every frame.

```csharp
[BurstCompile]
struct IntegrateJob : IJobParallelFor
{
    public void Execute(int i)
    {
        // Exponential damping for smooth deceleration
        float3 v = vel[i] * math.exp(-damping * dt);
        float3 p = pos[i] + v * dt;

        vel[i] = v;
        pos[i] = p;
        outMatrices[i] = float4x4.TRS(p, quaternion.identity, 1f);
    }
}
```

**Scheduling:**
```csharp
integrate.Schedule(count, 128);  // count=2000, batchSize=128
// Creates ~16 batches distributed across worker threads
```

---

## How Unity Job System Distributes Work

```
┌─────────────────────────────────────────────────────┐
│                    Main Thread                       │
│  Schedule(2000, 128) → Creates 16 batches           │
└─────────────────┬───────────────────────────────────┘
                  │
    ┌─────────────┼─────────────┐
    ▼             ▼             ▼
┌─────────┐ ┌─────────┐   ┌─────────┐
│Worker 0 │ │Worker 1 │...│Worker N │
│Batch 0  │ │Batch 1  │   │Batch 15 │
│i=0-127  │ │i=128-255│   │i=1920+  │
└─────────┘ └─────────┘   └─────────┘
    │             │             │
    └─────────────┼─────────────┘
                  ▼
         handle.Complete()
         (Synchronization)
```

---

## Key Data Structures

| Array | Type | Purpose |
|-------|------|---------|
| `pos` | `NativeArray<float3>` | Particle positions |
| `vel` | `NativeArray<float3>` | Particle velocities |
| `matrices` | `NativeArray<float4x4>` | Transform matrices for rendering |

All arrays use `Allocator.Persistent` for long-term allocation across frames.

---

## Performance Metrics

The script exposes static properties for monitoring:

- `LastJobMs` - Time spent in the parallel job (milliseconds)
- `LastJobCount` - Number of particles processed
- `WorkerCount` - Number of available worker threads
- `LastUpdateFrame` - Frame number of last update

Debug output every 60 frames:
```
[CosmeticJobsController] Job: 0.123 ms | count=2000 | frame=16.667 ms | workers=7
```

---

## Why This is True Parallel Processing

1. **Multiple Threads Execute Simultaneously**
   - `IJobParallelFor` distributes `Execute(int i)` calls across all available CPU cores
   - Each worker thread processes a batch independently

2. **No Main Thread Blocking During Computation**
   - Work is distributed to worker threads
   - Main thread only blocks at `Complete()` to synchronize results

3. **Burst Compilation**
   - Generates highly optimized SIMD instructions
   - Eliminates managed code overhead
   - Can be 10-100x faster than equivalent C# code

4. **Thread-Safe Design**
   - Each particle index `i` is processed by exactly one thread
   - No race conditions or locks needed
   - NativeArrays provide safe concurrent write access

---

## Batch Size Selection

| Job | Batch Size | Rationale |
|-----|------------|-----------|
| InitJob | 64 | More complex per-particle work (RNG, TRS) |
| IntegrateJob | 128 | Simpler math operations |

Smaller batches = better load balancing but more scheduling overhead.
Larger batches = less overhead but potential load imbalance.

---

## Rendering (Main Thread)

After parallel computation, rendering occurs on the main thread:

```csharp
void DrawBatched()
{
    // Graphics.DrawMeshInstanced max = 1023 per call
    while (i < matrices.Length)
    {
        int n = math.min(1023, matrices.Length - i);
        // Convert float4x4 → Matrix4x4
        Graphics.DrawMeshInstanced(mesh, 0, material, batch, n, ...);
        i += n;
    }
}
```

This efficiently renders thousands of particles using GPU instancing.

---

## Summary

`CosmeticJobsController` demonstrates proper use of Unity's parallel processing capabilities:

- **True parallelism** via `IJobParallelFor`
- **High performance** via Burst compilation
- **Memory efficiency** via NativeArrays
- **Clean separation** between parallel computation and main-thread rendering

The script can handle 2000+ particles with minimal CPU overhead due to the parallel distribution of physics calculations across all available CPU cores.
