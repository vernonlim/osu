// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils
{
    public class CatchPreprocessorTest
    {
        public static void Process(List<DifficultyHitObject> hitObjects, IBeatmap beatmap)
        {
            List<CatchDifficultyHitObject> catchHitObjects = hitObjects.OfType<CatchDifficultyHitObject>().ToList();

            testEffectiveTime(catchHitObjects, beatmap);
        }

        private static bool testEffectiveTime(List<CatchDifficultyHitObject> catchHitObjects, IBeatmap beatmap)
        {
            double maxTime = 1;

            foreach (CatchDifficultyHitObject catchHitObject in catchHitObjects)
            {
                CatchMovementData data = catchHitObject.MovementData;

                if (data.ActionProbability > 0.01)
                {
                    if (data.EffectiveTime > maxTime)
                    {
                        maxTime = data.EffectiveTime;
                    }
                    else
                    {
                        string? artist = beatmap.Metadata.Artist;
                        string? title = beatmap.Metadata.Title;
                        string? difficulty = beatmap.BeatmapInfo?.DifficultyName;
                        Console.WriteLine($"Map: {artist} - {title} [{difficulty}]");
                        Console.WriteLine($"Effective Time at t={catchHitObject.StartTime:0.0} is {data.EffectiveTime:0.0}, which is lower than {maxTime:0.0}");
                        Console.WriteLine();
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
