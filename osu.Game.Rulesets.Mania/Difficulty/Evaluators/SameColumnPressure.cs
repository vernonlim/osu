// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnPressure
    {
        public const double LAMBDA_1 = 0.11;

        public static double EvaluateSameColumnPressure(ManiaDifficultyHitObject note, double hitLeniency)
        {
            return 0;
        }
    }
}