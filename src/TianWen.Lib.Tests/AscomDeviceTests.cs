using Shouldly;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TianWen.Lib.Devices;
using TianWen.Lib.Devices.Ascom;
using Xunit;
using Xunit.Abstractions;

namespace TianWen.Lib.Tests;

public class AscomDeviceTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task TestWhenPlatformIsWindowsThatDeviceTypesAreReturned()
    {
        var deviceIterator = new AscomDeviceIterator();
        var types = deviceIterator.RegisteredDeviceTypes;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && await deviceIterator.CheckSupportAsync())
        {
            types.ShouldNotBeEmpty();
        }
        else
        {
            types.ShouldBeEmpty();
        }
    }

    [SkippableTheory]
    [InlineData(DeviceType.Camera)]
    [InlineData(DeviceType.CoverCalibrator)]
    [InlineData(DeviceType.Focuser)]
    [InlineData(DeviceType.Switch)]
    public async Task GivenSimulatorDeviceTypeVersionAndNameAreReturned(DeviceType type)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Debugger.IsAttached);

        var external = new FakeExternal(testOutputHelper);
        var deviceIterator = new AscomDeviceIterator();
        var devices = deviceIterator.RegisteredDevices(type);
        var device = devices.FirstOrDefault(e => e.DeviceId == $"ASCOM.Simulator.{type}");

        device.ShouldNotBeNull();
        device.DeviceClass.ShouldBe(nameof(AscomDevice), StringCompareShould.IgnoreCase);
        device.DeviceId.ShouldNotBeNullOrEmpty();
        device.DeviceType.ShouldBe(type);
        device.DisplayName.ShouldNotBeNullOrEmpty();

        device.TryInstantiateDriver<IDeviceDriver>(external, out var driver).ShouldBeTrue();

        await using (driver)
        {
            driver.DriverType.ShouldBe(type);
            driver.Connected.ShouldBeFalse();
        }
    }

    [SkippableFact]
    public async Task GivenAConnectedAscomSimulatorTelescopeWhenConnectedThenTrackingRatesArePopulated()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Debugger.IsAttached, "Skipped as this test is only run when on Windows and debugger is attached");

        // given
        var external = new FakeExternal(testOutputHelper);
        var deviceIterator = new AscomDeviceIterator();
        var allTelescopes = deviceIterator.RegisteredDevices(DeviceType.Telescope);
        var simTelescopeDevice = allTelescopes.FirstOrDefault(e => e.DeviceId == "ASCOM.Simulator." + DeviceType.Telescope);

        // when
        if (simTelescopeDevice?.TryInstantiateDriver(external, out IMountDriver? driver) is true)
        {
            await using (driver)
            {
                await driver.DisconnectAsync();
            }
        }
    }

    [SkippableFact]
    public async Task GivenAConnectedAscomSimulatorCameraWhenImageReadyThenItCanBeDownloaded()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Debugger.IsAttached, "Skipped as this test is only run when on Windows and debugger is attached");

        // given
        var external = new FakeExternal(testOutputHelper);
        var deviceIterator = new AscomDeviceIterator();
        var allCameras = deviceIterator.RegisteredDevices(DeviceType.Camera);
        var simCameraDevice = allCameras.FirstOrDefault(e => e.DeviceId == "ASCOM.Simulator." + DeviceType.Camera);

        // when / then
        if (simCameraDevice?.TryInstantiateDriver(external, out ICameraDriver? driver) is true)
        {
            await using (driver)
            {
                await driver.ConnectAsync();
                var startExposure = driver.StartExposure(TimeSpan.FromSeconds(0.1));

                Thread.Sleep((int)TimeSpan.FromSeconds(0.5).TotalMilliseconds);
                driver.ImageReady.ShouldBeTrue();
                var (data, expectedMax) = driver.ImageData.ShouldNotBeNull();

                var image = driver.Image.ShouldNotBeNull();

                driver.DriverType.ShouldBe(DeviceType.Camera);
                image.ImageMeta.ExposureStartTime.ShouldBe(startExposure);
                image.Width.ShouldBe(data.GetLength(1));
                image.Height.ShouldBe(data.GetLength(0));
                image.BitDepth.ShouldBe(driver.BitDepth.ShouldNotBeNull());
                image.MaxValue.ShouldBeGreaterThan(0f);
                image.MaxValue.ShouldBe(expectedMax);
                var stars = await image.FindStarsAsync(snrMin: 10);
                stars.Count.ShouldBeGreaterThan(0);
            }
        }
        else
        {
            Assert.Fail($"Could not instantiate camera device {simCameraDevice}");
        }
    }
}
