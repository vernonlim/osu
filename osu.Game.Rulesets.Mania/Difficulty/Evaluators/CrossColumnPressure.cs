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
        public static double[] EvaluateCrossColumnPressure(List<ManiaDifficultyHitObject> noteList, int totalColumns)
        {
            int mapLength = (int)noteList.Last().EndTime + 1;

            // we suppose that there is an extra column at the edges that is just empty
            double[][] perColumnPressure = new double[totalColumns + 1][];

            for (int col = 0; col < totalColumns + 1; col++)
            {
                perColumnPressure[col] = new double[mapLength];

                IEnumerable<ManiaDifficultyHitObject> pairedNotesList;

                int currentColumn = col;

                if (col == 0)
                {
                    pairedNotesList = noteList.Where(obj => obj.Column == 0);
                }
                else if (col == totalColumns)
                {
                    pairedNotesList = noteList.Where(obj => obj.Column == currentColumn - 1);
                }
                else
                {
                    // merges two columns together, forming pairs of notes adjacent in time
                    pairedNotesList = noteList.Where(obj => obj.Column == currentColumn || obj.Column == currentColumn - 1);
                }

                ManiaDifficultyHitObject? prevPrev = null;
                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in pairedNotesList)
                {
                    if (prev is not null && prevPrev is not null)
                    {
                        double hitLeniency = 0.3 * Math.Pow(prev.GreatHitWindow / 500.0, 0.5);
                        double delta = 0.001 * (prev.StartTime - prevPrev.StartTime);
                        double val = 0.1 * Math.Pow(Math.Max(hitLeniency, delta), -2);

                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            perColumnPressure[col][t] = val;
                        }
                    }

                    prevPrev = prev;
                    prev = note;
                }
            }

            // weights for each column (plus the extra one)
            double[][] crossMatrix =
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

            // consolidates the values
            double[] crossColumnPressure = new double[mapLength];

            for (int t = 0; t < mapLength; t++)
            {
                double total = 0;

                for (int col = 0; col < totalColumns + 1; col++)
                {
                    total += perColumnPressure[col][t] * crossMatrix[totalColumns][col];
                }

                crossColumnPressure[t] = total;
            }

            // smooths it out
            crossColumnPressure = ListUtils.Smooth(crossColumnPressure);

            return crossColumnPressure;
        }
    }
}
