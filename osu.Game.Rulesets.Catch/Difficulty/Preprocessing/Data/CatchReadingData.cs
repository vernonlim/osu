// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data
{
    public class CatchReadingData
    {
        public double CombinedReadingFactor;
        public double HighCSFactor;

        public CatchReadingData()
        {
            CombinedReadingFactor = 1.0;
            HighCSFactor = 1.0;
        }
    }
}
