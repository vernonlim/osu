// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils
{
    public class CatchPreprocessorTest
    {
        public static void Process(List<DifficultyHitObject> hitObjects)
        {
            List<CatchDifficultyHitObject> catchHitObjects = hitObjects.OfType<CatchDifficultyHitObject>().ToList();

            testEffectiveTime(catchHitObjects);
        }

        private static bool testEffectiveTime(List<CatchDifficultyHitObject> catchHitObjects)
        {
            double maxTime = 0;

            foreach (CatchDifficultyHitObject catchHitObject in catchHitObjects)
            {
                CatchMovementData data = catchHitObject.MovementData;

                if (data.EffectiveTime > maxTime)
                {
                    maxTime = data.EffectiveTime;
                }
                else
                {
                    Console.WriteLine($"Effective Time at t={catchHitObject.StartTime} is {data.EffectiveTime}, which is lower than {maxTime}");
                    return false;
                }
            }

            return true;
        }
    }
}
