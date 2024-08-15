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
        public static double[] EvaluatePressingIntensity(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            double[] pressingIntensity = new double[mapLength];

            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject note in noteList)
            {
                if (prev is not null)
                {
                    double deltaTime = 0.001 * (note.StartTime - prev.StartTime);

                    // if notes are less than 1ms apart
                    if (deltaTime <= 1e-3)
                    {
                        double value = 1000 / granularity * Math.Pow(0.02 * (4 / hitLeniency - SunnySkill.LAMBDA_3), 1.0 / 4.0);
                        pressingIntensity[(int)prev.AdjustedStartTime] += value;
                        // prev = note is skipped
                        // but that is fine because the only value that matters is the StartTime
                        // which is identical
                        continue;
                    }

                    double lnCount = calculateLnAmount(prev.StartTime, note.StartTime, prev.CurrentHitObjects, note.CurrentHitObjects);

                    double v = 1 + SunnySkill.LAMBDA_2 * lnCount;

                    if (deltaTime < 2 * hitLeniency / 3.0)
                    {
                        for (int t = (int)prev.AdjustedStartTime; t < note.AdjustedStartTime; t++)
                        {
                            // pressingIntensity[t] += 1 / deltaTime
                            //                        * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * hitLeniency / 4 + SunnySkill.LAMBDA_3 * deltaTime / 3), 1 / 4.0)
                            //                        * streamBooster(deltaTime) * v;
                            double value = 1 / deltaTime
                                        * Math.Pow(0.08 * (1 / hitLeniency) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0)
                                        * v;
                            pressingIntensity[t] += value;
                        }
                    }
                    else
                    {
                        for (int t = (int)prev.AdjustedStartTime; t < note.AdjustedStartTime; t++)
                        {
                            pressingIntensity[t] += 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / hitLeniency) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(hitLeniency / 6, 2)), 1 / 4.0)
                                                   * v;
                        }
                    }
                }

                prev = note;
            }

            pressingIntensity = ListUtils.Smooth(pressingIntensity, (int)(500 / granularity));

            return pressingIntensity;
        }

        // private static double streamBooster(double delta)
        // {
        //     double val = 15.0 / delta;

        //     if (val > 180 && val < 340)
        //     {
        //         return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
        //     }

        //     return 1;
        // }

        private static double calculateLnAmount(double startTime, double endTime, ManiaDifficultyHitObject?[] currentObjects, ManiaDifficultyHitObject?[] nextObjects)
        {
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

                if (currentNoteEndTime <= startTime)
                    return 0;

                // All values cropped to within range
                // The LN end or time range end
                double lnEnd = Math.Min(currentNoteEndTime, endTime);
                // 80ms after LN start or time range start
                double fullLnStart = Math.Max(currentNoteStartTime + 80, startTime);
                // LN start or time range start
                double partialLnStart = Math.Max(currentNoteStartTime, startTime);
                // 80ms after LN start or LN end (if shorter than 80ms)
                double partialLnEnd = Math.Min(currentNoteStartTime + 80, lnEnd);

                // Calculating the proportion of time the "full LN" takes up
                double timeTakenFullLn = lnEnd - fullLnStart;
                // If the above is negative, it means the 'full LN' starts after the LN already ends
                // Meaning the proportion is 0
                double proportionFullLn = Math.Max(timeTakenFullLn, 0);

                // Calculating the proportion of time the "partial LN" takes up
                double timeTakenPartialLn = partialLnEnd - partialLnStart;
                // If the above is negative, it either means that the LN is entirely outside of the time range
                // or that all of the LN within the time range is 'full'
                // Hence a the proportion would be 0
                double proportionPartialLn = Math.Max(timeTakenPartialLn, 0);

                const double full_ln_scaling_factor = 1;
                const double partial_ln_scaling_factor = 0.5;

                // Converts the unit to *seconds
                double fullLNs = full_ln_scaling_factor * proportionFullLn;
                double partialLNs = partial_ln_scaling_factor * proportionPartialLn;

                return fullLNs + partialLNs;
            }
        }
    }
}
