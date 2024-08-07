// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class CrossColumnPressure
    {
        // weights for each column (plus the extra one)
        private static readonly double[][] cross_matrix =
        [
            [-1],
            [0.075, 0.075],
            [0.125, 0.05, 0.125],
            [0.125, 0.125, 0.125, 0.125],
            [0.175, 0.25, 0.05, 0.25, 0.175],
            [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
            [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
            [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
            [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
            [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
            [0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325]
        ];

        public static double[] EvaluateCrossColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            double[] crossColumnPressure = new double[mapLength];

            for (int col = 0; col < totalColumns + 1; col++)
            {
                IEnumerable<ManiaDifficultyHitObject> pairedNotesList;

                if (col == 0)
                {
                    pairedNotesList = perColumnNoteList[col];
                }
                else if (col == totalColumns)
                {
                    pairedNotesList = perColumnNoteList[col - 1];
                }
                else
                {
                    // merges two columns together, forming pairs of notes adjacent in time
                    pairedNotesList = perColumnNoteList[col].Concat(perColumnNoteList[col - 1]);
                    pairedNotesList = pairedNotesList.OrderBy(obj => obj.StartTime);
                }

                ManiaDifficultyHitObject? prevPrev = null;
                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in pairedNotesList)
                {
                    if (prev is not null && prevPrev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (prev.StartTime - prevPrev.StartTime);
                        double val = 0.16 * Math.Pow(Math.Max(hitLeniency, delta), -2);

                        for (int t = (int)prevPrev.AdjustedStartTime; t < prev.AdjustedStartTime; t++)
                        {
                            double weight = totalColumns < cross_matrix.Length ? cross_matrix[totalColumns][col] : 0.4;
                            crossColumnPressure[t] += val * weight;
                        }
                    }

                    prevPrev = prev;
                    prev = note;
                }
            }

            // smooths it out
            crossColumnPressure = ListUtils.Smooth(crossColumnPressure, (int)(500 / granularity));

            return crossColumnPressure;
        }
    }
}
