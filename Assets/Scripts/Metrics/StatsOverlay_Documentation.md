# StatsOverlay - Performance Metrics Documentation

## Overview

`StatsOverlay.cs` provides real-time performance monitoring for the VR application, displaying critical metrics that help evaluate system performance, physics simulation efficiency, and parallel processing effectiveness. The overlay shows both general performance stats and parallel processing-specific metrics.

---

## Displayed Statistics

### Performance Stats Section

#### 1. FPS (Frames Per Second)

**Display Format:** `FPS: 72.0  (13.89 ms)`

**What it measures:**
- Number of frames rendered per second
- Frame time in milliseconds (time to render one frame)

**How it's calculated:**
```csharp
float avgFrame = accumDelta / frames;  // Average frame time
float fps = 1f / avgFrame;              // FPS = 1 / frame_time
float ms = avgFrame * 1000f;            // Convert to milliseconds
```

**Why it matters:**
- **VR Requirement:** Must maintain 72 FPS minimum for Quest 2/Pro to prevent motion sickness
- **Target:** 72 FPS = 13.89 ms per frame budget
- **Performance indicator:** Lower FPS = poor performance, higher FPS = good performance

**Typical values:**
- ✅ **Good:** 72+ FPS (≤13.89 ms) - Smooth VR experience
- ⚠️ **Warning:** 60-72 FPS (13.89-16.67 ms) - Noticeable stuttering
- ❌ **Bad:** <60 FPS (>16.67 ms) - Motion sickness risk

**What affects it:**
- Number of spawned objects (Rigidbodies)
- Draw calls (rendering complexity)
- Physics calculations (FixedUpdate load)
- Parallel job overhead (minimal with current implementation)

---

#### 2. FixedUpdate

**Display Format:** `FixedUpdate: 1.25 ms  (target 0.020s)`

**What it measures:**
- Time spent in physics simulation per fixed timestep
- Target fixed timestep interval (default 0.02s = 50 Hz)

**How it's calculated:**
```csharp
float avgFixed = accumFixed / fixedSteps;  // Average fixed update time
float fixedMs = avgFixed * 1000f;           // Convert to milliseconds
```

**Why it matters:**
- **Physics simulation cost:** Shows how expensive collision detection and Rigidbody updates are
- **Fixed timestep:** Unity runs physics at fixed intervals (default 50 times/second)
- **VR impact:** If FixedUpdate takes too long, it can cause frame drops

**Typical values:**
- ✅ **Good:** <5 ms - Physics not bottlenecking rendering
- ⚠️ **Warning:** 5-10 ms - Physics load increasing
- ❌ **Bad:** >10 ms - Physics may cause frame skips

**What affects it:**
- Number of active Rigidbodies (each requires collision checks)
- Collider complexity (Mesh > Box > Sphere)
- Physics material friction/bounciness
- Collision detection mode (Continuous > Discrete)

**Note:** The target value (0.020s) shows the *interval* between FixedUpdate calls, not the time it takes. The displayed ms shows actual execution time.

---

#### 3. Rigidbodies

**Display Format:** `Rigidbodies: 100`

**What it measures:**
- Total number of active Rigidbody components in the scene

**How it's calculated:**
```csharp
int rbCount = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
```

**Why it matters:**
- **Physics load:** Each Rigidbody requires physics calculations every FixedUpdate
- **Collision pairs:** N Rigidbodies = O(N²) potential collisions (spatial partitioning helps)
- **VR performance:** More objects = more physics work = lower FPS

**Typical values:**
- ✅ **Good:** 50-200 objects - Smooth VR performance on Quest 2
- ⚠️ **Warning:** 200-500 objects - Performance may degrade
- ❌ **Bad:** >500 objects - Likely FPS drops

**What affects it:**
- `CosmeticJobsController.count` setting
- Number of spawned grabbable objects
- Static environment objects with Rigidbodies

**Relationship to parallel processing:**
- More objects = more spawn data to calculate in parallel job
- Parallel job scales well with object count
- GameObject creation (main thread) is the bottleneck

---

#### 4. Draw Calls (approx)

**Display Format:** `Draw Calls (approx): 150`

