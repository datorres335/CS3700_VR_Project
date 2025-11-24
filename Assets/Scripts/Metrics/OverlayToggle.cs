using UnityEngine;
using UnityEngine.InputSystem;

public class OverlayToggle : MonoBehaviour
{
    [Tooltip("Root object that has the Canvas/CanvasGroup (e.g., your HUD panel)")]
    public GameObject overlayRoot;

    // Optional
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
        if (canvasGroup)
        {
            bool visible = canvasGroup.alpha > 0.5f;
            canvasGroup.alpha = visible ? 0f : 1f;
            canvasGroup.interactable = !visible;
            canvasGroup.blocksRaycasts = !visible;
            return;
        }

        if (overlayCanvas)
        {
            overlayCanvas.enabled = !overlayCanvas.enabled;
            return;
        }

        if (overlayRoot)
            overlayRoot.SetActive(!overlayRoot.activeSelf);
    }
}
