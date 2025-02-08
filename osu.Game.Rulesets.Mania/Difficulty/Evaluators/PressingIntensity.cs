// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public const double LAMBDA_2 = 6.0;
        public const double LAMBDA_3 = 24.0;

        public static double EvaluatePressingIntensity(ManiaDifficultyHitObject note, double hitLeniency)
        {
            return 0;
        }
    }
}
