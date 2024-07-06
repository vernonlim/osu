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
        private readonly double[][] crossMatrix =
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

        public double EvaluatePressingIntensityAt(ManiaDifficultyHitObject currentObject, ManiaDifficultyHitObject[] currentObjects)
        {
            ManiaDifficultyHitObject nextObj = (ManiaDifficultyHitObject)currentObject.Next(0);

            double hitLeniency = 0.3 * Math.Sqrt(currentObject.GreatHitWindow / 500);

            double deltaTime = 0.001 * (nextObj.StartTime - currentObject.StartTime);

            if (deltaTime < 1e-9)
                return Math.Pow(0.02 * (4 / hitLeniency - lambda_3), 1 / 4.0);

            // TODO: wtf is this
            double lnCount = 0;
            for (int ms = currentObject.StartTime; ms < nextObj.StartTime; ms++)
                lnCount += countLNBodiesAt(ms, currentObjects);

            double v = 1 + lambda_2 * 0.001 * lnCount;

            // There has to be a better way to do this
            if (deltaTime < 2 * hitLeniency / 3.0)
            {
                return (1 / deltaTime) * Math.Pow(0.08 * (1 / deltaTime) * (1 - lambda_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0) * streamBooster() * v;
            }

            return (1 / deltaTime) * Math.Pow(0.08 * (1 / deltaTime) * (1 - lambda_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 6, 2)), 1 / 4.0) * streamBooster() * v;

            // TODO: clean up this local function
            double streamBooster()
            {
                double val = 15.0 / deltaTime;

                if (val > 180 && val < 340)
                {
                    return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
                }

                return 1;
            }
        }

        public double EvaluateUnevennessIntensityAt(ManiaDifficultyHitObject[] currentObjects)
        {
            ManiaDifficultyHitObject[] nextObjects = currentObjects.Select(x => (ManiaDifficultyHitObject)x.Next(0)).ToArray();

            int totalColumns = currentObjects.Length;

            double unevenness = 1;

            for (int column = 0; column < totalColumns - 1; column++)
            {
                double curColumnDeltaTime = 0.001 * (nextObjects[column].StartTime - currentObjects[column].StartTime);
                double adjColumnDeltaTime = 0.001 * (nextObjects[column + 1].StartTime - currentObjects[column + 1].StartTime);
                double dynamicKeyStroke = Math.Abs(curColumnDeltaTime - adjColumnDeltaTime) + Math.Max(0, Math.Max(adjColumnDeltaTime, curColumnDeltaTime - 0.3));

                if (dynamicKeyStroke < 0.02)
                    unevenness *= Math.Min(0.75 + 0.5 * Math.Max(curColumnDeltaTime, adjColumnDeltaTime), 1);
                else if (dynamicKeyStroke < 0.07)
                    unevenness *= Math.Min(0.65 + 5 * dynamicKeyStroke + 0.5 * Math.Max(curColumnDeltaTime, adjColumnDeltaTime), 1);
            }

            return unevenness;
        }

        public double EvaluateReleaseFactorAt(ManiaDifficultyHitObject currentObject, ManiaDifficultyHitObject[] currentObjects)
        {
            ManiaDifficultyHitObject? currLn = currentObject.PrevLongNote;
            ManiaDifficultyHitObject? nextObjInColumn = (ManiaDifficultyHitObject?)currLn?.Next(0);

            ManiaDifficultyHitObject? nextLn = currLn?.NextLongNote;
            ManiaDifficultyHitObject? nextNoteAfterLn = (ManiaDifficultyHitObject?)nextLn?.Next(0);

            double hitLeniency = 0.3 * Math.Sqrt(currentObject.GreatHitWindow / 500);

            if (currLn is null || nextObjInColumn is null || nextLn is null || nextNoteAfterLn is null)
                return 0;

            double currI = 0.001 * Math.Abs(currLn.EndTime!.Value - currLn.StartTime - 80.0) / hitLeniency;
            double nextI = 0.001 * Math.Abs(nextObjInColumn.StartTime - currLn.EndTime.Value - 80.0) / hitLeniency;
            double currHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (currI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));

            double currJ = 0.001 * Math.Abs(nextLn.EndTime!.Value - nextLn.StartTime - 80.0) / hitLeniency;
            double nextJ = 0.001 * Math.Abs(nextNoteAfterLn.StartTime - nextLn.EndTime.Value - 80.0) / hitLeniency;
            double nextHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (currJ - 0.75)) + Math.Exp(-5 * (nextJ - 0.75)));

            double deltaR = 0.001 * (nextLn.EndTime.Value - currLn.EndTime.Value);

            return 0.08 * (1 / Math.Sqrt(deltaR)) * (1 / hitLeniency) * (1 + lambda_4 * (currHeadSpacingIndex + nextHeadSpacingIndex));
        }

        // ReSharper disable once InconsistentNaming
        private double countLNBodiesAt(int millisecond, ManiaDifficultyHitObject[] currentObjects)
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
