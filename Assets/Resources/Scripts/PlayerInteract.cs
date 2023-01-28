using UnityEngine;
using UnityEngine.InputSystem;

// Try to interact with objects when pressing the interact key.
// This is currently only used to pick up Portable objects.
// A raycast in the camera forward direction will be casted to see if the player points at something interactable.

[RequireComponent(typeof(PlayerController))]
public class PlayerInteract : MonoBehaviour
{
    [Tooltip("Max distance of the interact raycast.")]
    [Min(0f)]
    public float interactDistance = 2.5f;
    [Tooltip("Maximum distance the player can hold an object before its dropped.")]
    [Min(0f)]
    public float objectHoldMaxDistance = 3f;
    [Tooltip("Maximum velocity of an object that the player did hold and then released.")]
    [Min(0f)]
    public float objectMaxThrowingVelocity = 5f;
    [Tooltip("Target transform point that carried objects will follow.")]
    public Transform objectHoldTarget;
    private PlayerController playerController;
    private PortalGun portalGun;
    // Portable object that is currently hold.
    private Portable holdObject = null;
    private int raycastLayermask = -1;
    // Cooldown after picking up an object to prevent the player to pick up objects too often.
    private float objectHoldCooldown = 1f;
    private float objectHoldCooldownTimer = 0;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        portalGun = GetComponentInChildren<PortalGun>();

