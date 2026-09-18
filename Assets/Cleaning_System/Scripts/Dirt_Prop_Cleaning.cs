using UnityEngine;
using System;

public class Dirt_Prop_Cleaning : MonoBehaviour, ICleanable
{
    public static event Action onDirtCleaned;
    public float maxHealth = 100f;

    private float currDirtHealth;

    private Vector3 InitialScale; // the first scale of the object
    
    public Transform childTransform;

    [SerializeField] private Renderer dirtRenderer; // Reference to the Renderer component of the dirt object
    private Material dirtMaterial; // Reference to the material of the dirt object
private static readonly int DissolvePropID = Shader.PropertyToID("_DissolveAmount_");
    void Awake()
    {
        currDirtHealth = maxHealth; // set current to full health

        if (dirtRenderer != null)
        {
            dirtMaterial = dirtRenderer.material; // Get the material from the renderer
        }
    }

    public void Clean(float cleanAmount)
    {
        currDirtHealth -= cleanAmount;

        float dissolveRatio = 1f - Mathf.Clamp01(currDirtHealth / maxHealth);

        if (dirtMaterial != null)
        {
            dirtMaterial.SetFloat(DissolvePropID, dissolveRatio);
        }
        if(currDirtHealth <= 0)
        {
            Destroy(gameObject);
            onDirtCleaned?.Invoke();
            // Next Objectives Here :

        }
    }
}
