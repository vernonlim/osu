// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data
{
    public class CatchReadingData
    {
        public List<double> ReadingFactors;

        public CatchReadingData()
        {
            ReadingFactors = new List<double>();
            ReadingFactors.Add(1.0);
        }

        public double CombinedReadingFactor => ReadingFactors.Aggregate((prod, next) => prod * next);
    }
}
