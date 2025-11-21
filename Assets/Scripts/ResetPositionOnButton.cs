using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Resets object to its initial position/rotation when a VR controller button is pressed.
/// Useful for resetting physics objects to their starting state.
/// </summary>
public class ResetPositionOnButton : MonoBehaviour
{
    public enum Hand { Left, Right }
    public enum Button { Primary, Secondary, Grip, Trigger }

    [Header("Input Configuration")]
    [Tooltip("Which hand controller to use")]
    public Hand controllerHand = Hand.Right;

    [Tooltip("Which button to use")]
    public Button resetButton = Button.Primary;

    [Header("Reset Options")]
    [Tooltip("Require holding button vs single press")]
    public bool requireHold = false;

    [Tooltip("How long to hold button before reset (if requireHold is true)")]
    public float holdDuration = 0.5f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Initial state storage
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    // Components
    private Rigidbody rb;
    private InputAction resetAction;

    // Hold tracking
    private float holdTimer = 0f;
    private bool isHolding = false;

    void Awake()
    {
        // Get Rigidbody if present
        rb = GetComponent<Rigidbody>();

        // Build input binding string
        string handString = controllerHand == Hand.Left ? "LeftHand" : "RightHand";
        string buttonString = resetButton switch
        {
            Button.Primary => "primaryButton",
            Button.Secondary => "secondaryButton",
            Button.Grip => "gripButton",
            Button.Trigger => "triggerButton",
            _ => "primaryButton"
        };

        string binding = $"<XRController>{{{handString}}}/{buttonString}";

        // Create input action
        resetAction = new InputAction(type: InputActionType.Button, binding: binding);
    }

    void Start()
    {
        // Store initial transform state
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (showDebugLogs)
        {
            Debug.Log($"[ResetPosition] {gameObject.name} initial state stored: Pos={initialPosition}, Rot={initialRotation.eulerAngles}");
        }
    }

    void OnEnable()
    {
        resetAction.performed += OnButtonPressed;
        resetAction.canceled += OnButtonReleased;
        resetAction.Enable();
    }

    void OnDisable()
    {
        resetAction.performed -= OnButtonPressed;
        resetAction.canceled -= OnButtonReleased;
        resetAction.Disable();
    }

    void Update()
    {
        // Handle hold duration
        if (requireHold && isHolding)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdDuration)
            {
                ResetToInitialState();
                isHolding = false; // Only reset once per hold
            }
        }
    }

    void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (requireHold)
        {
            isHolding = true;
            holdTimer = 0f;
        }
        else
        {
            ResetToInitialState();
        }
    }

    void OnButtonReleased(InputAction.CallbackContext context)
    {
        if (requireHold)
        {
            isHolding = false;
            holdTimer = 0f;
        }
    }

    void ResetToInitialState()
    {
        // Reset transform
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;

        // Reset physics if Rigidbody exists
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); // Put to sleep to ensure clean reset
            rb.WakeUp(); // Wake up to apply new state
        }

        if (showDebugLogs)
        {
            string buttonName = $"{controllerHand} {resetButton}";
            Debug.Log($"[ResetPosition] {gameObject.name} reset to initial state via {buttonName} button");
        }
    }

    /// <summary>
    /// Manually reset the object (can be called from other scripts)
    /// </summary>
    public void ManualReset()
    {
        ResetToInitialState();
    }

    /// <summary>
    /// Update the stored initial position to current position
    /// </summary>
    public void UpdateInitialPosition()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (showDebugLogs)
        {
            Debug.Log($"[ResetPosition] {gameObject.name} initial state updated to current position");
        }
    }
}
