﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using ZWOptical.SDK;
using static ZWOptical.SDK.ASICamera2;
using static ZWOptical.SDK.EAFFocuser1_6;
using static ZWOptical.SDK.EFW1_7;

namespace TianWen.Lib.Devices.ZWO;

internal class ZWODeviceSource : IDeviceSource<ZWODevice>
{
    static readonly Dictionary<DeviceType, bool> _supportedDeviceTypes = [];

    static ZWODeviceSource()
    {
        CheckSupport(DeviceType.Camera, ASIGetSDKVersion);
        CheckSupport(DeviceType.Focuser, EAFGetSDKVersion);
        CheckSupport(DeviceType.FilterWheel, EFWGetSDKVersion);
    }

    private static void CheckSupport(DeviceType deviceType, Func<Version> sdkVersion)
    {
        bool isSupported;
        try
        {
            isSupported = sdkVersion().Major > 0;
        }
        catch
        {
            isSupported = false;
        }

        _supportedDeviceTypes[deviceType] = isSupported;
    }

    public ValueTask<bool> CheckSupportAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_supportedDeviceTypes.Count > 0);

    public ValueTask DiscoverAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public IEnumerable<DeviceType> RegisteredDeviceTypes { get; } = _supportedDeviceTypes
        .Where(p => p.Value)
        .Select(p => p.Key)
        .ToList();

    public IEnumerable<ZWODevice> RegisteredDevices(DeviceType deviceType)
    {
        if (_supportedDeviceTypes.TryGetValue(deviceType, out var isSupported) && isSupported)
        {
            return deviceType switch
            {
                DeviceType.Camera => ListCameras(),
                DeviceType.Focuser => ListEAFs(),
                DeviceType.FilterWheel => ListEFWs(),
                _ => throw new ArgumentException($"Device type {deviceType} not implemented!", nameof(deviceType))
            };
        }
        else
        {
            return [];
        }
    }

    static IEnumerable<ZWODevice> ListCameras() => ListDevice<ASI_CAMERA_INFO>(DeviceType.Camera);

    static IEnumerable<ZWODevice> ListEAFs() => ListDevice<EAF_INFO>(DeviceType.Focuser);

    static IEnumerable<ZWODevice> ListEFWs() => ListDevice<EFW_INFO>(DeviceType.FilterWheel);

    static IEnumerable<ZWODevice> ListDevice<TDeviceInfo>(DeviceType deviceType) where TDeviceInfo : struct, IZWODeviceInfo
    {
        var ids = new HashSet<int>();

        var cameraIterator = new DeviceIterator<TDeviceInfo>();

        foreach (var (camId, deviceInfo) in cameraIterator)
        {
            if (!ids.Contains(camId) && deviceInfo.Open())
            {
                try
                {
                    if (deviceInfo.SerialNumber?.ToString() is { Length: > 0 } serialNumber)
                    {
                        yield return new ZWODevice(deviceType, serialNumber, deviceInfo.Name);
                    }
                    else if (deviceInfo.IsUSB3Device && deviceInfo.CustomId is { Length: > 0 } customId)
                    {
                        yield return new ZWODevice(deviceType, customId, deviceInfo.Name);
                    }
                    else
                    {
                        yield return new ZWODevice(deviceType, deviceInfo.Name, deviceInfo.Name);
                    }

                    ids.Add(camId);
                }
                finally
                {
                    _ = deviceInfo.Close();
                }
            }
        }
    }
}