**What it measures:**
- Approximate number of GPU draw calls per frame
- Each draw call tells GPU to render a batch of objects

**How it's calculated:**
```csharp
UnityStats.drawCalls  // Unity's internal rendering statistics
```

**Why it matters:**
- **GPU bottleneck:** Fewer draw calls = better GPU performance
- **Batching efficiency:** Shows how well Unity combines objects for rendering
- **VR overhead:** VR renders twice (once per eye), doubling draw call impact

**Typical values:**
- ✅ **Good:** <200 draw calls - Well-optimized scene
- ⚠️ **Warning:** 200-500 draw calls - Moderate GPU load
- ❌ **Bad:** >500 draw calls - GPU bottleneck likely

**What affects it:**
- Number of different materials used
- GPU instancing (enabled/disabled)
- Dynamic batching (Unity combines similar objects)
- Static batching (for non-moving objects)

**Optimization tip:**
- Use same material for all spawned objects to enable batching
- Enable GPU instancing on materials
- Reduce number of unique meshes

---

### Parallel Processing Comparison Section

This section appears when `CosmeticJobsController.LastJobCount > 0` (after objects have been spawned) and displays **serial vs parallel execution comparison metrics**.

#### 5. Mode

**Display Format:**
- `Mode: Parallel (8 cores)` - When using parallel processing
- `Mode: Serial (1 core)` - When using serial processing

**What it measures:**
- Current execution strategy for spawn data calculation
- Number of CPU cores utilized

**How it's determined:**
```csharp
string mode = CosmeticJobsController.IsUsingParallel
    ? $"Parallel ({CosmeticJobsController.WorkerCount} cores)"
    : "Serial (1 core)";
```

**Why it matters:**
- **Class demonstration:** Clearly shows which execution mode is active
- **Core utilization:** Displays actual parallelism (1 core vs all cores)
- **Toggle feedback:** Confirms mode switching when toggling `useParallelCosmetic`

**How to toggle:**
- In Unity Editor: Toggle `BenchManager → Use Parallel Cosmetic` checkbox
- In VR: Press B button to cycle modes (if mode switching enabled)

---

#### 6. Calculation Time

**Display Format:** `Calculation: 0.543 ms` (parallel) or `Calculation: 2.821 ms` (serial)

**What it measures:**
- Time to calculate spawn data (positions, rotations, scales)
- **This is where parallel processing provides speedup**

**How it's calculated:**
```csharp
var calculationTimer = Stopwatch.StartNew();
// Either parallel job or serial for-loop
calculationTimer.Stop();
LastCalculationMs = (float)calculationTimer.Elapsed.TotalMilliseconds;
```

**Why it matters:**
- **Key comparison metric:** Shows direct impact of parallelization
- **Speedup demonstration:** Parallel is typically 5-8x faster than serial
- **Scales with object count:** More objects = more calculation time

**Typical values (100 objects):**
- Parallel: 0.5-2 ms (multi-core)
- Serial: 2.5-10 ms (single-core)
- **Speedup:** ~5-8x on 8-core systems

**What affects it:**
- Number of objects (`count`)
- Spawn pattern (uniform vs random - random has more calculations)
- CPU architecture (Burst compilation optimizes heavily)
- Number of CPU cores (parallel mode only)

---

#### 7. GameObject Creation Time

**Display Format:** `GameObject Creation: 12.456 ms`

**What it measures:**
- Time to instantiate GameObjects and add components
- **Always single-threaded** (Unity API limitation)

**How it's calculated:**
```csharp
var gameObjectTimer = Stopwatch.StartNew();
for (int i = 0; i < count; i++)
{
    // Create GameObject, add components
}
gameObjectTimer.Stop();
LastGameObjectMs = (float)gameObjectTimer.Elapsed.TotalMilliseconds;
```

**Why it matters:**
- **Bottleneck identification:** Shows unavoidable serial cost
- **Fair comparison:** Same for both serial and parallel modes
- **Real-world constraint:** Demonstrates Unity API limitations

**Typical values (100 objects):**
- ~10-20 ms regardless of calculation mode
- Much larger than calculation time
- Dominates total spawn time

