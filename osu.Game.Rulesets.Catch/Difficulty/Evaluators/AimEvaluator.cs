// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;


namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;


            // Parameters used for precision may also be applied here
            double amplitude = 100.0; //governs how much very low precision values are worth
            double limit = 2.0; //precision strain for very high precision values (easy jumps)
            double pace = 50.0; //measures how fast strain decreases between easy and hard jumps
            double shift = 70.0; //shifts the curve


            double aim = note.MovementData.NoteAim is null
                ? 0
                : limit + amplitude / (1 + 2 * Math.Exp(((double)note.MovementData.NoteAim + shift + 5) / (pace)));


            return aim / 18 * 50;
        }
    }
}
