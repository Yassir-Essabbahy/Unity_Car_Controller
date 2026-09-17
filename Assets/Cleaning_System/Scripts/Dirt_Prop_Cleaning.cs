using UnityEngine;

public class Dirt_Prop_Cleaning : MonoBehaviour
{
    public float maxHealth = 100f;

    private float currDirtHealth;

    private Vector3 InitialScale; // the first scale of the object
    
    public Transform childTransform;

    void Awake()
    {
        currDirtHealth = maxHealth; // set current to full health

        InitialScale = childTransform.transform.localScale; // take original scale of object
    }

    public void CleanMop(float cleanAmount)
    {
        currDirtHealth -= cleanAmount;

        float healthRatio = Mathf.Clamp01(currDirtHealth / maxHealth);

        childTransform.transform.localScale = InitialScale * healthRatio;

        if(currDirtHealth <= 0)
        {
            Destroy(gameObject);
            // Next Objectives Here :

        }
    }
}
