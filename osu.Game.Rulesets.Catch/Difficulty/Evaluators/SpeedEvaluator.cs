// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public class SpeedEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);
            CatchDifficultyHitObject? next = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            double speed = note.MovementData.RawNoteSpeed;

            return speed * 12 * 100;
        }
    }
}
