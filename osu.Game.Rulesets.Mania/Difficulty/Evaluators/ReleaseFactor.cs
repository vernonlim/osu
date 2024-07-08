// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ReleaseFactor
    {
        public static double[] EvaluateReleaseFactor(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double granularity)
        {
            List<ManiaDifficultyHitObject> longNoteList = noteList.Where(obj => obj.BaseObject is HoldNote).ToList();
            longNoteList.Sort((ln1, ln2) => ln1.EndTime.CompareTo(ln2.EndTime));

            // some value calculated from LN spacing within the same column
            double[] headSpacingIndex = new double[longNoteList.Count];

            for (int note = 0; note < longNoteList.Count; note++)
            {
                ManiaDifficultyHitObject currentNote = longNoteList[note];
                ManiaDifficultyHitObject? nextNote = (ManiaDifficultyHitObject?)currentNote.NextInColumn(0);
                double hitLeniency = 0.3 * Math.Pow(currentNote.GreatHitWindow / 500.0, 0.5);

                double currentI = 0.001 * Math.Abs(currentNote.EndTime - currentNote.StartTime - 80.0) / hitLeniency;

                if (nextNote is null)
                {
                    headSpacingIndex[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)));
                    continue;
                }

                double nextI = 0.001 * Math.Abs(nextNote.StartTime - currentNote.EndTime - 80.0) / hitLeniency;

                headSpacingIndex[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));
            }

            double[] releaseFactor = new double[mapLength];

            for (int note = 0; note < longNoteList.Count - 1; note++)
            {
                ManiaDifficultyHitObject currentNote = longNoteList[note];
                ManiaDifficultyHitObject nextNote = longNoteList[note + 1];

                double deltaR = 0.001 * (nextNote.EndTime - currentNote.EndTime);

                for (int t = (int)currentNote.QuantizedEndTime; t < nextNote.QuantizedEndTime; t++)
                {
                    double hitLeniency = 0.3 * Math.Pow(currentNote.GreatHitWindow / 500.0, 0.5);
                    releaseFactor[t] = 0.08 * Math.Pow(deltaR, -1.0 / 2.0) * Math.Pow(hitLeniency, -1.0) * (1 + SunnySkill.LAMBDA_4 * (headSpacingIndex[note] + headSpacingIndex[note + 1]));
                }
            }

            releaseFactor = ListUtils.Smooth(releaseFactor, (int)(500 / granularity));

            return releaseFactor;
        }
    }
}
