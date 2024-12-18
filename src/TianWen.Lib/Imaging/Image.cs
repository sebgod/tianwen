﻿using CommunityToolkit.HighPerformance;
using nom.tam.fits;
using nom.tam.util;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TianWen.Lib.Devices;
using static TianWen.Lib.Stat.StatisticsHelper;

namespace TianWen.Lib.Imaging;

public sealed class Image(float[,] data, int width, int height, BitDepth bitDepth, float maxVal, float blackLevel, ImageMeta imageMeta)
{
    public int Width => width;
    public int Height => height;
    public BitDepth BitDepth => bitDepth;
    public float MaxValue => maxVal;
    /// <summary>
    /// Black level or offset value, defaults to 0 if unknown
    /// </summary>
    public float BlackLevel => blackLevel;
    /// <summary>
    /// Image metadata such as instrument, exposure time, focal length, pixel size, ...
    /// </summary>
    public ImageMeta ImageMeta => imageMeta;

    const int HeaderIntSize = 6;
    public static async ValueTask<Image?> FromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[HeaderIntSize * sizeof(int)];
        await stream.ReadExactlyAsync(buffer, cancellationToken);

        if (buffer[0] != (byte)'I' || buffer[1] != (byte)'m')
        {
            throw new InvalidDataException("Stream does not have a valid file magic");
        }
        var dataIsLittleEndian = buffer[2] == 'L';

        if (dataIsLittleEndian != BitConverter.IsLittleEndian)
        {
            for (var i = 1; i < HeaderIntSize; i++)
            {
                Array.Reverse(buffer, i * sizeof(int), sizeof(int));
            }
        }

        if (buffer[3] != (byte)'1')
        {
            throw new InvalidDataException($"Unsupported image version {(char)buffer[3]}");
        }

        var ints = buffer.AsMemory().Cast<byte, int>().ToArray();
        var width = ints[1];
        var height = ints[2];
        var bitDepth = (BitDepth)ints[3];
        var maxVal = BitConverter.Int32BitsToSingle(ints[4]);
        var blackLevel = BitConverter.Int32BitsToSingle(ints[5]);

        var imageSize = width * height;
        var dataSize = imageSize * sizeof(float);

        var byteData = new byte[dataSize];
        await stream.ReadExactlyAsync(byteData, cancellationToken);

        if (dataIsLittleEndian != BitConverter.IsLittleEndian)
        {
            for (var i = 0; i < imageSize; i++)
            {
                Array.Reverse(byteData, i * sizeof(float), sizeof(float));
            }
        }
        var data = byteData.AsMemory().Cast<byte, float>().AsMemory2D(height, width).ToArray();

        var imageMeta = await JsonSerializer.DeserializeAsync(stream, ImageJsonSerializerContext.Default.ImageMeta, cancellationToken);

