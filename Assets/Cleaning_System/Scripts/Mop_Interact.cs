using UnityEngine;

public class Mop_Interact : MonoBehaviour
{

    public float MopRange = 3f;

    public float cleaningPower = 50f;

    public LayerMask dirtLayer;

    public AudioSource mopAudioSource;


    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButton(0))
        {
RaycastHit hit;
Ray ray = new Ray(transform.position, transform.forward);
            
            if(Physics.Raycast(ray, out hit, MopRange, dirtLayer))
            {

                Dirt_Prop_Cleaning dirt = hit.collider.GetComponent<Dirt_Prop_Cleaning>();
              
                if(dirt != null)
                {
                    dirt.CleanMop(cleaningPower * Time.deltaTime);

                    if(mopAudioSource != null && !mopAudioSource.isPlaying)
                    {
                        mopAudioSource.loop = true;

                        mopAudioSource.Play();

                    }

                    return;
                }
            }
        }

        if (mopAudioSource != null && mopAudioSource.isPlaying)
        {
            mopAudioSource.Stop();
        }
    }
}
