// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class ReadingEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool hidden, OsuDifficultyConstants tuning)
        {
            if (current.BaseObject is Spinner || current.Index == 0)
                return 0;

            var currObj = (OsuDifficultyHitObject)current;
            var nextObj = (OsuDifficultyHitObject)current.Next(0);

            double velocity = Math.Max(1, currObj.LazyJumpDistance / currObj.AdjustedDeltaTime); // Only allow velocity to buff

            double currentVisibleObjectDensity = retrieveCurrentVisibleObjectDensity(currObj, tuning);
            double pastObjectDifficultyInfluence = getPastObjectDifficultyInfluence(currObj, tuning);

            double constantAngleNerfFactor = getConstantAngleNerfFactor(currObj, tuning);

            double noteDensityDifficulty = calculateDensityDifficulty(nextObj, velocity, constantAngleNerfFactor, pastObjectDifficultyInfluence, currentVisibleObjectDensity, tuning);

            double hiddenDifficulty = hidden
                ? calculateHiddenDifficulty(currObj, pastObjectDifficultyInfluence, currentVisibleObjectDensity, velocity, constantAngleNerfFactor, tuning)
                : 0;

            double preemptDifficulty = calculatePreemptDifficulty(velocity, constantAngleNerfFactor, currObj.Preempt, tuning);

            double difficulty = DifficultyCalculationUtils.Norm(1.5, preemptDifficulty, hiddenDifficulty, noteDensityDifficulty);

            return difficulty;
        }

        /// <summary>
        /// Calculates the density difficulty of the current object and how hard it is to aim it because of it based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>how many times the current object's angle was repeated,</description></item>
        /// <item><description>density of objects visible when the current object appears,</description></item>
        /// <item><description>density of objects visible when the current object needs to be clicked,</description></item>
        /// /// </list>
        /// </summary>
        private static double calculateDensityDifficulty(OsuDifficultyHitObject? nextObj, double velocity, double constantAngleNerfFactor,
                                                         double pastObjectDifficultyInfluence, double currentVisibleObjectDensity, OsuDifficultyConstants tuning)
        {
            // Consider future densities too because it can make the path the cursor takes less clear
            double futureObjectDifficultyInfluence = Math.Sqrt(currentVisibleObjectDensity);

            if (nextObj != null)
            {
                // Reduce difficulty if movement to next object is small
                futureObjectDifficultyInfluence *= DifficultyCalculationUtils.Smootherstep(nextObj.LazyJumpDistance, 15, tuning.ReadingDistanceInfluenceThreshold);
            }

            // Value higher note densities exponentially
            double noteDensityDifficulty = Math.Pow(pastObjectDifficultyInfluence + futureObjectDifficultyInfluence, 1.7) * 0.4 * constantAngleNerfFactor * velocity;

            // Award only denser than average maps.
            noteDensityDifficulty = Math.Max(0, noteDensityDifficulty - tuning.ReadingDensityDifficultyBase);

            // Apply a soft cap to general density reading to account for partial memorization
            noteDensityDifficulty = Math.Pow(noteDensityDifficulty, 0.45) * tuning.ReadingDensityMultiplier;

            return noteDensityDifficulty;
        }

        /// <summary>
        /// Calculates the difficulty of aiming the current object when the approach rate is very high based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>how many times the current object's angle was repeated,</description></item>
        /// <item><description>how many milliseconds elapse between the approach circle appearing and touching the inner circle</description></item>
        /// </list>
        /// </summary>
        private static double calculatePreemptDifficulty(double velocity, double constantAngleNerfFactor, double preempt, OsuDifficultyConstants tuning)
        {
            // Arbitrary curve for the base value preempt difficulty should have as approach rate increases.
            // https://www.desmos.com/calculator/c175335a71
            double preemptDifficulty = Math.Pow((tuning.ReadingPreemptStartingPoint - preempt + Math.Abs(preempt - tuning.ReadingPreemptStartingPoint)) / 2, 2.5) / tuning.ReadingPreemptBalancingFactor;

            preemptDifficulty *= constantAngleNerfFactor * velocity;

            return preemptDifficulty;
        }

        /// <summary>
        /// Calculates the difficulty of aiming the current object when the hidden mod is active based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>time the current object spends invisible,</description></item>
        /// <item><description>density of objects visible when the current object appears,</description></item>
        /// <item><description>density of objects visible when the current object needs to be clicked,</description></item>
        /// <item><description>how many times the current object's angle was repeated,</description></item>
        /// <item><description>if the current object is perfectly stacked to the previous one</description></item>
        /// </list>
        /// </summary>
        private static double calculateHiddenDifficulty(OsuDifficultyHitObject currObj, double pastObjectDifficultyInfluence, double currentVisibleObjectDensity, double velocity,
                                                        double constantAngleNerfFactor, OsuDifficultyConstants tuning)
        {
            double timeSpentInvisible = currObj.DurationSpentInvisible() / currObj.ClockRate;

            // Value time spent invisible exponentially
            double timeSpentInvisibleFactor = Math.Pow(timeSpentInvisible, 2.2) * 0.022;

            // Account for both past and current densities
            double densityFactor = Math.Pow(currentVisibleObjectDensity + pastObjectDifficultyInfluence, 3.3) * 3;

            double hiddenDifficulty = (timeSpentInvisibleFactor + densityFactor) * constantAngleNerfFactor * velocity * 0.01;

            // Apply a soft cap to general HD reading to account for partial memorization
            hiddenDifficulty = Math.Pow(hiddenDifficulty, 0.4) * tuning.ReadingHiddenMultiplier;

            var previousObj = (OsuDifficultyHitObject)currObj.Previous(0);

            // Buff perfect stacks only if current note is completely invisible at the time you click the previous note.
            if (currObj.LazyJumpDistance == 0 && currObj.OpacityAt(previousObj.BaseObject.StartTime + previousObj.Preempt, true) == 0 && previousObj.StartTime + previousObj.Preempt > currObj.StartTime)
                hiddenDifficulty += tuning.ReadingHiddenMultiplier * 7500 / Math.Pow(currObj.AdjustedDeltaTime, 1.5); // Perfect stacks are harder the less time between notes

            return hiddenDifficulty;
        }

        private static double getPastObjectDifficultyInfluence(OsuDifficultyHitObject currObj, OsuDifficultyConstants tuning)
        {
            double pastObjectDifficultyInfluence = 0;

            foreach (var loopObj in retrievePastVisibleObjects(currObj, tuning))
            {
                double loopDifficulty = currObj.OpacityAt(loopObj.BaseObject.StartTime, false);

                // When aiming an object small distances mean previous objects may be cheesed, so it doesn't matter whether they were arranged confusingly.
                loopDifficulty *= DifficultyCalculationUtils.Smootherstep(loopObj.LazyJumpDistance, 15, tuning.ReadingDistanceInfluenceThreshold);

                // Account less for objects close to the max reading window
                double timeBetweenCurrAndLoopObj = currObj.StartTime - loopObj.StartTime;
                double timeNerfFactor = getTimeNerfFactor(timeBetweenCurrAndLoopObj, tuning);

                loopDifficulty *= timeNerfFactor;
                pastObjectDifficultyInfluence += loopDifficulty;
            }

            return pastObjectDifficultyInfluence;
        }

        // Returns a list of objects that are visible on screen at the point in time the current object becomes visible.
        private static IEnumerable<OsuDifficultyHitObject> retrievePastVisibleObjects(OsuDifficultyHitObject current, OsuDifficultyConstants tuning)
        {
            for (int i = 0; i < current.Index; i++)
            {
                OsuDifficultyHitObject hitObject = (OsuDifficultyHitObject)current.Previous(i);

                if (hitObject.IsNull() ||
                    current.StartTime - hitObject.StartTime > tuning.ReadingWindowSize ||
                    hitObject.StartTime + hitObject.Preempt < current.StartTime) // Current object not visible at the time object needs to be clicked
                    break;

                yield return hitObject;
            }
        }

        // Returns the density of objects visible at the point in time the current object needs to be clicked capped by the reading window.
        private static double retrieveCurrentVisibleObjectDensity(OsuDifficultyHitObject current, OsuDifficultyConstants tuning)
        {
            double visibleObjectCount = 0;

            OsuDifficultyHitObject? hitObject = (OsuDifficultyHitObject)current.Next(0);

            while (hitObject != null)
            {
                if (hitObject.StartTime - current.StartTime > tuning.ReadingWindowSize ||
                    current.StartTime + hitObject.Preempt < hitObject.StartTime) // Object not visible at the time current object needs to be clicked.
                    break;

                double timeBetweenCurrAndLoopObj = hitObject.StartTime - current.StartTime;
                double timeNerfFactor = getTimeNerfFactor(timeBetweenCurrAndLoopObj, tuning);

                visibleObjectCount += hitObject.OpacityAt(current.BaseObject.StartTime, false) * timeNerfFactor;

                hitObject = (OsuDifficultyHitObject?)hitObject.Next(0);
            }

            return visibleObjectCount;
        }

        // Returns a factor of how often the current object's angle has been repeated in a certain time frame.
        // It does this by checking the difference in angle between current and past objects and sums them based on a range of similarity.
        // https://www.desmos.com/calculator/eb057a4822
        private static double getConstantAngleNerfFactor(OsuDifficultyHitObject current, OsuDifficultyConstants tuning)
        {
            double constantAngleCount = 0;
            int index = 0;
            double currentTimeGap = 0;

            while (currentTimeGap < tuning.ReadingMinimumAngleRelevancyTime)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                // Account less for objects that are close to the time limit.
                double longIntervalFactor = 1 - DifficultyCalculationUtils.ReverseLerp(loopObj.AdjustedDeltaTime, tuning.ReadingMaximumAngleRelevancyTime, tuning.ReadingMinimumAngleRelevancyTime);

                if (loopObj.Angle.IsNotNull() && current.Angle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.Angle.Value - loopObj.Angle.Value);
                    double stackFactor = DifficultyCalculationUtils.Smootherstep(loopObj.LazyJumpDistance, 0, OsuDifficultyHitObject.NORMALISED_RADIUS);

                    constantAngleCount += Math.Cos(3 * Math.Min(double.DegreesToRadians(30), angleDifference * stackFactor)) * longIntervalFactor;
                }

                currentTimeGap = current.StartTime - loopObj.StartTime;
                index++;
            }

            return Math.Clamp(2 / constantAngleCount, 0.2, 1);
        }

        // Returns a nerfing factor for when objects are very distant in time, affecting reading less.
        private static double getTimeNerfFactor(double deltaTime, OsuDifficultyConstants tuning)
        {
            return Math.Clamp(2 - deltaTime / (tuning.ReadingWindowSize / 2), 0, 1);
        }
    }
}
