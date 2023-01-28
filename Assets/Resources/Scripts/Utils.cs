using UnityEngine;

// Contains static Utility functions.

public class Utils
{
    // Return an angle between -180 and 180 degrees.
    public static float DownConvertAngle(float angle)
    {
        angle %= 360;
        if (angle > 180)
            angle -= 360;
        else if (angle < -180)
            angle += 360;
        return angle;
    }

    // Return the Hash of an animation state specified by its name.
    public static int GetAnimationHash(Animator animator, string animationName, int layerIndex = 0)
    {
        return Animator.StringToHash(animator.GetLayerName(layerIndex) + "." + animationName);
    }

    // Get the parameters required in Physics.OverlapBox() from a BoxCollider.
    public static void GetOverlapBoxFromCollider(BoxCollider boxCollider,
        out Vector3 position, out Quaternion rotation, out Vector3 extends)
    {
        // Get BoxCollider center in world space.
        position = boxCollider.transform.TransformPoint(boxCollider.center);
        rotation = boxCollider.transform.rotation;
        extends = Vector3.Scale(boxCollider.size, boxCollider.transform.localScale) / 2;
    }

    // Check if the bounding box of a Renderer is inside the camera view frustum.
    public static bool IsInFrustum(Renderer renderer, Camera camera)
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
    }
}
