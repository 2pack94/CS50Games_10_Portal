using UnityEngine;
using UnityEngine.InputSystem;

// Manage the input that is used inside PlayerController

public class PlayerInputs : MonoBehaviour
{
    [Tooltip("Set to true if a controller is used for move input.")]
    public bool analogMovement = false;

    [System.NonSerialized]
    public Vector2 move;
    [System.NonSerialized]
    public Vector2 look;
    [System.NonSerialized]
    public bool jump;
    [System.NonSerialized]
    public bool sprint;
    [System.NonSerialized]
    public bool crouch;
    // PlayerInput Component that contains the Input Action Asset.
    private PlayerInput playerInput;

    void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
    }

    // To also get Events when the button is released, set "Action Type" to "Value"
    // instead of "Button" in the Input Action Asset.

    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        LookInput(value.Get<Vector2>());
    }

    public void OnJump(InputValue value)
    {
        JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }

    void OnCrouch(InputValue value)
    {
        CrouchInput(value.isPressed);
    }


    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    } 

    public void LookInput(Vector2 newLookDirection)
    {
        look = newLookDirection;
    }

    public void JumpInput(bool newJumpState)
    {
        jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        sprint = newSprintState;
    }

    public void CrouchInput(bool newCrouchState)
    {
        crouch = newCrouchState;
    }

    public bool IsMouseInput()
    {
        return playerInput.currentControlScheme == "KeyboardMouse";
    }
}
