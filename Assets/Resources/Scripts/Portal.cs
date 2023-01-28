using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The Portal Component keeps track of all Portable objects in front of it and teleports them
// if they move past the portal surface.

/*
A Portal has multiple child objects that have BoxCollider attached for different purposes.
enterTrigger:
A Trigger that is in front of the Portal that detects if a Portable entered. The Portable will then ignore Collisions
with Objects behind the portal surface. The trigger must be long enough so Objects that fall into the Portal
enter it in time so they don't collide with the wall/ ground the portal is on. The distance from the border colliders
should not be too high or small. When the portal is on the ground and an object is lying on a border collider it should
be able to tip over and fall into the portal, but it should not be able to tip over in the other direction and
fall through the ground next to the portal.
BorderColliders:
4 Colliders are placed around the portal plane like a door frame so an object can stand on them.
They should not be too thick so they don't stick out of the wall that the portal is on.
portalPlaneCollider:
A thin non-trigger Collider along the portal surface that ignores Collision with all other layers.
It is used to detect intersections in the portal placement checks. Its also used in the portal gun raycasts to check
if a portal was hit.
Ignoring Collisions based on layers can be done in the Settings: https://docs.unity3d.com/Manual/LayerBasedCollision.html
Edit -> Project Settings -> Physics -> Layer Collision Matrix
deepTrigger:
A Trigger behind the portal surface to detect any colliders behind the portal wall. A Portable should also
ignore collisions with these colliders to not collide with them when going through the portal.
It must be at least as long as the player camera (teleport point) height (relative to the player's feet).
It needs to be sufficiently wide so that all colliders behind the portal are intersecting, but when the other portal
is placed next to this one it should not intersect with its border colliders.
deepGuardTrigger:
When the portal is placed on an angled surface that is also near the horizontal floor, its deepTrigger might intersect
with the floor so that the player would fall through the floor when entering the portal.
To prevent this, this trigger was added in front of the portal. It needs to be large enough to also intersect with
the floor in this scenario. All colliders that this trigger touches will be excluded from ignoring collisions.

Open Problems:
- The main issue with this Portal implementation is that collisions can only be ignored for entire Colliders.
This puts many constraints on the dimensions of the portal trigger colliders and makes additional colliders necessary.
The issues produced by this are:
1. Bigger Portable objects cannot be used because they could intersect with the portal enter trigger and then they can
go through the wall outside of the border colliders.
2. When the player walks up a slope where a Portal is placed and his head touches the enter trigger he will fall through
the ground a bit and then get pushed up again in a loop.
3. For Portals on certain angled surfaces near the floor the player cannot go through the portal because he will collide
with the floor behind it (it intersects with deepGuardTrigger).
4. When building a level only simple geometry can be used. Any L-shaped object cannot be used.
E.g. when the wall and floor is one object, the player would fall through the floor when standing in front of a Portal.
5. When going through a Portal fast, the portal may be entered too late and the object gets grounded on the portal wall.
This can be fixed by reducing the maximum velocity of objects.
- When the player holds an object and moves through thisPortal, he loses the object if otherPortal is far away
and if otherPortal is near, the object tries to fly to the player's hold target point again.
- When the space in front of otherPortal is blocked and an object goes through thisPortal, it does not see that before
teleporting and may be teleported into a collider in front of otherPortal.

Possible next Features/ Fixes to implement:
- Currently the player camera does not render the player model. But this has the drawback of also not rendering the
player shadow. I did not find a fix for this yet.
- Implement Air resistance for all Portable objects (currently only implemented for Player).
- Currently the portal placement uses only a Overhang and Overlap detection, but its not able to correct them.
For Overhangs raycasts have to made behind and in parallel to the portal surface to get the distance to the
side face of the wall. The portal has to be moved by this distance to be fully on the wall.
For Overlaps raycasts have to made in front and in parallel to the portal surface to get the distance to the
face of the intersecting wall. The portal has to be moved by this distance to move out of the overlap.
Problems that have to be considered:
1. After moving the portal, it can have new Overlaps/ Overhangs.
2. A raycast only detects surfaces with normals that point towards the ray. When the raycasts start from the portal corners,
8 raycasts have to be made to find 1 overhang or overlap in the worst case.
- When an object is partly inside a portal surface, its Mesh renderer must be clipped behind the portal plane.
This fixes a bug where an object inside a Portal on a thin wall sticks out on the other side of the wall.
The solution is to use a Slice shader like this: https://github.com/SebLague/Portals/blob/master/Assets/Scripts/Core/Shaders/Slice.shader
(This shader only provides input for an Albedo texture. Support for normal maps etc. must also be added.)
- When an object is partly inside a portal surface, a Mesh renderer clone has to be created that sticks
out of the other portal plane. A solution to this is described here: https://danielilett.com/2020-01-03-tut4-4-portal-momentum/
- Implement Portal local space collision modelling (Solution from Valve)
In Front of thisPortal a collision model needs to be build. This is a cubic section of static non-moving world geometry
(only Colliders) in front of thisPortal. Behind thisPortal a collision model has to be placed that was taken from
a cubic section in front of otherPortal. Both collision models will have a hole in the geometry at the place where
the portal is placed to be able to move through.
For this the mesh colliders needs to be modified accordingly. There are 2 approaches how this could be done:
    1. Use a Constructive solid geometry (CSG) API that allows for boolean operations between 3D meshes.
    A Cube could be subtracted from the portal wall to create the hole.
    Cutting out the section for the rest of the collision model can be done similarly.
    ProBuilder has an experimental CSG Tool, but the API is not public.
    2. Modify the mesh by cutting through the polygons similar to this: https://www.youtube.com/watch?v=BVCNDUcnE1o
When thisPortal is entered then Collisions with any static world geometry should be disabled. Collisions with movable
objects behind thisPortal should be disabled too. Collisions with both previously built Collision Models should be
enabled.
This enables to move through thisPortal without colliding with anything behind thisPortal.
Also if geometry obstructs the space in front of otherPortal this would be noticeable before teleporting.
If the player carries an object and holds it through a portal, the object should not be teleported. It either teleports
together with the player or when the player releases it.
- Implement shadow clones (Solution from Valve)
When a movable object entered thisPortal, a shadow clone has to be created behind otherPortal with the same
relative position/ rotation. A shadow clone duplicates the collider of the real object.
Forces on the real object affect the shadow clone and forces on the shadow clone affect the real object in the same way.
The shadow clone does not collide with any static world geometry. It does also not collide with moving objects behind
otherPortal. It must collide with the collision models around otherPortal. It must collide with movable objects
in front of otherPortal.
When the player tries to pick up an object through thisPortal, the interact raycast must be split up. The second
ray segment will hit the object in front of otherPortal. (The shadow clones should not receive any raycasts,
only the original). In this case the original and the shadow clone can switch their position.
*/

