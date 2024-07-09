// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class ListUtils
    {
        // smoothing function 1
        public static double[] Smooth(double[] list, int intervalSize)
        {
            int length = list.Length;
            double[] smoothedList = new double[length];
            double windowSum = 0;
            int windowSize = 2 * intervalSize + 1;
            int halfWindowSize = intervalSize;

            // Initial window sum
            for (int i = 0; i < Math.Min(windowSize, length); i++)
            {
                windowSum += list[i];
            }

            // Calculate the smoothed values for the initial part of the list
            for (int i = 0; i <= halfWindowSize && i < length; i++)
            {
                smoothedList[i] = windowSum / windowSize;
            }

            // Main loop for the rest of the list
            for (int t = halfWindowSize + 1; t < length - halfWindowSize; t++)
            {
                // Slide the window to the right
                windowSum += list[t + halfWindowSize] - list[t - halfWindowSize - 1];
                smoothedList[t] = windowSum / windowSize;
            }

            // Handle the tail part of the list
            for (int i = length - halfWindowSize; i < length; i++)
            {
                smoothedList[i] = windowSum / windowSize;

                if (i + halfWindowSize < length)
                {
                    windowSum -= list[i - halfWindowSize];
                }
            }

            return smoothedList;
        }

        public static double[] Smooth2(double[] list, int intervalSize)
        {
            int length = list.Length;
            double[] smoothedList = new double[length];
            double windowSum = 0;
            int windowSize = intervalSize;

            // Calculate initial window sum and length
            for (int i = 0; i < Math.Min(windowSize, length); i++)
            {
                windowSum += list[i];
            }

            double windowLen = Math.Min(windowSize, length);

            // Calculate smoothed values
            for (int t = 0; t < length; t++)
            {
                smoothedList[t] = windowSum / windowLen;

                // Slide the window to the right
                if (t + windowSize < length)
                {
                    windowSum += list[t + windowSize];
                    windowLen += 1;
                }

                if (t - windowSize >= 0)
                {
                    windowSum -= list[t - windowSize];
                    windowLen -= 1;
                }
            }

            return smoothedList;
        }
    }
}
