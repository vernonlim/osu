// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Difficulty.Data;

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
    }
}
