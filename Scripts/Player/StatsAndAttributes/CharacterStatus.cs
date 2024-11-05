using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

public class CharacterStatus : MonoBehaviour
{
    [Tooltip("Bind StatusConstants on this character, one per type only")]
    [SerializeField] private StatusConstants[] statusConstants;

    [SerializeField][Range(0.0f, 20.0f)] private float hungerReducePerSec = 0.2f;
    [SerializeField][Range(0.0f, 20.0f)] private float sanityReducePerSec = 0.2f;
    [SerializeField] [Range(0.0f, 20.0f)] private float healthReducePerSecWhenHungry = 1.0f;
    [SerializeField] [Range(0.0f, 20.0f)] private float healthReducePerSecWhenInsane = 1.0f;
    
    [HideInInspector] public Status healthRef;
    [HideInInspector] public Status hungerRef;
    [HideInInspector] public Status sanityRef;
    [HideInInspector] public Status fuelRef;

    public bool shouldPauseDebuff = false;        // Used to pause debuff calculation during dialogue or other scenarios
    
    private Coroutine _hungerDebuff;        // Cache the coroutine for the bleeding debuff triggered by hunger
    private Coroutine _insanityDebuff;        // Cache the coroutine for the bleeding debuff triggered by insanity

    private Coroutine _hungerDecrease;      // Cache the coroutine for the persistent hunger decrease debuff
    private Coroutine _sanityDecrease;      // Cache the coroutine for the persistent sanity decrease debuff

    
    private void Awake()
    {
        foreach (var constant in statusConstants)
        {
            Status status = new Status(constant);
            switch (constant.statusType)
            {
                case StatusType.Health:
                    healthRef = status;
                    break;
                case StatusType.Hunger:
                    hungerRef = status;
                    break;
                case StatusType.Sanity:
                    sanityRef = status;
                    break;
                case StatusType.Fuel:
                    fuelRef = status;
                    break;
            }
        }
        
        if(healthRef != null && hungerRef != null)
        {
            _hungerDecrease = StartCoroutine(ModifyValuePersistent(StatusType.Hunger, hungerReducePerSec, 1.0f));
        }
        
        if(healthRef != null && sanityRef != null)
        {
            _sanityDecrease = StartCoroutine(ModifyValuePersistent(StatusType.Sanity, sanityReducePerSec, 1.0f));
        }
    }

    private void OnEnable()
    {
        if (hungerRef != null)
        {
            hungerRef.OnDepleting += HungerDepletingEventHandler;
            hungerRef.OnRecovering += HungerRecoveringEventHandler;
        }

        if (sanityRef != null)
        {
            sanityRef.OnDepleting += SanityDepletingEventHandler;
            sanityRef.OnRecovering += SanityRecoveringEventHandler;
        }
    }

    private void OnDisable()
    {
        if (hungerRef != null)
        {
            hungerRef.OnDepleting -= HungerDepletingEventHandler;
            hungerRef.OnRecovering -= HungerRecoveringEventHandler;
        }

        if (sanityRef != null)
        {
            sanityRef.OnDepleting -= SanityDepletingEventHandler;
            sanityRef.OnRecovering -= SanityRecoveringEventHandler;
        }
    }
    

    #region Event Handlers
    
    private void HungerDepletingEventHandler()
    {
        if(_hungerDebuff == null)
        {
            _hungerDebuff = StartCoroutine(ModifyValuePersistent(StatusType.Health, healthReducePerSecWhenHungry, 1.0f));
        }
    }

    private void HungerRecoveringEventHandler()
    {
        if (_hungerDebuff != null)
        {
            StopCoroutine(_hungerDebuff);
            _hungerDebuff = null;
        }
    }

    private void SanityDepletingEventHandler()
    {
        if(_insanityDebuff == null)
        {
            _insanityDebuff = StartCoroutine(ModifyValuePersistent(StatusType.Health, healthReducePerSecWhenInsane, 1.0f));
        }
    }
    
    private void SanityRecoveringEventHandler()
    {
        if(_insanityDebuff != null)
        {
            StopCoroutine(_insanityDebuff);
            _insanityDebuff = null;
        }
    }
    
