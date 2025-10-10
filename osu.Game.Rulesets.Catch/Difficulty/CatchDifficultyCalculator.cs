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
        private const double difficulty_multiplier = 1.45;

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

            double totalActions = DifficultyHitObjects
                                  .Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability)
                                  .Sum();

            int hyperWalkCount = DifficultyHitObjects.Count(n => ((CatchDifficultyHitObject)n).MovementData.IsHyperWalk);

            List<double> startTimes = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.EffectiveTime).ToList();
            List<double> actionProbabilities = DifficultyHitObjects.Select(n => ((CatchDifficultyHitObject)n).MovementData.ActionProbability).ToList();
            List<double> precisionStrains = skills.OfType<Precision>().Single().GetObjectStrains().ToList();
            List<double> speedStrains = skills.OfType<Speed>().Single().GetObjectStrains().ToList();
            List<double> aimStrains = skills.OfType<Aim>().Single().GetObjectStrains().ToList();
            // List<double> readingStrains = skills.OfType<Reading>().Single().GetObjectStrains().ToList();

            List<double> combinedStrains = combineStrains(actionProbabilities, precisionStrains, speedStrains, aimStrains);

            double sr = calculateDifficultyValue(startTimes, combinedStrains) * difficulty_multiplier;

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = sr,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                TotalActions = totalActions,
                HyperWalkCount = hyperWalkCount,
            };

            return attributes;
        }

        /// <summary>
        /// Replicates StrainSkill behaviour with Strain Peaks.
        /// </summary>
        /// <param name="startTimes"></param>
        /// <param name="combinedStrains"></param>
        /// <returns></returns>
        private double calculateDifficultyValue(List<double> startTimes, List<double> combinedStrains)
        {
            List<double> strainPeaks = new List<double>();

            const double decay_weight = 0.9;
            double currentSectionPeak = 0;
            double currentSectionEnd = 0;
            const double section_length = 400;

            for (int i = 0; i < combinedStrains.Count; i++)
            {
                double strain = combinedStrains[i];
                double startTime = startTimes[i];

                while (startTime > currentSectionEnd)
                {
                    strainPeaks.Add(currentSectionPeak);
                    currentSectionPeak = strain;
                    currentSectionEnd += section_length;
                }

                currentSectionPeak = Math.Max(strain, currentSectionPeak);
            }

            double difficulty = 0;
            double weight = 1;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var peaks = strainPeaks.Where(p => p > 0);

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in peaks.OrderDescending())
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
            List<CatchDifficultyHitObject> guaranteedActionNoteObjects = new List<CatchDifficultyHitObject>();
            List<CatchDifficultyHitObject> ambiguousActionNoteObjects = new List<CatchDifficultyHitObject>();

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, catcherWidth, objects, noteObjects, objects.Count, guaranteedActionNoteObjects,
                        ambiguousActionNoteObjects));

                lastObject = hitObject;
            }

            CatchMovementPreprocessor.Process(objects);
            CatchDifficultyPreprocessor.Process(objects);
            CatchReadingPreprocessor.Process(objects);

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
