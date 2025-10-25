// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Preprocessors;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.015;

        private float catcherWidth;

        public override int Version => 20250306;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            double totalMovements = DifficultyHitObjects
                                    .Select(n => (CatchDifficultyHitObject)n)
                                    .Select(n => n.MovementData.ActionProbability)
                                    .Sum();

            double totalAims = DifficultyHitObjects
                               .Select(n =>
                                   (CatchDifficultyHitObject)n)
                               .Count(n =>
                                   n.MovementData.ActionProbability < 0.03 && n.MovementData.NoteAim != null);

            double totalActions = totalMovements + totalAims;

            int hyperWalkCount = DifficultyHitObjects.Count(n => ((CatchDifficultyHitObject)n).MovementData.IsHyperWalk);

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.EffectiveTime).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();
            List<double> aimStrains = skills.OfType<Aim>().Single().GetObjectStrains().ToList();
            // List<double> readingStrains = skills.OfType<Reading>().Single().GetObjectStrains().ToList();

            List<double> zeroes = Enumerable.Repeat(0.0, precisionStrains.Count).ToList();

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, aimStrains);

            // 2B Hotfix
            for (int i = 1; i < combinedStrains.Count - 1; i++)
            {
                if (startTimes[i] - startTimes[i - 1] <= 2)
                {
                    combinedStrains[i + 1] = 0;
                    combinedStrains[i] = 0;
                    combinedStrains[i - 1] = 0;
                }
            }

            double sr = calculateDifficultyValue(combinedStrains) * difficulty_multiplier;

            double precision = calculateDifficultyValue(combineStrains(actionProbabilities, precisionStrains, zeroes, zeroes)) * difficulty_multiplier;
            double speed = calculateDifficultyValue(combineStrains(actionProbabilities, speedStrains, zeroes, zeroes)) * difficulty_multiplier;
            double aim = calculateDifficultyValue(combineStrains(actionProbabilities, zeroes, zeroes, aimStrains)) * difficulty_multiplier;

            // temporary rescaling to help with testing
            const double scaling_point = 5.8;
            Func<double, double> srScaler = d => d * Math.Max(Math.Min(1 + Math.Max((d - scaling_point) / 2.5, 0) * 0.4, 1.3), Math.Min(1 + Math.Max((d - 3.0) / 3.0, 0) * 0.15, 1.04));
            sr = srScaler(sr);

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                PrecisionSR = srScaler(precision),
                SpeedSR = srScaler(speed),
                AimSR = srScaler(aim),
            };

            return attributes;
        }

        /// <summary>
        /// Replicates StrainSkill behaviour with Strain Peaks.
        /// </summary>
        /// <param name="strains"></param>
        /// <param name="accuracy"></param>
        /// <returns></returns>
        private double calculateDifficultyValue(List<double> strains, double accuracy = 1.0)
        {
            const double decay_weight = 0.9;

            double missPercentage = 3.0 / 2.0 * (1.0 - accuracy);

            int missCount = Math.Max((int)Math.Round((missPercentage) * strains.Count), 0);

            // Missing one note can allow you to hit another with much less difficulty, this is a very rough estimate for that
            missCount = (int)(missCount);

            List<double> sorted = strains.OrderByDescending(x => x).ToList();

            List<double> remaining = sorted.Skip(missCount).ToList();

            double difficulty = 0.0;
            double weight = 1.0;

            foreach (double strain in remaining)
            {
                difficulty += strain * weight;
                weight *= decay_weight;
            }

            return difficulty;
        }

        private List<double> combineStrains(List<double> actionProbabilities, List<double> precisionStrains, List<double> speedStrains, List<double> aimStrains)
        {
            List<double> combinedStrains = new List<double>();

            for (int i = 0; i < precisionStrains.Count; i++)
            {
                double actionProbability = actionProbabilities[i];
                double precisionStrain = precisionStrains[i];
                double speedStrain = speedStrains[i];
                double aimStrain = aimStrains[i];

                combinedStrains.Add(CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, aimStrain));
            }

            return combinedStrains;
        }

        public static double CalculatePartialLocalStarRating(double actionProbability, double precisionStrain, double speedStrain)
        {
            return actionProbability * Math.Sqrt(Math.Pow(precisionStrain, 2) + Math.Pow(speedStrain, 2));
        }

        public static double CalculateLocalStarRating(double actionProbability, double precisionStrain, double speedStrain, double aimStrain)
        {
            double plsr = CalculatePartialLocalStarRating(actionProbability, precisionStrain, speedStrain);

            return Math.Sqrt(Math.Pow(plsr, 2) + Math.Pow(1 - actionProbability, 2) * Math.Pow(aimStrain, 2));
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<CatchDifficultyHitObject> noteObjects = new List<CatchDifficultyHitObject>();

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, catcherWidth, objects, noteObjects, objects.Count));

                lastObject = hitObject;
            }

            CatchMovementPreprocessor.Process(objects);
            CatchDifficultyPreprocessor.Process(objects);
            CatchReadingPreprocessor.Process(objects);
            // CatchPreprocessorTest.Process(objects, beatmap);

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            catcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty);

            return new Skill[]
            {
                new Aim(mods),
                new Precision(mods),
                new Speed(mods),
                new RealSpeed(mods),
                new AlternatingSpeed(mods),
                new SameDirectionSpeed(mods),
                new PartialLocalStarRating(mods),
                new LocalStarRating(mods),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new CatchModDoubleTime(),
            new CatchModHalfTime(),
            new CatchModHardRock(),
            new CatchModEasy(),
        };
    }
}
