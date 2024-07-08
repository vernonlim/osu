// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public static double[] EvaluatePressingIntensity(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength)
        {
            double[] pressingIntensity = new double[mapLength];

            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject note in noteList)
            {
                if (prev is not null)
                {
                    double hitLeniency = 0.3 * Math.Pow(note.GreatHitWindow / 500.0, 0.5);
                    double deltaTime = 0.001 * (note.StartTime - prev.StartTime);

                    if (deltaTime < 1e-4)
                    {
                        pressingIntensity[(int)prev.StartTime] += Math.Pow(0.02 * (4 / hitLeniency - SunnySkill.LAMBDA_3), 1.0 / 4.0);
                        continue;
                    }

                    double lnCount = 0;

                    for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                    {
                        lnCount += countLnBodiesAt(t, prev.CurrentHitObjects);
                    }

                    double v = 1 + SunnySkill.LAMBDA_2 * 0.001 * lnCount;

                    if (deltaTime < 2 * hitLeniency / 3.0)
                    {
                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                    else
                    {
                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(hitLeniency / 6, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                }

                prev = note;
            }

            pressingIntensity = ListUtils.Smooth(pressingIntensity);

            return pressingIntensity;
        }

        private static double streamBooster(double delta)
        {
            double val = 15.0 / delta;

            if (val > 180 && val < 340)
            {
                return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
            }

            return 1;
        }

        private static double countLnBodiesAt(int millisecond, ManiaDifficultyHitObject?[] currentObjects)
        {
            double count = 0;

            for (int column = 0; column < currentObjects.Length; column++)
            {
                ManiaDifficultyHitObject? currObj = currentObjects[column];

                if (currObj is null || currObj.BaseObject is Note)
                    continue;

                double currentNoteStartTime = currObj.StartTime;
                double currentNoteEndTime = currObj.EndTime;

                // If the current millisecond is before the end time of the previous hit note
                if (currentNoteEndTime > millisecond)
                {
                    // The first 80 milliseconds of a hold note are considered half a press, as they're easier.
                    // TODO: replace with a sigmoid
                    if (millisecond - currentNoteStartTime < 80)
                    {
                        count += 0.5;
                    }
                    else
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }
    }
}
