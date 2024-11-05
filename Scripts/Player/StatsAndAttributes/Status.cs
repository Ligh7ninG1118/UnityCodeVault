using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

public class Status
{
    private bool _isDepleted;
    public bool _isLowState = false;

    public event Action OnEnteringLowState;
    public event Action OnExitingLowState;
    public event Action OnDepleting;
    public event Action OnRecovering;
    public event Action<float> OnValueDecreased;
    public event Action<float> OnValueIncreased;

    private StatusType _statusType;
    private float _currentValue;
    private float _maxValue;
    private float _lowStateThreshold;

    private bool hasFiredLowStateEvent = false;

    // Cache the very start value, just in case
    private float _startingValue;

    public Status(StatusConstants constants)
    {
        _statusType = constants.statusType;
        _maxValue = constants.maxValue;
        _currentValue = _maxValue;
        _startingValue = _maxValue;
        _lowStateThreshold = constants.lowStateThreshold;
    }
    
    public bool ModifyValue(float val)
    {
        bool returnVal;
        
        if (-val < 0f)
        {
            if (OnValueDecreased != null)
            {
                OnValueDecreased(val);
            }
        }
        else if (-val > 0f)
        {
            OnValueIncreased?.Invoke(-val);
        }
        
        // Depending on whether the value is successfully modified, return True: Modified, False: Not changed
        float modifiedVal = _currentValue - val;
        modifiedVal = Mathf.Clamp(modifiedVal, 0.0f, _maxValue);
        returnVal = Mathf.Abs(_currentValue - modifiedVal) > Mathf.Epsilon;
        _currentValue = modifiedVal;
        
        CheckForEvent();

        return returnVal;
    }

    public void SetValue(float val)
    {
        if (val > _currentValue)
        {
            OnValueIncreased?.Invoke(val - _currentValue);
        }
        
        _currentValue = val;
        _currentValue = Mathf.Clamp(_currentValue, 0.0f, _maxValue);

        CheckForEvent();
    }
    
    public void IncreaseMaxValue(float val, bool shouldRefill = false)
    {
        _maxValue += val;
        if (shouldRefill)
            _currentValue = _maxValue;
    }

    public void SetMaxValue(float val, bool shouldRefill = true)
    {
        _maxValue = val;
        if (shouldRefill)
            _currentValue = _maxValue;
    }

    private void CheckForEvent()
    {
        if ( _currentValue < _lowStateThreshold )
        {
            _isLowState = true;
            if (OnEnteringLowState != null && !hasFiredLowStateEvent)
            {
                OnEnteringLowState();
                hasFiredLowStateEvent = true;
            }
        }
        else
        {
            if (OnExitingLowState != null && _isLowState)
            {
                OnExitingLowState();
                hasFiredLowStateEvent = false;
            }
            _isLowState = false;
        }

        if (Mathf.Abs(_currentValue) <= 0.001f)
        {
            if (OnDepleting != null && !_isDepleted)
                OnDepleting();
            _isDepleted = true;
        }
        else
        {
            if (OnRecovering != null && _isDepleted)
                OnRecovering();
            _isDepleted = false;
        }
    }

    public float GetValue()
    {
        return _currentValue;
    }

    public float GetMaxValue()
    {
        return _maxValue;
    }
    
    
    
}
