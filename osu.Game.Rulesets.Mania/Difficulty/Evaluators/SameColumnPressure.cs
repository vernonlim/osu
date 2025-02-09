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
            double delta = note.NormalizedColumnDeltaTime;

            double pressure = 1.0 / delta * Math.Pow(delta + LAMBDA_1 * Math.Pow(hitLeniency, 1.0 / 4.0), -1.0);

            return pressure * jackNerfer(delta);
        }

        private static double jackNerfer(double deltaTime) =>
            1 - 7e-5 * Math.Pow(0.15 + Math.Abs(deltaTime - 0.08), -4);
    }
}