public class Portal : MonoBehaviour
{
    [Tooltip("Material of the Portal plane used when this Portal is actively connected to the other.")]
    public Material portalActiveMat;
    [Tooltip("Material of the Portal plane used when the other Portal is not placed.")]
    public Material portalInactiveMat;
    [Tooltip("Trigger to detect when an objects enters the portal.")]
    public BoxCollider enterTrigger;
    [Tooltip("Collider that spans across the Portal plane.")]
    public BoxCollider portalPlaneCollider;
    [Tooltip("Trigger to detect all colliders that are behind the Portal plane.")]
    public BoxCollider deepTrigger;
    [Tooltip("Trigger in front of the portal to exclude colliders found by deepTrigger.")]
    public BoxCollider deepGuardTrigger;
    // Reference to the other Portal.
    [System.NonSerialized]
    public Portal otherPortal;
    // Reference to the MeshRenderer of the Portal plane.
    [System.NonSerialized]
    public MeshRenderer portalRenderer;
    // Collider of the GameObject that the portal is placed on. Note that this can be null even when the portal is placed.
    // For example when this GameObject was set to active in the Editor scene view (was not fired by portal gun).
    [System.NonSerialized]
    public Collider wallCollider;
    // List of all Colliders that may block moving through the portal. This includes wallCollider and other Colliders
    // behind the Portal plane. Objects must ignore collisions with them when they enter the portal.
    // This must happen in time before any collisions can occur.
    [System.NonSerialized]
    public HashSet<Collider> blockingColliders;
    // blockingColliders need to be updated regularly. Because when an object entered the portal,
    // other objects can move from behind the portal (in deep trigger) to in front of it and vice versa.
    // Maximum time blockingColliders is treated as valid after updating.
    private float blockingCollidersCacheTime = 0.05f;
    private float blockingCollidersCacheTimer = 0;
    private HashSet<Portable> enteredObjects;
    // Layermask used for checking when an objects enters/ leaves the portal.
    private int enterLayermask = -1;
    // Layermask used for portal placement checks.
    private int placementLayermask = -1;
    // Portal plane height (in y-direction) according to portalRenderer.
    // This may be a similar value as the height of portalPlaneCollider.
    [System.NonSerialized]
    public float planeHeight = 0;
    // Place the portal with this distance from the wall.
    // Otherwise graphical glitches will occur when the portal plane overlaps another surface.
    [System.NonSerialized]
    public float portalSurfaceOffset = 0.01f;
    private bool didInitialize = false;
    // OverlapBox parameters calculated from the different Portal trigger Colliders.
    private Vector3 enterTriggerPos;
    private Quaternion enterTriggerRot;
    private Vector3 enterTriggerExtends;
    private Vector3 deepTriggerPos;
    private Quaternion deepTriggerRot;
    private Vector3 deepTriggerExtends;
    private Vector3 deepGuardTriggerPos;
    private Quaternion deepGuardTriggerRot;
    private Vector3 deepGuardTriggerExtends;
    public event EventHandler<Type> OnRemove;
    public event EventHandler<Type> OnActivate;

