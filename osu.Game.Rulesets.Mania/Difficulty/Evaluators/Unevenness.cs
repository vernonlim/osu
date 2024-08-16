// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class Unevenness
    {
        public static double[] EvaluateUnevenness(List<ManiaDifficultyHitObject>[] perColumnNoteList, List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            // could be shared by passing in or with another structure
            bool[][] keyUsageByColumn = KeyUsage.PerColumnKeyUsage(noteList, totalColumns, mapLength, granularity);

            // some sort of value representing distance between notes in different columns
            double[][] perColumnUnevenness = new double[totalColumns - 1][];
            double[][] perColumnDeltaTimes = new double[totalColumns][];

            for (int col = 0; col < totalColumns - 1; col++)
            {
                perColumnUnevenness[col] = new double[mapLength];
            }

            for (int col = totalColumns - 1; col >= 0; col--)
            {
                List<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                perColumnDeltaTimes[col] = new double[mapLength];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    ManiaDifficultyHitObject prev = columnNotes[i - 1];
                    ManiaDifficultyHitObject curr = columnNotes[i];

                    double delta = 0.001 * (curr.StartTime - prev.StartTime);

                    // the variables created earlier are filled with delta/val
                    for (int t = (int)prev.AdjustedStartTime; t < curr.AdjustedStartTime; t++)
                    {
                        perColumnDeltaTimes[col][t] = delta;
                    }
                }

                if (col == totalColumns - 1)
                    continue;
            }

            for (int t = 0; t < mapLength; t++)
            {
                List<int> columns = new();

                for (int col = 0; col < totalColumns; col++)
                {
                    if (keyUsageByColumn[col][t])
                    {
                        columns.Add(col);
                    }
                }

                for (int colIndex = 0; colIndex < columns.Count - 1; colIndex++)
                {
                    if (columns[colIndex + 1] > totalColumns - 1)
                        continue;

                    perColumnUnevenness[columns[colIndex]][t] = Math.Abs(perColumnDeltaTimes[columns[colIndex]][t] - perColumnDeltaTimes[columns[colIndex + 1]][t]) + Math.Max(0, Math.Max(perColumnDeltaTimes[columns[colIndex + 1]][t], perColumnDeltaTimes[columns[colIndex]][t]) - 0.3);
                }
            }

            double[] unevenness = new double[mapLength];

            for (int t = 0; t < mapLength; t++)
            {
                List<int> columns = new();

                for (int col = 0; col < totalColumns; col++)
                {
                    if (keyUsageByColumn[col][t])
                    {
                        columns.Add(col);
                    }
                }

                unevenness[t] = 1;

                for (int colIndex = 0; colIndex < columns.Count - 1; colIndex++)
                {
                    if (columns[colIndex + 1] > totalColumns - 1)
                        continue;

                    if (perColumnUnevenness[columns[colIndex]][t] < 0.02)
                    {
                        unevenness[t] *= Math.Min(0.75 + 0.5 * Math.Max(perColumnDeltaTimes[columns[colIndex + 1]][t], perColumnDeltaTimes[columns[colIndex]][t]), 1);
                    }
                    else if (perColumnUnevenness[columns[colIndex]][t] < 0.07)
                    {
                        unevenness[t] *= Math.Min(0.65 + 5 * perColumnUnevenness[columns[colIndex]][t] + 0.5 * Math.Max(perColumnDeltaTimes[columns[colIndex + 1]][t], perColumnDeltaTimes[columns[colIndex]][t]), 1);
                    }
                }
            }

            unevenness = ListUtils.Smooth2(unevenness, (int)(500 / granularity));

            return unevenness;
        }
    }
}
