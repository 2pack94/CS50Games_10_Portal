using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Title screen is split into two different views. The Main Menu has a "Select Level" Button
// that leads to the Level Selection Menu and a "Quit Game" Button that closes the game.
// The Level Selection Menu has a scrollable window with Buttons for every Level.
// When pressing the "Play" Button while a Level Button is selected, the corresponding Level (Scene)
// will be loaded. The "Back" Button leads back to the Main Menu.
// The UI scale Mode for the Canvas is set to "Scale with Screen size", so the menu items stay
// in place and change their size when resolution changes.
// This UI is designed for a 16:9 aspect ratio and will look wrong for other aspect ratios.

public class TitleScreen : MonoBehaviour
{
    [Tooltip("Reference to the Play Button to toggle interactable state based on if a Level is selected.")]
    public Button playButton;
    [Tooltip("Reference to the Back Button to go back to the Main Menu when the Cancel key is pressed.")]
    public Button backButton;
    [Tooltip("Parent GameObject of the Level Buttons to be able to iterate over them.")]
    public GameObject levelButtonsParent;
    [Tooltip("Image UI Component to apply a Level preview Sprite to.")]
    public Image levelPreviewArea;
    // Currently selected level name
    private string levelName;
    // The MenuButton Component of playButton.
    private MenuButton playMenuButton;
    // Gets assigned when a Level Button is selected and set to null when it gets deselected.
    private LevelButton levelButtonSelected;

    void Start()
    {
        playMenuButton = playButton.GetComponent<MenuButton>();
        foreach (var levelButton in levelButtonsParent.GetComponentsInChildren<MenuButton>())
        {
            levelButton.OnSelectButton += SelectLevelButton;
        }
        ResetLevelSelection();
    }

    void ResetLevelSelection()
    {
        levelName = "";
        playButton.interactable = false;
        levelPreviewArea.sprite = null;
        levelPreviewArea.color = new Color(1f, 1f, 1f, 128f / 255f);
    }

    // Callback invoked when OnSelect() of any Level Button is called.
    public void SelectLevelButton(object button, Type _)
    {
        levelButtonSelected = (LevelButton)button;
        levelName = levelButtonSelected.GetSceneName();
        playButton.interactable = true;
        levelButtonSelected.OnDeselectButton += OnLevelButtonDeselect;
        levelPreviewArea.sprite = levelButtonSelected.levelPreviewSprite;
        levelPreviewArea.color = new Color(1f, 1f, 1f, 1f);
    }

    // Callback invoked when OnDeselect() of the selected Level Button is called.
    public void OnLevelButtonDeselect(object sender, Type _)
    {
        StartCoroutine(OnLevelButtonDeselectCoroutine());
        levelButtonSelected.OnDeselectButton -= OnLevelButtonDeselect;
        levelButtonSelected = null;
    }

    // levelName cannot be removed immediately, because after the player selects the level he needs to
    // click the Play Button. However Buttons are deselected a few frames before another Button is selected.
    IEnumerator OnLevelButtonDeselectCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
        // Check if no other Level Button was selected in the meantime.
        if (!levelButtonSelected)
        {
            ResetLevelSelection();
            // In case the Play Button was highlighted while the Level Button was deselected.
            playMenuButton.ResetButtonTextColor();
        }
    }

    // Callback when pressing the Cancel key.
    public void OnCancel(InputValue value)
    {
        // Deselect all Buttons
        EventSystem.current.SetSelectedGameObject(null);
        // If on Level Selection Menu
        if (backButton.gameObject.activeInHierarchy)
        {
            backButton.onClick.Invoke();
        }
    }

    // Callback functions that are registered as the OnClick event for different Buttons.
    // The registration was done in the Unity Inspector in the Button Component.
    // The "Select Level" and "Back" Button have their functionality defined directly in the Inspector.
    // They just enable/ disable the next/ current UI GameObjects.

    // Load the selected Level when pressing the Play button and a Level was selected.
    public void PlayLevel()
    {
        if (levelName.Length > 0)
            SceneManager.LoadScene(levelName);
    }

    // Called when the "Quit Game" Button is pressed.
    public void QuitGame()
    {
        Application.Quit();
    }
}
