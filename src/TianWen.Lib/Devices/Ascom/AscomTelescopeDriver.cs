﻿using System;
using System.Collections.Generic;
using TianWen.Lib.Astrometry;

namespace TianWen.Lib.Devices.Ascom;

public class AscomTelescopeDriver : AscomDeviceDriverBase, IMountDriver
{
    private Dictionary<TrackingSpeed, DriveRate> _trackingSpeedMapping = [];

    public AscomTelescopeDriver(AscomDevice device, IExternal external) : base(device, external)
    {
        DeviceConnectedEvent += AscomTelescopeDriver_DeviceConnectedEvent;
    }

    private void AscomTelescopeDriver_DeviceConnectedEvent(object? sender, DeviceConnectedEventArgs e)
    {
        if (e.Connected && _comObject is { } obj)
        {
            _trackingSpeedMapping = DriveRatesToTrackingSpeeds(EnumerateProperty<DriveRate>(obj.TrackingRates));

            CanSetTracking = obj.CanSetTracking is bool canSetTracking && canSetTracking;
            CanSetSideOfPier = obj.CanSetPierSide is bool canSetSideOfPier && canSetSideOfPier;
            CanPark = obj.CanPark is bool canPark && canPark;
            CanUnpark = obj.CanUnpark is bool canUnpark && canUnpark;
            CanSetPark = obj.CanSetPark is bool canSetPark && canSetPark;
            CanSlew = obj.CanSlew is bool canSlew && canSlew;
            CanSlewAsync = obj.CanSlewAsync is bool canSlewAsync && canSlewAsync;
            CanSync = obj.CanSync is bool canSync && canSync;
            CanPulseGuide = obj.CanPulseGuide is bool canPulseGuide && canPulseGuide;
            CanSetRightAscensionRate = obj.CanSetRightAscensionRate is bool canSetRightAscensionRate && canSetRightAscensionRate;
            CanSetDeclinationRate = obj.CanSetDeclinationRate is bool canSetDeclinationRate && CanSetDeclinationRate;
            CanSetGuideRates = obj.CanSetGuideRates is bool canSetGuideRates && canSetGuideRates;
        }
    }

    internal static Dictionary<TrackingSpeed, DriveRate> DriveRatesToTrackingSpeeds(IEnumerable<DriveRate> driveRates)
    {
        var trackingSpeedMapping = new Dictionary<TrackingSpeed, DriveRate>();

        foreach (var driveRate in driveRates)
        {
            var trackingSpeed = DriveRateToTrackingSpeed(driveRate);

            if (trackingSpeed != TrackingSpeed.None)
            {
                trackingSpeedMapping[trackingSpeed] = driveRate;
            }
        }

        return trackingSpeedMapping;
    }

    private static TrackingSpeed DriveRateToTrackingSpeed(DriveRate driveRate)
    {
        return driveRate switch
        {
            DriveRate.Sidereal => TrackingSpeed.Sidereal,
            DriveRate.Solar => TrackingSpeed.Solar,
            DriveRate.Lunar => TrackingSpeed.Lunar,
            _ => TrackingSpeed.None
        };
    }

