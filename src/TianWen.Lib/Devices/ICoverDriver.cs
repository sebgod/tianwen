﻿using System.Threading;
using System;

namespace TianWen.Lib.Devices;

public interface ICoverDriver : IDeviceDriver
{
    bool IsCalibrationReady
        => CoverState is not CoverStatus.Error and not CoverStatus.Moving
        && CalibratorState is not CalibratorStatus.NotReady and not CalibratorStatus.NotPresent and not CalibratorStatus.Error;

    /// <summary>
    /// Returns true if cover started opening.
    /// </summary>
    /// <returns></returns>
    bool Open();

    /// <summary>
    /// Returns true if cover started closing.
    /// </summary>
    /// <returns></returns>
    bool Close();

    bool CalibratorOn(int brightness);

    bool CalibratorOff();

    /// <summary>
    /// Returns the current calibrator brightness in the range 0 (completely off) to <see cref="MaxBrightness"/> (fully on).
    /// </summary>
    int Brightness { get; }

    /// <summary>
    /// Maximum brightness value for <see cref="CalibratorOn(int)"/>, will be -1 if unknown.
    /// </summary>
    int MaxBrightness { get; }

    CoverStatus CoverState { get; }

    /// <summary>
    /// Returns the state of the calibration device, if present, otherwise returns <see cref="CalibratorStatus.NotPresent"/>.
    /// </summary>
    CalibratorStatus CalibratorState { get; }

    /// <summary>
    /// Higher-level function to turn of the calibrator (if present)
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    bool TurnOffCalibratorAndWait(CancellationToken cancellationToken)
    {
        var calState = CalibratorState;

        if (calState is CalibratorStatus.NotPresent or CalibratorStatus.Off)
        {
            return true;
        }
        else if (calState is CalibratorStatus.Unknown or CalibratorStatus.Error)
        {
            return false;
        }
        else if (CalibratorOff())
        {
            var tries = 0;
            while ((calState = CalibratorState) == CalibratorStatus.NotReady
                && !cancellationToken.IsCancellationRequested
                && ++tries < MAX_FAILSAFE)
            {
                External.Sleep(TimeSpan.FromSeconds(3));
            }

            return calState is CalibratorStatus.Off;
        }
        else
        {
            return false;
        }
    }
}
