// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

            if (data.NotePrecision is null)
            {
                return 0;
            }

            return data.ActionProbability * 6 * DifficultyCalculationUtils.Erf(1 / (double)data.NotePrecision);
        }
    }
}
