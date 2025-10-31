// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public class SpeedEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            double alternatingSpeed = EvaluateAlternatingSpeedDifficultyOf(current);
            double sameDirectionSpeed = EvaluateSameDirectionSpeedDifficultyOf(current);
            double delayedSameDirectionSpeed = EvaluateDelayedSameDirectionSpeedDifficultyOf(current);
            double combinedSpeed = sameDirectionSpeed + 1.0 * delayedSameDirectionSpeed;

            return 11.0 * (Math.Max(alternatingSpeed, Math.Max(1.2 * sameDirectionSpeed, 1.3 * delayedSameDirectionSpeed)) + 0.0 * delayedSameDirectionSpeed) * Math.Pow(((CatchDifficultyHitObject)current).MovementData.ActionProbability, 2);

            //return 4.0 * Math.Sqrt(1.0 * Math.Pow(alternatingSpeed, 2) + Math.Pow(combinedSpeed, 2)
            //                       - 0.1 * alternatingSpeed * combinedSpeed);
        }

        public static double EvaluateSameDirectionSpeedDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? next = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.SameDirectionSpeed;
        }

        public static double EvaluateDelayedSameDirectionSpeedDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? next = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.DelayedSameDirectionSpeed;
        }

        public static double EvaluateAlternatingSpeedDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? next = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.AlternatingSpeed;
        }
    }
}
