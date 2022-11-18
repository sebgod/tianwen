﻿using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Astap.Lib.Astrometry.Catalogs.CatalogUtils;
using static Astap.Lib.Astrometry.NOVA.CoordinateUtils;
using static Astap.Lib.EnumHelper;

namespace Astap.Lib.Astrometry.Catalogs;

public class OpenNGCDB : ICelestialObjectDB
{
    private static readonly IReadOnlySet<string> EmptySet = ImmutableHashSet.Create<string>();

    private readonly Dictionary<CatalogIndex, CelestialObject> _objectsByIndex = new(14000);
    private readonly Dictionary<CatalogIndex, (CatalogIndex i1, CatalogIndex[]? ext)> _crossIndexLookuptable = new(5500);
    private readonly Dictionary<string, (CatalogIndex i1, CatalogIndex[]? ext)> _objectsByCommonName = new(200);

    private HashSet<CatalogIndex>? _catalogIndicesCache;
    private HashSet<Catalog>? _catalogCache;

    public OpenNGCDB() { }

    public IReadOnlyCollection<string> CommonNames => _objectsByCommonName.Keys;

    public IReadOnlySet<Catalog> Catalogs
    {
        get
        {
            if (_catalogCache is var cache and not null)
            {
                return cache;
            }

            var objs = _objectsByIndex.Count + _crossIndexLookuptable.Count;

            if (objs > 0)
            {
                return _catalogCache ??= this.IndicesToCatalogs<HashSet<Catalog>>();
            }
            return new HashSet<Catalog>(0);
        }
    }

    public IReadOnlySet<CatalogIndex> ObjectIndices
    {
        get
        {
            if (_catalogIndicesCache is var cache and not null)
            {
                return cache;
            }

            var objs = _objectsByIndex.Count + _crossIndexLookuptable.Count;
            if (objs > 0)
            {
                cache = new HashSet<CatalogIndex>(objs);
                cache.UnionWith(_objectsByIndex.Keys);
                cache.UnionWith(_crossIndexLookuptable.Keys);

                return _catalogIndicesCache ??= cache;
            }

            return new HashSet<CatalogIndex>(0);
        }
    }

    /// <inheritdoc/>
    public bool TryResolveCommonName(string name, out IReadOnlyList<CatalogIndex> matches) => _objectsByCommonName.TryGetLookupEntries(name, out matches);

    private static readonly Catalog[] CrossCats = new[] {
        Catalog.Barnard,
        Catalog.GUM,
        // Catalog.RCW,
        Catalog.Messier,
        Catalog.IC,
        Catalog.Caldwell,
        Catalog.Collinder,
        Catalog.Melotte,
        Catalog.UGC
    }.OrderBy(x => (ulong)x).ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCrossCat(Catalog cat) => Array.BinarySearch(CrossCats, cat) >= 0;

    /// <inheritdoc/>
    public bool TryLookupByIndex(CatalogIndex index, [NotNullWhen(true)] out CelestialObject celestialObject)
    {
        if (!_objectsByIndex.TryGetValue(index, out celestialObject)
            && IsCrossCat(index.ToCatalog())
            && _crossIndexLookuptable.TryGetValue(index, out var crossIndices)
        )
        {
            if (crossIndices.i1 > 0 && crossIndices.i1 != index && _objectsByIndex.TryGetValue(crossIndices.i1, out celestialObject))
            {
                index = crossIndices.i1;
            }
            else if (crossIndices.ext is not null)
            {
                foreach (var crossIndex in crossIndices.ext)
                {
                    if (crossIndex > 0 && crossIndex != index && _objectsByIndex.TryGetValue(crossIndex, out celestialObject))
                    {
                        index = crossIndex;
                        break;
                    }
                }
            }
        }

        if (celestialObject.Index is 0)
        {
            return false;
        }
        if (celestialObject.ObjectType is not ObjectType.Duplicate)
        {
            return true;
        }

        if (_crossIndexLookuptable.TryGetValue(index, out var followIndicies) && followIndicies.i1 > 0)
        {
            if (followIndicies.ext == null && followIndicies.i1 != index)
            {
                return TryLookupByIndex(followIndicies.i1, out celestialObject);
            }
            else if (followIndicies.ext is CatalogIndex[] { Length: > 0 } ext)
            {
                var followedObjs = new List<CelestialObject>(ext.Length + 1);
                AddToFollowObjs(followedObjs, index, followIndicies.i1);

                foreach (var followIndex in followIndicies.ext)
                {
                    AddToFollowObjs(followedObjs, index, followIndex);
                }

                if (followedObjs.Count == 1)
                {
                    celestialObject = followedObjs[0];
                    return true;
                }
            }
        }
        return false;

        void AddToFollowObjs(List<CelestialObject> followedObjs, CatalogIndex index, CatalogIndex followIndex)
        {
            if (followIndex != index && _objectsByIndex.TryGetValue(followIndex, out var followedObj) && followedObj.ObjectType != ObjectType.Duplicate)
            {
                followedObjs.Add(followedObj);
            }
        }
    }

