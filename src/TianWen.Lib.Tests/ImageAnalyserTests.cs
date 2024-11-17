﻿using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TianWen.Lib.Astrometry.Focus;
using TianWen.Lib.Devices;
using TianWen.Lib.Imaging;
using TianWen.Lib.Stat;
using Xunit;
using Xunit.Abstractions;

namespace TianWen.Lib.Tests;

public class ImageAnalyserTests(ITestOutputHelper testOutputHelper)
{
    const string PlateSolveTestFile = nameof(PlateSolveTestFile);
    const string PHD2SimGuider = nameof(PHD2SimGuider);

    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;
    private readonly IImageAnalyser _imageAnalyser = new ImageAnalyser();

    [Theory]
    [InlineData(PlateSolveTestFile, 10f)]
    [InlineData(PlateSolveTestFile, 15f)]
    public async Task GivenFileNameWhenWritingImageAndReadingBackThenItIsIdentical(string name, float snrMin)
    {
        // given
        var image = await SharedTestData.ExtractGZippedFitsImageAsync(name);
        var fullPath = Path.Combine(Path.GetTempPath(), $"roundtrip_{Guid.NewGuid():D}.fits");
        var expectedStars = await _imageAnalyser.FindStarsAsync(image, snrMin: snrMin);

        try
        {
            // when
            image.WriteToFitsFile(fullPath);

            // then
            File.Exists(fullPath).ShouldBeTrue();
            Image.TryReadFitsFile(fullPath, out var readoutImage).ShouldBeTrue();
            readoutImage.Width.ShouldBe(image.Width);
            readoutImage.Height.ShouldBe(image.Height);
            readoutImage.BitDepth.ShouldBe(image.BitDepth);
            readoutImage.ImageMeta.Instrument.ShouldBe(image.ImageMeta.Instrument);
            readoutImage.MaxValue.ShouldBe(image.MaxValue);
            readoutImage.ImageMeta.ExposureStartTime.ShouldBe(image.ImageMeta.ExposureStartTime);
            readoutImage.ImageMeta.ExposureDuration.ShouldBe(image.ImageMeta.ExposureDuration);
            var starsFromImage = await _imageAnalyser.FindStarsAsync(image, snrMin: snrMin);

            starsFromImage.ShouldBe(expectedStars, ignoreOrder: true);
        }
        finally
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }

    [Theory]
    [InlineData(PlateSolveTestFile)]
    public async Task GivenOnDiskFitsFileWithImageWhenTryingReadImageItSucceeds(string name)
    {
        // given
       

        ImageDim dim;
        SharedTestData.TestFileImageDimAndCoords.TryGetValue(name, out var dimAndCoords).ShouldBeTrue();

        (dim, _) = dimAndCoords;

        // when
        Image? image = null;
        await Should.NotThrowAsync(async () => image = await SharedTestData.ExtractGZippedFitsImageAsync(name));

        // then
        image.ShouldNotBeNull();
        image.Width.ShouldBe(dim.Width);
        image.Height.ShouldBe(dim.Height);
    }