    // This method initializes portal parameters and may be called before this GameObject is activated.
    // This is needed for the portal placement methods, that are called before activating the portal.
    private void Initialize()
    {
        if (didInitialize)
            return;
        didInitialize = true;
        portalRenderer = GetComponentInChildren<MeshRenderer>();
        foreach (var portal in GameObject.FindObjectsOfType<Portal>(includeInactive: true))
        {
            if (portal != this)
            {
                otherPortal = portal;
                break;
            }
        }

        blockingColliders = new();
        enteredObjects = new();

        // Only objects in the "Portable" and "Player" layer should be able to enter.
        enterLayermask = 0;
        enterLayermask |= 1 << LayerMask.NameToLayer("Portable");
        enterLayermask |= 1 << LayerMask.NameToLayer("Player");

        placementLayermask = GetPlacementLayerMask();

        // The negative z-axis of the portal plane child object is equivalent to the y-axis of the portal.
        planeHeight = Vector3.Scale(portalRenderer.localBounds.size, portalRenderer.transform.localScale).z;

        // On the first portal fire this will be called twice. Once here and once in the Activate() method.
        // This is needed regardless at this place to handle the case where the portal is enabled on
        // scene load without firing the portal.
        UpdateOverlapBoxParams();
    }

    // Awake() is needed because PortalCamera needs otherPortal in Start()
    void Awake()
    {
        Initialize();

        otherPortal.OnActivate += ActivatedOtherPortal;
        otherPortal.OnRemove += RemovedOtherPortal;
        UpdatePortalState(otherPortal.IsPlaced());
    }

