using System;
using System.Collections.Generic;
using UnityEngine;

// This is the character controller for the Player. The script was originally copied from
// Assets\StarterAssets\FirstPersonController\Scripts\FirstPersonController.cs
// It has been completely rewritten and additional features were added.

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputs))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("Move speed of the character in m/s")]
    [Min(0f)]
    public float moveSpeed = 4.0f;
    [Tooltip("Sprint speed of the character in m/s")]
    [Min(0f)]
    public float sprintSpeed = 6.0f;
    [Tooltip("Speed when the character is crouched in m/s")]
    [Min(0f)]
    public float crouchSpeed = 2.0f;
    [Tooltip("Rotation speed of the camera. This is the mouse sensitivity.")]
    [Min(0f)]
    public float rotationSpeed = 1.0f;
    [Tooltip("The velocity when jumping")]
    [Min(0f)]
    public float jumpVelocity = 7f;
    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float gravity = -15.0f;
    [Tooltip("Enable Flying")]
    public bool flyhackOn = false;

    // Time in seconds required to pass before being able to jump again.
    private float minJumpInterval = 0.1f;
    // Timer used to check minJumpInterval.
    private float jumpIntervalTimer = 0;
    // Buffer jump input for this amount of seconds.
    // Helpful when pressing jump slightly before the player touches the ground.
    private float jumpInputBufferTime = 0.1f;
    // Timer used to check jumpInputBufferTime.
    private float jumpInputBufferTimer = 0;
    // Amount of y-velocity applied when in grounded state. Helpful to stay on the ground
    // (produce ground collisions) and to go down angled surfaces more smoothly.
    private float groundedYVelocity = -1.5f;
    // Maximum velocity for falling. Gravity points in negative y-direction.
    private float terminalVelocity = 50.0f;
    // The total player velocity is movementVelocity + externalVelocity.
    // movementVelocity comes from move input and externalVelocity comes from falling etc.
    [System.NonSerialized]
    public Vector3 movementVelocity;
    [System.NonSerialized]
    public Vector3 externalVelocity;
    // externalVelocity.magnitude is stored here every frame, so it only need to be calculated once.
    [System.NonSerialized]
    public float externalVelocityMagnitude;
    // Deceleration (m/s^2) applied to externalVelocity when in Air or when grounded.
    private float airResistance = 0.5f;
    private float groundFriction = 50f;

    private CharacterController controller;
    private PlayerInputs input;
    // Variables that show the current state of the player.
    [System.NonSerialized]
    public bool isGrounded = true;
    [System.NonSerialized]
    public bool isCrouched = false;
    [System.NonSerialized]
    public bool isMoveInput = false;
    [System.NonSerialized]
    public bool isRunning = false;
    // If the player stands on a surface that is steeper than the character controller slope limit,
    // he will start sliding.
    [System.NonSerialized]
    public bool isSliding = false;
    // Called on different occasions
    public event EventHandler<Type> OnJump;
    public event EventHandler<float> OnGrounded;
    public event EventHandler<float> OnSliding;
    public event EventHandler<float> OnHitWall;

    // When the player is sliding, these parameters will be set.
    // They will be determined from the surface normal of the slope.
    private float slidingAngle = 0;
    private Vector3 slidingTangent;
    private Vector3 slidingNormal;
    private Vector3 slidingForward;

    // Collider that the CharacterController inherits from.
    [System.NonSerialized]
    public Collider thisCollider;
    // Capsule Collider that is attached to a child GameObject.
    private CapsuleCollider thisCapsuleCollider;
    // List to store all collisions on each frame.
    private List<ControllerColliderHit> collisions;
    // Store the collision info that caused the player to be grounded.
    [System.NonSerialized]
    public ControllerColliderHit lastGroundedCollision = null;
    // Store the collision info that caused the player to slide.
    [System.NonSerialized]
    public ControllerColliderHit lastSlidingCollision = null;
    // Store the collision info after the player hit a wall.
    [System.NonSerialized]
    public ControllerColliderHit lastHitWallCollision = null;
    // Restitution applied when a wall was hit. This is the bounciness.
    // Affects the velocity component normal (perpendicular) to the surface
    private float restitutionCollision = 0.1f;
    // Friction applied when a wall was hit.
    // Affects the velocity component tangential (parallel) to the surface.
    private float frictionCollision = 0.5f;

    // The main camera is the player camera.
    [System.NonSerialized]
    public Camera mainCamera;
    // The follow target set in the Cinemachine Virtual Camera that the player camera will follow.
    [System.NonSerialized]
    public GameObject cinemachineCameraTarget;
    private const string cinemachineTargetTag = "CinemachineTarget";
    // Cinemachine Camera target pitch (x-rotation) and roll (z-rotation)
    private float camTargetPitch = 0;
    private float camTargetRoll = 0;
    // Every time a camera roll rotation is set, it will automatically be lerped to 0 in this number of seconds.
    private float camTargetRollLerpDuration = 1;
    private float camTargetRollLerpElapsed = 0;
    // How far in degrees the camera can be moved up or down
    private float topCameraClamp = 89.0f;
    private float bottomCameraClamp = -89.0f;

    // Parameters required for crouching. All original values of parameters that the crouching modifies
    // need to be stored, so they can be restored after crouching.
    // The height of the character when crouching
    private float characterHeightCrouched = 1.2f;
    private float characterHeightOrig = 0;
    // Time in seconds to fully crouch
    private float crouchLerpDuration = 0.2f;
    private float crouchLerpTimer = 0;
    // Camera y position when crouched (calculated from characterHeightCrouched)
    private float camTargetYPosCrouched = 0;
    private float camTargetYPosOrig = 0;
    // Character controller y-center when crouched (calculated from characterHeightCrouched)
    private float controllerYCenterCrouched = 0;
    private float controllerYCenterOrig = 0;
    // Collider y-center when crouched (calculated from characterHeightCrouched)
    private float colliderYCenterCrouched = 0;
    private float colliderYCenterOrig = 0;
    // Character controller step offset when crouched (calculated from characterHeightCrouched)
    private float characterStepOffsetCrouched = 0;
    private float characterStepOffsetOrig = 0;
    // The colliders above the player head need to be checked to determine if the player can stand up again.
    private bool crouchCheckNeeded = false;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputs>();
        thisCollider = (Collider)controller;
        thisCapsuleCollider = GetComponentInChildren<CapsuleCollider>();
        cinemachineCameraTarget = GameObject.FindGameObjectWithTag(cinemachineTargetTag);
        mainCamera = Camera.main;

        externalVelocity = Vector3.zero;
        slidingTangent = Vector3.zero;
        slidingForward = Vector3.zero;
        slidingNormal = Vector3.zero;
        collisions = new();

        InitCrouchParameters();
    }

    private void InitCrouchParameters()
    {
        characterHeightOrig = controller.height;
        // Don't increase height
        characterHeightCrouched = Mathf.Min(characterHeightOrig, characterHeightCrouched);
        // A capsule with a radius of 0.5 can have a minimal height of 1
        characterHeightCrouched = Mathf.Max(controller.radius * 2, characterHeightCrouched);
        characterStepOffsetOrig = controller.stepOffset;
        characterStepOffsetCrouched = characterStepOffsetOrig * characterHeightCrouched / characterHeightOrig;
        camTargetYPosOrig = cinemachineCameraTarget.transform.localPosition.y;
        // Move the camera position down by the crouch height difference.
        // This assumes that the camera is in the top part of the character height.
        camTargetYPosCrouched = Mathf.Max(controller.radius,
            camTargetYPosOrig - (characterHeightOrig - characterHeightCrouched));
        controllerYCenterOrig = controller.center.y;
        controllerYCenterCrouched = controllerYCenterOrig - (characterHeightOrig - characterHeightCrouched) / 2;
        colliderYCenterOrig = thisCapsuleCollider.center.y;
        colliderYCenterCrouched = colliderYCenterOrig - (characterHeightOrig - characterHeightCrouched) / 2;
    }

    private void Update()
    {
        // If game is paused
        if (Time.deltaTime == 0)
            return;
        CollisionCheck();
        JumpAndGravity();
        Move();
    }

    private void LateUpdate()
    {
        if (Time.deltaTime == 0)
            return;
        UpdateCameraRotation();
    }

    // Use the collision list that was populated inside OnControllerColliderHit and
    // check if the player is grounded, is sliding or hit a wall.
    // Determine the state from the collision normal vector.
    private void CollisionCheck()
    {
        bool wasGrounded = isGrounded;
        bool wasSliding = isSliding;
        bool hitWall = false;
        bool noSlide = false;
        isGrounded = false;
        isSliding = false;
        if (flyhackOn)
            return;

        foreach (var collision in collisions)
        {
            // This angle is always between 0 and 180
            // An angle < 90 means the bottom part (feet) of the player capsule collided.
            // An angle of 90 means the the side of the player capsule collided.
            // An angle > 90 means the the top part (head) of the player capsule collided.
            float collisionNormalAngle = Vector3.Angle(Vector3.up, collision.normal);
            // Ground the player
            if (collisionNormalAngle <= controller.slopeLimit)
            {
                if (isGrounded)
                    continue;
                isGrounded = true;
                isSliding = false;
                lastGroundedCollision = collision;
                if (!wasGrounded)
                    OnGrounded?.Invoke(this, collisionNormalAngle);
            }
            // Check if player should slide down
            else if (collisionNormalAngle < 89.9)
            {
                if (isSliding || isGrounded || noSlide)
                    continue;

                // Additional to checking the collision normal, make a raycast downwards
                // to see if the player has flat ground under his feet. Don't start sliding in this case.
                // This fixes a bug where the player starts sliding every frame when walking
                // against a slope or a staircase resulting in movement jitter.
                // Start the raycast from inside the player collider downwards. This will not return the player collider.
                if (wasGrounded)
                {
                    RaycastHit hit;
                    float posOffset = 0.1f;
                    if (Physics.Raycast(transform.position + Vector3.up * posOffset, -Vector3.up,
                            out hit, controller.slopeLimit + posOffset, -1, QueryTriggerInteraction.Ignore) &&
                        !Physics.GetIgnoreCollision(thisCollider, hit.collider) &&
                        Vector3.Angle(Vector3.up, hit.normal) <= controller.slopeLimit)
                    {
                        noSlide = true;
                        continue;
                    }
                }

                isSliding = true;
                lastSlidingCollision = collision;
                // Vector that points along the slope surface. This will be the sliding direction.
                slidingTangent = -Vector3.ProjectOnPlane(Vector3.up, collision.normal).normalized;
                slidingForward = Vector3.ProjectOnPlane(slidingTangent, Vector3.up).normalized;
                slidingNormal = collision.normal;
                slidingAngle = collisionNormalAngle;
            }
            // Check if the side or head was hit.
            else
            {
                if (hitWall)
                    continue;
                // If wall moved towards player, while player was still or moving away from it.
                if (Vector3.Angle(-collision.normal, externalVelocity) > 89.9)
                    continue;
                hitWall = true;
                lastHitWallCollision = collision;
                OnHitWall?.Invoke(this, collisionNormalAngle);
            }
        }
        if (isSliding)
        {
            if (!wasSliding)
                OnSliding?.Invoke(this, slidingAngle);
        }
        if (hitWall)
        {
            // Bounce off of wall. Split velocity into normal and tangent component,
            // invert the normal component and apply friction and restitution.
            Vector3 tangentVel = Vector3.ProjectOnPlane(externalVelocity, lastHitWallCollision.normal);
            Vector3 normalVel = externalVelocity - tangentVel;
            externalVelocity = tangentVel * frictionCollision - normalVel * restitutionCollision;
        }
        collisions.Clear();
    }

    private void UpdateCameraRotation()
    {
        bool updateCamRot = false;
        // Lerp camTargetRoll (z-rotation) to 0 if it was set externally.
        if (camTargetRoll != 0)
        {
            updateCamRot = true;
            camTargetRoll = Mathf.Lerp(camTargetRoll, 0, camTargetRollLerpElapsed / camTargetRollLerpDuration);
            if (Mathf.Abs(camTargetRoll) < 0.01)
                camTargetRoll = 0;
            if (camTargetRoll != 0)
                camTargetRollLerpElapsed += Time.deltaTime;
            else
                camTargetRollLerpElapsed = 0;
        }
        // If there is an input.
        // Note that Vector2's == operator uses approximation so is not floating point error prone.
        if (input.look != Vector2.zero)
        {
            updateCamRot = true;
            // Don't multiply mouse input by Time.deltaTime, because the input vector already contains delta values.
            float deltaTimeMultiplier = input.IsMouseInput() ? 1.0f : Time.deltaTime;
            camTargetPitch += input.look.y * rotationSpeed * deltaTimeMultiplier;
            float rotateYAxis = input.look.x * rotationSpeed * deltaTimeMultiplier;
            // clamp pitch rotation
            camTargetPitch = Mathf.Clamp(camTargetPitch % 360, bottomCameraClamp, topCameraClamp);
            // rotate the player left and right
            transform.Rotate(Vector3.up * rotateYAxis);
        }
        if (updateCamRot)
        {
            cinemachineCameraTarget.transform.localRotation = Quaternion.Euler(
                camTargetPitch, 0.0f, camTargetRoll);
        }
    }

    // Handle crouching on crouch input and standing up when the crouch button is released.
    private void CheckCrouch()
    {
        // crouchLerpTimer counts up when crouching and counts down when releasing the crouch key.
        // This allows for smooth transitions when releasing the crouch key before fully crouched.
        isCrouched = false;
        if (input.crouch)
        {
            isCrouched = true;
            crouchCheckNeeded = true;
            // If fully crouched
            if (crouchLerpTimer >= crouchLerpDuration)
                return;            
            crouchLerpTimer = Mathf.Min(crouchLerpDuration, crouchLerpTimer + Time.deltaTime);

            float lerpStep = crouchLerpTimer / crouchLerpDuration;
            controller.stepOffset = characterStepOffsetCrouched;
            controller.height = Mathf.Lerp(characterHeightOrig, characterHeightCrouched, lerpStep);
            thisCapsuleCollider.height = Mathf.Lerp(characterHeightOrig, characterHeightCrouched, lerpStep);
            controller.center = new Vector3(
                controller.center.x,
                Mathf.Lerp(controllerYCenterOrig, controllerYCenterCrouched, lerpStep),
                controller.center.z);
            thisCapsuleCollider.center = new Vector3(
                thisCapsuleCollider.center.x,
                Mathf.Lerp(colliderYCenterOrig, colliderYCenterCrouched, lerpStep),
                thisCapsuleCollider.center.z);
            cinemachineCameraTarget.transform.localPosition = new Vector3(
                cinemachineCameraTarget.transform.localPosition.x,
                Mathf.Lerp(camTargetYPosOrig, camTargetYPosCrouched, lerpStep),
                cinemachineCameraTarget.transform.localPosition.z);
        }
        else if (crouchLerpTimer > 0)
        {
            // When crouched and trying to stand up, check if there is something above player head.
            // Use Physics.OverlapCapsule with the currently crouched player collider and with the
            // original full height collider. Compare the output of both queries and when there are
            // more collider found for the standing up collider, stay crouched.
            if (crouchCheckNeeded)
            {
                isCrouched = true;
                
                // Don't check layers that this gameObject ignores collisions with.
                int overlapCapsuleLayermask = 0;
                int max_num_layers = 32;
                for (int layer_i = 0; layer_i < max_num_layers; layer_i++)
                {
                    if (Physics.GetIgnoreLayerCollision(layer_i, gameObject.layer))
                        overlapCapsuleLayermask |= 1 << layer_i;
                }
                // NAND bit-operation
                overlapCapsuleLayermask = ~(-1 & overlapCapsuleLayermask);
                // Assume that player GameObject is not scaled or rotated (except y-rotation)
                Collider[] crouchedColliders = Physics.OverlapCapsule(
                    thisCapsuleCollider.transform.position + thisCapsuleCollider.center -
                        new Vector3(0, thisCapsuleCollider.height / 2 - thisCapsuleCollider.radius, 0),
                    thisCapsuleCollider.transform.position + thisCapsuleCollider.center +
                        new Vector3(0, thisCapsuleCollider.height / 2 - thisCapsuleCollider.radius, 0),
                    thisCapsuleCollider.radius, overlapCapsuleLayermask, QueryTriggerInteraction.Ignore);
                Collider[] standingColliders = Physics.OverlapCapsule(
                    thisCapsuleCollider.transform.position +
                        new Vector3(thisCapsuleCollider.center.x, colliderYCenterOrig, thisCapsuleCollider.center.z) -
                        new Vector3(0, characterHeightOrig / 2 - thisCapsuleCollider.radius, 0),
                    thisCapsuleCollider.transform.position +
                        new Vector3(thisCapsuleCollider.center.x, colliderYCenterOrig, thisCapsuleCollider.center.z) +
                        new Vector3(0, characterHeightOrig / 2 - thisCapsuleCollider.radius, 0),
                    thisCapsuleCollider.radius, overlapCapsuleLayermask, QueryTriggerInteraction.Ignore);

                int numRelevantCollidersCrouched = 0;
                int numRelevantCollidersStanding = 0;
                foreach (var crouchedCollider in crouchedColliders)
                {
                    if (crouchedCollider != thisCapsuleCollider && !Physics.GetIgnoreCollision(thisCollider, crouchedCollider))
                    {
                        numRelevantCollidersCrouched++;
                    }
                }
                foreach (var standingCollider in standingColliders)
                {
                    if (standingCollider != thisCapsuleCollider && !Physics.GetIgnoreCollision(thisCollider, standingCollider))
                    {
                        numRelevantCollidersStanding++;
                    }
                }
                if (numRelevantCollidersStanding <= numRelevantCollidersCrouched)
                {
                    crouchCheckNeeded = false;
                }
            }
            if (crouchCheckNeeded)
                return;
            isCrouched = false;
            crouchLerpTimer = Mathf.Max(0, crouchLerpTimer - Time.deltaTime);

            float lerpStep = (crouchLerpDuration - crouchLerpTimer) / crouchLerpDuration;
            controller.stepOffset = characterStepOffsetOrig;
            controller.height = Mathf.Lerp(characterHeightCrouched, characterHeightOrig, lerpStep);
            thisCapsuleCollider.height = Mathf.Lerp(characterHeightCrouched, characterHeightOrig, lerpStep);
            controller.center = new Vector3(
                controller.center.x,
                Mathf.Lerp(controllerYCenterCrouched, controllerYCenterOrig, lerpStep),
                controller.center.z);
            thisCapsuleCollider.center = new Vector3(
                thisCapsuleCollider.center.x,
                Mathf.Lerp(colliderYCenterCrouched, colliderYCenterOrig, lerpStep),
                thisCapsuleCollider.center.z);
            cinemachineCameraTarget.transform.localPosition = new Vector3(
                cinemachineCameraTarget.transform.localPosition.x,
                Mathf.Lerp(camTargetYPosCrouched, camTargetYPosOrig, lerpStep),
                cinemachineCameraTarget.transform.localPosition.z);
        }
    }

    private void Move()
    {
        CheckCrouch();

        float movementTargetSpeed = 0;
        isMoveInput = input.move != Vector2.zero;
        isRunning = false;
        float inputMagnitude = input.analogMovement ? input.move.magnitude : 1f;

        // Set target speed based on move speed, sprint speed and if sprint is pressed
        if (isMoveInput)
        {
            if (isCrouched)
            {
                movementTargetSpeed = crouchSpeed;
            }
            else if (input.sprint)
            {
                isRunning = true;
                movementTargetSpeed = sprintSpeed;
            }
            else
            {
                movementTargetSpeed = moveSpeed;
            }
            movementTargetSpeed *= inputMagnitude;
        }

        Vector3 inputDirection = Vector3.zero;
        if (flyhackOn)
        {
            inputDirection = cinemachineCameraTarget.transform.right * input.move.x +
                cinemachineCameraTarget.transform.forward * input.move.y;
        }
        else
        {
            inputDirection = transform.right * input.move.x + transform.forward * input.move.y;
        }
        movementVelocity = inputDirection.normalized * movementTargetSpeed;
        // If sliding, allow the player to only move forward, left and right.
        // Otherwise the player could build up sliding speed over time while staying in place
        // when moving against the surface.
        if (isSliding && Vector3.Angle(slidingForward, movementVelocity) > 90)
        {
            movementVelocity = Vector3.ProjectOnPlane(movementVelocity, slidingForward);
        }

        // Don't make horizontal velocity faster or slower with input, just allow to steer to the sides.
        Vector3 horExternalVelocity = new Vector3(externalVelocity.x, 0, externalVelocity.z);
        if (horExternalVelocity.sqrMagnitude > movementVelocity.sqrMagnitude)
        {
            movementVelocity = Vector3.ProjectOnPlane(movementVelocity, horExternalVelocity);
        }

        if (flyhackOn)
        {
            externalVelocity = Vector3.zero;
        }

        // Move the player
        controller.Move((movementVelocity + externalVelocity) * Time.deltaTime);
    }

    private void JumpAndGravity()
    {
        float velDamping = airResistance;
        float curGravity = flyhackOn ? 0 : gravity;

        // Apply gravity. If sliding, apply gravity along the sliding surface tangent, weighted by the sliding angle.
        if (isSliding)
        {
            float slidingAngleNorm = slidingAngle / 90;
            // Limit sliding velocity.
            // Use the dot product between velocity vector and tangent (unit) vector to get a projection of the
            // velocity on the tangent vector.
            bool isUnderTerminal = Vector3.Dot(externalVelocity, slidingTangent) < terminalVelocity * slidingAngleNorm;
            
            if (isUnderTerminal)
                externalVelocity += Math.Abs(curGravity) * slidingTangent * slidingAngleNorm * Time.deltaTime;
            // Cancel any velocity towards the sliding normal vector.
            if (Vector3.Angle(slidingNormal, externalVelocity) > 90)
                externalVelocity = Vector3.ProjectOnPlane(externalVelocity, slidingNormal);
            // Add a small amount of velocity towards the surface normal so the player sticks to the surface
            // (this amount gets canceled again in the next frame at ProjectOnPlane).
            externalVelocity += -0.5f * slidingNormal * Time.deltaTime;
            // If the velocity points down the slope.
            if (Vector3.Angle(slidingTangent, externalVelocity) < 89.9)
                velDamping = airResistance;
            else
                velDamping = groundFriction;
        }
        else
        {
            externalVelocity.y += curGravity * Time.deltaTime;
        }
        // Limit falling velocity
        externalVelocity.y = Mathf.Max(externalVelocity.y, -terminalVelocity);

        jumpIntervalTimer = Mathf.Max(0, jumpIntervalTimer - Time.deltaTime);

        // Handle jumping
        // Don't damp y-velocity when grounded, because it gets set to a constant.
        bool dampYVel = true;
        if (isGrounded)
        {
            velDamping = groundFriction;

            if (input.jump && jumpIntervalTimer <= 0)
            {
                // Reset the jump interval timer
                jumpIntervalTimer = minJumpInterval;
                input.jump = false;
                jumpInputBufferTimer = 0;
                velDamping = airResistance;
                OnJump?.Invoke(this, null);

                externalVelocity.y = jumpVelocity;
            }
            else if (externalVelocity.y < 0)
            {
                dampYVel = false;
                externalVelocity.y = groundedYVelocity;
            }
        }
        // Buffer jump input if pressed jump while in air.
        else if (input.jump)
        {
            jumpInputBufferTimer += Time.deltaTime;
            if (jumpInputBufferTimer > jumpInputBufferTime)
            {
                input.jump = false;
                jumpInputBufferTimer = 0;
            }
        }

        // Velocity Damping
        // Normally ground and air friction have different formulas and air resistance is proportional to v^2.
        // An easier formula is used here however.
        float externalVelocityYBefore = 0;
        if (!dampYVel)
        {
            externalVelocityYBefore = externalVelocity.y;
            externalVelocity.y = 0;
        }
        externalVelocityMagnitude = Math.Max(0, externalVelocity.magnitude - velDamping * Time.deltaTime);
        externalVelocity = externalVelocity.normalized * externalVelocityMagnitude;
        if (!dampYVel)
        {
            externalVelocity.y = externalVelocityYBefore;
        }
    }

    // Externally set the camera rotation.
    public void SetCamTargetRotation(float pitchAngle, float rollAngle = 0)
    {
        camTargetPitch = pitchAngle;
        camTargetRoll = rollAngle;
        // An absolute pitch angle > 90 means that the player needs to turn around by rotating 180 degrees
        // on the y-axis. After that the camera forward vector needs to be put back in place.
        if (Mathf.Abs(camTargetPitch) > 90)
        {
            transform.rotation = Quaternion.Euler(0f, 180.0f, 0.0f) * transform.rotation;
            // Mirror the camera forward vector on the player forward plane:
            // 1. Multiply x-rotation by -1 (equivalent of mirroring camera forward on player up-plane)
            // 2. Add 180 to x-rotation (equivalent of inverting camera forward)
            camTargetPitch *= -1;
            camTargetPitch += 180;
            if (camTargetPitch > 180)
                camTargetPitch -= 360;
        }
        camTargetPitch = Mathf.Clamp(camTargetPitch % 360, bottomCameraClamp, topCameraClamp);

        cinemachineCameraTarget.transform.localRotation = Quaternion.Euler(camTargetPitch, 0.0f, camTargetRoll);
        // Update also the main Camera rotation, otherwise it takes 1 frame until it is updated.
        mainCamera.transform.localRotation = cinemachineCameraTarget.transform.localRotation;
    }

    // Add collision information to a list. Called every frame once for every collision.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        collisions.Add(hit);
    }
}
