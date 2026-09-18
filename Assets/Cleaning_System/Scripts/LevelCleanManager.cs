using UnityEngine;
using TMPro;

public class LevelCleanManager : MonoBehaviour
{
    
    private int totalDirtCount;
    private int cleanedDirtCount;

    public TextMeshProUGUI dirtCountText;

    void Start()
    {
        totalDirtCount = FindObjectsByType<Dirt_Prop_Cleaning>().Length;
        dirtCountText.text = "Remaining Dirt: " + totalDirtCount;
        Debug.Log("Total dirt count: " + totalDirtCount);
    }

    void OnEnable()
    {
        Dirt_Prop_Cleaning.onDirtCleaned += HandleDirtCleaned;
    }

    void OnDisable()
    {
        Dirt_Prop_Cleaning.onDirtCleaned -= HandleDirtCleaned;
    }

    private void HandleDirtCleaned()
    {
        cleanedDirtCount++;
        int remaining = totalDirtCount - cleanedDirtCount;
        dirtCountText.text = "Remaining Dirt: " + remaining;
        Debug.Log("Dirt cleaned! Remaining dirt: " + remaining);

        if (remaining <= 0)
        {
            Debug.Log("All dirt cleaned! Level complete!");
            // Trigger level completion logic here
        }
    }
}