// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        // Integer start and end values are easier to work with.
        public new int StartTime;
        public new int? EndTime;

        public int Column;

        public double GreatHitWindow;

        // Closest in time previous object in either the current or one-over left column.
        // Used for the cross column intensity calculation.
        public ManiaDifficultyHitObject? CrossColumnPreviousObject;

        // Previous and next long notes relative to the current object.
        // Prev can be the current note.
        public ManiaDifficultyHitObject? PrevLongNote;
        public ManiaDifficultyHitObject? NextLongNote;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            StartTime = (int)BaseObject.StartTime;
            EndTime = BaseObject is HoldNote ? (int)BaseObject.GetEndTime() : null;
            Column = BaseObject.Column;

            GreatHitWindow = BaseObject.HitWindows.WindowFor(HitResult.Great);

            List<DifficultyHitObject> crossColumnObjects = objects.Where(x => ((ManiaDifficultyHitObject)x).BaseObject.Column == Column || ((ManiaDifficultyHitObject)x).BaseObject.Column == Column - 1).ToList();
            int crossColumnIndex = crossColumnObjects.FindIndex(x => x == objects[index]);

            if (0 < crossColumnIndex)
                CrossColumnPreviousObject = (ManiaDifficultyHitObject)crossColumnObjects[crossColumnIndex - 1];

            PrevLongNote = (ManiaDifficultyHitObject?)objects[..index].LastOrDefault(x => x.BaseObject is HoldNote);
            NextLongNote = (ManiaDifficultyHitObject?)objects[(index + 1)..].FirstOrDefault(x => x.BaseObject is HoldNote);
        }
    }
}
