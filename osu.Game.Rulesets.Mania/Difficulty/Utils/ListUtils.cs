// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class ListUtils
    {
        // smoothing function 1
        public static double[] Smooth(double[] list)
        {
            double[] smoothedList = new double[list.Length];

            double windowSum = 0;

            for (int i = 0; i < Math.Min(500, list.Length); i++)
            {
                windowSum += list[i];
            }

            for (int t = 0; t < list.Length; t++)
            {
                smoothedList[t] = 0.001 * windowSum;

                if (t + 500 < list.Length)
                {
                    windowSum += list[t + 500];
                }

                if (t - 500 >= 0)
                {
                    windowSum -= list[t - 500];
                }
            }

            return smoothedList;
        }

        // smoothing function 2
        public static double[] Smooth2(double[] list)
        {
            double[] listBar = new double[list.Length];

            double windowSum = 0;

            for (int i = 0; i < Math.Min(500, list.Length); i++)
            {
                windowSum += list[i];
            }

            double windowLen = Math.Min(500, list.Length);

            for (int t = 0; t < list.Length; t++)
            {
                listBar[t] = windowSum / windowLen;

                if (t + 500 < list.Length)
                {
                    windowSum += list[t + 500];
                    windowLen += 1;
                }

                if (t - 500 >= 0)
                {
                    windowSum -= list[t - 500];
                    windowLen -= 1;
                }
            }

            return listBar;
        }
    }
}