    [Theory]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 10f, 89)]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 20f, 28)]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 30f, 13)]
    [InlineData("RGGB_frame_bx0_by0_top_down", 30f, 2786, 5000)]
    [InlineData("RGGB_frame_bx0_by0_top_down", 10f, 3046, 5000)]
    public async Task GivenImageFileAndMinSNRWhenFindingStarsThenTheyAreFound(string name, float snrMin, int expectedStars, int? maxStars = null)
    {
        // given
        var image = await SharedTestData.ExtractGZippedFitsImageAsync(name);

        // when
        var sw = Stopwatch.StartNew();
        var actualStars = await _imageAnalyser.FindStarsAsync(image, snrMin, maxStars ?? 500);
        _testOutputHelper.WriteLine("Testing image {0} took {1} ms", name, sw.ElapsedMilliseconds);

        // then
        actualStars.ShouldNotBeEmpty();
        actualStars.Count.ShouldBe(expectedStars);
    }

    [Theory]
    [InlineData(10, 22)]
    [InlineData(15, 6)]
    [InlineData(20, 3)]
    public async Task GivenCameraImageDataWhenConvertingToImageThenStarsCanBeFound(int snr_min, int expectedStars)
    {
        // given
        const int Width = 1280;
        const int Height = 960;
        const BitDepth BitDepth = BitDepth.Int16;
        const int BlackLevel = 1;
        var expTime = TimeSpan.FromSeconds(42);
        var fileName = $"image_data_snr-{snr_min}_stars-{expectedStars}";
        var int16WxHData = await SharedTestData.ExtractGZippedImageData(fileName, Width, Height);
        var imageMeta = new ImageMeta(fileName, DateTime.UtcNow, expTime, FrameType.Light, "", 2.4f, 2.4f, 190, -1, Filter.None, 1, 1, float.NaN, SensorType.Monochrome, 0, 0, RowOrder.TopDown, float.NaN, float.NaN);

        // when
        var imageData = Float32HxWImageData.FromWxHImageData(int16WxHData);
        var image = ICameraDriver.DataToImage(imageData, BitDepth, BlackLevel, imageMeta);
        var stars = await _imageAnalyser.FindStarsAsync(image, snrMin: snr_min);

        // then
        image.ShouldNotBeNull();
        image.Height.ShouldBe(Height);
        image.Width.ShouldBe(Width);
        image.BitDepth.ShouldBe(BitDepth);
        stars.ShouldNotBeNull().Count.ShouldBe(expectedStars);
    }


    [Theory]
    [InlineData(PlateSolveTestFile, 5, 3, 11, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 9.5, 3, 6, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 20, 3, 2, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 30, 3, 1, 1242, 220, 38)]
    [InlineData(PHD2SimGuider, 2, 3, 10)]
    [InlineData(PHD2SimGuider, 5, 3, 10)]
    [InlineData(PHD2SimGuider, 5, 10, 10)]
    [InlineData(PHD2SimGuider, 20, 3, 6)]
    [InlineData(PHD2SimGuider, 30, 3, 2)]
    [InlineData(PHD2SimGuider, 30, 10, 2)]
    public async Task GivenFitsFileWhenAnalysingThenMedianHFDAndFWHMIsCalculated(string name, float snr_min, int max_retries, int expected_stars, params int[] sampleStar)
    {
        // when
        var image = await SharedTestData.ExtractGZippedFitsImageAsync(name);
        var result = await _imageAnalyser.FindStarsAsync(image, snrMin: snr_min, maxIterations: max_retries);

        // then
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(expected_stars);
        result.ShouldAllBe(p => p.SNR >= snr_min);

        if (sampleStar is { Length: 3 })
        {
            var x = sampleStar[0];
            var y = sampleStar[1];
            var snr = sampleStar[2];
            result.ShouldContain(p => p.XCentroid > x - 1 && p.XCentroid < x + 1 && p.YCentroid > y - 1 && p.YCentroid < y + 1 && p.SNR > snr);
        }
        else if (sampleStar is { Length: > 0 })
        {
            Assert.Fail($"Sample star needs to be exactly 3 elements (x, y, snr), but only {sampleStar.Length} where given.");
        }
    }

    [Theory]
    [InlineData(SampleKind.HFD, AggregationMethod.Average, 28208, 28211, 1, 1, 1, 10f, 20, 2, 130)]
    [InlineData(SampleKind.HFD, AggregationMethod.Average, 28227, 28231, 1, 1, 1, 10f, 20, 2, 140)]
    [InlineData(SampleKind.HFD, AggregationMethod.Average, 28208, 28231, 1, 1, 1, 10f, 20, 2, 130)]
    public async Task GivenFocusSamplesWhenSolvingAHyperboleIsFound(SampleKind kind, AggregationMethod aggregationMethod, int focusStart, int focusEndIncl, int focusStepSize, int sampleCount, int filterNo, float snrMin, int maxIterations, int expectedSolutionAfterSteps, int expectedMinStarCount)
    {
        // given
        var sampleMap = new MetricSampleMap(kind, aggregationMethod);

        // when
        for (int fp = focusStart; fp <= focusEndIncl; fp += focusStepSize)
        {
            for (int cs = 1; cs <= sampleCount; cs++)
            {
                var sw = Stopwatch.StartNew();
                var image = await SharedTestData.ExtractGZippedFitsImageAsync($"fp{fp}-cs{cs}-ms{sampleCount}-fw{filterNo}");
                var extractImageElapsed = sw.ElapsedMilliseconds;
                var stars = await _imageAnalyser.FindStarsAsync(image, snrMin: snrMin);
                var findStarsElapsed = sw.ElapsedMilliseconds - extractImageElapsed;
                var median = _imageAnalyser.MapReduceStarProperty(stars, sampleMap.Kind, AggregationMethod.Median);
                var calcMedianElapsed = sw.ElapsedMilliseconds - findStarsElapsed;
                var (solution, maybeMinPos, maybeMaxPos) = _imageAnalyser.SampleStarsAtFocusPosition(sampleMap, fp, median, stars.Count, maxFocusIterations: maxIterations);
                var addSampleElapsed = sw.ElapsedMilliseconds - calcMedianElapsed;

                _testOutputHelper.WriteLine($"focuspos={fp} stars={stars.Count} median={median} solution={solution} minPos={maybeMinPos} maxPos={maybeMaxPos} time (ms): image={extractImageElapsed} find stars={findStarsElapsed} median={calcMedianElapsed} sample={addSampleElapsed}");

                median.ShouldBeGreaterThan(1f);
                stars.Count.ShouldBeGreaterThan(expectedMinStarCount);

                if (fp - focusStart >= expectedSolutionAfterSteps)
                {
                    (_, _, _, double error, int iterations) = solution.ShouldNotBeNull();
                    var minPos = maybeMinPos.ShouldNotBeNull();
                    var maxPos = maybeMaxPos.ShouldNotBeNull();

                    maxPos.ShouldBeGreaterThan(minPos);
                    minPos.ShouldBe(focusStart);
                    iterations.ShouldBeLessThanOrEqualTo(maxIterations);
                    error.ShouldBeLessThan(1);
                }
                else
                {
                    solution.ShouldBeNull();
                }
            }
        }
    }
}
