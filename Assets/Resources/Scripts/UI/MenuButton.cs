using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Extends the functionality of the Button Component by managing Text color changes,
// playing sound effects on interaction and by having the possibility to register event handler functions.

[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    // Button Component of this GameObject
    [System.NonSerialized]
    public Button button;
    // Text of this Button
    [System.NonSerialized]
    public TextMeshProUGUI buttonText;
    // Default Button Text Color.
    private Color normalTextColor;
    // Button Text color when button is highlighted or selected.
    private Color selectedTextColor;
    // Set to True when Button is selected.
    private bool isSelected = false;
    // AudioSource to play the sound effects. It should not be a Component of this Button,
    // because it will get disabled and stop playing when the button gets disabled.
    private AudioSource audioSource;
    private AudioClip highlightSfx;
    private AudioClip selectSfx;
    public event EventHandler<Type> OnDeselectButton;
    public event EventHandler<Type> OnSelectButton;

    void Start()
    {
        button = GetComponent<Button>();
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        audioSource = GameManager.GetUIAudioSource();
        highlightSfx = (AudioClip)Resources.Load(GameManager.audioButtonHighlightPath);
        selectSfx = (AudioClip)Resources.Load(GameManager.audioButtonSelectPath);

        // Don't stop sounds from this AudioSource when the game is paused.
        audioSource.ignoreListenerPause = true;
        // Define the Button Colors in the Script instead of manually setting the color in the Inspector.
        // The Button Colors will be toggled automatically when interacting.
        var buttonColors = button.colors;
        buttonColors.normalColor      = new Color(1, 1, 1, 0);
        buttonColors.highlightedColor = new Color(230f / 255f, 219f / 255f, 33f / 255f, 40f / 255f);
        buttonColors.selectedColor    = new Color(230f / 255f, 219f / 255f, 33f / 255f, 80f / 255f);
        buttonColors.pressedColor     = new Color(230f / 255f, 219f / 255f, 33f / 255f, 100f / 255f);
        buttonColors.disabledColor    = buttonColors.normalColor;
        button.colors = buttonColors;

        // Highlighting the Button Text can only be done via Script.
        normalTextColor = new Color(170f / 255f, 170f / 255f, 170f / 255f, 1);
        selectedTextColor = new Color(240f / 255f, 240f / 255f, 240f / 255f, 1);        
        ResetButtonTextColor();
    }

    public void ResetButtonTextColor()
    {
        buttonText.color = normalTextColor;
    }

    // Called when this GameObject gets disabled.
    // When a Button is disabled while selected, OnDeselect is not called.
    public void OnDisable()
    {
        isSelected = false;
        ResetButtonTextColor();
    }

    // Called when clicking on the Button or when navigating to the Button with the arrow keys.
    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        buttonText.color = selectedTextColor;
        if (selectSfx)
            audioSource.PlayOneShot(selectSfx, GameManager.buttonSfxVolume);
        OnSelectButton?.Invoke(this, null);
    }

    // Called when clicking outside of the Button or when navigating away from it with the arrow keys.
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        ResetButtonTextColor();
        OnDeselectButton?.Invoke(this, null);
    }

    // Called on Mouse cursor hover. Highlight the Button.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.IsInteractable())
        {
            buttonText.color = selectedTextColor;
            if (highlightSfx)
                audioSource.PlayOneShot(highlightSfx, GameManager.buttonSfxVolume);
        }
    }

    // Called when the mouse cursor leaves the Button.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isSelected && button.IsInteractable())
            ResetButtonTextColor();
    }
}
