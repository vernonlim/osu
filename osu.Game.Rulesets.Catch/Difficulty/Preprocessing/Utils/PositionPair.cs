// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils
{
    public struct PositionPair
    {
        public readonly bool FacingRight;

        public double Left;
        public double Right;

        public PositionPair(double left, double right, bool facingRight)
        {
            FacingRight = facingRight;

            Left = left;
            Right = right;
        }

        public double Forward => FacingRight ? Right : Left;
        public double Backward => FacingRight ? Left : Right;
    }
}