    public bool TryGetCrossIndices(CatalogIndex catalogIndex, out IReadOnlyList<CatalogIndex> crossIndices)
        => _crossIndexLookuptable.TryGetLookupEntries(catalogIndex, out crossIndices);

    /// <inheritdoc/>
    public async Task<(int processed, int failed)> InitDBAsync()
    {
        var assembly = typeof(OpenNGCDB).Assembly;
        int totalProcessed = 0;
        int totalFailed = 0;

        foreach (var csvName in new[] { "NGC", "NGC.addendum" })
        {
            var (processed, failed) = await ReadEmbeddedGzippedCsvDataFileAsync(assembly, csvName);
            totalProcessed += processed;
            totalFailed += failed;
        }

        foreach (var simbadCatName in new[] { "GUM", "RCW" })
        {
            var (processed, failed) = await ReadEmbeddedGzippedJsonDataFileAsync(assembly, simbadCatName);
            totalProcessed += processed;
            totalFailed += failed;
        }

        return (totalProcessed, totalFailed);
    }

    private async Task<(int processed, int failed)> ReadEmbeddedGzippedCsvDataFileAsync(Assembly assembly, string csvName)
    {
        const string NGC = nameof(NGC);
        const string IC = nameof(IC);
        const string M = nameof(M);

        int processed = 0;
        int failed = 0;
        var manifestFileName = assembly.GetManifestResourceNames().FirstOrDefault(p => p.EndsWith("." + csvName + ".csv.gz"));
        if (manifestFileName is null || assembly.GetManifestResourceStream(manifestFileName) is not Stream stream)
        {
            return (processed, failed);
        }

        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, false);
        using var streamReader = new StreamReader(gzipStream, new UTF8Encoding(false), leaveOpen: true);
        using var csvReader = new CsvReader(new CsvParser(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" }, leaveOpen: true));

        if (!await csvReader.ReadAsync() || !csvReader.ReadHeader())
        {
            return (processed, failed);
        }

        while (await csvReader.ReadAsync())
        {
            if (csvReader.TryGetField<string>("Name", out var entryName)
                && csvReader.TryGetField<string>("Type", out var objectTypeAbbr)
                && objectTypeAbbr is not null
                && csvReader.TryGetField<string>("RA", out var raHMS)
                && raHMS is not null
                && csvReader.TryGetField<string>("Dec", out var decDMS)
                && decDMS is not null
                && csvReader.TryGetField<string>("Const", out var constAbbr)
                && constAbbr is not null
                && TryGetCleanedUpCatalogName(entryName, out var indexEntry)
            )
            {
                var objectType = AbbreviationToEnumMember<ObjectType>(objectTypeAbbr);
                var @const = AbbreviationToEnumMember<Constellation>(constAbbr);

                var vmag = csvReader.TryGetField<string>("V-Mag", out var vmagStr)
                    && float.TryParse(vmagStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var vmagFloat)
                    ? vmagFloat
                    : float.NaN;

                var surfaceBrightness = csvReader.TryGetField<string>("SurfBr", out var surfaceBrightnessStr)
                    && float.TryParse(surfaceBrightnessStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var surfaceBrightnessFloat)
                    ? surfaceBrightnessFloat
                    : float.NaN;

                IReadOnlySet<string> commonNames;
                if (csvReader.TryGetField<string>("Common names", out var commonNamesEntry) && !string.IsNullOrWhiteSpace(commonNamesEntry))
                {
                    commonNames = new HashSet<string>(commonNamesEntry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    foreach (var commonName in commonNames)
                    {
                        _objectsByCommonName.AddLookupEntry(commonName, indexEntry);
                    }
                }
                else
                {
                    commonNames = EmptySet;
                }

                var ra = HMSToHours(raHMS);
                var dec = DMSToDegree(decDMS);
                _objectsByIndex[indexEntry] = new CelestialObject(
                    indexEntry,
                    objectType,
                    ra,
                    dec,
                    @const,
                    vmag,
                    surfaceBrightness,
                    commonNames
                );

                if (objectType == ObjectType.Duplicate)
                {
                    // when the entry is a duplicate, use the cross lookup table to list the entries it duplicates
                    if (TryGetCatalogField(NGC, out var ngcIndexEntry))
                    {
                        _crossIndexLookuptable.AddLookupEntry(indexEntry, ngcIndexEntry);
                    }
                    if (TryGetCatalogField(M, out var messierIndexEntry))
                    {
                        _crossIndexLookuptable.AddLookupEntry(indexEntry, messierIndexEntry);
                    }
                    if (TryGetCatalogField(IC, out var icIndexEntry))
                    {
                        _crossIndexLookuptable.AddLookupEntry(indexEntry, icIndexEntry);
                    }
                }
                else
                {
                    if (TryGetCatalogField(IC, out var icIndexEntry) && indexEntry != icIndexEntry)
                    {
                        _crossIndexLookuptable.AddLookupEntry(icIndexEntry, indexEntry);
                        _crossIndexLookuptable.AddLookupEntry(indexEntry, icIndexEntry);
                    }
                    if (TryGetCatalogField(M, out var messierIndexEntry) && indexEntry != messierIndexEntry)
                    {
                        // Adds Messier to NGC/IC entry lookup, but only if its not a duplicate
                        _crossIndexLookuptable.AddLookupEntry(messierIndexEntry, indexEntry);
                        _crossIndexLookuptable.AddLookupEntry(indexEntry, messierIndexEntry);
                    }

                    if (csvReader.TryGetField<string>("Identifiers", out var identifiersEntry) && identifiersEntry is not null)
                    {
                        var identifiers = identifiersEntry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var identifier in identifiers)
                        {
                            if (identifier[0] is 'C' or 'M' or 'U'
                                && identifier.Length >= 2
                                && identifier[1] is 'G' or 'e' or 'l' or 'r' or ' ' or '0'
                                && TryGetCleanedUpCatalogName(identifier, out var crossCatIdx)
                                && IsCrossCat(crossCatIdx.ToCatalog())
                            )
                            {
                                _crossIndexLookuptable.AddLookupEntry(crossCatIdx, indexEntry);
                            }
                        }
                    }
                }

                processed++;
            }
            else
            {
                failed++;
            }
        }

        return (processed, failed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryGetCatalogField(string catPrefix, out CatalogIndex entry)
        {
            entry = 0;
            return csvReader.TryGetField<string>(catPrefix, out var suffix) && TryGetCleanedUpCatalogName(catPrefix + suffix, out entry);
        }
    }

    private async Task<(int processed, int failed)> ReadEmbeddedGzippedJsonDataFileAsync(Assembly assembly, string jsonName)
    {
        const string NAME_CAT_PREFIX = "NAME ";

        var processed = 0;
        var failed = 0;

        var manifestFileName = assembly.GetManifestResourceNames().FirstOrDefault(p => p.EndsWith("." + jsonName + ".json.gz"));
        if (manifestFileName is null || assembly.GetManifestResourceStream(manifestFileName) is not Stream stream)
        {
            return (processed, failed);
        }

        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, false);

        var mainCatalogs = new HashSet<Catalog>();
        foreach (var objIndex in new HashSet<CatalogIndex>(_objectsByIndex.Keys))
        {
            _ = mainCatalogs.Add(objIndex.ToCatalog());
        }

        Catalog catToAdd = AbbreviationToEnumMember<Catalog>(jsonName);
        await foreach (var record in JsonSerializer.DeserializeAsyncEnumerable<SimbadCatalogDto>(gzipStream))
        {
            if (record is null)
            {
                continue;
            }

            CatalogIndex catToAddIdx = 0;
            var relevantIds = new Dictionary<Catalog, CatalogIndex>();
            var commonNames = new HashSet<string>();
            foreach (var id in record.Ids)
            {
                if (id.StartsWith(NAME_CAT_PREFIX))
                {
                    commonNames.Add(id[NAME_CAT_PREFIX.Length..]);
                }
                else if (TryGetCleanedUpCatalogName(id, out var catId))
                {
                    var cat = catId.ToCatalog();
                    if (cat == catToAdd && catToAddIdx == 0)
                    {
                        catToAddIdx = catId;
                    }
                    else if (mainCatalogs.Contains(cat) && !relevantIds.ContainsKey(cat))
                    {
                        relevantIds.Add(cat, catId);
                    }
                }
            }

            if (catToAddIdx != 0)
            {
                var bestMatch = relevantIds.OrderBy(
                    p => p.Key switch
                    {
                        Catalog.NGC => 1u,
                        Catalog.IC => 2u,
                        Catalog.Messier => 3u,
                        _ => (ulong)p.Key
                    })
                    .FirstOrDefault(p => _objectsByIndex.ContainsKey(p.Value));

                if (bestMatch.Value is CatalogIndex id and not 0)
                {
                    _crossIndexLookuptable.AddLookupEntry(id, catToAddIdx);
                    _crossIndexLookuptable.AddLookupEntry(catToAddIdx, id);

                    if (commonNames.Count > 0 && _objectsByIndex.TryGetValue(id, out var obj))
                    {
                        if (!obj.CommonNames.SetEquals(commonNames))
                        {
                            var modObj = new CelestialObject(
                                obj.Index,
                                obj.ObjectType,
                                obj.RA,
                                obj.Dec,
                                obj.Constellation,
                                obj.V_Mag,
                                obj.SurfaceBrightness,
                                commonNames.UnionWithAsReadOnlyCopy(obj.CommonNames)
                            );

                            _objectsByIndex[id] = modObj;

                            commonNames.ExceptWith(obj.CommonNames);
                            foreach (var commonName in commonNames)
                            {
                                _objectsByCommonName.AddLookupEntry(commonName, id);
                            }
                        }
                    }
                }
                else
                {
                    if (ConstellationBoundary.TryFindConstellation(record.Ra, record.Dec, out var constellation))
                    {
                        _objectsByIndex[catToAddIdx] = new CelestialObject(catToAddIdx, 0, record.Ra, record.Dec, constellation, float.NaN, float.NaN, commonNames);
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            processed++;
        }

        return (processed, failed);
    }
}