    void Update()
    {
        if (Time.deltaTime == 0)
            return;

        blockingCollidersCacheTimer = Mathf.Max(0, blockingCollidersCacheTimer - Time.deltaTime);

        if (!otherPortal.IsPlaced())
            return;

        // Get all objects currently inside enterTrigger.
        // Using Collision Callback functions for this is not possible for the following reasons.
        // OnTriggerEnter cannot be used, because for the player collider it has about a 3 frame delay before its called.
        // OnTriggerExit cannot be used, because its not called after teleporting.

        Collider[] hitColliders = Physics.OverlapBox(enterTriggerPos, enterTriggerExtends, enterTriggerRot,
            enterLayermask, QueryTriggerInteraction.Ignore);
        HashSet<Portable> curEnteredObjects = new();
        foreach (var hitCollider in hitColliders)
        {
            Portable portable = hitCollider.GetComponentInParent<Portable>();
            if (!portable)
                continue;
            curEnteredObjects.Add(portable);
            if (enteredObjects.Contains(portable))
                continue;
            // If newly entered the trigger
            portable.EnterPortal(this);
        }

        // Filter for objects that are not in the trigger any more.
        enteredObjects.ExceptWith(curEnteredObjects);
        foreach (var leftObject in enteredObjects)
        {
            leftObject.ExitPortal(this);

            // Handle the case where a fast moving object goes into the portal and leaves enterTrigger
            // by going through the portal (fallthrough check).
            // The Teleportation check has to be delayed for 1 frame, because it has been observed that the collider
            // of an object can be 1 frame ahead of its transform position sometimes.
            StartCoroutine(TryTeleportObjectDelayed(leftObject));
        }
        enteredObjects = curEnteredObjects;

        foreach (var enteredObject in enteredObjects)
        {
            TryTeleportObject(enteredObject);
        }
    }

    // If the object's teleportPoint is behind the portal plane (using portal local coordinates), teleport.
    // There is also a check if this portal is nearer than the other. This fixes the following problems:
    // - The portals are on the front and back of a thin wall, so they are directly opposite to each other.
    // When going through a portal, the object might be in both triggers at the same time and teleport back and forth.
    // - The fallthrough check should not be triggered in the next frame after teleporting, if the exit portal was
    // behind this portal (in relative z-direction).
    // - In some cases the collider is still in the trigger 1 frame after teleport, even though it should physically
    // not be the case. This could wrongly trigger a teleport back.
    bool TryTeleportObject(Portable portable)
    {
        Vector3 relativePos = transform.InverseTransformPoint(portable.teleportPoint.position);
        if (relativePos.z > 0)
            return false;
        if (!IsNearestPortal(portable))
            return false;

        portable.Teleport(this, otherPortal);

        // EnterPortal(otherPortal) must be called manually, because the collisions with the colliders around otherPortal
        // must be ignored immediately. It cannot be waited until otherPortal updates the next time.
        portable.EnterPortal(otherPortal);
        return true;
    }

    // Call TryTeleportObject() after 1 frame.
    IEnumerator<Portable> TryTeleportObjectDelayed(Portable portable)
    {
        yield return null;
        TryTeleportObject(portable);
    }

    // Returns true if the object is nearer to this portal than to the other.
    // Either the distance to the portal center or to the portal surface must be smaller.
    bool IsNearestPortal(Portable portable)
    {
        Vector3 objPos = portable.teleportPoint.position;
        if ((otherPortal.transform.position - objPos).sqrMagnitude < (transform.position - objPos).sqrMagnitude &&
            Mathf.Abs(otherPortal.transform.InverseTransformPoint(objPos).z) <
            Mathf.Abs(transform.InverseTransformPoint(objPos).z))
        {
            return false;
        }
        return true;
    }

    // The OverlapBox parameters must be updated every time the portal is (re-)placed.
    void UpdateOverlapBoxParams()
    {
        Utils.GetOverlapBoxFromCollider(enterTrigger, out enterTriggerPos, out enterTriggerRot, out enterTriggerExtends);
        Utils.GetOverlapBoxFromCollider(deepTrigger, out deepTriggerPos, out deepTriggerRot, out deepTriggerExtends);
        Utils.GetOverlapBoxFromCollider(deepGuardTrigger, out deepGuardTriggerPos,
            out deepGuardTriggerRot, out deepGuardTriggerExtends);
    }

