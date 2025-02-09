// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public const double LAMBDA_2 = 6.0;
        public const double LAMBDA_3 = 24.0;

        public static double EvaluatePressingIntensity(ManiaDifficultyHitObject note, double hitLeniency)
        {
            double delta = note.NormalizedDeltaTime;

            // If this is a chord
            if (delta <= 0.001)
            {
                return 1000 * Math.Pow(0.02 * (4.0 / hitLeniency - LAMBDA_3), 1.0 / 4.0);
            }

            double lnCount = calculateLnAmountAround(note);
            double v = 1 + LAMBDA_2 * lnCount;

            if (delta < 2.0 * hitLeniency / 3.0)
            {
                return 1 / delta
                    * Math.Pow(
                        0.08 * (1 / hitLeniency) * (1 - LAMBDA_3 * (1 / hitLeniency) * Math.Pow(delta - hitLeniency / 2, 2))
                        , 1 / 4.0)
                    * streamBooster(delta) * v;
            }
            else
            {
                return 1 / delta
                    * Math.Pow(0.08 * (1 / hitLeniency) * (1 - LAMBDA_3 * (1 / hitLeniency) * Math.Pow(hitLeniency / 6, 2))
                    , 1 / 4.0)
                    * streamBooster(delta) * v;
            }
        }

        private static double streamBooster(double delta)
        {
            double val = 7.5 / delta;

            if (val > 160 && val < 360)
            {
                return 1 + 1.7e-7 * (val - 160) * Math.Pow(val - 360, 2);
            }

            return 1;
        }

        private static double calculateLnAmountAround(ManiaDifficultyHitObject note)
        {
            ManiaDifficultyHitObject? previousInTime = (ManiaDifficultyHitObject?)note.Previous(0);

            if (previousInTime is null)
                return 0;

            double lnAmount = 0;

            for (int column = 0; column < note.PreviousHitObjects.Length; column++)
            {
                ManiaDifficultyHitObject? previousInColumn = note.PreviousHitObjects[column].FirstOrDefault();

                lnAmount += calculateLnAmountFor(previousInTime.StartTime, note.StartTime, previousInColumn);
            }

            return lnAmount / 1000;
        }

        private static double calculateLnAmountFor(double startTime, double endTime, ManiaDifficultyHitObject? note)
        {
            if (note is null || note.BaseObject is Note)
                return 0;

            double currentNoteStartTime = note.StartTime;
            double currentNoteEndTime = note.EndTime;

            if (currentNoteEndTime <= startTime)
                return 0;

            // All values cropped to within range
            // The LN end or time range end
            double lnEnd = Math.Min(currentNoteEndTime, endTime);
            // 120ms after start or time range start
            double longLnStart = Math.Max(currentNoteStartTime + 120, startTime);
            // 60ms after start or time range start
            double midLnStart = Math.Max(currentNoteStartTime + 60, startTime);
            // 120ms after start or LN end
            double midLnEnd = Math.Min(currentNoteStartTime + 120, lnEnd);

            // Calculating the amount of time the "mid LN" takes up
            double timeTakenMidLn = midLnEnd - midLnStart;
            // If the above is negative, it means the 'full LN' starts after the LN already ends
            // Meaning the amount is 0
            double amountMidLn = Math.Max(timeTakenMidLn, 0);

            // Calculating the amount of time the "long LN" takes up
            double timeTakenLongLn = lnEnd - longLnStart;
            // If the above is negative, it either means that the LN is entirely outside of the time range
            // or that all of the LN within the time range is <60ms or 'mid'
            // Hence a the amount would be 0
            double amountLongLn = Math.Max(timeTakenLongLn, 0);

            const double long_ln_scaling_factor = 1.0;
            const double mid_ln_scaling_factor = 1.3;

            // Scales each portion
            double fullLNs = long_ln_scaling_factor * amountLongLn;
            double partialLNs = mid_ln_scaling_factor * amountMidLn;

            return fullLNs + partialLNs;
        }
    }
}
