// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public class SpeedEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            (double maxSpeed, _) = EvaluateMaxSpeed(current);

            return 10.0 * maxSpeed * ((CatchDifficultyHitObject)current).MovementData.ActionProbability;
        }

        public static (double, SpeedType) EvaluateMaxSpeed(DifficultyHitObject current)
        {
            double snap = EvaluateSnapDifficultyOf(current);
            double burst = EvaluateBurstDifficultyOf(current);
            double consistency = EvaluateConsistencyDifficultyOf(current);

            double maxAltSame = Math.Max(snap, burst);

            SpeedType speedType1 = snap >= burst ? SpeedType.Snap : SpeedType.Burst;
            SpeedType speedType = maxAltSame >= consistency ? speedType1 : SpeedType.Consistency;

            return (Math.Max(maxAltSame, consistency), speedType);
        }

        public static double EvaluateBurstDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.BurstSpeed;
        }

        public static double EvaluateConsistencyDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.ConsistencySpeed;
        }

        public static double EvaluateSnapDifficultyOf(DifficultyHitObject current)
        {
            CatchDifficultyHitObject note = (CatchDifficultyHitObject)current;

            CatchDifficultyHitObject? prev = note.PreviousNote(0);

            if (prev is null)
            {
                return 0;
            }

            return note.MovementData.SnapSpeed;
        }
    }
}
