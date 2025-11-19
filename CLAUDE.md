# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity VR project for Meta Quest that benchmarks physics performance across three rendering modes. Built for CS3700 coursework to compare rigidbody physics, fractured objects, and parallel job-based particle systems.

- **Unity Version**: 6000.2.9f1 (Unity 6)
- **Target Platform**: Android (Meta Quest)
- **Target FPS**: 72 Hz

## Build Commands

```bash
# Build APK via Unity command line
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod BuildScript.Build -quit

# Or use Unity Editor: File > Build Settings > Android > Build
```

No custom test framework - testing is done via in-headset performance monitoring.

## Architecture

### Core Systems

```
VR Controller Input
    ├── OverlayToggle.cs      [Primary Button] → Toggle StatsOverlay
    └── ModeCycler.cs         [Secondary Button] → Cycle BenchMode
                                        ↓
                              BenchManager.RunMode()
                                        ↓
                    ┌───────────────────┼───────────────────┐
                    ↓                   ↓                   ↓
              RigidOnly            Fractured            Cosmetic
           (150 rigidbodies)    (8 fractured sets)   (2000+ particles)
                                                           ↓
                                              CosmeticJobsController
                                              (IJobParallelFor + Burst)
```

### Key Scripts by System

**Benchmark Orchestration** (`Assets/Scripts/Sandbox/`)
- `BenchManager.cs` - Central controller for spawning/clearing debris across modes
- `ModeCycler.cs` - VR input handler for mode switching
- `PhysicsTunerUI.cs` - Runtime physics parameter adjustment

**Parallel Processing** (`Assets/Scripts/Parallel/`)
- `CosmeticJobsController.cs` - Burst-compiled parallel particle physics
- `ParallelSpawnExample.cs` - Example parallel spawning implementation

**Metrics** (`Assets/Scripts/Metrics/`)
- `StatsOverlay.cs` - Real-time FPS, rigidbody count, job timing display
- `OverlayToggle.cs` - VR input handler for overlay visibility

### Parallel Job System

The `CosmeticJobsController` uses true multi-threaded processing:

```csharp
// Two Burst-compiled jobs
InitJob : IJobParallelFor      // Initialize positions/velocities (batch 64)
IntegrateJob : IJobParallelFor // Per-frame physics update (batch 128)

// Data stored in NativeArrays (Allocator.Persistent)
NativeArray<float3> pos, vel;
NativeArray<float4x4> matrices;

// Rendering via batched instancing (max 1023 per call)
Graphics.DrawMeshInstanced()
```

Static metrics exposed: `LastJobMs`, `LastJobCount`, `WorkerCount`

## Important Constraints

### Burst Compilation Requirements
Code inside jobs must be Burst-compatible:
- Use `Unity.Mathematics` types (`float3`, `quaternion`), not `UnityEngine` types
- No managed collections or allocations in hot paths
- No virtual method calls
- Mark jobs with `[BurstCompile]`

### NativeArray Lifecycle
All persistent NativeArrays must be explicitly disposed:
```csharp
void OnDestroy() => DisposeIfAllocated();

void DisposeIfAllocated()
{
    if (pos.IsCreated) pos.Dispose();
    // ... dispose all arrays
}
```

### VR Input Bindings
Uses Unity Input System with Meta Quest controller bindings:
- `<XRController>{LeftHand}/primaryButton` - Toggle overlay
- `<XRController>{LeftHand}/secondaryButton` - Cycle modes

### GPU Instancing Limit
`Graphics.DrawMeshInstanced` supports max 1023 instances per call. Code must batch larger counts.

## Key Package Dependencies

- `com.meta.xr.sdk.core` v78.0.0 - Meta Quest SDK
- `com.unity.xr.openxr` v1.15.1 - OpenXR runtime
- `com.unity.inputsystem` v1.14.2 - New Input System
- `com.unity.render-pipelines.universal` v17.2.0 - URP
- `com.unity.burst` / `com.unity.jobs` / `com.unity.collections` - Job System

## Current Status

Phase 5 complete with known issues - see GitHub Issues tab for details. Recent commits show parallel job integration is functional but ParallelSpawnDemo prefab has bugs.
