// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class Unevenness
    {
        public static double[] EvaluateUnevenness(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double granularity)
        {
            // some sort of value representing distance between notes in different columns
            double[][] perColumnUnevenness = new double[totalColumns - 1][];
            double[][] perColumnDeltaTimes = new double[totalColumns][];

            for (int col = totalColumns - 1; col >= 0; col--)
            {
                int currentColumn = col;
                List<ManiaDifficultyHitObject> columnNotes = noteList.Where(obj => obj.Column == currentColumn).ToList();

                perColumnDeltaTimes[col] = new double[mapLength];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    ManiaDifficultyHitObject prev = columnNotes[i - 1];
                    ManiaDifficultyHitObject curr = columnNotes[i];

                    double delta = 0.001 * (curr.StartTime - prev.StartTime);

                    // the variables created earlier are filled with delta/val
                    for (int t = (int)prev.QuantizedStartTime; t < curr.QuantizedStartTime; t++)
                    {
                        perColumnDeltaTimes[col][t] = delta;
                    }
                }

                if (col == totalColumns - 1)
                    continue;

                perColumnUnevenness[col] = new double[mapLength];

                for (int t = 0; t < mapLength; t++)
                {
                    perColumnUnevenness[col][t] = Math.Abs(perColumnDeltaTimes[col][t] - perColumnDeltaTimes[col + 1][t]) + Math.Max(0, Math.Max(perColumnDeltaTimes[col + 1][t], perColumnDeltaTimes[col][t]) - 0.3);
                }
            }

            double[] unevenness = new double[mapLength];

            for (int t = 0; t < mapLength; t++)
            {
                for (int col = 0; col < totalColumns - 1; col++)
                {
                    if (perColumnUnevenness[col][t] < 0.02)
                    {
                        unevenness[t] = Math.Min(0.75 + 0.5 * Math.Max(perColumnDeltaTimes[col + 1][t], perColumnDeltaTimes[col][t]), 1);
                    }
                    else if (perColumnUnevenness[col][t] < 0.07)
                    {
                        unevenness[t] = Math.Min(0.65 + 5 * perColumnUnevenness[col][t] + 0.5 * Math.Max(perColumnDeltaTimes[col + 1][t], perColumnDeltaTimes[col][t]), 1);
                    }
                    else
                    {
                        unevenness[t] = 1;
                    }
                }
            }

            unevenness = ListUtils.Smooth2(unevenness, (int)(500 / granularity));

            return unevenness;
        }
    }
}
