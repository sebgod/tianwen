/*

MIT License

Copyright (c) 2018 Andy Galasso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TianWen.Lib.Astrometry.PlateSolve;
using TianWen.Lib.Imaging;

namespace TianWen.Lib.Devices.Guider;

public interface IGuider : IDeviceDriver
{
    /// <summary>
    /// Start guiding with the given settling parameters. PHD2 takes care of looping exposures,
    /// guide star selection, and settling. Call <see cref="GetSettleProgressAsync(CancellationToken)"/> periodically to see when settling
    /// is complete.
    /// </summary>
    /// <param name="settlePixels">Settle threshold in pixels.</param>
    /// <param name="settleTime">Settle time in seconds.</param>
    /// <param name="settleTimeout">Settle timeout in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask GuideAsync(double settlePixels, double settleTime, double settleTimeout, CancellationToken cancellationToken);

    /// <summary>
    /// Dither guiding with the given dither amount and settling parameters. Call <see cref="GetSettleProgressAsync(CancellationToken)"/> or <see cref="IsSettlingAsync(CancellationToken)"/>
    /// periodically to see when settling is complete.
    /// </summary>
    /// <param name="ditherPixels">Dither amount in pixels.</param>
    /// <param name="settlePixels">Settle threshold in pixels.</param>
    /// <param name="settleTime">Settle time in seconds.</param>
    /// <param name="settleTimeout">Settle timeout in seconds.</param>
    /// <param name="raOnly">If true, dither only in RA direction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DitherAsync(double ditherPixels, double settlePixels, double settleTime, double settleTimeout, bool raOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if PHD2 is currently looping exposures.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if looping, otherwise false.</returns>
    ValueTask<bool> IsLoopingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if guider is currently in the process of settling after a Guide or Dither.
    /// A simplified version of <see cref="GetSettleProgressAsync(CancellationToken)"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if settling is in progress.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command failed.</exception>
    ValueTask<bool> IsSettlingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if settling is in progress and additional information in <paramref name="settleProgress"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Settle progress information.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command failed.</exception>
    ValueTask<SettleProgress?> GetSettleProgressAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the guider statistics since guiding started. Frames captured while settling is in progress
    /// are excluded from the stats.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guide statistics.</returns>
    /// <exception cref="GuiderException">Throws if not connected.</exception>
    ValueTask<GuideStats?> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop looping and guiding.
    /// </summary>
    /// <param name="timeout">Timeout after which an exception is thrown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued (see timeout).</exception>
    ValueTask StopCaptureAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start looping exposures.
    /// </summary>
    /// <param name="timeout">Timeout after which looping attempt is cancelled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if looping.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<bool> LoopAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the guider pixel scale in arc-seconds per pixel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pixel scale of the guiding camera in arc-seconds per pixel.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<double> PixelScaleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns camera size in width and height (pixels).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Camera dimensions in pixels.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<(int Width, int Height)?> CameraFrameSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// On completion returns the image dimensions of the guiding exposure (if available),
    /// <see cref="PixelScaleAsync(CancellationToken)"/> and <see cref="CameraFrameSizeAsync(CancellationToken)"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Image dimensions if available.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    public async ValueTask<ImageDim?> GetImageDimAsync(CancellationToken cancellationToken)
    {
        if (await CameraFrameSizeAsync(cancellationToken) is var (width, height)
            && await PixelScaleAsync(cancellationToken) is var pixelScale and > 0)
        {
            return new ImageDim(pixelScale, width, height);
        }

        return default;
    }

    /// <summary>
    /// Get the exposure time of each looping exposure.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exposure time.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<TimeSpan> ExposureTimeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a list of the equipment profile names.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of profile names.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<IReadOnlyList<string>> GetEquipmentProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to obtain the active profile, useful for quick self-discovery.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active profile name (if any).</returns>
    ValueTask<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Connect to the specified profile as constructed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask ConnectEquipmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect equipment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask DisconnectEquipmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the AppState and current guide error.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AppState and average distance.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<(string? AppState, double AvgDist)> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if currently guiding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if guiding, otherwise false.</returns>
    /// <exception cref="GuiderException">Throws if not connected.</exception>
    ValueTask<bool> IsGuidingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause guiding (looping exposures continues).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Un-pause guiding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask UnpauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save the current guide camera frame (FITS format), returning the name of the file.
    /// The caller will need to remove the file when done.
    /// It is advisable to use a subfolder of <see cref="System.IO.Path.GetTempPath"/>.
    /// The implementation will copy the output file to <paramref name="outputFolder"/> and delete the temporary file created by the guider.
    /// &#x26A0; <em>This will only work as expected when the guider is on the same host.</em>.
    /// </summary>
    /// <param name="outputFolder">Output folder path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path of the output file if successfully captured.</returns>
    /// <exception cref="GuiderException">Throws if not connected or command could not be issued.</exception>
    ValueTask<string?> SaveImageAsync(string outputFolder, CancellationToken cancellationToken = default);

    const int SETTLE_TIMEOUT_FACTOR = 5;

    /// <summary>
    /// Start guiding loop with a maximum number of tries.
    /// </summary>
    /// <param name="maxTries">Maximum number of tries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if guiding started successfully, otherwise false.</returns>
    public async ValueTask<bool> StartGuidingLoopAsync(int maxTries, CancellationToken cancellationToken = default)
    {
        bool guidingSuccess = false;
        int startGuidingTries = 0;

        var activeProfile = await GetActiveProfileNameAsync(cancellationToken).ConfigureAwait(false) ?? Name;
        while (!guidingSuccess && ++startGuidingTries <= maxTries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var settlePix = 0.3 + (startGuidingTries * 0.2);
                var settleTime = 15 + (startGuidingTries * 5);
                var settleTimeout = settleTime * SETTLE_TIMEOUT_FACTOR;

                External.AppLogger.LogInformation("Start guiding using \"{ProfileName}\", settle pixels: {SettlePix}, settle time: {SettleTime}s, timeout: {SettleTimeout}s.",
                    activeProfile,
                    settlePix,
                    settleTime,
                    settleTimeout
                );
                await GuideAsync(settlePix, settleTime, settleTimeout, cancellationToken).ConfigureAwait(false);

                var failsafeCounter = 0;
                while (await IsSettlingAsync(cancellationToken).ConfigureAwait(false) && failsafeCounter++ < MAX_FAILSAFE && !cancellationToken.IsCancellationRequested)
                {
                    External.Sleep(TimeSpan.FromSeconds(10));
                }

                guidingSuccess = failsafeCounter < MAX_FAILSAFE && await IsGuidingAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                else if (!guidingSuccess)
                {
                    await External.SleepAsync(TimeSpan.FromMinutes(startGuidingTries), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                External.AppLogger.LogError(e, "Exception while on try #{StartGuidingTries} checking if \"{ProfileName}\" is guiding.", startGuidingTries, activeProfile);
                guidingSuccess = false;
            }
        }

        return guidingSuccess;
    }

    /// <summary>
    /// Dither and wait for settling to complete.
    /// </summary>
    /// <param name="ditherPixel">Dither amount in pixels.</param>
    /// <param name="settlePixel">Settle threshold in pixels.</param>
    /// <param name="settleTime">Settle time.</param>
    /// <param name="processQueuedWork">Function to process queued work.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if dithering and settling completed successfully, otherwise false.</returns>
    public async ValueTask<bool> DitherWaitAsync(double ditherPixel, double settlePixel, TimeSpan settleTime, Func<ValueTask<TimeSpan>> processQueuedWork, CancellationToken cancellationToken = default)
    {
        var settleTimeout = settleTime * SETTLE_TIMEOUT_FACTOR;

        External.AppLogger.LogInformation("Start dithering pixel={DitherPixel} settlePixel={SettlePixel} settleTime={SettleTime}, timeout={SettleTimeout}",
            ditherPixel, settlePixel, settlePixel, settleTimeout);

        await DitherAsync(ditherPixel, settlePixel, settleTime.TotalSeconds, settleTimeout.TotalSeconds, cancellationToken: cancellationToken);

        var overslept = TimeSpan.Zero;
        var elapsed = await processQueuedWork().ConfigureAwait(false);

        for (var i = 0; i < SETTLE_TIMEOUT_FACTOR; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                External.AppLogger.LogWarning("Cancellation requested, all images in queue written to disk, abort image acquisition and quit imaging loop");
                return false;
            }
            else
            {
                overslept = await External.SleepWithOvertimeAsync(settleTime, elapsed + overslept, cancellationToken);
            }

            if (await GetSettleProgressAsync(cancellationToken).ConfigureAwait(false) is { } settleProgress)
            {
                if (settleProgress.Done)
                {
                    if (settleProgress?.Error is { Length: > 0 } error)
                    {
                        External.AppLogger.LogError("Settling after dithering failed with: {ErrorMessage} pixel={SettlePx} dist={SettleDistance}",
                            error, settleProgress.SettlePx, settleProgress.Distance);
                        return false;
                    }
                    else if (settleProgress is not null)
                    {
                        External.AppLogger.LogInformation("Settling finished: settle pixel={SettlePx} dist={SettleDistance}", settleProgress.SettlePx, settleProgress.Distance);
                        return true;
                    }
                    else
                    {
                        External.AppLogger.LogError("Settling failed with no specific error message, assume dithering failed.");
                        return false;
                    }
                }
                else if (settleProgress.Error is { Length: > 0 } error)
                {
                    External.AppLogger.LogError("Settling after dithering failed with: {ErrorMessage}", error);
                    return false;
                }
                else
                {
                    External.AppLogger.LogInformation("Settle still in progress: settle pixel={SettlePx} dist={SettleDistance}", settleProgress.SettlePx, settleProgress.Distance);
                }
            }
            else
            {
                External.AppLogger.LogError("Failed to retrieve settling progress");
            }
        }

        External.AppLogger.LogError("Settling timeout after {SettleTimeout:c}, aborting dithering.", settleTimeout);
        return false;
    }

    /// <summary>
    /// Plate solve the guider image.
    /// </summary>
    /// <param name="plateSolver">Plate solver instance.</param>
    /// <param name="raJ2000">RA in J2000.</param>
    /// <param name="decJ2000">DEC in J2000.</param>
    /// <param name="timeout">Timeout duration.</param>
    /// <param name="searchRadius">Search radius.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>World coordinate system (WCS) if successful.</returns>
    public async ValueTask<WCS?> PlateSolveGuiderImageAsync(
        IPlateSolver plateSolver,
        double raJ2000,
        double decJ2000,
        TimeSpan timeout,
        double? searchRadius,
        CancellationToken cancellationToken = default
    )
    {
        await LoopAsync(timeout, cancellationToken).ConfigureAwait(false);
        var outputFolder = External.CreateSubDirectoryInOutputFolder("Guider").FullName;
        if (await SaveImageAsync(outputFolder, cancellationToken) is { Length: > 0 } file)
        {
            var dim = await GetImageDimAsync(cancellationToken).ConfigureAwait(false);

            return await plateSolver.SolveFileAsync(
                file,
                dim,
                searchOrigin: new WCS(raJ2000, decJ2000),
                searchRadius: searchRadius ?? 7,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            throw new GuiderException("Could not obtain guider image");
        }
    }

    /// <summary>
    /// Event that is triggered when an exception occurs.
    /// </summary>
    event EventHandler<GuidingErrorEventArgs>? GuidingErrorEvent;

    /// <summary>
    /// Event that is triggered when the application state changes.
    /// </summary>
    event EventHandler<GuiderStateChangedEventArgs>? GuiderStateChangedEvent;
}
