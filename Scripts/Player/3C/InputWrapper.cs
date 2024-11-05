using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputWrapper : MonoBehaviour
{
    [HideInInspector] public InputMaster inputMaster;
    [HideInInspector] public Vector3 rawMoveDir { get; private set; }
    [HideInInspector] public Vector2 rawGameplayLookDir { get; private set; }
    [HideInInspector] public Vector2 rawCameraModeLookDir { get; private set; }
    [HideInInspector] public float rawZoomVal { get; private set; }

    private void Awake()
    {
        inputMaster = new InputMaster();
        inputMaster.Enable();
    }

    private void Update()
    {
        ReadMoveDir();
        ReadLookDir();
        ReadZoomVal();
    }

    private void ReadMoveDir()
    {
        Vector2 readVal = inputMaster.Gameplay.Move.ReadValue<Vector2>();
        Vector3 combDir = new Vector3(readVal.x, inputMaster.Gameplay.FlyDescend.ReadValue<float>(), readVal.y);

        rawMoveDir = combDir;
    }

    private void ReadLookDir()
    {
        Vector2 readVal = inputMaster.Gameplay.Look.ReadValue<Vector2>();
        rawGameplayLookDir = readVal;

        readVal = inputMaster.CameraMode.CameraModeLook.ReadValue<Vector2>();
        rawCameraModeLookDir = readVal;
    }

    private void ReadZoomVal()
    {
        float readVal = inputMaster.Gameplay.CameraZoom.ReadValue<float>();

        rawZoomVal = readVal;
    }




}