    // Update the blockingColliders List and reset the cache timer for it.
    public void UpdateBlockingColliders()
    {
        blockingColliders.Clear();
        Collider[] deepColliders = Physics.OverlapBox(
            deepTriggerPos, deepTriggerExtends, deepTriggerRot, -1, QueryTriggerInteraction.Ignore);
        Collider[] excludeDeepColliders = Physics.OverlapBox(
            deepGuardTriggerPos, deepGuardTriggerExtends, deepGuardTriggerRot, -1, QueryTriggerInteraction.Ignore);
        
        foreach (var deepCollider in deepColliders)
        {
            // Don't add this Portal's colliders and colliders inside deepGuardTrigger.
            if (deepCollider.GetComponentInParent<Portal>() == this || excludeDeepColliders.Contains(deepCollider))
                continue;
            blockingColliders.Add(deepCollider);
        }
        // Add this manually in case it was not added in the previous loop.
        if (wallCollider)
            blockingColliders.Add(wallCollider);

        blockingCollidersCacheTimer = blockingCollidersCacheTime;
    }

    public bool isBlockingCollidersUpToDate()
    {
        return blockingCollidersCacheTimer > 0;
    }

    // Update blockingColliders if the cache timer ran out and return a deepcopied version of it.
    public HashSet<Collider> DeepcopyUpdatedBlockingColliders()
    {
        if (!isBlockingCollidersUpToDate())
            UpdateBlockingColliders();

        return new HashSet<Collider>(blockingColliders);
    }

    // Returns true if there is an overhang. An overhang arises when any portal corner point floats in the air
    // or is on a surface that a portal cannot be placed on.
    public bool IsPlacementOverhang()
    {
        Initialize();

        // Get the coordinates of the portal plane corners in local space.
        // Front view of portal plane child object:
        // x-axis points left, y-axis points towards viewer, z-axis points down
        Vector3 portalPlaneColliderHalfSize = portalPlaneCollider.size / 2;
        List<Vector3> checkOverhangPoints = new() {
            // top left
            new Vector3(portalPlaneColliderHalfSize.x, 0, -portalPlaneColliderHalfSize.z),
            // top right
            new Vector3(-portalPlaneColliderHalfSize.x, 0, -portalPlaneColliderHalfSize.z),
            // bottom left
            new Vector3(portalPlaneColliderHalfSize.x, 0, portalPlaneColliderHalfSize.z),
            // bottom right
            new Vector3(-portalPlaneColliderHalfSize.x, 0, portalPlaneColliderHalfSize.z),
        };

        // Cast a ray from each corner point towards the wall.
        for (int i = 0; i < checkOverhangPoints.Count(); i++)
        {
            RaycastHit hit;
            Vector3 raycastPos = portalPlaneCollider.transform.TransformPoint(checkOverhangPoints[i]);
            if (!Physics.Raycast(raycastPos, -portalPlaneCollider.transform.up, out hit,
                portalSurfaceOffset + 0.01f, placementLayermask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            else if (!GameManager.IsPortalSurfaceMaterial(hit.collider))
            {
                return true;
            }
        }
        return false;
    }

    // Returns true if there is an overlap. An overlap arises when the portal surface intersects with another collider.
    public bool IsPlacementOverlap()
    {
        Initialize();

        Vector3 portalPlaneColliderPos;
        Quaternion portalPlaneColliderRot;
        Vector3 portalPlaneColliderExtends;
        Utils.GetOverlapBoxFromCollider(portalPlaneCollider,
            out portalPlaneColliderPos, out portalPlaneColliderRot, out portalPlaneColliderExtends);

        Collider[] hitColliders = Physics.OverlapBox(portalPlaneColliderPos, portalPlaneColliderExtends,
            portalPlaneColliderRot, placementLayermask, QueryTriggerInteraction.Ignore);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.GetComponentInParent<Portal>() == this)
                continue;

            return true;
        }
        return false;
    }

