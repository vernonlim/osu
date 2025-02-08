// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ReleaseFactor
    {
        public const double LAMBDA_4 = 0.8;

        public static double EvaluateReleaseFactor(ManiaDifficultyHitObject note, double hitLeniency)
        {
            return 0;
        }
    }
}
