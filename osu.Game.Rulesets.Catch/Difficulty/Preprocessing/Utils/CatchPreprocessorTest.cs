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

            // testCatcherPositions(catchHitObjects, beatmap);
            testEffectiveTime(catchHitObjects, beatmap);
            testPrecision(catchHitObjects, beatmap);
        }

        private static bool testCatcherPositions(List<CatchDifficultyHitObject> catchHitObjects, IBeatmap beatmap)
        {
            foreach (CatchDifficultyHitObject catchHitObject in catchHitObjects)
            {
                CatchMovementData data = catchHitObject.MovementData;

                if (data.LeftCatcherPosition > data.RightCatcherPosition)
                {
                    printMapInformation(beatmap);
                    Console.WriteLine($"At time {catchHitObject.StartTime}, Left Catcher Position {data.LeftCatcherPosition} is to the right of Right Catcher Position {data.RightCatcherPosition}");
                    return false;
                }
            }

            return true;
        }

        private static bool testPrecision(List<CatchDifficultyHitObject> catchHitObjects, IBeatmap beatmap)
        {
            foreach (CatchDifficultyHitObject catchHitObject in catchHitObjects)
            {
                CatchMovementData data = catchHitObject.MovementData;

                if (data.NotePrecision <= 0)
                {
                    printMapInformation(beatmap);
                    Console.WriteLine($"Precision at t={catchHitObject.StartTime} is less than 0 at {data.NotePrecision}");
                    return false;
                }
            }

            return true;
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
                        printMapInformation(beatmap);
                        Console.WriteLine($"Effective Time at t={catchHitObject.StartTime:0.0} is {data.EffectiveTime:0.0}, which is lower than {maxTime:0.0}");
                        Console.WriteLine($"Note Pattern is {data.NotePattern}");
                        return false;
                    }
                }
            }

            return true;
        }

        private static void printMapInformation(IBeatmap beatmap)
        {
            string artist = beatmap.Metadata.Artist;
            string title = beatmap.Metadata.Title;
            string difficulty = beatmap.BeatmapInfo.DifficultyName;
            Console.WriteLine($"Map: {artist} - {title} [{difficulty}]");
            Console.WriteLine();
        }
    }
}
