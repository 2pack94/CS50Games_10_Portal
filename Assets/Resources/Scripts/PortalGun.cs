using UnityEngine;
using UnityEngine.InputSystem;

// The Portal Gun can shoot both portals or remove a portal on player input.
// The target point for portal placement is determined by a raycast.
// This Component also handles the portal gun animations and sound effects.

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class PortalGun : MonoBehaviour
{
    [Tooltip("Reference to the blue portal in the scene for shooting.")]
    public Portal bluePortal;
    [Tooltip("Reference to the orange portal in the scene for shooting.")]
    public Portal orangePortal;
    [Tooltip("Start point for the raycast. This should be the player camera transform.")]
    public Transform rootTransform;
    [Tooltip("Prefab of the particle system that should be played at the raycast hit point.")]
    public ParticleSystem particlesImpactPrefab;
    [Tooltip("Color of the Particle System when shooting the blue portal.")]
    public Color particlesBluePortalColor;
    [Tooltip("Color of the Particle System when shooting the orange portal.")]
    public Color particlesOrangePortalColor;
    [Tooltip("Audio played when the portal gun is fired.")]
    public AudioClip firePortalSfx;
    [Tooltip("Audio played when a portal is removed by the portal gun.")]
    public AudioClip removePortalSfx;
    [Tooltip("Audio played when the player does an action that is rejected.")]
    public AudioClip rejectSfx;
    // Volumes for the sound effects
    private float firePortalSfxVolume = 1f;
    private float removePortalSfxVolume = 1f;
    private float rejectSfxVolume = 1.3f;
    private AudioSource audioSource;
    // The animation state machine currently has 2 non looping animation clips.
    // After playing a clip the state will automatically go back to an empty state.
    private Animator animator;
    // Animation hashes needed to play animations.
    private int animationHashWeaponRecoil;
    private int animationHashWeaponReject;
    private int raycastLayermask = -1;
    // Can be used to externally control if the portal gun can shoot.
    [System.NonSerialized]
    public bool canShoot = true;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

        raycastLayermask = Portal.GetPlacementLayerMask();

        animationHashWeaponRecoil = Utils.GetAnimationHash(animator, "WeaponRecoil");
        animationHashWeaponReject = Utils.GetAnimationHash(animator, "WeaponReject");
    }

    // Casting a ray from the player camera position in player camera forward direction should
    // produce a hit where the crosshair in the middle of the screen points to.
    // The raycast returns the closest hit.
    bool RaycastGun(out RaycastHit hit)
    {
        return Physics.Raycast(rootTransform.position, rootTransform.forward, out hit, Mathf.Infinity,
            raycastLayermask, QueryTriggerInteraction.Ignore);
    }

    bool IsShootAnimationPlaying()
    {
        return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == animationHashWeaponRecoil;
    }

    void PlayShootAnimation()
    {
        audioSource.PlayOneShot(firePortalSfx, firePortalSfxVolume);
        // This function call will be ignored when the animation is still playing.
        animator.Play(animationHashWeaponRecoil);
    }

    public void PlayRejectAnimation()
    {
        // Don't cancel shoot animation
        if (IsShootAnimationPlaying())
            return;
        audioSource.PlayOneShot(rejectSfx, rejectSfxVolume);
        animator.Play(animationHashWeaponReject);
    }

    // The particles will be spawned in a color that fits the portal.
    // The particle system is configured to automatically destroy its GameObject after playing.
    void PlayImpactParticles(Portal portal, Vector3 particlesPosition, Quaternion particlesRotation)
    {
        ParticleSystem particles = Instantiate(particlesImpactPrefab, particlesPosition, particlesRotation);
        ParticleSystem.MainModule particlesMain = particles.main;
        if (portal == bluePortal)
            particlesMain.startColor = new ParticleSystem.MinMaxGradient(particlesBluePortalColor);
        else
            particlesMain.startColor = new ParticleSystem.MinMaxGradient(particlesOrangePortalColor);
        particles.Play();
    }

    // Try to place the portal. Return true if portal was (re-)placed.
    // If a portal gets removed/ deactivated when an object is inside of a portal, it gets
    // pushed out of the wall in the correct direction automatically from the Unity collision detection.
    // If an object is inside of a portal and the other portal gets replaced, the object does not get pushed out.
    bool FirePortal(Portal portal)
    {
        if (!canShoot)
            return false;

        RaycastHit hit;
        if (!RaycastGun(out hit))
        {
            PlayRejectAnimation();
            return false;
        }

        // If this portal was hit, try to replace it. If the other portal was hit don't proceed.
        // The portals have a collider at their portal plane that raycasts can hit.
        Portal hitPortal = hit.collider.gameObject.GetComponentInParent<Portal>();
        if (hitPortal)
        {
            if (hitPortal != portal)
            {
                PlayRejectAnimation();
                return false;
            }

            // Fire portal again after removing it to hit the wall the portal was on.
            // The shoot animation will be played in the first recursion.
            Collider portalWallColliderPrev = portal.wallCollider;
            hitPortal.Remove(tmpRemove: true);
            if (!FirePortal(portal))
            {
                // If the portal cannot be placed at the new position.
                hitPortal.Activate(portalWallColliderPrev);
                return false;
            }
            else
            {
                return true;
            }
        }

        // Backup the portal transform in case it will not be placed.
        // In any case the portal transform needs to be changed to the target transform to be able
        // to check the portal overhangs/ overlaps.
        Vector3 oldPortalPosition = portal.transform.position;
        Quaternion oldPortalRotation = portal.transform.rotation;

        // Place the portal at the intersection of the ray
        portal.transform.position = hit.point;

        // How the local coordinate system for the portal must be rotated can be seen in the Editor scene view.
        // The portal forward vector should be the normal of the surface the ray hit.
        // The right vector should be the inverse right vector of the player.
        // From these vectors the up vector can be calculated with the cross product a x b = c.
        // The right-hand rule applies here, where a is the index finger and c is the thumb.
        Vector3 portalForward = hit.normal;
        Vector3 portalRight = -rootTransform.right;
        Vector3 portalUp = Vector3.Cross(portalForward, portalRight);
        portal.transform.rotation = Quaternion.LookRotation(portalForward, portalUp);

        // If a vertical surface was hit, place the portal upright (set z-rotation to 0).
        Vector3 portalAngles = portal.transform.rotation.eulerAngles;
        if (Portal.IsVertical(portalAngles))
        {
            portal.transform.rotation = Quaternion.Euler(portalAngles.x, portalAngles.y, 0);
        }

        // Move the portal a bit away from the surface.
        portal.transform.position += portalForward * portal.portalSurfaceOffset;

        PlayShootAnimation();
        PlayImpactParticles(portal, portal.transform.position, portal.transform.rotation);

        // Verify that a valid portal surface with the correct material was hit.
        // Don't place the portal if it has an overlap/ overhang.
        if (!GameManager.IsPortalSurfaceMaterial(hit.collider) ||
            portal.IsPlacementOverlap() || portal.IsPlacementOverhang())
        {
            portal.transform.position = oldPortalPosition;
            portal.transform.rotation = oldPortalRotation;
            return false;
        }
        // Activate() has to be called again even if the portal was already active.
        portal.Activate(hit.collider);
        return true;
    }

    // The player needs to aim at a placed portal to remove it. Return true if the portal was removed.
    bool TryRemovePortal()
    {
        if (!canShoot)
            return false;

        RaycastHit hit;
        if (!RaycastGun(out hit))
        {
            PlayRejectAnimation();
            return false;
        }
        Portal hitPortal = hit.collider.gameObject.GetComponentInParent<Portal>();
        if (!hitPortal)
        {
            PlayRejectAnimation();
            return false;
        }
        audioSource.PlayOneShot(removePortalSfx, removePortalSfxVolume);
        hitPortal.Remove();
        return true;
    }

    // Called on player input.
    void OnFireBluePortal(InputValue _)
    {
        if (bluePortal)
            FirePortal(bluePortal);
    }

    void OnFireOrangePortal(InputValue _)
    {
        if (orangePortal)
            FirePortal(orangePortal);
    }

    void OnTryRemovePortal(InputValue _)
    {
        TryRemovePortal();
    }
}
