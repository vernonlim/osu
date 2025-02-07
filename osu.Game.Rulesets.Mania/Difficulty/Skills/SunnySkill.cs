// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Difficulty.Calculators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        private readonly List<ManiaDifficultyHitObject> noteList;
        private double od;
        private int totalColumns;

        public SunnySkill(Mod[] mods, int totalColumns, double od, int objectCount)
            : base(mods)
        {
            this.od = od;

            this.totalColumns = totalColumns;

            noteList = new List<ManiaDifficultyHitObject>(objectCount);
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            noteList.Add(currObj);
        }

        public override double DifficultyValue()
        {
            if (noteList.Count <= 0)
                return 0;

            double x = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3.0)) / 500.0, 0.5);
            x = Math.Min(x, 0.6 * (x - 0.09) + 0.09);

            List<Calculators.Note> noteSeq = new List<Calculators.Note>();

            foreach (ManiaDifficultyHitObject obj in noteList)
            {
                int endTime = obj.EndTime == obj.StartTime ? -1 : (int)obj.EndTime;
                noteSeq.Add(new Calculators.Note(obj.BaseObject.Column, (int)obj.StartTime, endTime));
            }

            double starRating = MACalculator.Calculate(noteSeq, totalColumns, x);

            return starRating;
        }
    }
}
