using System;
using UnityEngine;

// This component changes the animation of the player based on his state.
// Its also used for playing sound effects.

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Tooltip("Audio that is played periodically when moving.")]
    public AudioClip footstepSfx;
    [Tooltip("Audio that is played in a loop when the player velocity is high enough.")]
    public AudioClip headwindSfx;
    [Tooltip("Audio that is played on jump.")]
    public AudioClip jumpSfx;
    [Tooltip("Audio that is played when landing on the ground.")]
    public AudioClip landSfx;
    [Tooltip("Audio that is played when hitting a wall with high enough velocity (randomly selected).")]
    public AudioClip[] bonkSfx;
    // Volumes and Periods for playing footstepSfx on the different player movement speeds.
    private float footstepSfxTimer = 0;
    private float footstepSfxCrouchingVolume = 1.5f;
    private float footstepSfxWalkingVolume = 2f;
    private float footstepSfxRunningVolume = 2.5f;
    private float footstepSfxPeriodCrouching = 0.6f;
    private float footstepSfxPeriodWalking = 0.5f;
    private float footstepSfxPeriodRunning = 0.4f;
    private float jumpSfxVolume = 2.8f;
    private float landSfxVolume = 2f;
    private float bonkSfxVolume = 1f;
    // Minimum player velocity at which headwindSfx is played.
    private float headwindSfxVelocityThreshold = 10f;
    // Multiplied by the player velocity to get the current headwindSfx volume.
    private float headwindSfxVolumeScale = 0.05f;
    private float headwindSfxMaxVolume = 1.5f;
    // Minimum player velocity at which landSfx and bonkSfx is played
    private float collisionSfxVelocityThreshold = 1f;
    // Time in seconds not grounded before the player is considered in-air (sliding is handled the same as in-air).
    // This protects from triggering the in-air animation or the landSfx when only dropping down a little.
    private float animMinInAirTime = 0.1f;
    private float inAirTimer = 0;
    private bool fullyInAir = false;
    private AnimationSwitcher animationSwitcher;
    private Animator animator;
    private PlayerController playerController;
    // AudioSource for all sound effects that are played one-shot.
    private AudioSource audioSourceOneShot;
    // An extra AudioSource is needed to play headwindSfx, so other sound effects
    // are not stopped when stopping headwindSfx.
    private AudioSource audioSourceLooping;
    // Animation hashes to switch Animation states.
    private int animationHashIdle;
    private int animationHashWalking;
    private int animationHashRunning;
    private int animationHashInAir;
    private int animationHashCrouchIdle;
    private int animationHashCrouchWalking;
    // Time in seconds used for cross fading between animations.
    private float defaultTransitionTime = 0.25f;

    void Start()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        AudioSource[] audioSources = GetComponents<AudioSource>();
        // It does not matter which one in the list is used as which.
        audioSourceOneShot = audioSources[0];
        audioSourceLooping = audioSources[1];
        animationSwitcher = new AnimationSwitcher(animator);

        animationHashIdle = Utils.GetAnimationHash(animator, "Idle");
        animationHashWalking = Utils.GetAnimationHash(animator, "Walking");
        animationHashRunning = Utils.GetAnimationHash(animator, "Running");
        animationHashInAir = Utils.GetAnimationHash(animator, "Jump Loop");
        animationHashCrouchIdle = Utils.GetAnimationHash(animator, "Idle Crouching");
        animationHashCrouchWalking = Utils.GetAnimationHash(animator, "Walk Crouching Forward");

        playerController.OnJump += PlayerJumped;
        playerController.OnGrounded += PlayerGrounded;
        playerController.OnHitWall += PlayerHitWall;

        audioSourceLooping.loop = true;
        audioSourceLooping.clip = headwindSfx;
    }

    void Update()
    {
        if (Time.deltaTime == 0)
            return;

        float footstepSfxPeriod = Mathf.Infinity;
        float footstepSfxVolume = 0;

        int newStateHash = 0;
        if (playerController.isGrounded)
        {
            fullyInAir = false;
            inAirTimer = 0;
            if (audioSourceLooping.isPlaying)
                audioSourceLooping.Stop();

            if (playerController.isCrouched)
            {
                if (playerController.isMoveInput)
                {
                    footstepSfxPeriod = footstepSfxPeriodCrouching;
                    footstepSfxVolume = footstepSfxCrouchingVolume;
                    newStateHash = animationHashCrouchWalking;
                }
                else
                {
                    newStateHash = animationHashCrouchIdle;
                }
            }
            else
            {
                if (playerController.isRunning)
                {
                    footstepSfxPeriod = footstepSfxPeriodRunning;
                    footstepSfxVolume = footstepSfxRunningVolume;
                    newStateHash = animationHashRunning;
                }
                else if (playerController.isMoveInput)
                {
                    footstepSfxPeriod = footstepSfxPeriodWalking;
                    footstepSfxVolume = footstepSfxWalkingVolume;
                    newStateHash = animationHashWalking;
                }
                else
                {
                    newStateHash = animationHashIdle;
                }
            }
        }
        else
        {
            inAirTimer += Time.deltaTime;
            if (inAirTimer > animMinInAirTime)
            {
                fullyInAir = true;
                footstepSfxTimer = 0;
                newStateHash = animationHashInAir;
                if (playerController.externalVelocityMagnitude > headwindSfxVelocityThreshold)
                {
                    audioSourceLooping.volume = Mathf.Min(headwindSfxMaxVolume,
                        playerController.externalVelocityMagnitude * headwindSfxVolumeScale);
                    if (!audioSourceLooping.isPlaying)
                        audioSourceLooping.Play();
                }
                else if (audioSourceLooping.isPlaying)
                {
                    audioSourceLooping.Stop();
                }
            }
        }

        if (!fullyInAir && playerController.isMoveInput)
        {
            footstepSfxTimer += Time.deltaTime;

            if (footstepSfxTimer > footstepSfxPeriod)
            {
                footstepSfxTimer = 0;
                audioSourceOneShot.PlayOneShot(footstepSfx, footstepSfxVolume);
            }
        }

        animationSwitcher.ChangeAnimation(newStateHash, defaultTransitionTime);
    }

    void PlayerJumped(object sender, Type _)
    {
        audioSourceOneShot.PlayOneShot(jumpSfx, jumpSfxVolume);
    }

    void PlayerGrounded(object sender, float collisionNormalAngle)
    {
        // Calculate the relative velocity of the player towards the surface to compare against
        // the sound effect velocity threshold. The velocity of the hit object is ignored here for simplicity.
        // Use the dot product between velocity vector and normal (unit) vector to get a projection of the
        // velocity on the normal vector.
        // When the Player moves towards the surface the dot product will be negative.
        float relVelocityMag =
            Vector3.Dot(playerController.externalVelocity, playerController.lastGroundedCollision.normal);
        if (fullyInAir && -relVelocityMag > collisionSfxVelocityThreshold)
            audioSourceOneShot.PlayOneShot(landSfx, landSfxVolume);
    }

    void PlayerHitWall(object sender, float collisionNormalAngle)
    {
        float relVelocityMag =
            Vector3.Dot(playerController.externalVelocity, playerController.lastHitWallCollision.normal);
        if (-relVelocityMag > collisionSfxVelocityThreshold)
            audioSourceOneShot.PlayOneShot(bonkSfx[UnityEngine.Random.Range(0, bonkSfx.Length)], bonkSfxVolume);
    }
}
