using UnityEngine;

public class PlayerColliderDriver : MonoBehaviour
{
    public Transform xrCamera;               // assign your rig’s camera (HMD) transform
    public CharacterController controller;   // assign the CharacterController on this rig

    [Header("Capsule sizing")]
    public float minHeight = 1.2f;
    public float maxHeight = 2.2f;
    public float radius = 0.25f;
    public float headPadding = 0.15f;        // extra space above head inside capsule

    void Reset()
    {
        controller = GetComponent<CharacterController>();
        if (!xrCamera)
        {
            // try to find a Camera under this rig
            var cam = GetComponentInChildren<Camera>();
            if (cam) xrCamera = cam.transform;
        }
    }

    void LateUpdate()
    {
        if (!controller || !xrCamera) return;

        // Height = head local Y + padding (clamped)
        float h = Mathf.Clamp(xrCamera.localPosition.y + headPadding, minHeight, maxHeight);
        controller.height = h;
        controller.radius = radius;

        // Center the capsule under the head (keep Y at half height)
        Vector3 center = xrCamera.localPosition;
        center.y = h * 0.5f;
        controller.center = center;
    }
}
