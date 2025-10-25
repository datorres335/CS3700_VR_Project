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
        sb.AppendLine("<b>VR Sandbox Stats</b>");
        sb.AppendFormat("FPS: {0:0.0}  ({1:0.00} ms)\n", fps, ms);
        sb.AppendFormat("FixedUpdate: {0:0.00} ms  (target {1:0.000}s)\n", fixedMs, Time.fixedDeltaTime);
        sb.AppendFormat("Rigidbodies: {0}\n", rbCount);
        sb.AppendLine("Debris (cosmetic): 0  (Phase 2+)");
        sb.AppendFormat("Draw Calls (approx): {0}\n", UnityStats.drawCalls);
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
