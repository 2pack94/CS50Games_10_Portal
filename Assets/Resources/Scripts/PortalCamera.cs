using UnityEngine;

// This Component is attached to a Unity Camera. The camera will move/ rotate relative to its portal depending
// on the Main Camera (Player Camera) position/ rotation. It will render to the Render Texture of the other portal.
// The Code was mostly taken from:
// https://github.com/daniel-ilett/shaders-portal/blob/master/Assets/Scripts/RecursivePortal/RecursivePortalCamera.cs

[RequireComponent(typeof(Camera))]
public class PortalCamera : MonoBehaviour
{
    // Camera of this portal. It renders to the other portal's Render Texture.
    private Camera cam;
    // Main Camera which will be the Player's camera when in-game.
    private Camera mainCam;
    // Portal that this camera moves relative to.
    private Portal thisPortal;
    // Portal that this camera renders to.
    private Portal otherPortal;
    // Store the Screen size to detect resolution changes.
    private Vector2 prevScreenSize;
    // Maximum number of portal view recursions.
    private int maxRenderIterations = 5;
    // When there is no recursive portal view, renderIterations can be set to 1.
    private int renderIterations = 0;

    void Start()
    {
        cam = GetComponent<Camera>();
        mainCam = Camera.main;
        thisPortal = GetComponentInParent<Portal>();
        otherPortal = thisPortal.otherPortal;

        // Instantiate the Render Texture at runtime with the current game resolution.
        cam.targetTexture = new RenderTexture(Screen.width, Screen.height, 32);
        otherPortal.portalActiveMat.mainTexture = cam.targetTexture;
        prevScreenSize = new(Screen.width, Screen.height);
    }

    // When a resolution change is detected, change also the Render Texture resolution.
    void CheckResolutionChange()
    {
        Vector2 screenSize = new(Screen.width, Screen.height);
        if (prevScreenSize != screenSize)
        {
            // This function releases the hardware resources used by the render texture.
            // The texture itself is not destroyed, and will be automatically created again when being used.
            cam.targetTexture.Release();
            cam.targetTexture.width = Screen.width;
            cam.targetTexture.height = Screen.height;
            cam.targetTexture.depth = 32;

            prevScreenSize = screenSize;
        }
    }

    void RenderCamera(int recursionNum)
    {
        transform.position = mainCam.transform.position;
        transform.rotation = mainCam.transform.rotation;

        // On recursion level 1 (last iteration) this loop is traversed 1 time.
        for (int i = 0; i < recursionNum; i++)
        {
            // Move and rotate the Portal Camera.
            // 1. Transform camera position from world space to otherPortal local space.
            // 2. Rotate the local position vector 180 degrees around the y-axis.
            // 3. Transform the relative position back to world space and assign it to the camera's position.
            Vector3 relativePos = otherPortal.transform.InverseTransformPoint(transform.position);
            relativePos = Quaternion.Euler(0f, 180.0f, 0.0f) * relativePos;
            transform.position = thisPortal.transform.TransformPoint(relativePos);

            // 1. Calculate the rotational difference from otherPortal to the camera.
            // (subtract otherPortal.transform.rotation from transform.rotation)
            // 2. Rotate 180 degrees around the y-axis.
            // 3. Add this rotation to thisPortal.transform.rotation to get the camera rotation.
            Quaternion relativeRot = Quaternion.Inverse(otherPortal.transform.rotation) * transform.rotation;
            relativeRot = Quaternion.Euler(0f, 180.0f, 0.0f) * relativeRot;
            transform.rotation = thisPortal.transform.rotation * relativeRot;
        }

        // Calculate an oblique projection matrix for the Portal Camera.
        // 1. Define the desired camera near clipping plane in world space. It should be the surface plane of thisPortal.
        // 2. Redefine the same plane as Vector4 with the plane normal and its distance to the origin along the normal.
        // 3. Transform the near clip plane from world space to portal camera space.
        // 4. Get the projection matrix of mainCam but with the calculated clip plane as its near plane.
        Plane tmpPlane = new Plane(thisPortal.transform.forward, thisPortal.transform.position);
        Vector4 clipPlane = new Vector4(tmpPlane.normal.x, tmpPlane.normal.y, tmpPlane.normal.z, tmpPlane.distance);
        Vector4 clipPlaneCameraSpace = Matrix4x4.Transpose(Matrix4x4.Inverse(cam.worldToCameraMatrix)) * clipPlane;
        cam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlaneCameraSpace);

