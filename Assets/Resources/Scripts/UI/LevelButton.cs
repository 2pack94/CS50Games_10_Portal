using UnityEngine;

// The level Buttons are shown in the Level selection screen.
// They hold information about a level (A scene that can be loaded).

public class LevelButton : MenuButton
{
    [Tooltip("Sprite that can be shown as a Level preview.")]
    public Sprite levelPreviewSprite;

    // Get the Level name from the button text.
    public string GetSceneName()
    {
        return buttonText.text;
    }
}
