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
        private readonly List<DifficultyHitObject>[] perColumnDifficultyHitObjects;

        private readonly int columnIndex;

        // The current hit object in each column
        public readonly ManiaDifficultyHitObject?[] CurrentHitObjects;

        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        public readonly int Column;

        public readonly double GreatHitWindow;

        public readonly double QuantizedStartTime;

        public readonly double QuantizedEndTime;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index, double granularity)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;

            perColumnDifficultyHitObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnDifficultyHitObjects[Column].Count;
            CurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];

            for (int i = 0; i < totalColumns; i++)
                CurrentHitObjects[i] = (ManiaDifficultyHitObject?)perColumnDifficultyHitObjects[i].LastOrDefault();

            GreatHitWindow = BaseObject is HoldNote ? BaseObject.NestedHitObjects[0].HitWindows.WindowFor(HitResult.Great) : BaseObject.HitWindows.WindowFor(HitResult.Great);

            QuantizedStartTime = (int)System.Math.Round(hitObject.StartTime / granularity);
            QuantizedEndTime = (int)System.Math.Round(hitObject.GetEndTime() / granularity);
        }

        public DifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnDifficultyHitObjects[Column].Count ? perColumnDifficultyHitObjects[Column][index] : default;
        }

        public DifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnDifficultyHitObjects[Column].Count ? perColumnDifficultyHitObjects[Column][index] : default;
        }
    }
}
