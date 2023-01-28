using UnityEngine;

// Plays collision sound effects when the GameObject Collider collides based on the collision velocity.
// This is currently used for the Cube.

[RequireComponent(typeof(AudioSource))]
public class CollisionAudio : MonoBehaviour
{
    [Tooltip("Audio that is played when colliding (randomly selected).")]
    public AudioClip[] collisionSfx;
    // Multiplied by the collision velocity to get the sound effect volume.
    private float collisionSfxVolumeScale = 0.15f;
    private float collisionSfxVolumeMax = 2f;
    // The AudioSource has 3D spatial blend set, so the audio volume decreases with greater distance
    // according to the rolloff function.
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnCollisionEnter(Collision collision)
    {
        audioSource.PlayOneShot(
            collisionSfx[UnityEngine.Random.Range(0, collisionSfx.Length)],
            Mathf.Min(collisionSfxVolumeMax, collision.relativeVelocity.magnitude * collisionSfxVolumeScale));
    }
}
