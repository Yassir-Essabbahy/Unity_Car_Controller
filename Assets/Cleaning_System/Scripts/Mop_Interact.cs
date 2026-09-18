using UnityEngine;

public class Mop_Interact : MonoBehaviour
{

    public float MopRange = 3f;

    public float cleaningPower = 50f;

    public LayerMask dirtLayer;

    public AudioSource mopAudioSource;

    [Header("Sway / Cleaning Motion")]
    [SerializeField] private float swingSpeed = 10f;
    [SerializeField] private float swingAngle = 15f;
    [SerializeField] private Transform broomTransform;

    private Quaternion defaultLocalRotation;


    void Start()
    {
        defaultLocalRotation = broomTransform.localRotation;
    }


void Update()
{
    bool isCleaning = false;

    if (Input.GetMouseButton(0))
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, MopRange, dirtLayer))
        {
            ICleanable dirt = hit.collider.GetComponent<ICleanable>();

            if (dirt != null)
            {
                dirt.Clean(cleaningPower * Time.deltaTime);
                isCleaning = true;

                if (mopAudioSource != null && !mopAudioSource.isPlaying)
                {
                    mopAudioSource.loop = true;
                    mopAudioSource.Play();
                }
            }
        }
    }

    if (isCleaning)
    {
        float zAngle = Mathf.Sin(Time.time * swingSpeed) * swingAngle;
        broomTransform.localRotation = defaultLocalRotation * Quaternion.Euler(0, 0, zAngle);
    }
    else
    {
        broomTransform.localRotation = Quaternion.Slerp(broomTransform.localRotation, defaultLocalRotation, Time.deltaTime * 5f);

        if (mopAudioSource != null && mopAudioSource.isPlaying)
        {
            mopAudioSource.Stop();
        }
    }
}
}
