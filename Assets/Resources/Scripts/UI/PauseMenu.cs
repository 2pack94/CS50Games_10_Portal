using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The pause Menu can be brought up in-Game to pause it.
// It has a Cancel and a "Back to Main Menu" Button.

public class PauseMenu : MonoBehaviour
{
    [Tooltip("Parent GameObject for the Menu which will be activated when the Pause Key is pressed.")]
    public GameObject panel;
    // PlayerInput Component that contains the Input Action Asset.
    private PlayerInput playerInput;
    // AudioSource to play a sound effect when the Menu is closed.
    private AudioSource audioSource;
    // Sound effect played when the Menu is closed through the Cancel key instead of pressing
    // the Cancel Menu Button. This will be the same as the default Button select sound.
    private AudioClip backSfx;

    // For some reason an input action asset cannot be used in the Input System UI Input Module
    // when it is also used in a PlayerInput Component. No input will be received in the UI.
    // So the DefaultInputActions input action asset is used for UI input.

    void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        // Get the same AudioSource that is used by the Button too.
        audioSource = GameManager.GetUIAudioSource();
        backSfx = (AudioClip)Resources.Load(GameManager.audioButtonSelectPath);
        ResumeGame();
    }

    // Pause the Game and show the Pause Menu.
    void PauseGame()
    {
        // Sets Time.deltaTime to 0 and stops all Physics and Animations.
        // All Update functions will still be called once per frame however.
        Time.timeScale = 0;
        // Show the Cursor when in Menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Pause all Audio sources
        AudioListener.pause = true;
        // Even though PlayerInput is not used for the Pause Menu, switch the Action Map to UI
        // to not receive any Player Input while in Menu.
        playerInput.SwitchCurrentActionMap(GameManager.actionMapNameUI);
        panel.SetActive(true);
    }

    // Unpause the Game and hide the Pause Menu.
    void ResumeGame()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1;
        AudioListener.pause = false;
        // Deselect Buttons in case one was selected when closing the Pause Menu.
        EventSystem.current.SetSelectedGameObject(null);
        playerInput.SwitchCurrentActionMap(GameManager.actionMapNamePlayer);
        panel.SetActive(false);
    }

    // Callback function registered as the OnClick event for the Cancel Button.
    public void PressedCancelButton()
    {
        ResumeGame();
    }

    // Callback function registered as the OnClick event for the "To Main Menu" Button.
    public void PressedToMainMenuButton()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        SceneManager.LoadScene(GameManager.sceneTitleScreen);
    }

    // Callback when pressing the Pause key. This comes from the Player Action Map.
    public void OnPause(InputValue value)
    {
        PauseGame();
    }

    // Callback when pressing the Cancel key. This comes from the UI Action Map.
    public void OnCancel(InputValue value)
    {
        if (backSfx)
            audioSource.PlayOneShot(backSfx, GameManager.buttonSfxVolume);
        ResumeGame();
    }
}
