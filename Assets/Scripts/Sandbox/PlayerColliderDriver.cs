using UnityEngine;

public class PlayerColliderDriver : MonoBehaviour
{
    public Transform xrCamera;   
    public CharacterController controller;  

    [Header("Capsule sizing")]
    public float minHeight = 1.2f;
    public float maxHeight = 2.2f;
    public float radius = 0.25f;
    public float headPadding = 0.15f;  

    void Reset()
    {
        controller = GetComponent<CharacterController>();
        if (!xrCamera)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) xrCamera = cam.transform;
        }
    }

    void LateUpdate()
    {
        if (!controller || !xrCamera) return;

        float h = Mathf.Clamp(xrCamera.localPosition.y + headPadding, minHeight, maxHeight);
        controller.height = h;
        controller.radius = radius;

        Vector3 center = xrCamera.localPosition;
        center.y = h * 0.5f;
        controller.center = center;
    }
}
