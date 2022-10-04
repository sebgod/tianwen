﻿using Astap.Lib.Devices;

namespace Astap.Lib.Plan;

public class Switch : ControllableDeviceBase<ISwitchDriver>
{
    public Switch(DeviceBase device) : base(device)
    {
        // calls base
    }

    protected override void Driver_DeviceConnectedEvent(object? sender, DeviceConnectedEventArgs e)
    {

    }
}