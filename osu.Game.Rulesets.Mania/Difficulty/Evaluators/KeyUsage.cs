// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class KeyUsage
    {
        public static bool[][] PerColumnKeyUsage(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double granularity)
        {
            bool[][] keyUsageByColumn = new bool[totalColumns][];

            for (int col = 0; col < totalColumns; col++)
            {
                keyUsageByColumn[col] = new bool[mapLength];
            }

            foreach (var note in noteList)
            {
                int startTime = Math.Max((int)(note.AdjustedStartTime - (500 / granularity)), 0);
                int endTime = Math.Min((int)(note.AdjustedEndTime + (500 / granularity)), mapLength - 1);

                for (int t = startTime; t < endTime; t++)
                {
                    keyUsageByColumn[note.Column][t] = true;
                }
            }

            return keyUsageByColumn;
        }
        public static uint[] EvaluateKeyUsage(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double granularity)
        {
            bool[][] keyUsageByColumn = PerColumnKeyUsage(noteList, totalColumns, mapLength, granularity);

            uint[] keyUsage = new uint[mapLength];

            for (int t = 0; t < mapLength; t++)
            {
                uint count = 0;

                for (int col = 0; col < totalColumns; col++)
                {
                    if (keyUsageByColumn[col][t])
                    {
                        count++;
                    }
                }

                keyUsage[t] = Math.Max(count, 1);
            }

            return keyUsage;
        }
    }
}
