// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Movement;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class MovementEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, double catcherSpeedMultiplier)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;
            CatchMovementData data = note.MovementData;

            if (data.NotePrecision is null)
            {
                return 0;
            }

            return data.ActionProbability * 3 * erf(1 / (double)data.NotePrecision);
        }

        private static double erf(double x)
        {
            // constants
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x);

            // A&S formula 7.1.26
            double t = 1.0 / (1.0 + p*x);
            double y = 1.0 - (((((a5*t + a4)*t) + a3)*t + a2)*t + a1)*t*Math.Exp(-x*x);

            return sign*y;
        }
    }
}
