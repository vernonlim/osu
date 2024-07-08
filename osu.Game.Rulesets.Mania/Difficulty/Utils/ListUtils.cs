// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class ListUtils
    {
        // smoothing function 1
        public static double[] Smooth(double[] list, int halfIntervalSize)
        {
            double[] smoothedList = new double[list.Length];

            double windowSum = 0;

            for (int i = 0; i < Math.Min(halfIntervalSize, list.Length); i++)
            {
                windowSum += list[i];
            }

            for (int t = 0; t < list.Length; t++)
            {
                smoothedList[t] = (1.0 / (halfIntervalSize * 2)) * windowSum;

                if (t + halfIntervalSize < list.Length)
                {
                    windowSum += list[t + halfIntervalSize];
                }

                if (t - halfIntervalSize >= 0)
                {
                    windowSum -= list[t - halfIntervalSize];
                }
            }

            return smoothedList;
        }

        // smoothing function 2
        public static double[] Smooth2(double[] list, int halfIntervalSize)
        {
            double[] listBar = new double[list.Length];

            double windowSum = 0;

            for (int i = 0; i < Math.Min(halfIntervalSize, list.Length); i++)
            {
                windowSum += list[i];
            }

            double windowLen = Math.Min(halfIntervalSize, list.Length);

            for (int t = 0; t < list.Length; t++)
            {
                listBar[t] = windowSum / windowLen;

                if (t + halfIntervalSize < list.Length)
                {
                    windowSum += list[t + halfIntervalSize];
                    windowLen += 1;
                }

                if (t - halfIntervalSize >= 0)
                {
                    windowSum -= list[t - halfIntervalSize];
                    windowLen -= 1;
                }
            }

            return listBar;
        }
    }
}
