using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeviceManager : MonoBehaviour
{
    public static DeviceManager Instance;

    public List<InputDevice> assignedDevices = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPrimaryDevice(InputDevice device)
    {
        if (!assignedDevices.Contains(device))
            assignedDevices.Add(device);
    }

    public List<InputDevice> GetAvailableDevices()
    {
        List<InputDevice> list = new();

        if (Keyboard.current != null && !assignedDevices.Contains(Keyboard.current))
            list.Add(Keyboard.current);

        foreach (var g in Gamepad.all)
            if (!assignedDevices.Contains(g))
                list.Add(g);

        return list;
    }

    public void AssignDevice(InputDevice device)
    {
        if (!assignedDevices.Contains(device))
            assignedDevices.Add(device);
    }
}