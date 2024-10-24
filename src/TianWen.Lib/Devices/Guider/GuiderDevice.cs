﻿using System;

namespace TianWen.Lib.Devices.Guider;

public record class GuiderDevice(Uri DeviceUri) : DeviceBase(DeviceUri)
{
    public GuiderDevice(DeviceType deviceType, string deviceId, string displayName)
        : this(new Uri($"{deviceType}://{typeof(GuiderDevice).Name}/{deviceId}#{displayName}"))
    {

    }

    protected override IDeviceDriver? NewInstanceFromDevice(IExternal external) => DeviceType switch
    {
        DeviceType.DedicatedGuiderSoftware => new PHD2GuiderDriver(this, external),
        _ => null
    };
}