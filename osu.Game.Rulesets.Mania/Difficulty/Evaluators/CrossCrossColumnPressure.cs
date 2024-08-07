// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class CrossCrossColumnPressure
    {
        // weights for each column (plus the extra one)
        private static readonly double[][] cross_matrix =
        [
            [-1],
            [0.075, 0.75, 0.75],
            [0.075, 0.075, 0.075, 0.075],
            [0.075, 0.075, 0.125, 0.075, 0.075],
            [0.175, 0.175, 0.05, 0.05, 0.175, 0.175],
            [0.175, 0.175, 0.05, 0.05, 0.05, 0.175, 0.175],
            [0.225, 0.225, 0.35, 0.05, 0.05, 0.35, 0.225, 0.225],
            [0.225, 0.225, 0.35, 0.05, 0.05, 0.05, 0.35, 0.225, 0.225],
            [0.275, 0.225, 0.45, 0.35, 0.05, 0.05, 0.35, 0.45, 0.225, 0.275],
            [0.275, 0.225, 0.45, 0.35, 0.05, 0.05, 0.05, 0.35, 0.45, 0.225, 0.275],
            [0.275, 0.225, 0.45, 0.35, 0.25, 0.05, 0.05, 0.25, 0.35, 0.45, 0.225, 0.275]
        ];

        public static double[] EvaluateCrossColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            double[] crossColumnPressure = new double[mapLength];

            for (int col = 2; col < totalColumns; col++)
            {
                IEnumerable<ManiaDifficultyHitObject> pairedNotesList;

                // merges two columns together, forming pairs of notes adjacent in time
                pairedNotesList = perColumnNoteList[col].Concat(perColumnNoteList[col - 2]);
                pairedNotesList = pairedNotesList.OrderBy(obj => obj.StartTime);

                ManiaDifficultyHitObject? prevPrev = null;
                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in pairedNotesList)
                {
                    if (prev is not null && prevPrev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (prev.StartTime - prevPrev.StartTime);
                        double val = 0.1 * Math.Pow(Math.Max(hitLeniency, delta), -2);

                        for (int t = (int)prev.AdjustedStartTime; t < note.AdjustedStartTime; t++)
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
