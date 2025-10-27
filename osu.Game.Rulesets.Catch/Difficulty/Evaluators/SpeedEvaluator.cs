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
            return 5.0 * Math.Sqrt(1.0 * Math.Pow(EvaluateAlternatingSpeedDifficultyOf(current), 2) + 12.0 * Math.Pow(EvaluateSameDirectionSpeedDifficultyOf(current), 2)
                             - 1.0 * EvaluateAlternatingSpeedDifficultyOf(current) * EvaluateSameDirectionSpeedDifficultyOf(current));
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

            return note.MovementData.SameDirectionSpeed + 1.3 * note.MovementData.DelayedSameDirectionSpeed;
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
