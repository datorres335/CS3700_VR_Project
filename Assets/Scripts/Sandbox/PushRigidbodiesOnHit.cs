using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PushRigidbodiesOnHit : MonoBehaviour
{
    public float pushPower = 2.0f;      // tune: 1–5 for light objects
    public float maxMass = 50f;       // don't try to push super heavy bodies
    public bool onlyHorizontal = true; // don't push upward

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.rigidbody;
        if (!rb || rb.isKinematic) return;
        if (rb.mass > maxMass) return;

        // Direction we moved into the object
        Vector3 pushDir = hit.moveDirection;
        if (onlyHorizontal) pushDir.y = 0f;

        rb.AddForce(pushDir * pushPower, ForceMode.VelocityChange);
    }
}
