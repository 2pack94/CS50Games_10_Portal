using System.Collections.Generic;
using UnityEngine;

// Portables are objects that can move and that can be teleported through portals.

public class Portable : MonoBehaviour
{
    // Teleporting occurs when the teleportPoint moves behind the portal plane.
    // For the player the teleportPoint should be the camera, so it cannot get behind the portal plane.
    // The player camera's near clip plane should be set to a very small value, because it can be very near the
    // portal plane and it should not clip anything.
    // For all other objects this should be the center of mass.
    // If left empty, it gets set to the transform of this GameObject.
    // If setting it in the Inspector it shall be the transform of a child object.
    [Tooltip("Transform Point at which the object should be teleported at.")]
    public Transform teleportPoint;
    // Assume that this GameObject (with children) has exactly 1 collider.
    [System.NonSerialized]
    public Collider thisCollider;
    // If the GameObject has a Rigidbody it will be used to modify its velocity on teleport.
    [System.NonSerialized]
    public Rigidbody thisRigidbody;
    // If the GameObject is the Player.
    private PlayerController playerController;
    private CharacterController characterController;
    // List of all currently entered portals. An object can be inside the enter trigger of both portals.
    private HashSet<Portal> enteredPortals;
    // Colliders that thisCollider currently ignores collisions with while a portal is entered.
    // This is needed to go through the wall that the portal is attached to.
    // Each of the Collider HashSets shall be synchronized with blockingColliders for each portal.
    private Dictionary<Portal, HashSet<Collider>> ignoredColliders;
    // Minimal required offset from portal surface in portal forward-axis direction after teleporting.
    // This is needed to have a hysteresis for the teleportation.
    private float afterTeleportMinOffset = 0.01f;
    // When the exit portal is on the floor, the objects coming out of it need a minimal velocity to be able to
    // get out of it. The player needs a different velocity value because he uses a different gravity value/ calculation.
    private float minExitVelocityCharacter = 8f;
    private float minExitVelocityRigidbody = 4f;
    // After teleporting while in grounded state, the player is still grounded one frame after teleporting
    // and ground friction applies, so the minimum exit velocity should be higher to compensate for this.
    private float addExitVelocityFromGround = 0.9f;
    // This Component also enforces a maximum falling speed for rigidbodies. This is important, because
    // when an object goes too fast it can introduce bugs.
    // It's important to use Continuous Collision detection instead of Discrete. Otherwise fast objects may tunnel through
    // the wall. Set Collision Detection to "Continuous Dynamic" in the Rigidbody Component.
    private float terminalVelRigidbody = 40f;

    void Start()
    {
        thisRigidbody = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        if (characterController)
            thisCollider = (Collider)characterController;
        else
            thisCollider = GetComponentInChildren<Collider>();
        enteredPortals = new();
        ignoredColliders = new();
        if (!teleportPoint)
            teleportPoint = transform;
    }

    void Update()
    {
        // Limit Rigidbody falling velocity.
        if (thisRigidbody && thisRigidbody.velocity.y < -terminalVelRigidbody)
        {
            thisRigidbody.velocity = new Vector3(thisRigidbody.velocity.x, -terminalVelRigidbody, thisRigidbody.velocity.z);
        }

        // Update ignoredColliders if there is a change.
        foreach (var portal in enteredPortals)
        {
            if (portal.isBlockingCollidersUpToDate())
                continue;
            portal.UpdateBlockingColliders();
            if (ignoredColliders[portal].SetEquals(portal.blockingColliders))
                continue;
            foreach (var ignoredCollider in ignoredColliders[portal])
            {
                // If collider was removed
                if (!portal.blockingColliders.Contains(ignoredCollider))
                {
                    RevertIgnoredCollider(portal, ignoredCollider);
                }
            }
            foreach (var blockingCollider in portal.blockingColliders)
            {
                // If collider was added
                if (!ignoredColliders[portal].Contains(blockingCollider))
                {
                    ApplyIgnoredCollider(blockingCollider);
                }
            }
            ignoredColliders[portal] = portal.DeepcopyUpdatedBlockingColliders();
        }
    }

