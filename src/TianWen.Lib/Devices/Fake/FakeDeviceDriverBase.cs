﻿using System;
using System.Threading;

namespace TianWen.Lib.Devices.Fake;

internal abstract class FakeDeviceDriverBase(FakeDevice fakeDevice, IExternal external) : IDeviceDriver
{
    private readonly FakeDevice _fakeDevice = fakeDevice;
    private bool _connected;

    protected IExternal _external = external;
    protected bool disposedValue;

    public string Name => _fakeDevice.DisplayName;

    public string? Description => $"Fake device that implements a fake {DriverType}";

    public string? DriverInfo => Description;

    public string? DriverVersion => typeof(IDeviceDriver).Assembly.GetName().Version?.ToString() ?? "1.0";

    public bool Connected => Volatile.Read(ref _connected);

    public void Connect() => SetConnect(true);

    public void Disconnect() => SetConnect(false);

    private void SetConnect(bool connected)
    {
        Volatile.Write(ref _connected, connected);

        DeviceConnectedEvent?.Invoke(this, new DeviceConnectedEventArgs(connected));
    }

    public abstract DeviceType DriverType { get; }

    public IExternal External { get; } = external;

    public event EventHandler<DeviceConnectedEventArgs>? DeviceConnectedEvent;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}