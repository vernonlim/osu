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

            double amplitude = 23.0; //governs how much very low precision values are worth
            double limit = 3.5; //precision strain for very high precision values (easy jumps)
            double shift = -40.0; //measures how fast strain decreases between easy and hard jumps (shifts the curve)
            double pace = 18.0; //normalises shift

            double precision = note.MovementData.NotePrecision is null
                ? 0
                : limit + amplitude / (1 + Math.Exp(((double)note.MovementData.NotePrecision + shift) / pace));

            return precision / 18 * 44; //* note.ReadingData.CombinedReadingFactor;
        }
    }
}
