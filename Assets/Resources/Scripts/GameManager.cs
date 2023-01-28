using UnityEngine;

// Scriptable Objects: https://docs.unity3d.com/Manual/class-ScriptableObject.html

[CreateAssetMenu(menuName = "Game Manager")]
public class GameManager : ScriptableObject
{
    // Define constants that can be referenced across the application.
    // Every Scene with a Menu should have a GameObject with this tag and an AudioSource
    // that can be used to play the UI Audio.
    public const string UIAudioSourceTag = "UIAudioSource";
    public const string sceneTitleScreen = "Title";
    public const string actionMapNameUI = "UI";
    public const string actionMapNamePlayer = "Player";
    // Substring that all Names of concrete Materials start with.
    // Only on these materials a portal can be placed.
    public const string concreteMaterialName = "concrete_modular";
    public const float buttonSfxVolume = 5f;
    // The following Paths are relative to Assets/Resources/
    // Load button sound effects from their path instead of adding them manually to the Component
    // through the inspector.
    public const string audioButtonHighlightPath = "Audio/button_highlight";
    public const string audioButtonSelectPath = "Audio/button_select";
    public const string textureMouseCursorPath = "Sprites/MouseCursor";

    // Runs before first scene load (needs to be defined inside a non-MonoBehaviour class)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoadRuntimeMethod()
    {
        Application.targetFrameRate = 60;
        // Set a custom cursor texture.
        Cursor.SetCursor((Texture2D)Resources.Load(GameManager.textureMouseCursorPath),
            Vector2.zero, CursorMode.Auto);
    }

    // Find the UIAudioSource in the Scene.
    public static AudioSource GetUIAudioSource()
    {
        return GameObject.FindWithTag(GameManager.UIAudioSourceTag).GetComponent<AudioSource>();
    }

    // Check if the Material name belongs to a material where a portal can be placed.
    public static bool IsPortalSurfaceMaterial(string matName)
    {
        return matName.StartsWith(GameManager.concreteMaterialName);
    }

    // Check if the Collider has a material where a portal can be placed.
    public static bool IsPortalSurfaceMaterial(Collider checkCollider)
    {
        var renderer = checkCollider.GetComponent<Renderer>();
        if (!renderer || !GameManager.IsPortalSurfaceMaterial(renderer.material.name))
            return false;
        return true;
    }
}