    public bool SlewRaDecAsync(double ra, double dec)
    {
        if (_comObject?.CanSlewAsync is bool canSlewAsync && canSlewAsync)
        {
            try
            {
                _comObject.SlewToCoordinatesAsync(ra, dec);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public IReadOnlyCollection<TrackingSpeed> TrackingSpeeds => _trackingSpeedMapping.Keys;

    public TrackingSpeed TrackingSpeed
    {
        get => _comObject?.TrackingRate is DriveRate driveRate ? DriveRateToTrackingSpeed(driveRate) : TrackingSpeed.None;
        set
        {
            if (_trackingSpeedMapping.TryGetValue(value, out var driveRate) && _comObject is { } obj)
            {
                obj.TrackingRate = driveRate;
            }
        }
    }

    public bool AtHome => _comObject?.AtHome is bool atHome && atHome;

    public bool AtPark => _comObject?.AtPark is bool atPark && atPark;

    public bool IsSlewing => _comObject?.Slewing is bool slewing && slewing;

    public double SiderealTime => _comObject?.SiderealTime is double siderealTime ? siderealTime : double.NaN;

    public bool TimeIsSetByUs { get; private set; }

    public DateTime? UTCDate
    {
        get
        {
            try
            {
                return Connected && _comObject?.UTCDate is DateTime utcDate ? utcDate : default;
            }
            catch
            {
                return default;
            }
        }

        set
        {
            if (_comObject is { } obj && value is { } utcDate)
            {
                try
                {
                    obj.UTCDate = utcDate;
                    TimeIsSetByUs = true;
                }
                catch
                {
                    TimeIsSetByUs = false;
                }
            }
            else
            {
                TimeIsSetByUs = false;
            }
        }
    }

    public bool Tracking
    {
        get => _comObject?.Tracking is bool tracking && tracking;
        set
        {
            if (_comObject is { } obj)
            {
                if (obj.CanSetTracking is false)
                {
                    throw new InvalidOperationException("Driver does not support setting tracking");
                }
                obj.Tracking = value;
            }
        }
    }

    public bool CanSetTracking { get; private set; }

    public bool CanSetSideOfPier { get; private set; }

    public bool CanPark { get; private set; }

    public bool CanUnpark { get; private set; }

    public bool CanSetPark { get; private set; }

    public bool CanSlew { get; private set; }

    public bool CanSlewAsync { get; private set; }

    public bool CanSync { get; private set; }

    public bool CanPulseGuide { get; private set; }

    public bool CanSetRightAscensionRate { get; private set; }

    public bool CanSetDeclinationRate { get; private set; }

    public bool CanSetGuideRates { get; private set; }

    public PierSide SideOfPier
    {
        get => _comObject?.SideOfPier is int sop ? (PierSide)sop : PierSide.Unknown;
        set
        {
            if (CanSetSideOfPier && _comObject is { } obj)
            {
                obj.SideOfPier = value;
            }
            else
            {
                throw new InvalidOperationException("Cannot set side of pier to: " + value);
            }
        }
    }

    public PierSide DestinationSideOfPier(double ra, double dec)
        => _comObject?.DestinationSideOfPier(ra, dec) is int dsop ? (PierSide)dsop : PierSide.Unknown;

    public EquatorialCoordinateType EquatorialSystem => Connected && _comObject?.EquatorialSystem is int es ? (EquatorialCoordinateType)es : EquatorialCoordinateType.Other;

    public AlignmentMode? Alignment => Connected && _comObject?.AlignmentMode is int am ? (AlignmentMode)am : null as AlignmentMode?;

    public double RightAscension => _comObject?.RightAscension is double ra ? ra : double.NaN;

    public double Declination => _comObject?.Declination is double dec ? dec : double.NaN;

    public double SiteElevation
    {
        get => _comObject?.SiteElevation is double siteElevation ? siteElevation : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.SiteElevation = value;
            }
        }
    }

    public double SiteLatitude
    {
        get => _comObject?.SiteLatitude is double siteLatitude ? siteLatitude : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.SiteLatitude = value;
            }
        }
    }

    public double SiteLongitude
    {
        get => _comObject?.SiteLongitude is double siteLongitude ? siteLongitude : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.SiteLongitude = value;
            }
        }
    }

    public bool IsPulseGuiding => _comObject?.IsPulseGuiding is bool isPulseGuiding && isPulseGuiding;

    public double RightAscensionRate
    {
        get => _comObject?.RightAscensionRate is double rightAscensionRate ? rightAscensionRate : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.RightAscensionRate = value;
            }
        }
    }

    public double DeclinationRate
    {
        get => _comObject?.DeclinationRate is double declinationRate ? declinationRate : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.DeclinationRate = value;
            }
        }
    }

    public double GuideRateRightAscension
    {
        get => _comObject?.GuideRateRightAscension is double guideRateRightAscension ? guideRateRightAscension : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.GuideRateRightAscension = value;
            }
        }
    }

    public double GuideRateDeclination
    {
        get => _comObject?.GuideRateDeclination is double guideRateDeclination ? guideRateDeclination : double.NaN;
        set
        {
            if (_comObject is { } obj)
            {
                obj.GuideRateDeclination = value;
            }
        }
    }

    public bool Park()
    {
        if (Connected && CanPark && _comObject is { } obj)
        {
            obj.Park();
            return true;
        }

        return false;
    }
    public bool Unpark()
    {
        if (Connected && CanUnpark && _comObject is { } obj)
        {
            obj.Unpark();
            return !AtPark;
        }

        return false;
    }

    public bool PulseGuide(GuideDirection direction, TimeSpan duration)
    {
        if (Connected && CanPulseGuide && _comObject is { } obj)
        {
            obj.PulseGuide(direction, (int)duration.TotalMilliseconds);

            return true;
        }

        return false;
    }

    public bool SyncRaDec(double ra, double dec)
    {
        // prevent syncs on other side of meridian (most mounts do not support that).
        if (Connected && CanSync && Tracking && !AtPark && DestinationSideOfPier(ra, dec) == SideOfPier && _comObject is { } obj)
        {
            obj.SyncToCoordinates(ra, dec);
            return true;
        }

        return false;
    }

    public bool AbortSlew()
    {
        if (Connected && _comObject is { } obj)
        {
            obj.AbortSlew();

            return true;
        }

        return false;
    }
}