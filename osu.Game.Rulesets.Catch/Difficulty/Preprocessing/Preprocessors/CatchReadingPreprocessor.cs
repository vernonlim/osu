// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors
{
    public static class CatchReadingPreprocessor
    {
        private const double rhythm_penalty = 1.0;
        private const double rhythm_range = 20.0;

        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            List<CatchDifficultyHitObject> cdhos = hitObjects.Select(n => (CatchDifficultyHitObject)n).ToList();

            localRhythmPenalty(cdhos);
        }

        private static void localRhythmPenalty(List<CatchDifficultyHitObject> cdhos)
        {
            foreach (var note in cdhos)
            {
                if (note.MovementData.ActionProbability == 0) continue;

                double timeDifference = Math.Abs(note.MovementData.EffectiveTime - note.StartTime);

                double multiplier = Math.Min(timeDifference / rhythm_range, 1.0);

                double penalty = (1.0 - rhythm_penalty) * (1.0 - multiplier);

                note.ReadingData.ReadingFactors.Add(1.0 - penalty);
            }
        }
    }
}
