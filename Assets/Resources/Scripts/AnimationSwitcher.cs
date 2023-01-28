using UnityEngine;

// Class used to cross fade between animation states. It is used instead of using
// animation parameters to trigger hard-linked state transitions drawn in the animator window. 

public class AnimationSwitcher
{
    public Animator animator;
    public int currentStateHash { get; private set; }

    public AnimationSwitcher(Animator _animator)
    {
        animator = _animator;
        currentStateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
    }

    public void ChangeAnimation(int newStateHash, float transitionTime)
    {
        if (currentStateHash == newStateHash)
            return;
        animator.CrossFadeInFixedTime(newStateHash, transitionTime);
        currentStateHash = newStateHash;
    }
}
