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
        public static double[] EvaluateReleaseFactor(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double hitLeniency, double granularity)
        {
            List<ManiaDifficultyHitObject> longNoteList = noteList.Where(obj => obj.BaseObject is HoldNote).OrderBy(obj => obj.EndTime).ToList();

            // some value calculated from LN spacing within the same column
            double[] headSpacingIndex = new double[longNoteList.Count];

            for (int note = 0; note < longNoteList.Count; note++)
            {
                ManiaDifficultyHitObject currentNote = longNoteList[note];
                ManiaDifficultyHitObject? nextNote = (ManiaDifficultyHitObject?)currentNote.NextInColumn(0);

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

            ManiaDifficultyHitObject? prev = null;
            int index = 0;

            foreach (ManiaDifficultyHitObject note in longNoteList)
            {
                if (prev is not null && prev.EndTime < note.EndTime)
                {
                    double deltaR = 0.001 * (note.EndTime - prev.EndTime);

                    for (int t = (int)prev.AdjustedEndTime; t < note.AdjustedEndTime; t++)
                    {
                        releaseFactor[t] = 0.08 * Math.Pow(deltaR, -1.0 / 2.0) * (1 / hitLeniency) * (1 + SunnySkill.LAMBDA_4 * (headSpacingIndex[index - 1] + headSpacingIndex[index]));
                    }
                }

                index++;
                prev = note;
            }

            releaseFactor = ListUtils.Smooth(releaseFactor, (int)(500 / granularity));

            return releaseFactor;
        }
    }
}