    #endregion


    // Modify status value over a time period
    // value per tick = totalVal / totalTime
    public IEnumerator ModifyValueOverTime(StatusType type, float totalVal, float tickInterval, float totalTime)
    {
        Status status = null;
        switch (type)
        {
            case StatusType.Health:
                status = healthRef;
                break;
            case StatusType.Hunger:
                status = hungerRef;
                break;
            case StatusType.Sanity:
                status = sanityRef;
                break;
            case StatusType.Fuel:
                status = fuelRef;
                break;
        }

        if (status == null)
        {
            yield break;
        }

        float stepVal = totalVal / totalTime;
        float affectedAmount = 0.0f;
        
        while (Mathf.Abs(affectedAmount) < Mathf.Abs(totalVal))
        {
            if(shouldPauseDebuff)
                yield return null;
            else
            {
                status.ModifyValue(stepVal);
                affectedAmount += stepVal;
                yield return new WaitForSeconds(tickInterval);
            }
        }
    }

    // Modify status value for indefinite amount of time
    // Cache the returned coroutine value so you have a way to stop it!
    public IEnumerator ModifyValuePersistent(StatusType type, float valPerTick, float tickInterval)
    {
        Status status = null;
        switch (type)
        {
            case StatusType.Health:
                status = healthRef;
                break;
            case StatusType.Hunger:
                status = hungerRef;
                break;
            case StatusType.Sanity:
                status = sanityRef;
                break;
            case StatusType.Fuel:
                status = fuelRef;
                break;
        }

        if (status == null)
            yield break;
        
        while (true)
        {
            if(shouldPauseDebuff)
                yield return null;
            else
            {
                status.ModifyValue(valPerTick);
                yield return new WaitForSeconds(tickInterval);
            }
        }
    }

    #region Inspector Debug Commands
    
    [ContextMenu("Clear Health")]
    private void ClearHealth()
    {
        healthRef?.SetValue(0.0f);
    }
    
    [ContextMenu("Clear Hunger")]
    private void ClearHunger()
    {
        hungerRef?.SetValue(0.0f);
    }
    
    [ContextMenu("Clear Sanity")]
    private void ClearSanity()
    {
        sanityRef?.SetValue(0.0f);
    }
    
    [ContextMenu("Clear Fuel")]
    private void ClearFuel()
    {
        fuelRef?.SetValue(0.0f);
    }
    
    [ContextMenu("Refill Health")]
    private void RefillHealth()
    {
        healthRef?.SetValue(healthRef.GetMaxValue());
    }
    
    [ContextMenu("Refill Hunger")]
    private void RefillHunger()
    {
        hungerRef?.SetValue(hungerRef.GetMaxValue());
    }
    
    [ContextMenu("Refill Sanity")]
    private void RefillSanity()
    {
        sanityRef?.SetValue(sanityRef.GetMaxValue());
    }
    
    [ContextMenu("Refill Fuel")]
    private void RefillFuel()
    {
        fuelRef?.SetValue(fuelRef.GetMaxValue());
    }

    [ContextMenu("Set Health to 10")]
    private void SetHealthToTen()
    {
        healthRef?.SetValue(10.0f);
    }
    
    [ContextMenu("Set Hunger to 10")]
    private void SetHungerTTene()
    {
        hungerRef?.SetValue(10.0f);
    }
    
    [ContextMenu("Set Sanity to 10")]
    private void SetSanityToTen()
    {
        sanityRef?.SetValue(10.0f);
    }
    
    [ContextMenu("Set Fuel to 10")]
    private void SetFuelToTen()
    {
        fuelRef?.SetValue(10.0f);
    }
    
    [ContextMenu("Set Fuel to 23")]
    private void SetFuelToTT()
    {
        fuelRef?.SetValue(23.0f);
    }
    
    [ContextMenu("Reduce Fuel by 50")]
    private void ReduceFuelBy50()
    {
        fuelRef?.ModifyValue(50.0f);
    }
    
    [ContextMenu("Reduce Health by 1")]
    private void ReduceHealthBy1()
    {
        healthRef?.ModifyValue(1.0f);
    }
    
    #endregion
}