    // When the GameObject is active, the portal is considered to be placed.
    public bool IsPlaced()
    {
        return gameObject.activeSelf;
    }

    // Update the state of this portal depending on if otherPortal is placed or not.
    void UpdatePortalState(bool isOtherPortalPlaced)
    {
        if(isOtherPortalPlaced)
        {
            portalRenderer.material = portalActiveMat;
        }
        else
        {
            portalRenderer.material = portalInactiveMat;
            // If an object was inside enterTrigger while the other portal got removed.
            RemoveAllEnteredObjects();
        }
    }

    void ActivatedOtherPortal(object sender, Type _)
    {
        UpdatePortalState(true);
    }

    void RemovedOtherPortal(object sender, Type _)
    {
        UpdatePortalState(false);
    }

    // Called when the Portal is placed by the portal gun. Will be called also when the portal was already placed.
    public void Activate(Collider atCollider)
    {
        wallCollider = atCollider;
        blockingCollidersCacheTimer = 0;
        UpdateOverlapBoxParams();
        if (!IsPlaced())
        {
            gameObject.SetActive(true);
            OnActivate?.Invoke(this, null);
        }
    }

    // Deactivate this portal if currently placed.
    // tmpRemove: Indicates that the portal will be removed only temporarily and will be activated again in the same frame.
    // This is needed to not invoke RemovedOtherPortal() on the other portal in this case.
    public void Remove(bool tmpRemove = false)
    {
        if (!IsPlaced())
            return;
        RemoveAllEnteredObjects();
        blockingCollidersCacheTimer = 0;
        wallCollider = null;
        gameObject.SetActive(false);
        if (!tmpRemove)
        {
            OnRemove?.Invoke(this, null);
        }
    }

    // Let all currently objects inside enteredObjects exit the portal.
    void RemoveAllEnteredObjects()
    {
        foreach (var enteredObject in enteredObjects)
        {
            enteredObject.ExitPortal(this);
        }
        enteredObjects.Clear();
    }

    // Returns true if the portal is placed on a vertical surface. Returns false if horizontal.
    // A surface with a tilt angle of 45 degrees (included) or less will be considered vertical.
    // An offset of 0.1f is used for the comparisons, because of a possible floating point error.
    public static bool IsVertical(Vector3 portalAngles)
    {
        float verticalAngle = 45.1f;
        float portalAngleX = Utils.DownConvertAngle(portalAngles.x);
        return (portalAngleX > -verticalAngle && portalAngleX < verticalAngle) ||
            portalAngleX < -180 + verticalAngle || portalAngleX > 180 - verticalAngle;
    }

    // Returns true if the portal is on the ground and the surface points upwards.
    // A tilt angle of 30 degrees (included) or more will meet this condition. The angle is measured from Vector3.up.
    public static bool PointsUpwards(Vector3 portalAngles)
    {
        float tiltAngle = 30.1f;
        float portalAngleX = Utils.DownConvertAngle(portalAngles.x);
        return portalAngleX < -tiltAngle && portalAngleX > -180 + tiltAngle;
    }

    // Return a layermask with layers that should be included in the portal placement checks.
    public static int GetPlacementLayerMask()
    {
        // Exclude the following layers.
        // The portal border colliders are in the "Ignore Raycast" layer.
        int layerMask = 0;
        layerMask |= 1 << LayerMask.NameToLayer("Player");
        layerMask |= 1 << LayerMask.NameToLayer("Portable");
        layerMask |= 1 << LayerMask.NameToLayer("Ignore Raycast");
        // NAND bit operation
        layerMask = ~(-1 & layerMask);
        return layerMask;
    }
}
