// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Data;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty.Preprocessing.Utils
{
    public static class CatchPreprocessingUtils
    {
        public static void PopulateDifficultyData(List<CatchDifficultyHitObject> cdhos, double catcherWidth, double clockRate, CatchDifficultyConstants tuning)
        {
            cdhos[0].DisplayData.NoteCombo = 1;
            cdhos[^1].DisplayData.NoteCombo = cdhos.Count;

            for (int i = 1; i < cdhos.Count - 1; ++i)
            {
                CatchDifficultyHitObject cdho = cdhos[i];
                CatchDifficultyHitObject prev = cdhos[i - 1];
                CatchDifficultyHitObject next = cdhos[i + 1];

                double actionProbability = cdho.MovementData.ActionProbability;
                double speedStrain = SpeedEvaluator.EvaluateDifficultyOf(cdho);
                double precisionStrain = PrecisionEvaluator.EvaluateDifficultyOf(cdho);
                double distanceBonus = cdho.MovementData.DistanceBonus;
                double readingFactor = cdho.ReadingData.CombinedReadingFactor;
                double highCSFactor = cdho.ReadingData.HighCSFactor;

                (_, SpeedType speedType) = SpeedEvaluator.EvaluateMaxSpeed(cdho);

                cdho.DisplayData.CatcherWidth = catcherWidth;
                cdho.DisplayData.SpeedType = speedType;
                cdho.DisplayData.NoteSpeed = speedStrain;
                cdho.DisplayData.PartialLocalStarRating = CatchDifficultyCalculator.CalculatePartialLocalStarRating(precisionStrain, speedStrain, tuning);
                cdho.DisplayData.LocalStarRating = CatchDifficultyCalculator.CalculateLocalStarRating(actionProbability, precisionStrain, speedStrain, distanceBonus, readingFactor, highCSFactor, tuning);
                cdho.DisplayData.CatcherStandingWidth = MillisecondsToCatcherStandingWidth(next.DeltaTime, prev.MovementData.StackWiggleCount, clockRate, tuning);
                cdho.DisplayData.SignificantMovementDirection = cdho.SignificantMovementDirection(catcherWidth, clockRate);
                cdho.DisplayData.NoteCombo = i + 1;
            }
        }

        public static double MillisecondsToCatcherStandingWidth(double ms, int wiggleCount, double clockRate, CatchDifficultyConstants tuning)
        {
            const double standing_bound = 0.6;

            const double linear_decrease = -0.0045;
            double additive_constant = tuning.StandingWidthAdditiveConstant;
            const double series_decay = 0.025;

            double adjustedDelta = ms * clockRate;

            return Math.Min(1.0, Math.Max(standing_bound, linear_decrease * adjustedDelta + additive_constant)) * (1 + Math.Max(0, wiggleCount) * series_decay);
        }

        /// <summary>
        /// Calculates the value of the CDF for the catcher position at the given note for the value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        public static double NormalCdfForNote(double x, CatchDifficultyHitObject note) =>
            Cdf(x, (note.MovementData.LeftCatcherPosition + note.MovementData.RightCatcherPosition) / 2.0,
                Math.Abs(note.MovementData.ForwardCatcherPosition - note.MovementData.BackwardCatcherPosition) / 6.0);

        /// <summary>
        /// Returns the value of the CDF with given mean and standard deviation at value x.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="mean"></param>
        /// <param name="std"></param>
        /// <returns></returns>
        public static double Cdf(double x, double mean, double std) => 0.5 * DifficultyCalculationUtils.Erfc((mean - x) / (std * Math.Sqrt(2)));

        /// <summary>
        /// Gets the catcher position of the last note closest to the current one.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        public static double GetPrevForwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.RightCatcherPosition : prev.MovementData.LeftCatcherPosition;

        public static double GetPrevBackwardCatcherPosition(CatchDifficultyHitObject note, CatchDifficultyHitObject prev) =>
            note.IsMovingRight ? prev.MovementData.LeftCatcherPosition : prev.MovementData.RightCatcherPosition;

        /// <summary>
        /// Similar to MaximalDistance, but taking into account the maximal position the catcher can actually reach from
        /// the previous note, assuming it isn't a hyperdash.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static double CalculateHighestDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next) =>
            Math.Abs(note.MovementData.FurthestBackward(prev.MovementData.ForwardCatcherPosition + note.MovementData.Directionize(note.DeltaTime), note.ForwardNoteBorder) - next.Position);

        /// <summary>
        /// Calculates the minimal distance a catcher could travel between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="catcherWidth"></param>
        /// <returns></returns>
        public static double CalculateMinimalDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double catcherWidth) =>
            Math.Abs(note.Position - note.MovementData.FurthestBackward(GetPrevForwardCatcherPosition(note, prev), prev.Position + note.MovementData.Directionize(catcherWidth / 2.0)));

        /// <summary>
        /// Calculates the maximal distance a catcher could travel between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="catcherWidth"></param>
        /// <returns></returns>
        public static double CalculateMaximalDistance(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double catcherWidth) =>
            Math.Abs(note.Position - note.MovementData.FurthestForward(GetPrevBackwardCatcherPosition(note, prev), prev.Position - note.MovementData.Directionize(catcherWidth / 2.0)));

        /// <summary>
        /// Calculates the simple speed between a note and the one before it.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <returns></returns>
        public static double CalculateSpeed(CatchDifficultyHitObject note) => note.DeltaPosition / note.DeltaTime;

        public static double CalculateSpeedFrom(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double position, double frameTime) =>
            Math.Abs(note.Position - position) / Math.Max((note.StartTime - prev.StartTime) - frameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming that the catcher is perfectly positioned.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev"></param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        public static double CalculatePerfectHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double frameTime) =>
            (Math.Abs(note.Position - prev.Position)) / (Math.Max((note.StartTime - prev.StartTime) - frameTime, 1));

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        public static double CalculateMinimalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double catcherWidth, double frameTime) =>
            CalculateMinimalDistance(note, prev, catcherWidth) / Math.Max((note.StartTime - prev.StartTime) - frameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, based on the expected player position.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="catcherWidth"></param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        public static double CalculateMaximalHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double catcherWidth, double frameTime) =>
            CalculateMaximalDistance(note, prev, catcherWidth) / Math.Max(note.DeltaTime - frameTime, 1);

        /// <summary>
        /// Calculates the hyperdash speed between a note and the one before it, assuming the player starts from
        /// the right catcher position of the previous.
        /// </summary>
        /// <param name="note">The current note.</param>
        /// <param name="prev">The previous note.</param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        public static double CalculateExpectedHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double frameTime) =>
            Math.Abs(note.Position - GetPrevForwardCatcherPosition(note, prev)) / Math.Max(note.DeltaTime - frameTime, 1);

        /// <summary>
        /// Calculates the average hyperdash speed between two notes.
        /// </summary>
        /// <param name="note"></param>
        /// <param name="prev"></param>
        /// <param name="frameTime"></param>
        /// <returns></returns>
        public static double CalculateAverageHyperdashSpeed(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, double frameTime)
        {
            _ = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            double left = Math.Max(prevData.LeftCatcherPosition, prev.LeftNoteBorder);
            double right = Math.Min(prevData.RightCatcherPosition, prev.RightNoteBorder);
            double average = (left + right) / 2.0;

            double distance = Math.Abs(note.Position - average);

            return distance / Math.Max(note.DeltaTime - frameTime, 1);
        }

        /// <summary>
        /// Calculates the probability that a direction change should instead be considered a standstill for the previous note.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="note"></param>
        /// <param name="velocity"></param>
        /// <param name="catcherWidth"></param>
        /// <returns></returns>
        public static double CalculateDirectionChangeWeight(CatchDifficultyHitObject next, CatchDifficultyHitObject note, double velocity, double catcherWidth)
        {
            CatchDifficultyHitObject? nextNext = next.NextNote(0);

            double deltaPosition = Math.Abs(next.Position - note.Position);

            if (nextNext != null)
            {
                if (next.IsHyper)
                {
                    if (note.MovementData.IsDirectionChange && !next.MovementData.IsDirectionChange)
                    {
                        return 1.0;
                    }
                }
                else if (!next.MovementData.IsDirectionChange)
                {
                    deltaPosition = Math.Abs(nextNext.Position - note.Position);
                }
            }

            const double power = 0.6; // Increase results in lower weight
            const double velocity_power = 1.1; // Increase results in higher weight for hyperdashes
            double normalisedVelocity = Math.Pow(velocity, velocity_power);

            return Math.Clamp(
                Math.Pow(Math.Min(deltaPosition, 3.0 / 5.0 * catcherWidth) / (3.0 / 5.0 * catcherWidth), (power / normalisedVelocity)),
                0.0, 1.0);
        }

        public static double CalculatePotentialStandstillEffectiveTime(CatchDifficultyHitObject note, CatchDifficultyHitObject next, double catcherWidth, double frameTime)
        {
            double nextDeltaPosition = Math.Abs(next.Position - note.Position);

            if (note.DeltaPosition <= catcherWidth / 2.0)
            {
                double first = (-note.DeltaPosition - catcherWidth / 2.0
                                + (catcherWidth - 2 * nextDeltaPosition) / (2 * CalculatePerfectHyperdashSpeed(next, note, frameTime)));

                double second = note.StartTime + next.StartTime;

                return (first + second) / 2.0;
            }
            else
            {
                double first = (-catcherWidth + (catcherWidth - 2 * nextDeltaPosition) / (2 * CalculateSpeedFrom(next, note, note.BackwardNoteBorder, frameTime)));

                double second = note.StartTime + next.StartTime;

                return (first + second) / 2.0;
            }
        }

        public static double CalculatePrecisionCorrection(double deltaPosition, double deltaTime, double catcherWidth, double maxPrecisionCorrection, bool isStandstill,
                                                          CatchDifficultyConstants tuning)
        {
            double distance_exponent = tuning.PrecisionCorrectionDistanceExponent; // The lower exponent is, the higher precision correction for medium values is
            double time_exponent = tuning.PrecisionCorrectionTimeExponent; // The higher exponent is, the higher precision correction for medium values is
            double distance_weight = tuning.PrecisionCorrectionDistanceWeight;

            double standingTime = Math.Max(0.0, deltaTime - deltaPosition);

            double distanceRatio = isStandstill ? 0.0 : Math.Min(1.0, deltaPosition / catcherWidth);
            double timeRatio = Math.Min(1.0, standingTime / catcherWidth);

            double timeEffect = Math.Pow(timeRatio, time_exponent);
            double distanceEffect = Math.Pow(1.0 - distanceRatio, distance_exponent);

            double value = distance_weight * distanceEffect + (1.0 - distance_weight) * timeEffect; // No scaling yet, in [0,1] range
            return Math.Clamp(value * (maxPrecisionCorrection - 1.0) + 1.0, 1.0, maxPrecisionCorrection);
        }

        public static double? CalculateCurvedStackProbability(CatchDifficultyHitObject note, CatchDifficultyHitObject prev, CatchDifficultyHitObject next, PatternType type, double catcherWidth)
        {
            CatchMovementData data = note.MovementData;
            CatchMovementData prevData = prev.MovementData;

            switch (type)
            {
                case PatternType.JumpAfterHyperjump:
                {
                    if (prevData.IsHyperWalk)
                    {
                        if ((data.Directionize(next.Position - note.Position) < next.DeltaTime - catcherWidth / 2.0
                             && data.Directionize(next.Position - note.Position) > next.DeltaTime / 2.0 + catcherWidth / 2.0)
                            || data.Directionize(next.Position - note.Position) < next.DeltaTime - catcherWidth / 2.0)
                        {
                            return 1.0;
                        }

                        return 0.0;
                    }

                    if (data.Directionize(next.Position - note.Position) < next.DeltaTime - catcherWidth / 2.0)
                    {
                        return 1.0;
                    }

                    return 0.0;
                }

                case PatternType.Jumps:
                {
                    double val1 = note.Position - data.Directionize(catcherWidth / 2.0 + note.DeltaTime);
                    double val2 = next.Position + data.Directionize(catcherWidth / 2.0 - note.DeltaTime - next.DeltaTime);
                    double val3 = note.Position - data.Directionize(catcherWidth / 2.0 + note.DeltaTime / 2.0);
                    double val4 = next.Position + data.Directionize(catcherWidth / 2.0 - (note.DeltaTime + next.DeltaTime) / 2.0);

                    // As these are symmetric (min and max for both) we don't need FurthestBackward/FurthestForward
                    double min1 = Math.Min(val1, val2);
                    double min2 = Math.Min(val3, val4);
                    double max1 = Math.Max(val1, val2);
                    double max2 = Math.Max(val3, val4);

                    bool distinct = Math.Max(min1, min2) < Math.Min(max1, max2);

                    if (distinct)
                    {
                        return Math.Max(1 - NormalCdfForNote(max1, prev) + NormalCdfForNote(min1, prev)
                            - NormalCdfForNote(max2, prev) + NormalCdfForNote(min2, prev), 0);
                    }

                    return Math.Max(1 - NormalCdfForNote(Math.Max(max1, max2), prev) + NormalCdfForNote(Math.Min(min1, min2), prev), 0);
                }

                case PatternType.HyperStream:
                {
                    return 0.0;
                }

                default:
                {
                    return null;
                }
            }
        }

        public static bool NoteWithinBelt(CatchDifficultyHitObject note, CatchDifficultyHitObject belt, PatternType type, double catcherWidth)
        {
            CatchDifficultyHitObject? beltPrevOrNull = belt.PreviousNote(0);
            Debug.Assert(beltPrevOrNull != null);

            // Temporary hotfix for a single map (Future Raver)
            // bool leftInBelt = positionWithinBelt(note.Position - 3.0, note, belt, type);
            // bool rightInBelt = positionWithinBelt(note.Position + 3.0, note, belt, type);
            bool inBelt = positionWithinBelt(note.Position, note, belt, type, catcherWidth);

            // return leftInBelt || rightInBelt;
            return inBelt;
        }

        private static bool positionWithinBelt(double position, CatchDifficultyHitObject note, CatchDifficultyHitObject belt, PatternType type, double catcherWidth)
        {
            CatchDifficultyHitObject? beltPrevOrNull = belt.PreviousNote(0);
            Debug.Assert(beltPrevOrNull != null);

            CatchDifficultyHitObject beltPrev = beltPrevOrNull;

            CatchMovementData beltData = belt.MovementData;

            switch (type)
            {
                case PatternType.JumpAfterHyperjump:
                {
                    // I believe these are symmetric outside the gradient of x, i.e. position
                    double val1 = beltData.Directionize(position - (belt.Position + catcherWidth / 2.0)) + belt.StartTime;
                    double val2 = beltData.Directionize(position - (belt.Position - catcherWidth / 2.0)) + belt.StartTime;

                    double lower1 = Math.Min(val1, val2);
                    double higher1 = Math.Max(val1, val2);

                    bool bound1 = note.StartTime >= lower1 && note.StartTime <= higher1;

                    if (beltPrev.MovementData.IsHyperWalk)
                    {
                        double val3 = beltData.Directionize(2.0 * position - 2.0 * (belt.Position + catcherWidth / 2.0)) + belt.StartTime;
                        double val4 = beltData.Directionize(2.0 * position - 2.0 * (belt.Position - catcherWidth / 2.0)) + belt.StartTime;

                        double lower2 = Math.Min(val3, val4);
                        double higher2 = Math.Max(val3, val4);

                        bool bound2 = note.StartTime >= lower2 && note.StartTime <= higher2;

                        return bound1 || bound2;
                    }

                    return bound1;
                }

                case PatternType.Jumps:
                {
                    double prevBeltForward = GetPrevForwardCatcherPosition(belt, beltPrev);
                    double prevBeltBackward = GetPrevBackwardCatcherPosition(belt, beltPrev);

                    double val1 = beltData.Directionize(position - (beltData.FurthestBackward(prevBeltForward, belt.ForwardNoteBorder) + beltData.Directionize(catcherWidth / 2.0)))
                                  + belt.StartTime;
                    double val2 = beltData.Directionize(
                                      position - (beltData.FurthestForward(prevBeltBackward + beltData.Directionize(belt.DeltaTime), belt.BackwardNoteBorder) - beltData.Directionize(catcherWidth / 2.0)))
                                  + belt.StartTime;
                    double val3 = beltData.Directionize(2.0 * position - 2.0 * (beltData.FurthestBackward(prevBeltForward, belt.ForwardNoteBorder) + beltData.Directionize(catcherWidth / 2.0)))
                                  + belt.StartTime;
                    double val4 = beltData.Directionize(
                                      2.0 * position - 2.0 * (beltData.FurthestForward(prevBeltBackward + beltData.Directionize(belt.DeltaTime), belt.BackwardNoteBorder) - beltData.Directionize(catcherWidth / 2.0)))
                                  + belt.StartTime;

                    double lower1 = Math.Min(val1, val2);
                    double higher1 = Math.Max(val1, val2);

                    double lower2 = Math.Min(val3, val4);
                    double higher2 = Math.Max(val3, val4);

                    bool bound1 = note.StartTime >= lower1 && note.StartTime <= higher1;
                    bool bound2 = note.StartTime >= lower2 && note.StartTime <= higher2;

                    return bound1 || bound2;
                }

                default:
                {
                    return false;
                }
            }
        }

        public static double Lerp(double x, double x0, double y0, double x1, double y1)
            => y0 + (x - x0) * (y1 - y0) / (x1 - x0);
    }
}
