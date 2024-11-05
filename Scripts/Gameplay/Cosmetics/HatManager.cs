using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HatManager : MonoBehaviour
{
    public static HatManager Instance { get; private set; }

    [SerializeField] private bool debugUnlockAllHats = false;
    public List<Hat> hatList;
    public BirdModelController birdModel;
    private static int unlockHatIndex = -1;


    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this.gameObject);

        // Init
        unlockHatIndex = -1;
        ResetHatsLockStatus(unlockHatIndex);

        if (debugUnlockAllHats)
            UnlockAllHats();
    }

    public void UnlockHat(Hat hat)
    {
        unlockHatIndex++;
        hat.isUnlocked = true;
        Debug.Log(hat.hatName + " is unlocked");
    }

    public void EquipHat(Hat hat)
    {
        // If new hat == old hat, or is trying to unequip hat when there is none
        if (hat == PlayerController.Instance.currentHat || ((hat.hatPrefab == null) && (PlayerController.Instance.currentHat == null)))
            return;

        // Delete previous hat
        if (PlayerController.Instance.currentHat != null)
        {
            Destroy(PlayerController.Instance.currentHat.HatGameObj);
            PlayerController.Instance.currentHat = null;
        }


        // Equip hat for bird model
        StartCoroutine(birdModel.EquipHat(hat));

        if (hat.hatPrefab != null)
        {
            PlayerController.Instance.currentHat = hat;
            Transform hatTransformRef = PlayerController.Instance.hatTransformRef;
            GameObject hatGO = Instantiate(hat.hatPrefab, hatTransformRef.position, PlayerController.Instance.transform.rotation, hatTransformRef);
            hat.HatGameObj = hatGO;
        }
    }



    private void ResetHatsLockStatus(int index)
    {
        for (int i = index + 1; i < hatList.Count; i++)
            hatList[i].isUnlocked = false;
    }

    private void UnlockAllHats()
    {
        foreach (var h in hatList)
            h.isUnlocked = true;
    }

    public void ResetAllHatsLockStatus()
    {
        unlockHatIndex = -1;
        ResetHatsLockStatus(unlockHatIndex);
    }
}
