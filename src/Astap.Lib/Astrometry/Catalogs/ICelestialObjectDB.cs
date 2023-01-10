﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Astap.Lib.Astrometry.Catalogs.CatalogUtils;

namespace Astap.Lib.Astrometry.Catalogs;

public interface ICelestialObjectDB
{
    bool TryResolveCommonName(string name, out IReadOnlyList<CatalogIndex> matches);

    bool TryGetCrossIndices(CatalogIndex catalogIndex, out IReadOnlyList<CatalogIndex> crossIndices);

    IReadOnlySet<CatalogIndex> ObjectIndices { get; }

    IReadOnlySet<Catalog> Catalogs { get; }

    Task<(int processed, int failed)> InitDBAsync();

    IReadOnlyCollection<string> CommonNames { get; }

    IRaDecIndex CoordinateGrid { get; }

    bool TryLookupByIndex(CatalogIndex index, out CelestialObject celestialObject);

    public bool TryLookupByIndex(string name, out CelestialObject celestialObject)
    {
        if (TryGetCleanedUpCatalogName(name, out var index) && TryLookupByIndex(index, out celestialObject))
        {
            return true;
        }
        else
        {
            celestialObject = default;
            return false;
        }
    }

    /// <summary>
    /// Uses <see cref="ICelestialObjectDB.CommonNames"/> and <see cref="ICelestialObjectDB.ObjectIndices"/> to create a list
    /// of all names and designations.
    /// </summary>
    /// <param name="this">Initialised object db</param>
    /// <returns>copied array of all names and canonical designations</returns>
    public string[] CreateAutoCompleteList()
    {
        var commonNames = CommonNames;
        var objIndices = ObjectIndices;

        var canonicalSet = new HashSet<string>((int)(objIndices.Count * 1.3f));
        foreach (var objIndex in objIndices)
        {
            canonicalSet.Add(objIndex.ToCanonical(CanonicalFormat.Normal));
            canonicalSet.Add(objIndex.ToCanonical(CanonicalFormat.Alternative));
        }

        var names = new string[canonicalSet.Count + commonNames.Count];
        canonicalSet.CopyTo(names, 0);
        commonNames.CopyTo(names, canonicalSet.Count);

        return names;
    }
}
