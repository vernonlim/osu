// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SunnyEvaluator
    {
        // Balancing constants
        private const double lambda_n = 4.0;
        private const double lambda_1 = 0.11;
        private const double lambda_2 = 5.0;
        private const double lambda_3 = 8.0;
        private const double lambda_4 = 0.1;
        private const double w_0 = 0.37;
        private const double w_1 = 2.7;
        private const double w_2 = 0.27;
        private const double p_0 = 1.2;
        private const double p_1 = 1.5;

        // Weights for each column (plus the extra one)
        private double[][] crossMatrix =
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

        public double EvaluateSameColumnIntensityAt(ManiaDifficultyHitObject[] currentObjects)
        {
            ManiaDifficultyHitObject[] nextObjects = currentObjects.Select(x => (ManiaDifficultyHitObject)x.Next(0)).ToArray();

            int totalColumns = currentObjects.Length;

            double weightSum = 0;
            double weightedValueSum = 0;

            for (int column = 0; column < totalColumns; column++)
            {
                // TODO: Need to ask why this formula is.
                double hitLeniency = 0.3 * Math.Sqrt(currentObjects[column].GreatHitWindow / 500);
                double deltaTime = (currentObjects[column].StartTime - nextObjects[column].StartTime) / 1000.0;
                double intensity = 1 / (Math.Pow(deltaTime, 2) + deltaTime * lambda_1 * Math.Pow(hitLeniency, 1 / 4.0));

                // TODO: Smoothing here. Probably impossible so I'll look at alternatives

                double weight = 1.0 / deltaTime;

                weightSum += weight;
                weightedValueSum += Math.Pow(intensity, lambda_n) * weight;
            }

            return Math.Pow(weightedValueSum / Math.Max(1e-9, weightSum), 1 / lambda_n);
        }

        public double EvaluateCrossColumnIntensityAt(ManiaDifficultyHitObject[] currentObjects)
        {
            ManiaDifficultyHitObject[] nextObjects = currentObjects.Select(x => (ManiaDifficultyHitObject)x.Next(0)).ToArray();

            int totalColumns = currentObjects.Length;

            double totalIntensity = 0;

            for (int column = 0; column < totalColumns + 1; column++)
            {
                ManiaDifficultyHitObject pairedPreviousObject;

                // Inefficient but it'll have to do
                if (column == 0)
                {
                    pairedPreviousObject = (ManiaDifficultyHitObject)currentObjects[column].Previous(0);
                }
                else if (column == totalColumns)
                {
                    pairedPreviousObject = (ManiaDifficultyHitObject)currentObjects[column - 1].Previous(0);
                }
                else
                {
                    pairedPreviousObject = currentObjects[column].CrossColumnPreviousObject!;
                }

                double hitLeniency = 0.3 * Math.Sqrt(currentObjects[column].GreatHitWindow / 500);
                double deltaTime = 0.001 * (currentObjects[column].StartTime - pairedPreviousObject.StartTime);
                double intensity = 0.1 * (1 / Math.Pow(Math.Max(hitLeniency, deltaTime), 2));

                totalIntensity += intensity * crossMatrix[totalColumns][column];
            }

            // There'd be Smoothing here. Yayy!!!!

            return totalIntensity;
        }

        // Use the single list of hit objects for this one.
        public double EvaluatePressingIntensityAt(ManiaDifficultyHitObject currentObject)
        {

        }

        public double EvaluateUnevennessIntensityAt(ManiaDifficultyHitObject[] currentObjects)
        {

        }

        public double EvaluateReleaseFactorAt(int millisecond, ManiaDifficultyHitObject[] currentObjects)
        {

        }

        private double countHeldBodiesAt(int millisecond, ManiaDifficultyHitObject[] currentObjects)
        {
            double count = 0;

            for (int column = 0; column < currentObjects.Length; column++)
            {
                int currentNoteStartTime = currentObjects[column].StartTime;
                int currentNoteEndTime = currentObjects[column].EndTime ?? -1;

                // If the current millisecond is before the end time of the previous hit note
                if (currentNoteEndTime > millisecond)
                {
                    // The first 80 milliseconds of a hold note are considered half a press, as they're easier.
                    // TODO: replace with a sigmoid
                    if (millisecond - currentNoteStartTime < 80)
                    {
                        count += 0.5;
                    }
                    else
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }
    }
}