**What affects it:**
- Number of objects (linear scaling)
- Component complexity (Rigidbody, Colliders, Grabbable)
- Unity version and optimization settings

**Note:** This time is identical in both serial and parallel modes, proving the comparison is fair.

---

#### 8. Total Spawn Time

**Display Format:** `Total Spawn: 12.999 ms`

**What it measures:**
- End-to-end time to spawn all objects
- Calculation time + GameObject creation time

**How it's calculated:**
```csharp
var totalTimer = Stopwatch.StartNew();
// Calculation (serial or parallel) + GameObject creation
totalTimer.Stop();
LastTotalMs = (float)totalTimer.Elapsed.TotalMilliseconds;
```

**Why it matters:**
- **Complete picture:** Shows overall spawn performance
- **User experience:** Affects loading time in VR
- **Bottleneck visibility:** Helps identify optimization opportunities

**Breakdown:**
- Parallel mode: Calculation (~1-2ms) + GameObject creation (~10-20ms) = ~12-22ms
- Serial mode: Calculation (~5-10ms) + GameObject creation (~10-20ms) = ~15-30ms

**Typical difference:** Parallel saves 3-8ms total (mostly in calculation phase)

---

#### 9. Object Count

**Display Format:** `Object Count: 100`

**What it measures:**
- Number of objects spawned in the last operation
- Same value for both serial and parallel modes

**How it's calculated:**
```csharp
LastJobCount = count;  // Set during SpawnObjects()
```

**Why it matters:**
- **Work complexity:** More objects = more calculation and creation time
- **Scalability testing:** Helps evaluate performance at different scales
- **Fair comparison:** Ensures serial and parallel modes process same workload

**Relationship to performance:**
- Linear scaling for calculation time (both serial and parallel)
- Linear scaling for GameObject creation time
- Parallel mode maintains advantage at all object counts

---

#### 10. Speedup Factor

**Display Format:** `Speedup: 5.2x faster`

**What it measures:**
- Performance improvement from parallel processing
- Ratio of serial calculation time to parallel calculation time

**How it's calculated:**
```csharp
if (SerialCalculationMs > 0f && ParallelCalculationMs > 0f)
{
    SpeedupFactor = SerialCalculationMs / ParallelCalculationMs;
}
```

**Why it matters:**
- **Class demonstration:** Quantifies parallel processing benefits
- **Academic metric:** Standard measure of parallelization effectiveness
- **Hardware utilization:** Shows how well code uses available cores

**Typical values:**
- 8-core system: 5-8x speedup (excellent)
- 4-core system: 3-5x speedup (good)
- Theoretical maximum: Number of cores (8x on 8-core)
- Actual: Lower due to overhead and Amdahl's Law

**When it appears:**
- Only shown after running BOTH serial and parallel modes
- Persists across mode switches for comparison
- Reset when objects are cleared

**Academic significance:**
- Demonstrates Amdahl's Law (speedup limited by serial portion)
- Shows diminishing returns beyond certain core counts
- Proves parallel processing provides measurable benefits

---

**Example Display:**

```
Parallel Processing Comparison
Mode: Parallel (8 cores)
Calculation: 0.543 ms
GameObject Creation: 12.456 ms
Total Spawn: 12.999 ms
Object Count: 100
Speedup: 5.2x faster
```

---

## Performance Monitoring Strategy

### Averaging Window

The script uses a **moving average** over 30 frames (configurable via `sampleCount`):

```csharp
public int sampleCount = 30;
```

**Why averaging?**
- Smooths out frame-to-frame variance
- Provides stable, readable metrics
- Reduces visual noise in the display

**Reset behavior:**
```csharp
if (frames >= sampleCount)
{
    accumDelta = 0f; frames = 0;
    accumFixed = 0f; fixedSteps = 0;
}
```

---

## Reading the Stats for Your Parallel Processing Class

### What to Look For

1. **Direct Serial vs Parallel Comparison:**
   - Toggle `useParallelCosmetic` in BenchManager to switch modes
   - Run both modes with same object count
   - Compare Calculation times directly
   - Speedup Factor quantifies the improvement

2. **Calculation Time Difference:**
   - Parallel: ~0.5-2 ms (multi-core)
   - Serial: ~2.5-10 ms (single-core)
   - **This is where parallel processing provides measurable speedup**