        // Exclude the following layers from the interact raycast.
        raycastLayermask = 0;
        raycastLayermask |= 1 << LayerMask.NameToLayer("Ignore Raycast");
        raycastLayermask |= 1 << LayerMask.NameToLayer("Player");
        // NAND bit operation
        raycastLayermask = ~(-1 & raycastLayermask);
    }

    // Check if the player can hold this object.
    // The player should not be able to pick up an object that he is standing on. This, together with
    // the pickup cooldown fixes infinite object jumping. The player can still jump from the object,
    // grab it in the air and jump again from it in the air one time.
    bool CanHoldObject(Portable portable)
    {
        if (!portable.thisRigidbody ||
            (playerController.isGrounded &&
            playerController.lastGroundedCollision.collider == portable.thisCollider))
        {
            return false;
        }
        return true;
    }

    // Start holding an object. Return true if the object was picked up.
    bool PickupObject(Portable portable)
    {
        if (holdObject || objectHoldCooldownTimer > 0 || !CanHoldObject(portable))
            return false;
        holdObject = portable;
        // Turning off gravity is not necessary, but its done anyways.
        holdObject.thisRigidbody.useGravity = false;
        portalGun.canShoot = false;
        objectHoldCooldownTimer = objectHoldCooldown;
        return true;
    }

    // Drop the carried object
    void ReleaseHoldObject()
    {
        if (!holdObject)
            return;
        holdObject.thisRigidbody.useGravity = true;
        holdObject.thisRigidbody.velocity =
            Vector3.ClampMagnitude(holdObject.thisRigidbody.velocity, objectMaxThrowingVelocity);
        holdObject = null;
        portalGun.canShoot = true;
    }

    // Update position and rotation of the carried Rigidbody.
    // To avoid camera jittering, turn on Rigidbody.interpolation:
    // https://docs.unity3d.com/ScriptReference/Rigidbody-interpolation.html
    // Instead of modifying the position and rotation directly, a force is applied
    // to reach the target position and a torque is applied to reach the target rotation.
    void UpdateHoldObject()
    {
        float dt = Time.fixedDeltaTime;
        objectHoldCooldownTimer = Mathf.Max(0, objectHoldCooldownTimer - dt);

        if (!holdObject)
            return;

        if (!CanHoldObject(holdObject))
        {
            ReleaseHoldObject();
            return;
        }

        // Apply a Force to reach the target position.
        // This is the same solution as in the project 6a_Box2D_demonstration

        // Maximum velocity the object should have when following the target point in m/s.
        float targetVelMag = 12;
        // Divide the force to apply to the object, so it doesn't reach the target velocity immediately.
        // If set too high, acceleration amd deceleration will be low and the overshooting will be high.
        // If set too low, the applied force will be very high which could introduce problems with the physics.
        float forceDiv = 3;
        // The future position of the object is predicted for this number of time steps under the assumption
        // of constant velocity. If the object will reach its target in the foresight, reduce targetVel.
        // The higher the number, the earlier the object will decelerate before reaching the target point.
        // Low values will produce more oscillation around the target point before converging.
        float numTimeStepsForesight = 4f;
        Vector3 objectToTarget = objectHoldTarget.position - holdObject.transform.position;
        float targetDist = objectToTarget.magnitude;
        if (targetDist > objectHoldMaxDistance)
        {
            ReleaseHoldObject();
            return;
        }
        Vector3 targetVel = objectToTarget.normalized * targetVelMag;
        Vector3 nextPos = targetVel * numTimeStepsForesight * dt;
        float nextPosDist = nextPos.magnitude;
        if (targetDist < nextPosDist && 0 != nextPosDist)
        {
            targetVel *= targetDist / nextPosDist;
        }
        Vector3 deltaVel = targetVel - holdObject.thisRigidbody.velocity;
        Vector3 targetForce = holdObject.thisRigidbody.mass * deltaVel / dt;
        targetForce /= forceDiv;
        holdObject.thisRigidbody.AddForce(targetForce, ForceMode.Force);

        // Apply torque to reach the target rotation.
        // This code was copied from: https://digitalopus.ca/site/pd-controllers/

        // The target rotation should be the player rotation.
        Quaternion desiredRotation = transform.rotation;
        float frequency = 7;
        float damping = 0.8f;
        float kp = (6f*frequency)*(6f*frequency)* 0.25f;
        float kd = 4.5f*frequency*damping;
        // float g = 1 / (1 + kd * dt + kp * dt * dt);
        // float ksg = kp * g;
        // float kdg = (kd + kp * dt) * g;
        Vector3 rotAxis;
        float rotAngle;
        Quaternion q = desiredRotation * Quaternion.Inverse(holdObject.transform.rotation);
        // Q can be the-long-rotation-around-the-sphere eg. 350 degrees
        // We want the equivalent short rotation eg. -10 degrees
        // Check if rotation is greater than 180 degrees == q.w is negative
        if (q.w < 0)
        {
            q.x = -q.x;
            q.y = -q.y;
            q.z = -q.z;
            q.w = -q.w;
        }
        q.ToAngleAxis(out rotAngle, out rotAxis);
        rotAxis.Normalize();
        rotAxis *= Mathf.Deg2Rad;
        Vector3 pidv = kp * rotAxis * rotAngle - kd * holdObject.thisRigidbody.angularVelocity;
        Quaternion rotInertia2World = holdObject.thisRigidbody.inertiaTensorRotation * holdObject.transform.rotation;
        pidv = Quaternion.Inverse(rotInertia2World) * pidv;
        pidv.Scale(holdObject.thisRigidbody.inertiaTensor);
        pidv = rotInertia2World * pidv;
        holdObject.thisRigidbody.AddTorque(pidv);
    }

    // Its better to use FixedUpdate() instead of Update() for physics calculations.
    void FixedUpdate()
    {
        if (Time.deltaTime == 0)
            return;
        UpdateHoldObject();
    }

    // Called when pressed the interact key.
    public void OnInteract(InputValue _)
    {
        if (holdObject)
        {
            ReleaseHoldObject();
            return;
        }
        // Cast the interact raycast and get closest hit.
        bool didInteract = false;
        RaycastHit hit;
        if (Physics.Raycast(playerController.mainCamera.transform.position, playerController.mainCamera.transform.forward,
            out hit, interactDistance, raycastLayermask, QueryTriggerInteraction.Ignore))
        {
            // When an object is halfway through a portal on a thin wall its collider sticks out on the other side.
            // Use the GetIgnoreCollision check so the player cannot pick that object up from behind the wall
            // (from that point he ignores collisions with that object).
            Portable portable = hit.collider.gameObject.GetComponentInParent<Portable>();
            if (portable && !Physics.GetIgnoreCollision(playerController.thisCollider, hit.collider) &&
                PickupObject(portable))
            {
                didInteract = true;
            }
        }
        if (!didInteract)
        {
            portalGun.PlayRejectAnimation();
        }
    }
}
