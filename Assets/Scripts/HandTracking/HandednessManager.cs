using UnityEngine;

public class HandednessManager : MonoBehaviour
{
    private static HandednessManager instance;

    public enum Handedness
    {
        NotSet,
        RightHanded,
        LeftHanded
    }

    private Handedness dominantHand = Handedness.NotSet;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static HandednessManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HandednessManager");
                instance = go.AddComponent<HandednessManager>();
            }
            return instance;
        }
    }

    public void SetDominantHand(Handedness hand)
    {
        dominantHand = hand;
        Debug.Log($"Dominant hand set to: {hand}");
    }

    public Handedness GetDominantHand()
    {
        return dominantHand;
    }

    public bool IsRightHanded()
    {
        return dominantHand == Handedness.RightHanded;
    }

    public bool IsLeftHanded()
    {
        return dominantHand == Handedness.LeftHanded;
    }

    public bool IsHandednessSet()
    {
        return dominantHand != Handedness.NotSet;
    }
}
