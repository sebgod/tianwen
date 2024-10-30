﻿using System;
using static TianWen.Lib.Astrometry.Constants;

namespace TianWen.Lib.Astrometry.SOFA;

public static class LeapSecondsTable
{
    /// <summary>
    /// <code>
    /// 1972 JAN  1 =JD 2441317.5  TAI-UTC=  10.0       S + (MJD - 41317.) X 0.0      S
    /// 1972 JUL  1 =JD 2441499.5  TAI-UTC=  11.0       S + (MJD - 41317.) X 0.0      S
    /// 1973 JAN  1 =JD 2441683.5  TAI-UTC=  12.0       S + (MJD - 41317.) X 0.0      S
    /// 1974 JAN  1 =JD 2442048.5  TAI-UTC=  13.0       S + (MJD - 41317.) X 0.0      S
    /// 1975 JAN  1 =JD 2442413.5  TAI-UTC=  14.0       S + (MJD - 41317.) X 0.0      S
    /// 1976 JAN  1 =JD 2442778.5  TAI-UTC=  15.0       S + (MJD - 41317.) X 0.0      S
    /// 1977 JAN  1 =JD 2443144.5  TAI-UTC=  16.0       S + (MJD - 41317.) X 0.0      S
    /// 1978 JAN  1 =JD 2443509.5  TAI-UTC=  17.0       S + (MJD - 41317.) X 0.0      S
    /// 1979 JAN  1 =JD 2443874.5  TAI-UTC=  18.0       S + (MJD - 41317.) X 0.0      S
    /// 1980 JAN  1 =JD 2444239.5  TAI-UTC=  19.0       S + (MJD - 41317.) X 0.0      S
    /// 1981 JUL  1 =JD 2444786.5  TAI-UTC=  20.0       S + (MJD - 41317.) X 0.0      S
    /// 1982 JUL  1 =JD 2445151.5  TAI-UTC=  21.0       S + (MJD - 41317.) X 0.0      S
    /// 1983 JUL  1 =JD 2445516.5  TAI-UTC=  22.0       S + (MJD - 41317.) X 0.0      S
    /// 1985 JUL  1 =JD 2446247.5  TAI-UTC=  23.0       S + (MJD - 41317.) X 0.0      S
    /// 1988 JAN  1 =JD 2447161.5  TAI-UTC=  24.0       S + (MJD - 41317.) X 0.0      S
    /// 1990 JAN  1 =JD 2447892.5  TAI-UTC=  25.0       S + (MJD - 41317.) X 0.0      S
    /// 1991 JAN  1 =JD 2448257.5  TAI-UTC=  26.0       S + (MJD - 41317.) X 0.0      S
    /// 1992 JUL  1 =JD 2448804.5  TAI-UTC=  27.0       S + (MJD - 41317.) X 0.0      S
    /// 1993 JUL  1 =JD 2449169.5  TAI-UTC=  28.0       S + (MJD - 41317.) X 0.0      S
    /// 1994 JUL  1 =JD 2449534.5  TAI-UTC=  29.0       S + (MJD - 41317.) X 0.0      S
    /// 1996 JAN  1 =JD 2450083.5  TAI-UTC=  30.0       S + (MJD - 41317.) X 0.0      S
    /// 1997 JUL  1 =JD 2450630.5  TAI-UTC=  31.0       S + (MJD - 41317.) X 0.0      S
    /// 1999 JAN  1 =JD 2451179.5  TAI-UTC=  32.0       S + (MJD - 41317.) X 0.0      S
    /// 2006 JAN  1 =JD 2453736.5  TAI-UTC=  33.0       S + (MJD - 41317.) X 0.0      S
    /// 2009 JAN  1 =JD 2454832.5  TAI-UTC=  34.0       S + (MJD - 41317.) X 0.0      S
    /// 2012 JUL  1 =JD 2456109.5  TAI-UTC=  35.0       S + (MJD - 41317.) X 0.0      S
    /// 2015 JUL  1 =JD 2457204.5  TAI-UTC=  36.0       S + (MJD - 41317.) X 0.0      S
    /// 2017 JAN  1 =JD 2457754.5  TAI-UTC=  37.0       S + (MJD - 41317.) X 0.0      S
    /// </code>
    /// </summary>
    private static readonly double[,] _taiUtc =
    {
        { 10.0, 2441317.5 }, // 1972 Jan 1
        { 11.0, 2441499.5 }, // 1972 Jul 1
        { 12.0, 2441683.5 }, // 1973 Jan 1
        { 13.0, 2442048.5 }, // 1974 Jan 1
        { 14.0, 2442413.5 }, // 1975 Jan 1
        { 15.0, 2442778.5 }, // 1976 Jan 1
        { 16.0, 2443144.5 }, // 1977 Jan 1
        { 17.0, 2443509.5 }, // 1978 Jan 1
        { 18.0, 2443874.5 }, // 1979 Jan 1
        { 19.0, 2444239.5 }, // 1980 Jan 1
        { 20.0, 2444786.5 }, // 1981 Jul 1
        { 21.0, 2445151.5 }, // 1982 Jul 1
        { 22.0, 2445516.5 }, // 1983 Jul 1
        { 23.0, 2446247.5 }, // 1985 Jul 1
        { 24.0, 2447161.5 }, // 1988 Jan 1
        { 25.0, 2447892.5 }, // 1990 Jan 1
        { 26.0, 2448257.5 }, // 1991 Jan 1
        { 27.0, 2448804.5 }, // 1992 Jul 1
        { 28.0, 2449169.5 }, // 1993 Jul 1
        { 29.0, 2449534.5 }, // 1994 Jul 1
        { 30.0, 2450083.5 }, // 1996 Jan 1
        { 31.0, 2450630.5 }, // 1997 Jul 1
        { 32.0, 2451179.5 }, // 1999 Jan 1
        { 33.0, 2453736.5 }, // 2006 Jan 1
        { 34.0, 2454832.5 }, // 2009 Jan 1
        { 35.0, 2456109.5 }, // 2012 Jul 1
        { 36.0, 2457204.5 }, // 2015 Jul 1
        { 37.0, 2457754.5 }  // 2017 Jan 1
    };