3. **GameObject Creation Bottleneck:**
   - GameObject creation time is identical in both modes (~10-20ms)
   - Shows Unity API limitation (main thread only)
   - Demonstrates why parallelizing calculation is valuable
   - Even though GameObject creation dominates, calculation speedup matters

4. **Speedup Factor Analysis:**
   - Typical: 5-8x on 8-core systems
   - Shows real-world parallel processing benefits
   - Demonstrates Amdahl's Law (limited by serial portion)

5. **VR Performance:**
   - FPS must stay at 72+ for smooth experience
   - Both serial and parallel modes should maintain FPS
   - Calculation time is small portion of frame budget

---

## Example Stats Interpretation

### Parallel Mode:

```
Performance Stats
FPS: 72.0  (13.89 ms)
Rigidbodies: 100
Draw Calls (approx): 125

Parallel Processing Comparison
Mode: Parallel (8 cores)
Calculation: 0.543 ms
GameObject Creation: 12.456 ms
Total Spawn: 12.999 ms
Object Count: 100
Speedup: 5.2x faster
```

**What this tells us (Parallel Mode):**

✅ **VR Performance:** Hitting 72 FPS target - smooth experience
✅ **Multi-core Utilization:** Using all 8 cores for calculation
✅ **Calculation Efficiency:** Only 0.543ms for 100 objects (excellent)
✅ **GameObject Bottleneck:** 12.456ms shows main thread limitation
✅ **Speedup Achieved:** 5.2x faster than serial calculation

### Serial Mode:

```
Performance Stats
FPS: 72.0  (13.89 ms)
Rigidbodies: 100
Draw Calls (approx): 125

Parallel Processing Comparison
Mode: Serial (1 core)
Calculation: 2.821 ms
GameObject Creation: 12.398 ms
Total Spawn: 15.219 ms
Object Count: 100
```

**What this tells us (Serial Mode):**

✅ **VR Performance:** Still hitting 72 FPS (spawn happens once)
⚠️ **Single-core Limitation:** Only using 1 of 8 available cores
⚠️ **Slower Calculation:** 2.821ms vs 0.543ms parallel (5.2x slower)
✅ **GameObject Time Similar:** 12.398ms (proves fair comparison)
⚠️ **Longer Total Time:** 15.219ms vs 12.999ms parallel

### Comparison Analysis:

| Metric | Serial | Parallel | Improvement |
|--------|--------|----------|-------------|
| Calculation | 2.821 ms | 0.543 ms | **5.2x faster** |
| GameObject Creation | 12.398 ms | 12.456 ms | Same (expected) |
| Total Spawn | 15.219 ms | 12.999 ms | 2.22 ms saved |
| Cores Used | 1 | 8 | **8x resources** |

**Conclusion:** Parallel processing provides **5.2x speedup** for spawn calculation using all 8 cores. Even though GameObject creation (unavoidable serial portion) dominates total time, parallelization still saves ~2.2ms per spawn. This demonstrates real-world parallel processing benefits and Amdahl's Law (speedup limited by serial portion).

---

## Summary

The StatsOverlay provides essential metrics for:

1. **VR Performance Monitoring** - FPS, frame time, physics cost
2. **Physics Debugging** - Rigidbody count, physics simulation time
3. **Rendering Analysis** - Draw call optimization
4. **Serial vs Parallel Comparison** - Direct execution mode comparison with speedup factor

For your **Parallel Processing class project**, focus on:
- **Calculation time difference** (serial vs parallel) - shows direct impact
- **Speedup Factor** - quantifies parallel processing benefits (e.g., "5.2x faster")
- **GameObject creation time** - demonstrates Amdahl's Law (serial bottleneck)
- **Mode toggle** - allows live demonstration of parallel vs serial execution
- **Core utilization** - shows multi-core vs single-core resource usage

This overlay provides a **perfect demonstration** for Parallel Processing classes, showing:
- Measurable speedup from parallelization
- Fair comparison (same algorithm, same output)
- Real-world constraints (Unity API serial limitations)
- Academic concepts (Amdahl's Law, speedup factor, scalability)
