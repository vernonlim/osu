// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class DifficultySkill : Skill
    {
        // Evaluator balancing constants
        public const double LAMBDA_N = 5.0;

        // Star Rating balancing constants
        private const double w_0 = 0.4;
        private const double w_1 = 2.7;
        private const double w_2 = 0.27;
        private const double p_0 = 1.0;
        private const double p_1 = 1.5;

        private double hitLeniency;
        private int totalColumns;

        private readonly List<double> difficultyValues = new List<double>();

        public DifficultySkill(Mod[] mods, int totalColumns, double od)
            : base(mods)
        {
            // A value shared between skills representing how "lenient" the map is. 
            // Scales inversely with OD.
            // Uses a custom value instead of `getHitWindow300` to match stable (and Sunny's work).
            hitLeniency = 0.3 * Math.Pow(
                (64.5 - Math.Ceiling(od * 3.0)) / 500.0,
                0.5);

            // Reduces the slope of the hitLeniency curve from OD0 to ~OD6.3, punishing low ODs less.
            hitLeniency = Math.Min(hitLeniency, 0.6 * (hitLeniency - 0.09) + 0.09);

            this.totalColumns = totalColumns;
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject note = (ManiaDifficultyHitObject)current;

            double j = SameColumnPressure.EvaluateSameColumnPressure(note, hitLeniency);
            double x = CrossColumnPressure.EvaluateCrossColumnPressure(note, hitLeniency);
            double p = PressingIntensity.EvaluatePressingIntensity(note, hitLeniency);
            double a = Unevenness.EvaluateUnevenness(note, hitLeniency);
            double r = ReleaseFactor.EvaluateReleaseFactor(note, hitLeniency);
            int ku = KeyUsage.EvaluateKeyUsage(note);
            int c = NoteCount.EvaluateNoteCount(note);

            // Replace with the actual d calculation
            double d = j + x + p + a + r + ku + c;

            difficultyValues.Add(d);
        }

        public override double DifficultyValue()
        {
            // Calculate SR from difficultyValues
            return difficultyValues.Sum();
        }
    }
}