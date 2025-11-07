using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhysicsTunerUI : MonoBehaviour
{
    [Header("References")]
    public BenchManager bench;   // drag your BenchRoot (BenchManager) here

    [Header("Fixed timestep (Hz)")]
    public Slider hzSlider;
    public TMP_Text hzLabel;
    // 60–120 Hz range; Quest baseline ~72 Hz
    const float MinHz = 45f, MaxHz = 120f;

    [Header("Solver iterations")]
    public Slider solverIterSlider;
    public TMP_Text solverIterLabel;   // 1–20

    public Slider solverVelIterSlider;
    public TMP_Text solverVelIterLabel; // 1–20

    [Header("Sleep threshold (Rigidbody)")]
    public Slider sleepSlider;
    public TMP_Text sleepLabel; // 0–0.1 common

    [Header("Max depenetration velocity (global)")]
    public Slider depenSlider;
    public TMP_Text depenLabel; // 0–20 typical mobile

    [Header("Mix toggles")]
    public Toggle heavyPhysicsToggle;   // high solver, fewer rigids
    public Toggle manyCosmeticToggle;   // reduce rigids, increase cosmetic

    [Header("Spawn counts")]
    public Slider rigidCountSlider;     // mirrors BenchManager.rigidCount
    public TMP_Text rigidCountLabel;
    public Slider cosmeticCountSlider;  // mirrors BenchManager.cosmeticCount
    public TMP_Text cosmeticCountLabel;

    void Start()
    {
        // Defaults / ranges
        if (hzSlider)
        {
            hzSlider.minValue = MinHz; hzSlider.maxValue = MaxHz;
            float currentHz = 1f / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            hzSlider.value = Mathf.Clamp(currentHz, MinHz, MaxHz);
            UpdateFixedHz(hzSlider.value);
            hzSlider.onValueChanged.AddListener(UpdateFixedHz);
        }

        if (solverIterSlider)
        {
            solverIterSlider.minValue = 1; solverIterSlider.maxValue = 20;
            solverIterSlider.wholeNumbers = true;
            solverIterSlider.value = Physics.defaultSolverIterations;
            UpdateSolverIter(solverIterSlider.value);
            solverIterSlider.onValueChanged.AddListener(UpdateSolverIter);
        }

        if (solverVelIterSlider)
        {
            solverVelIterSlider.minValue = 1; solverVelIterSlider.maxValue = 20;
            solverVelIterSlider.wholeNumbers = true;
            solverVelIterSlider.value = Physics.defaultSolverVelocityIterations;
            UpdateSolverVelIter(solverVelIterSlider.value);
            solverVelIterSlider.onValueChanged.AddListener(UpdateSolverVelIter);
        }

        if (sleepSlider)
        {
            sleepSlider.minValue = 0.0f; sleepSlider.maxValue = 0.1f;
            sleepSlider.value = 0.005f;
            UpdateSleep(sleepSlider.value);
            sleepSlider.onValueChanged.AddListener(UpdateSleep);
        }

        if (depenSlider)
        {
            depenSlider.minValue = 0f; depenSlider.maxValue = 20f;
            depenSlider.value = Mathf.Clamp(Physics.defaultMaxDepenetrationVelocity, 0f, 20f);
            UpdateDepen(depenSlider.value);
            depenSlider.onValueChanged.AddListener(UpdateDepen);
        }

        if (rigidCountSlider)
        {
            rigidCountSlider.minValue = 0; rigidCountSlider.maxValue = 1000;
            rigidCountSlider.wholeNumbers = true;
            rigidCountSlider.value = bench ? bench.rigidCount : 150;
            UpdateRigidCount(rigidCountSlider.value);
            rigidCountSlider.onValueChanged.AddListener(UpdateRigidCount);
        }

        if (cosmeticCountSlider)
        {
            cosmeticCountSlider.minValue = 0; cosmeticCountSlider.maxValue = 20000;
            cosmeticCountSlider.wholeNumbers = true;
            cosmeticCountSlider.value = bench ? bench.cosmeticCount : 2000;
            UpdateCosmeticCount(cosmeticCountSlider.value);
            cosmeticCountSlider.onValueChanged.AddListener(UpdateCosmeticCount);
        }

        if (heavyPhysicsToggle)
            heavyPhysicsToggle.onValueChanged.AddListener(SetHeavyPhysicsPreset);
        if (manyCosmeticToggle)
            manyCosmeticToggle.onValueChanged.AddListener(SetManyCosmeticPreset);

        // Apply any toggle that is already ON at startup
        if (heavyPhysicsToggle && heavyPhysicsToggle.isOn) SetHeavyPhysicsPreset(true);
        if (manyCosmeticToggle && manyCosmeticToggle.isOn) SetManyCosmeticPreset(true);
    }

    // --- Handlers ---

    void UpdateFixedHz(float hz)
    {
        Time.fixedDeltaTime = 1f / Mathf.Max(1f, hz);
        if (hzLabel) hzLabel.text = $"Fixed Timestep (Hz): {hz:0}";
    }

    void UpdateSolverIter(float v)
    {
        Physics.defaultSolverIterations = Mathf.RoundToInt(v);
        if (solverIterLabel) solverIterLabel.text = $"Solver Iterations: {Physics.defaultSolverIterations}";
    }

    void UpdateSolverVelIter(float v)
    {
        Physics.defaultSolverVelocityIterations = Mathf.RoundToInt(v);
        if (solverVelIterLabel) solverVelIterLabel.text = $"Solver Velocity Iter: {Physics.defaultSolverVelocityIterations}";
    }

    void UpdateSleep(float v)
    {
        if (sleepLabel) sleepLabel.text = $"Sleep Threshold: {v:0.000}";
        // apply to existing rigidbodies in scene (live)
        var rbs = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        foreach (var rb in rbs) rb.sleepThreshold = v;
    }

    void UpdateDepen(float v)
    {
        Physics.defaultMaxDepenetrationVelocity = v;
        if (depenLabel) depenLabel.text = $"Max Depenetration Vel: {v:0.0}";
    }

    void UpdateRigidCount(float v)
    {
        if (!bench) return;
        bench.rigidCount = Mathf.RoundToInt(v);
        if (rigidCountLabel) rigidCountLabel.text = $"Rigid Count: {bench.rigidCount}";
        if (bench.mode == BenchMode.RigidOnly) bench.RunMode(BenchMode.RigidOnly); // respawn
    }

    void UpdateCosmeticCount(float v)
    {
        if (!bench) return;
        bench.cosmeticCount = Mathf.RoundToInt(v);
        if (cosmeticCountLabel) cosmeticCountLabel.text = $"Cosmetic Count: {bench.cosmeticCount}";
        if (bench.mode == BenchMode.Cosmetic) bench.RunMode(BenchMode.Cosmetic);   // rebuild cosmetic
    }

    // --- Presets to compare headroom tradeoffs ---

    void SetHeavyPhysicsPreset(bool on)
    {
        if (!on) return;
        // higher quality constraints, keep counts modest
        UpdateSolverIter(12);
        solverIterSlider.SetValueWithoutNotify(12);
        UpdateSolverVelIter(6);
        solverVelIterSlider.SetValueWithoutNotify(6);
        UpdateDepen(5f);
        depenSlider.SetValueWithoutNotify(5f);
        UpdateSleep(0.005f);
        sleepSlider.SetValueWithoutNotify(0.005f);

        // shift work from rigidbodies to cosmetic
        UpdateRigidCount(Mathf.Min(bench.rigidCount, 150));
        rigidCountSlider.SetValueWithoutNotify(bench.rigidCount);
        UpdateCosmeticCount(Mathf.Max(bench.cosmeticCount, 4000));
        cosmeticCountSlider.SetValueWithoutNotify(bench.cosmeticCount);

        // untick the other toggle to keep them mutually exclusive
        if (manyCosmeticToggle) manyCosmeticToggle.isOn = false;
    }

    void SetManyCosmeticPreset(bool on)
    {
        if (!on) return;
        // lighter solver, more cosmetic debris driven by jobs
        UpdateSolverIter(6);
        solverIterSlider.SetValueWithoutNotify(6);
        UpdateSolverVelIter(2);
        solverVelIterSlider.SetValueWithoutNotify(2);
        UpdateDepen(3f);
        depenSlider.SetValueWithoutNotify(3f);
        UpdateSleep(0.01f);
        sleepSlider.SetValueWithoutNotify(0.01f);

        UpdateRigidCount(80);
        rigidCountSlider.SetValueWithoutNotify(80);
        UpdateCosmeticCount(8000);
        cosmeticCountSlider.SetValueWithoutNotify(8000);

        if (heavyPhysicsToggle) heavyPhysicsToggle.isOn = false;
    }
}
