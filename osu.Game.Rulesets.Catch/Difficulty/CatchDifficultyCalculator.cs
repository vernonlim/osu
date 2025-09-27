// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using ScottPlot;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 4.59;

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

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = Math.Sqrt(skills.OfType<Movement>().Single().DifficultyValue()) * difficulty_multiplier,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            CatchHitObject? lastLastObject = null;
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null && lastLastObject != null)
                    objects.Add(new CatchDifficultyHitObject(lastObject, lastLastObject, hitObject, clockRate, catcherWidth, objects, objects.Count));

                lastLastObject = lastObject;
                lastObject = hitObject;
            }

            // Debug code
            bool debug = false;

            if (debug)
            {
                // Change to output path
                string outputPath = "/mnt/Storage/Programming/C#/osu-tools/PerformanceCalculator/Output/output.png";

                List<CatchDifficultyHitObject> cdhos = objects.Select(o => (CatchDifficultyHitObject) o).ToList();

                double[] times = cdhos.Select(o => o.StartTime).ToArray();
                int[] breaks = cdhos.Select(o => o.IsBreak ? 1 : 0).ToArray();
                int[] stacks = cdhos.Select(o => o.IsStack ? 1 : 0).ToArray();
                float[] lefts = cdhos.Select(o => o.LeftCatcherPosition).ToArray();
                float[] rights = cdhos.Select(o => o.RightCatcherPosition).ToArray();
                float[] leftMost = cdhos.Select(o => o.Position - o.HalfCatcherWidth).ToArray();
                float[] rightMost = cdhos.Select(o => o.Position + o.HalfCatcherWidth).ToArray();
                float[] leftStands = cdhos.Select(o => o.LeftStandingPosition ?? -1).ToArray();
                float[] rightStands = cdhos.Select(o => o.RightStandingPosition ?? -1).ToArray();
                float[] actionProb = cdhos.Select(o => o.ActionProbability).ToArray();

                ScottPlot.Plot plot = new ScottPlot.Plot();
                // var bp = plot.Add.Scatter(times, breaks);
                // var sp = plot.Add.Scatter(times, stacks);
                // var ap = plot.Add.Scatter(times, actionProb);
                // bp.Axes.YAxis = plot.Axes.Right;
                // sp.Axes.YAxis = plot.Axes.Right;
                // ap.Axes.YAxis = plot.Axes.Right;

                var l = plot.Add.ScatterPoints(times, lefts);
                var r = plot.Add.ScatterPoints(times, rights);
                l.Color = Colors.Orange;
                l.MarkerSize = 20;
                r.Color = Colors.Blue;
                r.MarkerSize = 20;
                var lb = plot.Add.ScatterPoints(times, leftMost);
                var rb = plot.Add.ScatterPoints(times, rightMost);
                lb.Color = Colors.Purple;
                lb.MarkerSize = 30;
                lb.MarkerLineWidth = 5;
                lb.MarkerShape = MarkerShape.HorizontalBar;
                rb.Color = Colors.Brown;
                rb.MarkerSize = 30;
                rb.MarkerLineWidth = 5;
                rb.MarkerShape = MarkerShape.HorizontalBar;
                // plot.Add.Scatter(times, leftStands);
                // plot.Add.Scatter(times, rightStands);

                // float[] leftDisplacement = cdhos.Select(o => o.Position - o.LeftCatcherPosition).ToArray();
                // float[] rightDisplacement = cdhos.Select(o => o.RightCatcherPosition - o.Position).ToArray();

                // plot.Add.Scatter(times, leftDisplacement);
                // plot.Add.Scatter(times, rightDisplacement);

                plot.Axes.SetLimitsY(512, 0);

                int scale = (int)(times[^1] * 1.5);

                plot.SavePng(outputPath, scale, 512 * 2 + 24);

                Console.WriteLine($"Catcher Width: {catcherWidth}");
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            catcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty);

            return new Skill[]
            {
                new Movement(mods, catcherWidth, clockRate),
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
