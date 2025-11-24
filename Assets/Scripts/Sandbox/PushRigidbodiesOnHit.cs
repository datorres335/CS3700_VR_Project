using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PushRigidbodiesOnHit : MonoBehaviour
{
    public float pushPower = 2.0f; 
    public float maxMass = 50f; 
    public bool onlyHorizontal = true;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.rigidbody;
        if (!rb || rb.isKinematic) return;
        if (rb.mass > maxMass) return;

        Vector3 pushDir = hit.moveDirection;
        if (onlyHorizontal) pushDir.y = 0f;

        rb.AddForce(pushDir * pushPower, ForceMode.VelocityChange);
    }
}
