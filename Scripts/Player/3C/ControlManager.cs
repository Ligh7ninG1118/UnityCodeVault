using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public enum ControlDeviceType
{
    KeyboardAndMouse,
    Xbox,
    Playstation,
    Switch
};

[Serializable]
public struct InputSpriteMapping
{
    public string inputKey;
    public int spriteIndex;
}

public class ControlManager : MonoBehaviour
{
    public static ControlManager Instance { get; private set; }

    [Header("Default Control Settings")]
    public float mouseSensGameplay = 0.15f;
    public float mouseSensCameraMode = 0.3f;
    public float joystickSensGameplay = 2f;
    public float joystickSensCameraMode = 1.0f;
    public float joystickSensAccelTime = 1.2f;
    public bool InvertXAxis = false;
    public bool InvertYAxis = false;
    private float globalRumbleIntensity = 1.0f;



    [Header("Util")]
    public List<InputSpriteMapping> inputSpriteMappingList;
    private Dictionary<string, int> inputSpriteMappingDict;


    private PlayerInput playerInput;
    public ControlDeviceType currControlScheme;
    public event Action<ControlDeviceType> OnControlSchemeChanges;
    public event Action<string> OnActionMapChanges;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this.gameObject);
        playerInput = FindObjectOfType<PlayerInput>();
        DontDestroyOnLoad(this);

        inputSpriteMappingDict = new Dictionary<string, int>();
        foreach (InputSpriteMapping i in inputSpriteMappingList)
        {
            try
            {
                inputSpriteMappingDict.Add(i.inputKey, i.spriteIndex);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Mapping with key " + i.inputKey + " already existed in the dictionary. Check mapping in ControlManager");
            }
        }
    }

    public void ControlSchemeChangedEventHandler(PlayerInput playerInput)
    {
        ControlDeviceType controlDeviceType = ToControlDeviceType(playerInput.currentControlScheme);
        currControlScheme = controlDeviceType;
        OnControlSchemeChanges?.Invoke(controlDeviceType);
    }

    public void SwtichActionMap(string actionMap)
    {
        playerInput.SwitchCurrentActionMap(actionMap);
        OnActionMapChanges?.Invoke(actionMap);
    }

    public string GetCurrentActionMap()
    {
        return playerInput.currentActionMap.ToString();
    }

    public void SetSenstivity(SensType controlType, float val)
    {
        switch (controlType)
        {
            case SensType.MouseGameplay:
                mouseSensGameplay = val;
                break;
            case SensType.MouseCameraMode:
                mouseSensCameraMode = val;
                break;
            case SensType.JoystickGameplay:
                joystickSensGameplay = val;
                break;
            case SensType.JoystickCameraMode:
                joystickSensCameraMode = val;
                break;
        }

        if (PlayerController.Instance != null)
        {
            // if current device is KB&M, and changed type is mouse
            if (currControlScheme == 0 && (int)controlType < 2)
            {
                PlayerController.Instance.thirdPersonCam.m_YAxis.m_MaxSpeed = mouseSensGameplay / 100.0f;
                PlayerController.Instance.thirdPersonCam.m_XAxis.m_MaxSpeed = mouseSensGameplay;
                PlayerController.Instance.cameraMode.UpdatePanningSpeed(mouseSensCameraMode, ControlDeviceType.KeyboardAndMouse);
            }
            // if current device xbox or ps, and changed type is joystick
            if ((int)currControlScheme >= 1 && (int)controlType >= 2)
            {
                PlayerController.Instance.thirdPersonCam.m_YAxis.m_MaxSpeed = joystickSensGameplay;
                PlayerController.Instance.thirdPersonCam.m_XAxis.m_MaxSpeed = joystickSensGameplay * 100.0f;
                PlayerController.Instance.cameraMode.UpdatePanningSpeed(joystickSensCameraMode, ControlDeviceType.Xbox);
            }
        }
    }

    public void SetAxisInvert(AxisType axisType, bool val)
    {
        switch (axisType)
        {
            case AxisType.XAxis:
                InvertXAxis = val;
                break;
            case AxisType.YAxis:
                InvertYAxis = val;
                break;
        }

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.thirdPersonCam.m_XAxis.m_InvertInput = InvertXAxis;
            PlayerController.Instance.cameraMode.cinemachinePOV.m_HorizontalAxis.m_InvertInput = InvertXAxis;
            PlayerController.Instance.thirdPersonCam.m_YAxis.m_InvertInput = !InvertYAxis;
            PlayerController.Instance.cameraMode.cinemachinePOV.m_VerticalAxis.m_InvertInput = !InvertYAxis;
        }
    }

    public IEnumerator RumblePulse(float low, float high, float duration, float interval = 0.0f, int repeats = 1)
    {
        // Get current (last used) gamepad
        var gamepadRef = Gamepad.current;

        if (gamepadRef == null)
            yield break;

        // Check if gamepad is current in use, break if not
        if (!playerInput.devices.Any(d => d.deviceId == gamepadRef.deviceId))
            yield break;

        for (int i = 0; i < repeats; i++)
        {
            gamepadRef.SetMotorSpeeds(low * globalRumbleIntensity, high * globalRumbleIntensity);
            yield return new WaitForSecondsRealtime(duration);
            StopRumble(gamepadRef);
            yield return new WaitForSecondsRealtime(interval);
        }

        // just in case
        StopRumble(gamepadRef);
    }

    public IEnumerator RumbleAlternate(float intensity, bool startingLeft, float duration, float interval, int repeats)
    {
        // Get current (last used) gamepad
        var gamepadRef = Gamepad.current;

        if (gamepadRef == null)
            yield break;

        // Check if gamepad is current in use, break if not
        if (!playerInput.devices.Any(d => d.deviceId == gamepadRef.deviceId))
            yield break;

        bool isLeftside = startingLeft;
        for (int i = 0; i < repeats * 2; i++)
        {
            if (isLeftside) gamepadRef.SetMotorSpeeds(intensity * globalRumbleIntensity, 0f);
            else gamepadRef.SetMotorSpeeds(0f, intensity * globalRumbleIntensity);
            isLeftside = !isLeftside;
            yield return new WaitForSecondsRealtime(duration);
            StopRumble(gamepadRef);
            yield return new WaitForSecondsRealtime(interval);
        }

        // just in case
        StopRumble(gamepadRef);
    }

    public void StopRumble(Gamepad gamepadRef)
    {
        if (gamepadRef == null)
            gamepadRef = Gamepad.current;
        if (gamepadRef == null)
            return;
        gamepadRef.SetMotorSpeeds(0f, 0f);
    }

    public InputActionAsset GetActionAssets()
    {
        return playerInput.actions;
    }

    public string TranslateInputStringToSpriteRef(string inputMapping)
    {
        //Debug.Log(inputMapping);
        string dictStr = currControlScheme.ToString() + "_" + inputMapping;
        int index = inputSpriteMappingDict[dictStr];
        if (index == -1) Debug.Log("ControlManager::TranslateInputStringToSpriteRef(): Cannot find index for " + inputMapping);
        return "<sprite=" + index.ToString() + ">";
    }

    private ControlDeviceType ToControlDeviceType(string s)
    {
        switch (s)
        {
            case "KeyboardAndMouse":
                return ControlDeviceType.KeyboardAndMouse;
            case "Xbox":
                return ControlDeviceType.Xbox;
            case "Playstation":
                return ControlDeviceType.Playstation;
            case "Switch":
                return ControlDeviceType.Switch;
            default:
                return ControlDeviceType.KeyboardAndMouse;
        }
    }
}
