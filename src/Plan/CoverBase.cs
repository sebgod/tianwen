﻿using Astap.Lib.Devices;

namespace Astap.Lib.Plan
{
    public abstract class CoverBase<TDevice, TDriver> : ControllableDeviceBase<TDevice, TDriver>
        where TDevice : DeviceBase
        where TDriver : IDeviceDriver
    {
        public CoverBase(TDevice device)
            : base(device)
        {

        }

        public abstract bool? IsOpen { get; }
    }
}