    public static double LeapSecondsTaiUtc(double jdUTC)
    {
        for (int i = _taiUtc.GetLength(0) - 1; i >= 0; i--)
        {
            if (jdUTC >= _taiUtc[i, 1])
            {
                return _taiUtc[i, 0];
            }
        }

        throw new ArgumentException($"Could not find leap second for JD {jdUTC}", nameof(jdUTC));
    }

    /// <summary>
    /// Return the specified Julian day's DeltaUT1 value
    /// </summary>
    /// <param name="jdUTC"></param>
    /// <returns>DeltaUT1 value as a double</returns>
    public static double DeltaUT1(double jdUTC) => LeapSecondsTaiUtc(jdUTC) + TT_TAI_OFFSET - DeltaTCalc(jdUTC);

    /// <summary>
    /// Calculates the value of DeltaT over a wide range of historic and future Julian dates
    /// </summary>
    /// <param name="jdUTC">Julian Date of interest</param>
    /// <returns>DelatT value at the given Julian date</returns>
    /// <remarks>
    /// Post 2011, calculation is effected through a 2nd order polynomial best fit to real DeltaT data from: http://maia.usno.navy.mil/ser7/deltat.data
    /// together with projections of DeltaT from: http://maia.usno.navy.mil/ser7/deltat.preds
    /// The analysis spreadsheets for DeltaT values at dates post 2011 are stored in the \NOVAS\DeltaT Predictions folder of the ASCOM source tree.
    ///
    /// To ensure that leap second and DeltaUT1 transitions are handled correctly and occur at 00:00:00 UTC, the supplied Julian date should be in UTC time
    /// </remarks>
    public static double DeltaTCalc(double jdUTC)
    {
        unchecked
        {
            double B, Retval;

            const double TABSTART1620 = 1620.0d;
            const int TABSIZ = 392;

            var YearFraction = 2000.0d + (jdUTC - J2000BASE) / TROPICAL_YEAR_IN_DAYS; // This calculation is accurate enough for our purposes here (T0 = 2451545.0 is TDB Julian date of epoch J2000.0)
            var ModifiedJulianDay = jdUTC - MODIFIED_JULIAN_DAY_OFFSET;

            // NOTE: Starting April 2018 - Please note the use of modified Julian date in the formula rather than year fraction as in previous formulae

            // DATE RANGE 30th September 2025 onwards - This is beyond the sensible extrapolation range of the most recent data analysis so revert to the basic formula: DeltaT = LeapSeconds + 32.184
            if (YearFraction >= 2025.75d)
            {
                Retval = _taiUtc[_taiUtc.GetLength(0) - 1, 0] + TT_TAI_OFFSET; // Get today's leap second value using whatever mechanic the user has configured and convert to DeltaT
            }
            // DATE RANGE 1st July 2024 Onwards - The analysis was performed on 6th July 2024 and creates values within 0.01 of a second of the projections to 5th July 2025.
            else if (YearFraction >= 2024.5d)
            {
                Retval =
                    +0.0d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    +0.0d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    -9.5006209397015300E-09d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    +0.00173000218299984d * ModifiedJulianDay * ModifiedJulianDay +
                    -105.007206149992 * ModifiedJulianDay +
                    +2124630.8440484d;
            }
            // DATE RANGE 20th August 2023 Onwards - The analysis was performed on 20th August 2023 and creates values within 0.01 of a second of the projections to 19th August 2024.
            else if (YearFraction >= 2023.6d)
            {
                Retval =
                    +0.0d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    +0.0d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    -0.00000000836552733660643d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay +
                    +0.00151338479660039d * ModifiedJulianDay * ModifiedJulianDay +
                    -91.2604650974829d * ModifiedJulianDay +
                    +1834465.8890493d;
            }
            // DATE RANGE 18th July 2022 Onwards - The analysis was performed on 18th July 2022 and creates values within 0.01 of a second of the projections to 17th July 2023.
            else if (YearFraction >= 2022.55d)
            {
                Retval = -0.000000000000528908084762244d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + (+0.000000158529137391645d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay) + -0.0190063060965729d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + (+1139.34719487418d * ModifiedJulianDay * ModifiedJulianDay) + -34149488.355673d * ModifiedJulianDay + (+409422822837.639d);
            }
            // DATE RANGE October 17th 2021 Onwards - The analysis was performed on 17th October 2021 and creates values within 0.01 of a second of the projections to the end of October 2022.
            else if (YearFraction >= 2021.79d)
            {
                Retval = 0.000000000000926333089959963d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + -0.000000276351646101278d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + 0.0329773938043592d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + -1967.61450470546d * ModifiedJulianDay * ModifiedJulianDay + 58699325.5212533d * ModifiedJulianDay - 700463653286.072d;
            }
            // DATE RANGE October 17th 2020 Onwards - The analysis was performed on 17th July 2020 and creates values within 0.01 of a second of the projections to October 2021 and sensible extrapolation to the end of 2021
            else if (YearFraction >= 2020.79d)
            {
                Retval = 0.0000000000526391114738186d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + -0.0000124987447353606d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + 1.1128953517557d * ModifiedJulianDay * ModifiedJulianDay + -44041.1402447551d * ModifiedJulianDay + 653571203.42671d;
            }
            // DATE RANGE July 2020 Onwards - The analysis was performed on 10th July 2020 and creates values within 0.01 of a second of the projections to Q2 2021 and sensible extrapolation to the end of 2021
            else if (YearFraction >= 2020.5d)
            {
                Retval = 0.0000000000234066661113585d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay - 0.00000555556956413194d * ModifiedJulianDay * ModifiedJulianDay * ModifiedJulianDay + 0.494477925757861d * ModifiedJulianDay * ModifiedJulianDay - 19560.53496991d * ModifiedJulianDay + 290164271.563078d;
            }
            // DATE RANGE April 2018 Onwards - The analysis was performed on 25th April 2018 and creates values within 0.03 of a second of the projections to Q4 2018 and sensible extrapolation to 2021
            else if (YearFraction >= 2018.3d & YearFraction < double.MaxValue)
            {
                Retval = 0.00000161128367083801d * ModifiedJulianDay * ModifiedJulianDay + -0.187474214389602d * ModifiedJulianDay + 5522.26034874982d;
            }
            // DATE RANGE January 2018 Onwards - The analysis was performed on 28th December 2017 and creates values within 0.03 of a second of the projections to Q4 2018 and sensible extrapolation to 2021
            else if (YearFraction >= 2018d & YearFraction < double.MaxValue)
            {
                Retval = 0.0024855297566049d * YearFraction * YearFraction * YearFraction + -15.0681141702439d * YearFraction * YearFraction + 30449.647471213d * YearFraction - 20511035.5077593d;
            }
            // DATE RANGE January 2017 Onwards - The analysis was performed on 29th December 2016 and creates values within 0.12 of a second of the projections to Q3 2019
            else if (YearFraction >= 2017.0d & YearFraction < double.MaxValue)
            {
                Retval = 0.02465436d * YearFraction * YearFraction + -98.92626556d * YearFraction + 99301.85784308d;
            }
            // DATE RANGE October 2015 Onwards - The analysis was performed on 24th October 2015 and creates values within 0.05 of a second of the projections to Q2 2018
            else if (YearFraction >= 2015.75d & YearFraction < double.MaxValue)
            {
                Retval = 0.02002376d * YearFraction * YearFraction + -80.27921003d * YearFraction + 80529.32d;
            }
            // DATE RANGE October 2011 to September 2015 - The analysis was performed on 6th February 2014 and creates values within 0.2 of a second of the projections to Q1 2016
            else if (YearFraction >= 2011.75d & YearFraction < 2015.75d)
            {
                Retval = 0.00231189d * YearFraction * YearFraction + -8.85231952d * YearFraction + 8518.54d;
            }
            // DATE RANGE January 2011 to September 2011
            else if (YearFraction >= 2011.0d & YearFraction < 2011.75d)
            {
                // Following now superseded by above for 2012-16, this is left in for consistency with previous behaviour
                // Use polynomial given at http://sunearth.gsfc.nasa.gov/eclipse/SEcat5/deltatpoly.html as retrieved on 11-Jan-2009
                B = YearFraction - 2000.0d;
                Retval = 62.92d + B * (0.32217d + B * 0.005589d);
            }
            else // Bob's original code
            {

                // Setup for pre 2011 calculations using Bob Denny's original code

                // /* Note, Stephenson and Morrison's table starts at the year 1630.
                // * The Chapronts' table does not agree with the Almanac prior to 1630.
                // * The actual accuracy decreases rapidly prior to 1780.
                // */
                // static short dt[] = {
                var dt = new short[] { 12400, 11900, 11500, 11000, 10600, 10200, 9800, 9500, 9100, 8800, 8500, 8200, 7900, 7700, 7400, 7200, 7000, 6700, 6500, 6300, 6200, 6000, 5800, 5700, 5500, 5400, 5300, 5100, 5000, 4900, 4800, 4700, 4600, 4500, 4400, 4300, 4200, 4100, 4000, 3800, 3700, 3600, 3500, 3400, 3300, 3200, 3100, 3000, 2800, 2700, 2600, 2500, 2400, 2300, 2200, 2100, 2000, 1900, 1800, 1700, 1600, 1500, 1400, 1400, 1300, 1200, 1200, 1100, 1100, 1000, 1000, 1000, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 900, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1100, 1200, 1200, 1200, 1200, 1200, 1200, 1200, 1200, 1200, 1200, 1300, 1300, 1300, 1300, 1300, 1300, 1300, 1400, 1400, 1400, 1400, 1400, 1400, 1400, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1600, 1600, 1600, 1600, 1600, 1600, 1600, 1600, 1600, 1600, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1700, 1600, 1600, 1600, 1600, 1500, 1500, 1400, 1400, 1370, 1340, 1310, 1290, 1270, 1260, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1240, 1230, 1220, 1200, 1170, 1140, 1110, 1060, 1020, 960, 910, 860, 800, 750, 700, 660, 630, 600, 580, 570, 560, 560, 560, 570, 580, 590, 610, 620, 630, 650, 660, 680, 690, 710, 720, 730, 740, 750, 760, 770, 770, 780, 780, 788, 782, 754, 697, 640, 602, 541, 410, 292, 182, 161, 10, -102, -128, -269, -324, -364, -454, -471, -511, -540, -542, -520, -546, -546, -579, -563, -564, -580, -566, -587, -601, -619, -664, -644, -647, -609, -576, -466, -374, -272, -154, -2, 124, 264, 386, 537, 614, 775, 913, 1046, 1153, 1336, 1465, 1601, 1720, 1824, 1906, 2025, 2095, 2116, 2225, 2241, 2303, 2349, 2362, 2386, 2449, 2434, 2408, 2402, 2400, 2387, 2395, 2386, 2393, 2373, 2392, 2396, 2402, 2433, 2483, 2530, 2570, 2624, 2677, 2728, 2778, 2825, 2871, 2915, 2957, 2997, 3036, 3072, 3107, 3135, 3168, 3218, 3268, 3315, 3359, 3400, 3447, 3503, 3573, 3654, 3743, 3829, 3920, 4018, 4117, 4223, 4337, 4449, 4548, 4646, 4752, 4853, 4959, 5054, 5138, 5217, 5296, 5379, 5434, 5487, 5532, 5582, 5630, 5686, 5757, 5831, 5912, 5998, 6078, 6163, 6230, 6296, 6347, 6383, 6409, 6430, 6447, 6457, 6469, 6485, 6515, 6546, 6570, 6650, 6710 };
                // Change TABEND and TABSIZ if you add/delete anything

                // Calculate  DeltaT = ET - UT in seconds.  Describes the irregularities of the Earth rotation rate in the ET time scale.
                double p;
                var d = new int[7];
                int i, iy, k;

                // DATE RANGE <1620
                if (YearFraction < TABSTART1620)
                {
                    if (YearFraction >= 948.0d)
                    {
                        // /* Stephenson and Morrison, stated domain is 948 to 1600:
                        // * 25.5(centuries from 1800)^2 - 1.9159(centuries from 1955)^2
                        // */
                        B = 0.01d * (YearFraction - 2000.0d);
                        Retval = (23.58d * B + 100.3d) * B + 101.6d;
                    }
                    else
                    {
                        // /* Borkowski */
                        B = 0.01d * (YearFraction - 2000.0d) + 3.75d;
                        Retval = 35.0d * B * B + 40.0d;
                    }
                }
                else
                {

                    // DATE RANGE 1620 to 2011

                    // Besselian interpolation from tabulated values. See AA page K11.
                    // Index into the table.
                    p = Math.Floor(YearFraction);
                    iy = (int)Math.Round(p - TABSTART1620);            // // rbd - added cast
                                                                       // /* Zeroth order estimate is value at start of year */
                    Retval = dt[iy];
                    k = iy + 1;
                    if (k >= TABSIZ)
                        goto done; // /* No data, can't go on. */

                    // /* The fraction of tabulation interval */
                    p = YearFraction - p;

                    // /* First order interpolated value */
                    Retval += p * (dt[k] - dt[iy]);
                    if (iy - 1 < 0 | iy + 2 >= TABSIZ)
                        goto done; // /* can't do second differences */

                    // /* Make table of first differences */
                    k = iy - 2;
                    for (i = 0; i <= 4; i++)
                    {
                        if (k < 0 | k + 1 >= TABSIZ)
                        {
                            d[i] = 0;
                        }
                        else
                        {
                            d[i] = dt[k + 1] - dt[k];
                        }
                        k += 1;
                    }
                    // /* Compute second differences */
                    for (i = 0; i <= 3; i++)
                        d[i] = d[i + 1] - d[i];
                    B = 0.25d * p * (p - 1.0d);
                    Retval += B * (d[1] + d[2]);
                    if (iy + 2 >= TABSIZ)
                        goto done;

                    // /* Compute third differences */
                    for (i = 0; i <= 2; i++)
                        d[i] = d[i + 1] - d[i];
                    B = 2.0d * B / 3.0d;
                    Retval += (p - 0.5d) * B * d[1];
                    if (iy - 2 < 0 | iy + 3 > TABSIZ)
                        goto done;

                    // /* Compute fourth differences */
                    for (i = 0; i <= 1; i++)
                        d[i] = d[i + 1] - d[i];
                    B = 0.125d * B * (p + 1.0d) * (p - 2.0d);
                    Retval += B * (d[0] + d[1]);

                // /* Astronomical Almanac table is corrected by adding the expression
                // *     -0.000091 (ndot + 26)(year-1955)^2  seconds
                // * to entries prior to 1955 (AA page K8), where ndot is the secular
                // * tidal term in the mean motion of the Moon.
                // *
                // * Entries after 1955 are referred to atomic time standards and
                // * are not affected by errors in Lunar or planetary theory.
                // */
                done:
                    Retval *= 0.01d;
                    if (YearFraction < 1955.0d)
                    {
                        B = YearFraction - 1955.0d;
                        Retval += -0.000091d * (-25.8d + 26.0d) * B * B;
                    }
                }
            }

            return Retval;
        }
    }
}