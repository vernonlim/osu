// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Movement;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;
            CatchMovementData data = note.MovementData;

            double precision = EvaluatePrecisionOf(note);
            double plsr = EvaluatePartialLocalStarRatingOf(note);
            double aim = EvaluateAimOf(note);

            double lsr = Math.Sqrt(Math.Pow(plsr, 2) + Math.Pow(1 - data.ActionProbability, 2) * Math.Pow(aim, 2));


            // To switch to precision-only mode, comment out this line
            // return aim;
            // return plsr * 0.85;
            return lsr * 0.9;
            // return speed;

            return precision;
        }

        public static double EvaluatePrecisionOf(CatchDifficultyHitObject current)
        {
            double precision = current.MovementData.NotePrecision is null ? 0 : 6 * DifficultyCalculationUtils.Erf(1 / (double)current.MovementData.NotePrecision);

            return precision;
        }

        public static double EvaluateAimOf(CatchDifficultyHitObject current)
        {
            double aim = current.MovementData.NoteAim is null ? 0 : 6 * DifficultyCalculationUtils.Erf(1 / (double)current.MovementData.NoteAim);

            return aim;
        }

        public static double EvaluateSpeedOf(CatchDifficultyHitObject current)
        {
            double speed = current.MovementData.NoteSpeed * current.MovementData.SpeedWeight * 0.9;

            return speed;
        }

        public static double EvaluatePartialLocalStarRatingOf(CatchDifficultyHitObject current)
        {
            double precision = EvaluatePrecisionOf(current);
            double aim = EvaluateAimOf(current);
            double speed = EvaluateSpeedOf(current);

            double plsr = current.MovementData.ActionProbability * Math.Sqrt(Math.Pow(precision, 2) + Math.Pow(speed, 2) + 0.2 * precision * speed);

            return plsr;
        }
    }
}
