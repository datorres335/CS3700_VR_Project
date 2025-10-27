using UnityEngine;
using UnityEngine.InputSystem;

public class OverlayToggle : MonoBehaviour
{
    [Tooltip("Root object that has the Canvas/CanvasGroup (e.g., your HUD panel)")]
    public GameObject overlayRoot;

    // Optional: drag these if you want; otherwise we auto-find them under overlayRoot.
    public Canvas overlayCanvas;
    public CanvasGroup canvasGroup;

    private InputAction toggle;

    void Awake()
    {
        toggle = new InputAction(type: InputActionType.Button,
                                 binding: "<XRController>{LeftHand}/primaryButton");
        //toggle.AddBinding("<XRController>{RightHand}/primaryButton");

        if (!overlayCanvas && overlayRoot)
            overlayCanvas = overlayRoot.GetComponentInChildren<Canvas>(true);

        if (!canvasGroup && overlayRoot)
            canvasGroup = overlayRoot.GetComponentInChildren<CanvasGroup>(true);
    }

    void OnEnable()
    {
        toggle.performed += OnPressed;
        toggle.Enable();
    }

    void OnDisable()
    {
        toggle.performed -= OnPressed;
        toggle.Disable();
    }

    void OnPressed(InputAction.CallbackContext _)
    {
        // Prefer CanvasGroup if available (also handles UI interactivity)
        if (canvasGroup)
        {
            bool visible = canvasGroup.alpha > 0.5f;
            canvasGroup.alpha = visible ? 0f : 1f;
            canvasGroup.interactable = !visible;
            canvasGroup.blocksRaycasts = !visible;
            return;
        }

        // Fallback: just toggle the Canvas component
        if (overlayCanvas)
        {
            overlayCanvas.enabled = !overlayCanvas.enabled;
            return;
        }

        // Last resort: toggle the whole object (only safe if this script lives elsewhere!)
        if (overlayRoot)
            overlayRoot.SetActive(!overlayRoot.activeSelf);
    }
}
