// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public static double[] EvaluatePressingIntensity(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double granularity)
        {
            double[] pressingIntensity = new double[mapLength];

            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject note in noteList)
            {
                if (prev is not null)
                {
                    double hitLeniency = 0.3 * Math.Pow(note.GreatHitWindow / 500.0, 0.5);
                    double deltaTime = 0.001 * (note.StartTime - prev.StartTime);

                    if (deltaTime < 1e-9)
                    {
                        pressingIntensity[(int)prev.QuantizedStartTime] += Math.Pow(0.02 * (4 / hitLeniency - SunnySkill.LAMBDA_3), 1.0 / 4.0);
                        continue;
                    }

                    double lnAmount = calculateLNAmount(prev.StartTime, note.StartTime, prev.CurrentHitObjects, note.CurrentHitObjects);

                    double v = 1 + SunnySkill.LAMBDA_2 * lnAmount;

                    if (deltaTime < 2 * hitLeniency / 3.0)
                    {
                        for (int t = (int)prev.QuantizedStartTime; t < note.QuantizedStartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                    else
                    {
                        for (int t = (int)prev.QuantizedStartTime; t < note.QuantizedStartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(hitLeniency / 6, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                }

                prev = note;
            }

            pressingIntensity = ListUtils.Smooth(pressingIntensity, (int)(500 / granularity));

            return pressingIntensity;
        }

        private static double streamBooster(double delta)
        {
            double val = 15.0 / delta;

            if (val > 180 && val < 340)
            {
                return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
            }

            return 1;
        }

        private static double calculateLNAmount(double startTime, double endTime, ManiaDifficultyHitObject?[] currentObjects, ManiaDifficultyHitObject?[] nextObjects)
        {
            // The size of the range of time being considered
            double timeOccupied = endTime - startTime;
            double lnAmount = 0;

            for (int column = 0; column < currentObjects.Length; column++)
            {
                ManiaDifficultyHitObject? currObj = currentObjects[column];
                ManiaDifficultyHitObject? nextObj = nextObjects[column];

                lnAmount += processObject(currObj);

                if (nextObj is not null && currObj is not null && nextObj.StartTime != currObj.StartTime)
                {
                    lnAmount += processObject(nextObj);
                }
            }

            return lnAmount / 1000;

            // local function to find the lnAmount surrounding an object
            double processObject(ManiaDifficultyHitObject? currObj)
            {
                if (currObj is null || currObj.BaseObject is Note)
                    return 0;

                double currentNoteStartTime = currObj.StartTime;
                double currentNoteEndTime = currObj.EndTime;

                // If the current note's end time is within the time range
                // i.e if the note is an LN and it's within the time range
                if (currentNoteEndTime > startTime)
                {
                    // All values cropped to within range
                    // The LN end or time range end
                    double lnEnd = Math.Min(currentNoteEndTime, endTime);
                    // 80ms after LN start or time range start
                    double fullLNStart = Math.Max(currentNoteStartTime + 80, startTime);
                    // LN start or time range start
                    double partialLNStart = Math.Max(currentNoteStartTime, startTime);
                    // 80ms after LN start or LN end (if shorter than 80ms)
                    double partialLNEnd = Math.Min(currentNoteStartTime + 80, lnEnd);

                    // Calculating the proportion of time the "full LN" takes up
                    double timeTakenFullLN = lnEnd - fullLNStart;
                    // If the above is negative, it means the 'full LN' starts after the LN already ends
                    // Meaning the proportion is 0
                    double proportionFullLN = Math.Max(timeTakenFullLN / timeOccupied, 0);

                    // Calculating the proportion of time the "partial LN" takes up
                    double timeTakenPartialLN = partialLNEnd - partialLNStart;
                    // If the above is negative, it either means that the LN is entirely outside of the time range
                    // or that all of the LN within the time range is 'full'
                    // Hence a the proportion would be 0
                    double proportionPartialLN = Math.Max(timeTakenPartialLN / timeOccupied, 0);

                    double fullLNScalingFactor = 1;
                    double partialLNScalingFactor = 0.5;

                    // Converts the unit to *seconds
                    double fullLNs = fullLNScalingFactor * proportionFullLN * timeOccupied;
                    double partialLNs = partialLNScalingFactor * proportionPartialLN * timeOccupied;

                    return fullLNs + partialLNs;
                }

                // the LN is not within the time range
                return 0;
            }
        }
    }
}
