using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{


    public static UpgradeManager Instance { get; private set;}
    [Header("Upgrade Stats")]
    [SerializeField] public float extraTimeOnKill = 1f;
    [SerializeField] public float biteDmgBonus = 0f;
    [SerializeField] public float chargeDmgBonus = 0f;
    [SerializeField] public float chargeRadiusBonus = 0f;
    [SerializeField] public float chargeCoolDownBonus = 0f;
    private float maxCoolDownReduction = 60f;
    [SerializeField] public bool cheatMode = false;


    private void Awake()
    {
        if (Instance == null){
            Instance = this;
        }else{
            Destroy(gameObject);
        }
    }

    public void ApplyUpgrade(string upgradeType){
        switch(upgradeType){

            case "Time":
                extraTimeOnKill +=1.5f;
                break;

            case "Bite":
                biteDmgBonus +=2f;
                break;

            case "Charge":
                chargeDmgBonus +=2.5f;
                break;

            case "Range":
                chargeRadiusBonus += 0.5f;
                break;

            case "Cooldown":
                if(chargeCoolDownBonus < maxCoolDownReduction){
                    chargeCoolDownBonus += 10f; // decrease cooldown by 10%
                }
                break;

        }

        Debug.Log($"Upgrade applied: {upgradeType}");        
        if(!cheatMode){
            UIManager.Instance?.winScreen.SetActive(false);
            GameManager.Instance?.StartNextRound(); // start the next round after an upgrade is chosen
        }        
    }

    public void ResetUpgrades(){ // reset all upgrade bonuses
        extraTimeOnKill = 1f;
        biteDmgBonus = 0f;
        chargeDmgBonus = 0f;
        chargeRadiusBonus = 0f;
        chargeCoolDownBonus = 0f;
    }


    public void ChangeCheat(){
        cheatMode = !cheatMode;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
