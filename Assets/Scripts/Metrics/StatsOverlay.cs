using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public class StatsOverlay : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public TextMeshProUGUI statsText;

    [Header("Averaging")]
    public int sampleCount = 30;

    float accumDelta, accumFixed;
    int frames, fixedSteps;

    StringBuilder sb = new StringBuilder(256);

    void Update()
    {
        // accumulate for a simple moving average of frame time
        accumDelta += Time.unscaledDeltaTime;
        frames++;

        // rigidbodies active in scene (cheap and fine for Phase 1)
        int rbCount = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;

        // averages
        float avgFrame = (frames > 0) ? (accumDelta / frames) : Time.unscaledDeltaTime;
        float fps = 1f / Mathf.Max(0.00001f, avgFrame);
        float ms = avgFrame * 1000f;

        float avgFixed = (fixedSteps > 0) ? (accumFixed / fixedSteps) : Time.fixedUnscaledDeltaTime;
        float fixedMs = avgFixed * 1000f;

        // build text
        sb.Clear();
        sb.AppendLine("<b>Performance Stats</b>");
        sb.AppendFormat("FPS: {0:0.0}  ({1:0.00} ms)\n", fps, ms);
        //sb.AppendFormat("FixedUpdate: {0:0.00} ms  (target {1:0.000}s)\n", fixedMs, Time.fixedDeltaTime);
        sb.AppendFormat("Rigidbodies: {0}\n", rbCount);
        sb.AppendFormat("Draw Calls (approx): {0}\n", UnityStats.drawCalls);

        // append parallel processing comparison stats (if available)
        if (CosmeticJobsController.LastJobCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Parallel Processing Comparison</b>");

            // Current execution mode
            string mode = CosmeticJobsController.IsUsingParallel
                ? $"Parallel ({CosmeticJobsController.WorkerCount} cores)"
                : "Serial (1 core)";
            sb.AppendFormat("Mode: {0}\n", mode);

            // Timing breakdown
            sb.AppendFormat("Calculation: {0:0.000} ms\n", CosmeticJobsController.LastCalculationMs);
            sb.AppendFormat("GameObject Creation: {0:0.000} ms\n", CosmeticJobsController.LastGameObjectMs);
            sb.AppendFormat("Total Spawn: {0:0.000} ms\n", CosmeticJobsController.LastTotalMs);
            sb.AppendFormat("Object Count: {0}\n", CosmeticJobsController.LastJobCount);

            // Show speedup factor if we have both serial and parallel measurements
            if (CosmeticJobsController.SpeedupFactor > 0f)
            {
                sb.AppendFormat("Speedup: {0:0.0}x faster\n", CosmeticJobsController.SpeedupFactor);
            }
        }

        statsText.text = sb.ToString();

        // reset the window every N samples
        if (frames >= sampleCount)
        {
            accumDelta = 0f; frames = 0;
            accumFixed = 0f; fixedSteps = 0;
        }
    }

    void FixedUpdate()
    {
        accumFixed += Time.fixedUnscaledDeltaTime;
        fixedSteps++;
    }
}
