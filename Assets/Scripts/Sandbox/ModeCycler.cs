using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BenchManager))]
public class ModeCycler : MonoBehaviour
{
    BenchManager bench;
    InputAction toggle;

    void Awake()
    {
        bench = GetComponent<BenchManager>();
        toggle = new InputAction(type: InputActionType.Button, binding: "<XRController>{LeftHand}/secondaryButton");
    }
    void OnEnable() { toggle.performed += OnPressed; toggle.Enable(); }
    void OnDisable() { toggle.performed -= OnPressed; toggle.Disable(); }

    void OnPressed(InputAction.CallbackContext _)
    {
        var next = bench.mode switch
        {
            BenchMode.RigidOnly => BenchMode.Fractured,
            BenchMode.Fractured => BenchMode.Cosmetic,
            _ => BenchMode.RigidOnly
        };
        bench.RunMode(next);
        Debug.Log($"Bench mode => {next}");
    }
}