        // Switch out the Render Texture of otherPortal on the first iteration while this camera renders.
        Material matBackup = otherPortal.portalRenderer.material;
        if (recursionNum == renderIterations)
            otherPortal.portalRenderer.material = otherPortal.portalInactiveMat;
        // The camera component has to be disabled to be able to call Render() manually.
        cam.Render();
        if (recursionNum == renderIterations)
            otherPortal.portalRenderer.material = matBackup;
    }

    // To avoid a 1 frame render delay use LateUpdate. This camera must render after the player movement is calculated.
    // Note when using cinemachine cameras, the following properties must be set on the CinemachineBrain Component:
    //  Blend Update Method: Fixed Update
    void LateUpdate()
    {
        if (Time.deltaTime == 0)
            return;

        if (!otherPortal.IsPlaced())
            return;

        // If otherPortal is not in the view frustum, don't update this camera.
        // The IsInFrustum check is not 100% optimal, because it returns true also if the view is obstructed or
        // if the portal is seen only from behind.
        // Note that otherPortal.portalRenderer.isVisible cannot be used here, because when teleporting its updated
        // 1 frame after the teleport. The wrong render texture content would be visible for one frame when going through
        // a portal backwards when both portals face a similar direction. Because otherPortal will not have been visible
        // by any camera before teleportation.
        if (!Utils.IsInFrustum(otherPortal.portalRenderer, mainCam))
            return;

        CheckResolutionChange();

        // Don't do recursive rendering if the portal camera (position from last iteration from previous frame)
        // does not have otherPortal in its view frustum.
        renderIterations = maxRenderIterations;
        if (maxRenderIterations > 1 && !Utils.IsInFrustum(otherPortal.portalRenderer, cam))
            renderIterations = 1;

        // Iterate backwards from deepest recursion to recursion level 1.
        for (int i = renderIterations; i > 0; i--)
            RenderCamera(i);
    }
}

/*
// This solution modifies position and rotation in world space only.
// However it does not work properly when both portals have a rotation.
// It's unclear what the reason for this is.
// Position the camera //////////
// Get a vector from otherPortal to mainCam
Vector3 otherPortalToPlayer = mainCam.transform.position - otherPortal.transform.position;
// Calculate the rotational difference from otherPortal to thisPortal
// (subtract otherPortal.transform.rotation from thisPortal.transform.rotation)
Quaternion portalRotDiff = Quaternion.Inverse(otherPortal.transform.rotation) * thisPortal.transform.rotation;
// Rotate the vector otherPortalToPlayer to the perspective of thisPortal.
Vector3 thisPortalToCamera = portalRotDiff * otherPortalToPlayer;
// Rotate the vector 180 degrees around the portal up-axis.
thisPortalToCamera = Quaternion.AngleAxis(180f, thisPortal.transform.up) * thisPortalToCamera;
// Translate the camera position along the calculated vector.
transform.position = thisPortal.transform.position + thisPortalToCamera;

// Rotate the camera //////////
// Start from the player camera rotation and rotate it to the perspective of thisPortal.
transform.rotation = portalRotDiff * mainCam.transform.rotation;
// Rotate the camera 180 degrees around the portal up-axis.
transform.rotation = Quaternion.AngleAxis(180f, thisPortal.transform.up) * transform.rotation;
*/