    // Perform the teleportation between portals.
    public void Teleport(Portal fromPortal, Portal toPortal)
    {
        // Temporarily disabling the CharacterController is required to be able to change its position.
        if (characterController)
            characterController.enabled = false;

        // Changing the position and rotation uses the same logic as in the PortalCamera Script.
        // The relative position/ rotation to fromPortal should be rotated by 180 degrees around the local y-axis and
        // then translated to toPortal.
        Vector3 relativePos = fromPortal.transform.InverseTransformPoint(teleportPoint.position);
        relativePos = Quaternion.Euler(0f, 180.0f, 0.0f) * relativePos;
        if (relativePos.z < afterTeleportMinOffset)
            relativePos.z = afterTeleportMinOffset;

        Vector3 toPortalAngles = toPortal.transform.rotation.eulerAngles;

        // When the player camera (teleportPoint) goes through a horizontal portal (on floor or on ceiling) and
        // comes out at a vertical portal, its relative y-position may need to be adjusted.
        // The local y-position should be put at a height so that the player touches the ground with his feet.
        if (playerController && teleportPoint != transform &&
            !Portal.IsVertical(fromPortal.transform.rotation.eulerAngles) && Portal.IsVertical(toPortalAngles))
        {
            relativePos.y = Mathf.Max(relativePos.y, -fromPortal.planeHeight / 2 + teleportPoint.localPosition.y);
        }

        // The teleportPoint Transform should be moved to the exit position, but it cannot be modified directly because
        // it may be a child object.
        // Instead first place the parent transform and then move it so teleportPoint will be at the right position.
        transform.position = toPortal.transform.TransformPoint(relativePos);
        if (teleportPoint != transform)
            transform.position -= teleportPoint.localPosition;

        // Calculate the exit rotation
        Quaternion currentRot;
        if (playerController)
            currentRot = playerController.cinemachineCameraTarget.transform.rotation;
        else
            currentRot = transform.rotation;
        Quaternion relativeRot = Quaternion.Inverse(fromPortal.transform.rotation) * currentRot;
        relativeRot = Quaternion.Euler(0f, 180.0f, 0.0f) * relativeRot;
        Quaternion newRot = toPortal.transform.rotation * relativeRot;
        if (playerController)
        {
            // The full rotation of the player is the rotation around the y-axis (this GameObject) and
            // the camera rotation around the x- and z-axis (child object).
            // newRot must be split up into these rotations and applied separately.
            // The player camera z-rotation will be automatically lerped to 0 in the PlayerController.
            Vector3 newRotAngles = newRot.eulerAngles;
            newRotAngles.x = Utils.DownConvertAngle(newRotAngles.x);
            newRotAngles.y = Utils.DownConvertAngle(newRotAngles.y);
            newRotAngles.z = Utils.DownConvertAngle(newRotAngles.z);
            transform.rotation = Quaternion.Euler(0, newRotAngles.y, 0);
            playerController.SetCamTargetRotation(newRotAngles.x, newRotAngles.z);
        }
        else
        {
            transform.rotation = newRot;
        }

        // Calculate the exit velocity
        Vector3 velocity = Vector3.zero;
        // For the player use externalVelocity and don't include movementVelocity. movementVelocity
        // is redirected immediately through the camera rotation when continuing to hold a movement button.
        // If movementVelocity had acceleration/ deceleration this would be different.
        if (playerController)
            velocity = playerController.externalVelocity;
        else if (thisRigidbody)
            velocity = thisRigidbody.velocity;

        // When the player is grounded he has a constant negative y-velocity.
        // This velocity should be set to 0 to not be translated to the other portal.
        if (playerController && playerController.isGrounded && velocity.y < 0)
        {
            velocity.y = 0;
        }

        // If the exit portal is on the floor the object needs a minimal exit velocity.
        if (Portal.PointsUpwards(toPortalAngles))
        {
            float minExitVelocity = 0;
            if (playerController)
            {
                minExitVelocity = minExitVelocityCharacter;
                if (playerController.isGrounded)
                    minExitVelocity += addExitVelocityFromGround;
            }
            else if (thisRigidbody)
            {
                minExitVelocity = minExitVelocityRigidbody;
            }

            // Project the velocity on the inverse forward vector of fromPortal (unit vector).
            // This magnitude shows how fast the object is going into the portal.
            float enterVelocityProj = Vector3.Dot(velocity, -fromPortal.transform.forward);
            // Cancel the velocity component that points away from the portal. This would mean the portal moves
            // towards the object.
            if (enterVelocityProj < 0)
            {
                velocity = Vector3.ProjectOnPlane(velocity, fromPortal.transform.forward);
                enterVelocityProj = 0;
            }
            if (minExitVelocity > 0 && enterVelocityProj < minExitVelocity)
            {
                velocity += -fromPortal.transform.forward * (minExitVelocity - enterVelocityProj);
            }
        }

        // This is the same logic as for transforming the position/ rotation just with a direction (velocity).
        Vector3 relativeVel = fromPortal.transform.InverseTransformDirection(velocity);
        relativeVel = Quaternion.Euler(0f, 180.0f, 0.0f) * relativeVel;
        velocity = toPortal.transform.TransformDirection(relativeVel);

        if (playerController)
            playerController.externalVelocity = velocity;
        else if (thisRigidbody)
            thisRigidbody.velocity = velocity;

        if (characterController)
            characterController.enabled = true;
    }

    // Ignore collisions with a collider.
    // Executing this multiple times with the same collider is not a problem.
    private void ApplyIgnoredCollider(Collider ignoreCollider)
    {
        Physics.IgnoreCollision(thisCollider, ignoreCollider);
    }

    // Enable Collisions with a collider again.
    // If the collider is also inside the list for the other portal, don't do anything.
    // This may happen if both portals are on the same wall and the object is in the enter trigger of both portals.
    private void RevertIgnoredCollider(Portal portal, Collider ignoredCollider)
    {
        if (!ignoredColliders[portal.otherPortal].Contains(ignoredCollider))
        {
            Physics.IgnoreCollision(thisCollider, ignoredCollider, false);
        }
    }

    // Called when the object is in the enter trigger of the portal. When entering a portal collisions with colliders
    // behind the portal surface will be ignored so the object can go through the portal.
    public void EnterPortal(Portal portal)
    {
        if (enteredPortals.Contains(portal))
            return;

        // Inside ignoredColliders may be this object's own collider, but this is not a problem.
        ignoredColliders[portal] = portal.DeepcopyUpdatedBlockingColliders();
        // Fully initialize the ignoredColliders dictionary. This can only be done here because otherPortal
        // could not be accessed earlier and the full dictionary is needed when exiting the portal again.
        if (!ignoredColliders.ContainsKey(portal.otherPortal))
            ignoredColliders[portal.otherPortal] = new HashSet<Collider>();
        foreach (var ignoredCollider in ignoredColliders[portal])
        {
            ApplyIgnoredCollider(ignoredCollider);
        }
        enteredPortals.Add(portal);
    }

    // Exit the portal and revert the ignored colliders.
    public void ExitPortal(Portal portal)
    {
        if (!enteredPortals.Contains(portal))
            return;

        enteredPortals.Remove(portal);        
        foreach (var ignoredCollider in ignoredColliders[portal])
        {
            RevertIgnoredCollider(portal, ignoredCollider);
        }
        ignoredColliders[portal].Clear();
    }
}
