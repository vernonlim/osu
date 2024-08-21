// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnPressure
    {
        public static double[] EvaluateSameColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            double[][] perColumnPressure = new double[totalColumns][];
            double[][] perColumnDeltaTimes = new double[totalColumns][];

            for (int col = 0; col < totalColumns; col++)
            {
                IEnumerable<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                perColumnPressure[col] = new double[mapLength];
                perColumnDeltaTimes[col] = new double[mapLength];

                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in columnNotes)
                {
                    if (prev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (note.StartTime - prev.StartTime);
                        double val = 1 / delta * Math.Pow(delta + SunnySkill.LAMBDA_1 * Math.Pow(hitLeniency, 1.0 / 4.0), -1.0);

                        // the variables created earlier are filled with delta/val
                        for (int t = (int)prev.AdjustedStartTime; t < note.AdjustedStartTime; t++)
                        {
                            perColumnDeltaTimes[col][t] = delta;
                            perColumnPressure[col][t] = val * jackNerfer(delta);
                        }
                    }

                    prev = note;
                }

                perColumnPressure[col] = ListUtils.Smooth(perColumnPressure[col], (int)(500 / granularity));
            }

            double[] sameColumnPressure = new double[mapLength];

            for (int t = 0; t < mapLength; t++)
            {
                double sumWeights = 0;
                double sumValLambdaWeight = 0;

                for (int col = 0; col < totalColumns; col++)
                {
                    double val = perColumnPressure[col][t];
                    double weight = perColumnDeltaTimes[col][t] > 0 ? 1.0 / perColumnDeltaTimes[col][t] : 0;

                    sumWeights += weight;
                    sumValLambdaWeight += Math.Pow(val, SunnySkill.LAMBDA_N) * weight;
                }

                double firstPart = sumWeights > 0 ? sumValLambdaWeight / sumWeights : 0;

                double weightedAverage = Math.Pow(
                    firstPart,
                    1.0 / SunnySkill.LAMBDA_N
                );

                sameColumnPressure[t] = weightedAverage;
            }

            return sameColumnPressure;
        }

        private static double jackNerfer(double delta)
        {
            return 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4.0);
        }
    }
}
