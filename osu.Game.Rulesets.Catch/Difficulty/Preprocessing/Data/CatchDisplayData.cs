// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data
{
    public class CatchDisplayData
    {
        public double CatcherWidth;
        public double NoteSpeed;
        public SpeedType SpeedType;
        public double DirectionChangeWeight = 1;
        public double PrecisionCorrection = 1;
        public double PartialLocalStarRating;
        public double LocalStarRating;
        public double CatcherStandingWidth;
        public CatchDifficultyHitObject? FurthestLeft;
        public CatchDifficultyHitObject? FurthestRight;
        public MovementDirection SignificantMovementDirection;
        public double PrevToNextDistance;
        public double MinimalHyperdashSpeed;
        public double PerfectHyperdashSpeed;
        public double AverageHyperdashSpeed;
        public double NoteCombo;
        public double? FuturePrecisionDifference = null;
    }
}
