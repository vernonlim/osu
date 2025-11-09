// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class PrecisionEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            double amplitude = 42.0; //governs how much very low precision values are worth
            double limit = 1.0; //precision strain for very high precision values (easy jumps)
            double shift = -10.0; //shifts the boundary between concave and convex part (shifts the curve)
            double pace = 42.0; //measures how fast strain decreases between easy and hard jumps

            double precision = note.MovementData.NotePrecision is null
                ? 0
                : limit + amplitude / (1 + Math.Exp(((double)note.MovementData.NotePrecision + shift) / pace));

            return precision / 18 * 42 * note.MovementData.ActionProbability;
        }
    }
}