        return new Image(data, width, height, bitDepth, maxVal, blackLevel, imageMeta);
    }

    public async Task WriteStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var magic = (BitConverter.IsLittleEndian ? "ImL1"u8 : "ImB1"u8).ToArray();
        await stream.WriteAsync(magic, cancellationToken);

        var header = new int[HeaderIntSize - 1]; // substract one for the file magic
        header[0] = Width;
        header[1] = Height;
        header[2] = (int)BitDepth;
        header[3] = BitConverter.SingleToInt32Bits(MaxValue);
        header[4] = BitConverter.SingleToInt32Bits(BlackLevel);

        for (var i = 0; i < header.Length; i++)
        {
            await stream.WriteAsync(BitConverter.GetBytes(header[i]), cancellationToken);
        }

        await stream.WriteAsync(data.AsMemory().Cast<float, byte>(), cancellationToken);

        await JsonSerializer.SerializeAsync(stream, ImageMeta, ImageJsonSerializerContext.Default.ImageMeta, cancellationToken);
    }

    public static bool TryReadFitsFile(string fileName, [NotNullWhen(true)] out Image? image)
    {
        using var bufferedReader = new BufferedFile(fileName, FileAccess.ReadWrite, FileShare.Read, 1000 * 2088);
        return TryReadFitsFile(new Fits(bufferedReader, fileName.EndsWith(".gz")), out image);
    }

    public static bool TryReadFitsFile(Fits fitsFile, [NotNullWhen(true)] out Image? image)
    {
        var hdu = fitsFile.ReadHDU();
        if ((hdu?.Axes?.Length) != 2
            || hdu.Data is not ImageData imageData
            || imageData.DataArray is not object[] heightArray
            || heightArray.Length == 0
            || !(BitDepthEx.FromValue(hdu.BitPix) is { } bitDepth)
        )
        {
            image = default;
            return false;
        }

        var height = hdu.Axes[0];
        var width = hdu.Axes[1];
        var exposureStartTime = new DateTime(hdu.ObservationDate.Ticks, DateTimeKind.Utc);
        var maybeExpTime = hdu.Header.GetDoubleValue("EXPTIME", double.NaN);
        var maybeExposure = hdu.Header.GetDoubleValue("EXPOSURE", double.NaN);
        var exposureDuration = TimeSpan.FromSeconds(new double[] { maybeExpTime, maybeExpTime, 0.0 }.First(x => !double.IsNaN(x)));
        var instrument = hdu.Instrument;
        var telescope = hdu.Telescope;
        var equinox = hdu.Equinox;
        var pixelSizeX = hdu.Header.GetFloatValue("XPIXSZ", float.NaN);
        var pixelSizeY = hdu.Header.GetFloatValue("YPIXSZ", float.NaN);
        var xbinning = hdu.Header.GetIntValue("XBINNING", 1);
        var ybinning = hdu.Header.GetIntValue("YBINNING", 1);
        var blackLevel = hdu.Header.GetFloatValue("BLKLEVEL", 0f);
        var pixelScale = hdu.Header.GetFloatValue("PIXSCALE", float.NaN);
        var focalLength = hdu.Header.GetIntValue("FOCALLEN", -1);
        var focusPos = hdu.Header.GetIntValue("FOCUSPOS", -1);
        var filterName = hdu.Header.GetStringValue("FILTER");
        var ccdTemp = hdu.Header.GetFloatValue("CCD-TEMP", float.NaN);
        var colorType = hdu.Header.GetStringValue("COLORTYP");
        var bayerPattern = hdu.Header.GetStringValue("BAYERPAT");
        var bayerOffsetX = hdu.Header.GetIntValue("BAYOFFX", 0);
        var bayerOffsetY = hdu.Header.GetIntValue("BAYOFFY", 0);
        var rowOrder = RowOrderEx.FromFITSValue(hdu.Header.GetStringValue("ROWORDER")) ?? RowOrder.TopDown;
        var frameType = FrameTypeEx.FromFITSValue(hdu.Header.GetStringValue("FRAMETYP") ?? hdu.Header.GetStringValue("IMAGETYP")) ?? FrameType.None;
        var filter = string.IsNullOrWhiteSpace(filterName) ? Filter.None : new Filter(filterName);
        var bzero = (float)hdu.BZero;
        var bscale = (float)hdu.BScale;
        var sensorType = SensorTypeEx.FromFITSValue(bayerPattern, colorType);
        var latitude = hdu.Header.GetFloatValue("LATITUDE", float.NaN);
        var longitude = hdu.Header.GetFloatValue("LONGITUDE", float.NaN);

        var elementType = Type.GetTypeCode(heightArray[0].GetType().GetElementType());

        var imgArray = new float[height, width];
        Span2D<float> imgArray2d = imgArray.AsSpan2D();
        Span<float> scratchRow = stackalloc float[Math.Min(256, width)];

        var quot = Math.DivRem(width, scratchRow.Length, out var rem);
        var maxVal = (float)hdu.MaximumValue;
        bool needsMaxValRecalc = double.IsNaN(maxVal) || maxVal is <= 0;
        if (needsMaxValRecalc)
        {
            maxVal = float.MinValue;
        }

        switch (elementType)
        {
            case TypeCode.Byte:
                for (int h = 0; h < height; h++)
                {
                    var byteWidthArray = (byte[])heightArray[h];
                    var row = imgArray2d.GetRowSpan(h);
                    var sourceIndex = 0;
                    for (int i = 0; i < quot; i++)
                    {
                        for (int w = 0; w < scratchRow.Length; w++)
                        {
                            var val = bscale * byteWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        sourceIndex += scratchRow.Length;
                        scratchRow.CopyTo(row);
                        row = row[scratchRow.Length..];
                    }
                    if (rem > 0)
                    {
                        // copy rest
                        for (int w = 0; w < rem; w++)
                        {
                            var val = bscale * byteWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        scratchRow[..rem].CopyTo(row);
                    }
                }
                break;

            case TypeCode.Int16:
                for (int h = 0; h < height; h++)
                {
                    var shortWidthArray = (short[])heightArray[h];
                    var row = imgArray2d.GetRowSpan(h);
                    var sourceIndex = 0;
                    for (int i = 0; i < quot; i++)
                    {
                        for (int w = 0; w < scratchRow.Length; w++)
                        {
                            var val = bscale * shortWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        sourceIndex += scratchRow.Length;
                        scratchRow.CopyTo(row);
                        row = row[scratchRow.Length..];
                    }
                    if (rem > 0)
                    {
                        // copy rest
                        for (int w = 0; w < rem; w++)
                        {
                            var val = bscale * shortWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        scratchRow[..rem].CopyTo(row);
                    }
                }
                break;

            case TypeCode.Int32:
                for (int h = 0; h < height; h++)
                {
                    var intWidthArray = (int[])heightArray[h];
                    var row = imgArray2d.GetRowSpan(h);
                    var sourceIndex = 0;
                    for (int i = 0; i < quot; i++)
                    {
                        for (int w = 0; w < scratchRow.Length; w++)
                        {
                            var val = bscale * intWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        sourceIndex += scratchRow.Length;
                        scratchRow.CopyTo(row);
                        row = row[scratchRow.Length..];
                    }
                    if (rem > 0)
                    {
                        // copy rest
                        for (int w = 0; w < rem; w++)
                        {
                            var val = bscale * intWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        scratchRow[..rem].CopyTo(row);
                    }
                }
                break;

            case TypeCode.Single:
                for (int h = 0; h < height; h++)
                {
                    var floatWidthArray = (float[])heightArray[h];
                    var row = imgArray2d.GetRowSpan(h);
                    var sourceIndex = 0;
                    for (int i = 0; i < quot; i++)
                    {
                        for (int w = 0; w < scratchRow.Length; w++)
                        {
                            var val = bscale * floatWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        sourceIndex += scratchRow.Length;
                        scratchRow.CopyTo(row);
                        row = row[scratchRow.Length..];
                    }
                    if (rem > 0)
                    {
                        // copy rest
                        for (int w = 0; w < rem; w++)
                        {
                            var val = bscale * floatWidthArray[sourceIndex + w] + bzero;
                            scratchRow[w] = val;
                            if (needsMaxValRecalc)
                            {
                                maxVal = MathF.Max(maxVal, val);
                            }
                        }
                        scratchRow[..rem].CopyTo(row);
                    }
                }
                break;

            default:
                image = null;
                return false;
        }

        var imageMeta = new ImageMeta(
            instrument,
            exposureStartTime,
            exposureDuration,
            frameType,
            telescope,
            pixelSizeX,
            pixelSizeY,
            focalLength,
            focusPos,
            filter,
            xbinning,
            ybinning,
            ccdTemp,
            sensorType,
            bayerOffsetX,
            bayerOffsetY,
            rowOrder,
            latitude,
            longitude
        );
        image = new Image(imgArray, width, height, bitDepth, maxVal, blackLevel, imageMeta);
        return true;
    }

    public void WriteToFitsFile(string fileName)
    {
        var fits = new Fits();
        object[] jaggedArray;
        int bzero;
        bool dataIsInt;
        switch (bitDepth)
        {
            case BitDepth.Int8:
                var jaggedByteArray = new byte[height][];
                bzero = 0;
                dataIsInt = true;
                for (var h = 0; h < height; h++)
                {
                    var row = new byte[width];
                    for (var w = 0; w < width; w++)
                    {
                        row[w] = (byte)data[h, w];
                    }
                    jaggedByteArray[h] = row;
                }
                jaggedArray = jaggedByteArray;
                break;

            case BitDepth.Int16:
                var jaggedShortArray = new short[height][];
                bzero = 32768;
                dataIsInt = true;
                for (var h = 0; h < height; h++)
                {
                    var row = new short[width];
                    for (var w = 0; w < width; w++)
                    {
                        row[w] = (short)(data[h, w] - bzero);
                    }
                    jaggedShortArray[h] = row;
                }
                jaggedArray = jaggedShortArray;
                break;

            case BitDepth.Float32:
                var jaggedFloatArray = new float[height][];
                bzero = 0;
                dataIsInt = false;
                for (var h = 0; h < height; h++)
                {
                    jaggedFloatArray[h] = data.GetRowSpan(h).ToArray();
                }
                jaggedArray = jaggedFloatArray;
                break;

            default:
                throw new NotSupportedException($"Bits per pixel {bitDepth} is not supported");
        }
        var basicHdu = FitsFactory.HDUFactory(jaggedArray);
        basicHdu.Header.Bitpix = (int)bitDepth;
        AddHeaderValueIfHasValue("BZERO", bzero, "offset data range to that of unsigned short");
        AddHeaderValueIfHasValue("BSCALE", 1, "default scaling factor");
        AddHeaderValueIfHasValue("BSCALE", 1, "default scaling factor");
        AddHeaderValueIfHasValue("BLKLEVEL", BlackLevel, "", isDataValue: true);
        AddHeaderValueIfHasValue("XBINNING", imageMeta.BinX, "");
        AddHeaderValueIfHasValue("YBINNING", imageMeta.BinY, "");
        AddHeaderValueIfHasValue("XPIXSZ", imageMeta.PixelSizeX, "");
        AddHeaderValueIfHasValue("YPIXSZ", imageMeta.PixelSizeX, "");
        AddHeaderValueIfHasValue("DATE-OBS", FitsDate.GetFitsDateString(imageMeta.ExposureStartTime.UtcDateTime), "UT");
        AddHeaderValueIfHasValue("EXPTIME", imageMeta.ExposureDuration.TotalSeconds, "seconds");
        AddHeaderValueIfHasValue("IMAGETYP", imageMeta.FrameType, "");
        AddHeaderValueIfHasValue("FRAMETYP", imageMeta.FrameType, "");
        AddHeaderValueIfHasValue("DATAMAX", maxVal, "");
        AddHeaderValueIfHasValue("INSTRUME", imageMeta.Instrument, "");
        AddHeaderValueIfHasValue("TELESCOP", imageMeta.Telescope, "");
        AddHeaderValueIfHasValue("ROWORDER", imageMeta.RowOrder, "");
        AddHeaderValueIfHasValue("CCD-TEMP", imageMeta.CCDTemperature, "Celsius");
        AddHeaderValueIfHasValue("BAYOFFX", imageMeta.BayerOffsetX, "");
        AddHeaderValueIfHasValue("BAYOFFY", imageMeta.BayerOffsetY, "");
        AddHeaderValueIfHasValue("LATITUDE", imageMeta.Latitude, "degrees");
        AddHeaderValueIfHasValue("LONGITUDE", imageMeta.Longitude, "degrees");
        if (imageMeta.SensorType is SensorType.RGGB)
        {
            // TODO support other Bayer patterns
            AddHeaderValueIfHasValue("BAYERPAT", "RGGB", "");
            AddHeaderValueIfHasValue("COLORTYP", "RGGB", "");
        }
        fits.AddHDU(basicHdu);

        using var bufferedWriter = new BufferedFile(fileName, FileAccess.ReadWrite, FileShare.Read, 1000 * 2088);
        fits.Write(bufferedWriter);
        bufferedWriter.Flush();
        bufferedWriter.Close();

        void AddHeaderValueIfHasValue<T>(string key, T value, string comment = "", bool isDataValue = false)
        {
            var card = value switch
            {
                float f when !float.IsNaN(f) => new HeaderCard(key, f, comment),
                float f when isDataValue && dataIsInt => new HeaderCard(key, (int)f, comment),
                double d when !double.IsNaN(d) => new HeaderCard(key, d, comment),
                double d when isDataValue && dataIsInt => new HeaderCard(key, (int)d, comment),
                int i => new HeaderCard(key, i, comment),
                long l => new HeaderCard(key, l, comment),
                string s => new HeaderCard(key, s, comment),
                bool b =>  new HeaderCard(key, b, comment),
                FrameType ft => new HeaderCard(key, ft.ToFITSValue(), comment),
                RowOrder ro => new HeaderCard(key, ro.ToFITSValue(), comment),
                _ => null
            };

            if (card is not null)
            {
                basicHdu.Header.AddCard(card);
            }
        }
    }

    const int BoxRadius = 14;
    const float HfdFactor = 1.5f;
    const int MaxScaledRadius = (int)(HfdFactor * BoxRadius) + 1;
    static readonly ImmutableArray<BitMatrix> StarMasks;
    static Image()
    {
        var starMasksBuilder = ImmutableArray.CreateBuilder<BitMatrix>(MaxScaledRadius);
        for (var radius = 1; radius < MaxScaledRadius; radius++)
        {
            MakeStarMask(radius, out var mask);
            starMasksBuilder.Add(mask);
        }

        StarMasks = starMasksBuilder.ToImmutable();
    }

    static void MakeStarMask(int radius, out BitMatrix starMask)
    {
        var diameter = radius << 1;
        var radius_squared = radius * radius;
        starMask = new BitMatrix(diameter + 1, diameter + 1);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius_squared)
                {
                    int pixelX = radius + x;
                    int pixelY = radius + y;
                    if (pixelX >= 0 && pixelX <= diameter && pixelY >= 0 && pixelY <= diameter)
                    {
                        starMask[pixelY, pixelX] = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find background, noise level, number of stars and their HFD, FWHM, SNR, flux and centroid.
    /// </summary>
    /// <param name="snrMin">S/N ratio threshold for star detection</param>
    /// <param name="maxStars"></param>
    /// <param name="maxRetries"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public async Task<StarList> FindStarsAsync(float snrMin = 20f, int maxStars = 500, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        const int ChunkSize = 2 * MaxScaledRadius;
        const float HalfChunkSizeInv = 1.0f / 2.0f * ChunkSize;

        if (imageMeta.SensorType is not SensorType.Monochrome)
        {
            return await DebayerOSCToSyntheticLuminance().FindStarsAsync(snrMin, maxStars, maxRetries, cancellationToken);
        }

        var (background, star_level, noise_level, hist_threshold) = Background();

        var detection_level = MathF.Max(3.5f * noise_level, star_level); /* level above background. Start with a high value */
        var retries = maxRetries;

        if (background >= hist_threshold || background <= 0)  /* abnormal file */
        {
            return new StarList([]);
        }

        var starList = new ConcurrentBag<ImagedStar>();
        var img_star_area = new BitMatrix(height, width);

        // we use interleaved processing of rows (so that we do not have to lock to protect the bitmatrix
        var halfChunkCount = (int)Math.Ceiling(height * HalfChunkSizeInv);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken };

        do
        {
            for (var i = 0; i <= 1; i++)
            {
                await Parallel.ForAsync(0, halfChunkCount, parallelOptions, async (halfChunk, cancellationToken) =>
                {
                    await Task.Run(() =>
                    {
                        var chunk = 2 * halfChunk + i;
                        var chunkEnd = Math.Min(height, (chunk + 1) * ChunkSize);
                        for (var fitsY = chunk * ChunkSize; fitsY < chunkEnd; fitsY++)
                        {
                            for (var fitsX = 0; fitsX < width; fitsX++)
                            {
                                // new star. For analyse used sigma is 5, so not too low.
                                if (data[fitsY, fitsX] - background > detection_level
                                    && !img_star_area[fitsY, fitsX]
                                    && AnalyseStar(fitsX, fitsY, BoxRadius, out var star)
                                    && star.HFD is > 0.8f and <= BoxRadius * 2 /* at least 2 pixels in size */
                                    && star.SNR >= snrMin
                                )
                                {
                                    starList.Add(star);
                                    var scaledHfd = HfdFactor * star.HFD;
                                    var r = (int)MathF.Round(scaledHfd); /* radius for marking star area, factor 1.5 is chosen emperiacally. */
                                    var xc_offset = (int)MathF.Round(star.XCentroid - scaledHfd); /* star center as integer */
                                    var yc_offset = (int)MathF.Round(star.YCentroid - scaledHfd);

                                    var mask = StarMasks[Math.Max(r - 1, 0)];

                                    img_star_area.SetRegionClipped(yc_offset, xc_offset, mask);
                                }
                            }
                        }
                    }, cancellationToken);
                });
            }

            /* In principle not required. Try again with lower detection level */
            if (detection_level <= 7 * noise_level)
            {
                retries = -1; /* stop */
            }
            else
            {
                retries--;
                detection_level = MathF.Max(6.999f * noise_level, MathF.Min(30 * noise_level, detection_level * 6.999f / 30)); /* very high -> 30 -> 7 -> stop.  Or  60 -> 14 -> 7.0. Or for very short exposures 3.5 -> stop */
            }
        } while (starList.Count < maxStars && retries > 0);/* reduce detection level till enough stars are found. Note that faint stars have less positional accuracy */

        return new StarList(starList);
    }

    /// <summary>
    /// This function assumes that data has discrete integer values.
    /// TODO: support normalized floating point data.
    /// </summary>
    /// <returns></returns>
    public ImageHistogram Histogram()
    {
        var threshold = (uint)(maxVal * 0.91);
        var histogram = new uint[threshold];

        var hist_total = 0u;
        var count = 1; /* prevent divide by zero */
        var total_value = 0f;

        for (var h = 0; h <= height - 1; h++)
        {
            for (var w = 0; w <= width - 1; w++)
            {
                var value = data[h, w];
                var valueAsInt = (int)MathF.Round(value);

                // ignore black overlap areas and bright stars
                if (value >= 1 && value < threshold)
                {
                    histogram[valueAsInt]++; // calculate histogram
                    hist_total++;
                    total_value += value;
                    count++;
                }
            }
        }

        var hist_mean = 1.0f / count * total_value;

        return new ImageHistogram(histogram, hist_mean, hist_total, threshold);
    }

    /// <summary>
    /// get background and star level from peek histogram
    /// </summary>
    /// <returns>background and star level</returns>
    public (float background, float starLevel, float noise_level, float threshold) Background()
    {
        // get histogram of img_loaded and his_total
        var histogram = Histogram();
        var background = data[0, 0]; // define something for images containing 0 or 65535 only

        // find peak in histogram which should be the average background
        var pixels = 0u;
        var max_range = histogram.Mean;
        uint i;
        // mean value from histogram
        for (i = 1; i <= max_range; i++)
        {
            // find peak, ignore value 0 from oversize
            var histVal = histogram.Histogram[(int)i];
            if (histVal > pixels) // find colour peak
            {
                pixels = histVal;
                background = i;
            }
        }

        // check alternative mean value
        if (histogram.Mean > 1.5f * background) // 1.5 * most common
        {
            background = histogram.Mean; // strange peak at low value, ignore histogram and use mean
        }

        i = (uint)MathF.Ceiling(maxVal);

        var starLevel = 0.0f;
        var above = 0u;

        while (starLevel == 0 && i > background + 1)
        {
            i--;
            if (i < histogram.Histogram.Length)
            {
                above += histogram.Histogram[(int)i];
            }
            if (above > 0.001f * histogram.Total)
            {
                starLevel = i;
            }
        }

        if (starLevel <= background)
        {
            starLevel = background + 1; // no or very few stars
        }
        else
        {
            // star level above background. Important subtract 1 for saturated images. Otherwise no stars are detected
            starLevel = starLevel - background - 1;
        }

        // calculate noise level
        var stepSize = (int)MathF.Round(height / 71.0f); // get about 71x71 = 5000 samples.So use only a fraction of the pixels

        // prevent problems with even raw OSC images
        if (stepSize % 2 == 0)
        {
            stepSize++;
        }

        var sd = 99999.0f;
        float sd_old;
        var iterations = 0;

        // repeat until sd is stable or 7 iterations
        do
        {
            var counter = 1; // never divide by zero

            sd_old = sd;
            var fitsY = 15;
            while (fitsY <= height - 1 - 15)
            {
                var fitsX = 15;
                while (fitsX <= width - 1 - 15)
                {
                    var value = data[fitsY, fitsX];
                    // not an outlier, noise should be symmetrical so should be less then twice background
                    if (value < background * 2 && value != 0)
                    {
                        // ignore outliers after first run
                        if (iterations == 0 || (value - background) <= 3 * sd_old)
                        {
                            var bgSub = value - background;
                            sd += bgSub * bgSub;
                            // keep record of number of pixels processed
                            counter++;
                        }
                    }
                    fitsX += stepSize; // skip pixels for speed
                }
                fitsY += stepSize; // skip pixels for speed
            }
            sd = MathF.Sqrt(sd / counter); // standard deviation
            iterations++;
        } while (sd_old - sd >= 0.05f * sd && iterations < 7); // repeat until sd is stable or 7 iterations

        return (background, starLevel, MathF.Round(sd), histogram.Threshold);
    }

    /// <summary>
    /// calculate star HFD and FWHM, SNR, xc and yc are center of gravity.All x, y coordinates in array[0..] positions
    /// </summary>
    /// <param name="x1">x</param>
    /// <param name="y1">y</param>
    /// <param name="boxRadius">box radius</param>
    /// <returns>true if a star was detected</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool AnalyseStar(int x1, int y1, int boxRadius, out ImagedStar star)
    {
        const int maxAnnulusBg = 328; // depends on boxSize <= 50
        Debug.Assert(boxRadius <= 50, nameof(boxRadius) + " should be <= 50 to prevent runtime errors");

        var r1_square = boxRadius * boxRadius; /*square radius*/
        var r2 = boxRadius + 1; /*annulus width plus 1*/
        var r2_square = r2 * r2;

        var valMax = 0.0f;
        float sumVal;
        float bg;
        float sd_bg;

        float xc = float.NaN, yc = float.NaN;
        int r_aperture = -1;

        if (x1 - r2 <= 0 || x1 + r2 >= width - 1 || y1 - r2 <= 0 || y1 + r2 >= height - 1)
        {
            star = default;
            return false;
        }

        Span<float> backgroundScratch = stackalloc float[maxAnnulusBg];
        int backgroundIndex = 0;

        try
        {
            /*calculate the mean outside the the detection area*/
            for (var i = -r2; i <= r2; i++)
            {
                for (var j = -r2; j <= r2; j++)
                {
                    var distance = i * i + j * j; /*working with sqr(distance) is faster then applying sqrt*/
                    /*annulus, circular area outside rs, typical one pixel wide*/
                    if (distance > r1_square && distance <= r2_square)
                    {
                        backgroundScratch[backgroundIndex++] = data[y1 + i, x1 + j];
                    }
                }
            }

            var background = backgroundScratch[..backgroundIndex];
            bg = Median(background);

            /* fill background with offsets */
            for (var i = 0; i < background.Length; i++)
            {
                background[i] = MathF.Abs(background[i] - bg);
            }

            var mad_bg = Median(background); //median absolute deviation (MAD)
            sd_bg = mad_bg * 1.4826f; /* Conversion from mad to sd for a normal distribution. See https://en.wikipedia.org/wiki/Median_absolute_deviation */
            sd_bg = MathF.Max(sd_bg, 1); /* add some value for images with zero noise background. This will prevent that background is seen as a star. E.g. some jpg processed by nova.astrometry.net*/

            bool boxed;
            do /* reduce square annulus radius till symmetry to remove stars */
            {
                // Get center of gravity whithin star detection box and count signal pixels, repeat reduce annulus radius till symmetry to remove stars
                sumVal = 0.0f;
                var sumValX = 0.0f;
                var sumValY = 0.0f;
                var signal_counter = 0;

                for (var i = -boxRadius; i <= boxRadius; i++)
                {
                    for (var j = -boxRadius; j <= boxRadius; j++)
                    {
                        var val = data[y1 + i, x1 + j] - bg;
                        if (val > 3.0f * sd_bg)
                        {
                            sumVal += val;
                            sumValX += val * j;
                            sumValY += val * i;
                            signal_counter++; /* how many pixels are illuminated */
                        }
                    }
                }

                if (sumVal <= 12 * sd_bg)
                {
                    star = default; /*no star found, too noisy */
                    return false;
                }

                var xg = sumValX / sumVal;
                var yg = sumValY / sumVal;

                xc = x1 + xg;
                yc = y1 + yg;
                /* center of gravity found */

                if (xc - boxRadius < 0 || xc + boxRadius > width - 1 || yc - boxRadius < 0 || yc + boxRadius > height - 1)
                {
                    star = default; /* prevent runtime errors near sides of images */
                    return false;
                }

                var rs2_1 = boxRadius + boxRadius + 1;
                boxed = signal_counter >= 2.0f / 9 * (rs2_1 * rs2_1);/*are inside the box 2 of the 9 of the pixels illuminated? Works in general better for solving then ovality measurement as used in the past*/

                if (!boxed)
                {
                    if (boxRadius > 4)
                    {
                        boxRadius -= 2;
                    }
                    else
                    {
                        boxRadius--; /*try a smaller window to exclude nearby stars*/
                    }
                }

                /* check on hot pixels */
                if (signal_counter <= 1)
                {
                    star = default; /*one hot pixel*/
                    return false;
                }
            } while (!boxed && boxRadius > 1); /*loop and reduce aperture radius until star is boxed*/

            boxRadius += 2; /* add some space */

            // Build signal histogram from center of gravity
            Span<int> distance_histogram = stackalloc int[boxRadius + 1]; // this has a fixed upper bound

            for (var i = -boxRadius; i <= boxRadius; i++)
            {
                for (var j = -boxRadius; j <= boxRadius; j++)
                {
                    var distance = (int)MathF.Round(MathF.Sqrt(i * i + j * j)); /* distance from gravity center */
                    if (distance <= boxRadius) /* build histogram for circle with radius boxRadius */
                    {
                        var val = SubpixelValue(xc + i, yc + j) - bg;
                        if (val > 3.0 * sd_bg) /* 3 * sd should be signal */
                        {
                            distance_histogram[distance]++; /* build distance histogram up to circle with diameter rs */

                            if (val > valMax)
                            {
                                valMax = val; /* record the peak value of the star */
                            }
                        }
                    }
                }
            }

            var distance_top_value = 0;
            var histStart = false;
            var illuminated_pixels = 0;
            do
            {
                r_aperture++;
                illuminated_pixels += distance_histogram[r_aperture];
                if (distance_histogram[r_aperture] > 0)
                {
                    histStart = true; /*continue until we found a value>0, center of defocused star image can be black having a central obstruction in the telescope*/
                }

                if (distance_top_value < distance_histogram[r_aperture])
                {
                    distance_top_value = distance_histogram[r_aperture]; /* this should be 2*pi*r_aperture if it is nice defocused star disk */
                }
                /* find a distance where there is no pixel illuminated, so the border of the star image of interest */
            } while (r_aperture < boxRadius && (!histStart || distance_histogram[r_aperture] > 0.1f * distance_top_value));

            if (r_aperture >= boxRadius)
            {
                star = default; /* star is equal or larger then box, abort */
                return false;
            }

            if (r_aperture > 2)
            {
                /* if more than 35% surface is illuminated */
                var r_aperture2_2 = 2 * r_aperture - 2;
                if (illuminated_pixels < 0.35f * (r_aperture2_2 * r_aperture2_2))
                {
                    star = default; /* not a star disk but stars, abort */
                    return false;
                }
            }
        }
        catch
        {
            star = default;
            return false;
        }

        // Get HFD
        var pixel_counter = 0;
        sumVal = 0.0f; // reset
        var sumValR = 0.0f;

        // Get HFD using the aproximation routine assuming that HFD line divides the star in equal portions of gravity:
        for (var i = -r_aperture; i <= r_aperture; i++) /*Make steps of one pixel*/
        {
            for (var j = -r_aperture; j <= r_aperture; j++)
            {
                var val = SubpixelValue(xc + i, yc + j) - bg; /* the calculated center of gravity is a floating point position and can be anywhere, so calculate pixel values on sub-pixel level */
                var r = MathF.Sqrt(i * i + j * j); /* distance from star gravity center */
                sumVal += val;/* sumVal will be star total star flux*/
                sumValR += val * r; /* method Kazuhisa Miyashita, see notes of HFD calculation method, note calculate HFD over square area. Works more accurate then for round area */
                if (val >= valMax * 0.5)
                {
                    pixel_counter++; /* How many pixels are above half maximum */
                }
            }
        }

        var flux = MathF.Max(sumVal, 0.00001f); /* prevent dividing by zero or negative values */
        var hfd = MathF.Max(0.7f, 2 * sumValR / flux);
        var star_fwhm = 2 * MathF.Sqrt(pixel_counter / MathF.PI);/*calculate from surface (by counting pixels above half max) the diameter equals FWHM */
        var snr = flux / MathF.Sqrt(flux + r_aperture * r_aperture * MathF.PI * sd_bg * sd_bg);

        star = new(hfd, star_fwhm, snr, flux, xc, yc);
        return true;
        /*For both bright stars (shot-noise limited) or skybackground limited situations
        snr := signal/noise
        snr := star_signal/sqrt(total_signal)
        snr := star_signal/sqrt(star_signal + sky_signal)
        equals
        snr:=flux/sqrt(flux + r*r*pi* sd^2).

        r is the diameter used for star flux measurement. Flux is the total star flux detected above 3* sd.

        Assuming unity gain ADU/e-=1
        See https://en.wikipedia.org/wiki/Signal-to-noise_ratio_(imaging)
        https://www1.phys.vt.edu/~jhs/phys3154/snr20040108.pdf
        http://spiff.rit.edu/classes/phys373/lectures/signal/signal_illus.html*/


        /*==========Notes on HFD calculation method=================
          Documented this HFD definition also in https://en.wikipedia.org/wiki/Half_flux_diameter
          References:
          https://astro-limovie.info/occultation_observation/halffluxdiameter/halffluxdiameter_en.html       by Kazuhisa Miyashita. No sub-pixel calculation
          https://www.lost-infinity.com/night-sky-image-processing-part-6-measuring-the-half-flux-diameter-hfd-of-a-star-a-simple-c-implementation/
          http://www.ccdware.com/Files/ITS%20Paper.pdf     See page 10, HFD Measurement Algorithm

          HFD, Half Flux Diameter is defined as: The diameter of circle where total flux value of pixels inside is equal to the outside pixel's.
          HFR, half flux radius:=0.5*HFD
          The pixel_flux:=pixel_value - background.

          The approximation routine assumes that the HFD line divides the star in equal portions of gravity:
              sum(pixel_flux * (distance_from_the_centroid - HFR))=0
          This can be rewritten as
             sum(pixel_flux * distance_from_the_centroid) - sum(pixel_values * (HFR))=0
             or
             HFR:=sum(pixel_flux * distance_from_the_centroid))/sum(pixel_flux)
             HFD:=2*HFR

          This is not an exact method but a very efficient routine. Numerical checking with an a highly oversampled artificial Gaussian shaped star indicates the following:

          Perfect two dimensional Gaussian shape with σ=1:   Numerical HFD=2.3548*σ                     Approximation 2.5066, an offset of +6.4%
          Homogeneous disk of a single value  :              Numerical HFD:=disk_diameter/sqrt(2)       Approximation disk_diameter/1.5, an offset of -6.1%

          The approximate routine is robust and efficient.

          Since the number of pixels illuminated is small and the calculated center of star gravity is not at the center of an pixel, above summation should be calculated on sub-pixel level (as used here)
          or the image should be re-sampled to a higher resolution.

          A sufficient signal to noise is required to have valid HFD value due to background noise.

          Note that for perfect Gaussian shape both the HFD and FWHM are at the same 2.3548 σ.
          */


        /*=============Notes on FWHM:=====================
           1)	Determine the background level by the averaging the boarder pixels.
           2)	Calculate the standard deviation of the background.

               Signal is anything 3 * standard deviation above background

           3)	Determine the maximum signal level of region of interest.
           4)	Count pixels which are equal or above half maximum level.
           5)	Use the pixel count as area and calculate the diameter of that area  as diameter:=2 *sqrt(count/pi).*/
    }

    /// <summary>
    /// calculate image pixel value on subpixel level
    /// </summary>
    /// <param name="x1"></param>
    /// <param name="y1"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    float SubpixelValue(float x1, float y1)
    {
        var x_trunc = (int)MathF.Truncate(x1);
        var y_trunc = (int)MathF.Truncate(y1);

        if (x_trunc <= 0 || x_trunc >= width - 2 || y_trunc <= 0 || y_trunc >= height - 2)
        {
            return 0;
        }

        var x_frac = x1 - x_trunc;
        var y_frac = y1 - y_trunc;
        try
        {
            var result = (double)data[y_trunc, x_trunc]      * (1 - x_frac) * (1 - y_frac); // pixel left top, 1
            result += (double)data[y_trunc, x_trunc + 1]     * x_frac * (1 - y_frac);       // pixel right top, 2
            result += (double)data[y_trunc + 1, x_trunc]     * (1 - x_frac) * y_frac;       // pixel left bottom, 3
            result += (double)data[y_trunc + 1, x_trunc + 1] * x_frac * y_frac;             // pixel right bottom, 4
            return (float)result;
        }
        catch (Exception ex) when (Environment.UserInteractive)
        {
            GC.KeepAlive(ex);
            throw;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Uses a simple 2x2 sliding window to calculate the average of 4 pixels, assumes simple 2x2 Bayer matrix.
    /// Is a no-op for monochrome fames.
    /// </summary>
    /// <returns>Debayered monochrome image</returns>
    public Image DebayerOSCToSyntheticLuminance()
    {
        // NO-OP for monochrome images
        if (imageMeta.SensorType is SensorType.Monochrome)
        {
            return this;
        }

        var debayered = new float[height,width];
        // Loop through each pixel in the raw image
        var w1 = width - 1;
        var h1 = height - 1;

        for (int y = 0; y < h1; y++)
        {
            for (int x = 0; x < w1; x++)
            {
                debayered[y, x] = (float)(0.25d * ((double)data[y, x] + data[y+1, x+1] + data[y, x+1] + data[y + 1, x]));
            }

            // last column
            debayered[y, w1] = (float)(0.25d * ((double)data[y, w1] + data[y+1, w1 - 1] + data[y, w1 - 1] + data[y + 1, w1]));
        }

        // last row
        for (int x = 0; x < w1; x++)
        {
            debayered[h1, x] = (float)(0.25d * ((double)data[h1, x] + data[h1 - 1, x+1] + data[h1, x+1] + data[h1 - 1, x]));
        }

        // last pixel
        debayered[h1, w1] = (float)(0.25d * ((double)data[h1, w1] + data[h1 - 1, w1 - 1] + data[h1, w1 - 1] + data[h1 - 1, w1]));

        return new Image(debayered, width, height, BitDepth.Float32, maxVal, blackLevel, imageMeta with
        {
            SensorType = SensorType.Monochrome,
            BayerOffsetX = 0,
            BayerOffsetY = 0,
            Filter = new Filter("LUM")
        });
    }

    public async Task FindOffsetAndRotationAsync(Image other, float snrMin = 20f, int maxStars = 500, int maxRetries = 2, int minStars = 24, float quadTolerance = 0.008f, CancellationToken cancellationToken = default)
    {
        var starList1Task = FindStarsAsync(snrMin, maxStars, maxRetries, cancellationToken);
        var starList2Task = other.FindStarsAsync(snrMin, maxStars, maxRetries, cancellationToken);

        var starLists = await Task.WhenAll(starList1Task, starList2Task);

        if (starLists[0].Count >= minStars || starLists[1].Count >= minStars)
        {
            new SortedStarList(starLists[0]).FindOffsetAndRotation(new SortedStarList(starLists[1]), minStars/ 4, quadTolerance);
        }
    }
}