// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public record CatchDifficultyConstants
    {
        public static CatchDifficultyConstants Default { get; } = new CatchDifficultyConstants();

        public double SrPostMultiplier { get; init; } = 0.87;
        public double SrScalerY0 { get; init; } = 1.7; // SrScaled are different in DifficultyPreprocessor.cs; the one written here aren't applied
        public double SrScalerY1 { get; init; } = 4.55;
        public double SrScalerY2 { get; init; } = 6.9;
        public double SrScalerY3 { get; init; } = 8.7;
        public double SrScalerY4 { get; init; } = 9.4;
        public double SrScalerY5 { get; init; } = 10.2;
        public double SrScalerY6 { get; init; } = 11.0;
        public double SrOffset { get; init; } = 0.0; // Not used

        public double ApproachRateSecondConstant { get; init; } = 0.37;

        public double BeginningTimePenaltyPower { get; init; } = 0.45;
        public double BeginningFullPenalty { get; init; } = 0.6;

        public double DefaultDecayWeight { get; init; } = 0.902;
        public double[] DecayWeights { get; init; } = new[] { 0.88, 0.83, 0.78, 0.71, 0.65 };

        public double LowSpeedThresholdLSR { get; init; } = 7.0;
        public double UnaffectedPercantagePrecisionLSR { get; init; } = 0.55;

        public double LocalStarRatingMaxConstant { get; init; } = 1.06;
        public double LocalStarRatingMinConstant { get; init; } = 0.85;
        public double LocalStarRatingCorrelationConstant { get; init; } = 0.18;

        public double PerformanceLengthLinearPace { get; init; } = 0.26;
        public double PerformanceLengthCutoff { get; init; } = 1700;
        public double PerformanceLengthLogarithmicPace { get; init; } = 0.26;

        public double PerformanceValueMultiplier { get; init; } = 1.05;

        public double PrecisionRawWeightHyperjumps { get; init; } = 0.94;
        public double PrecisionRawWeightHyperjumpAfterJump { get; init; } = 0.98;
        public double PrecisionRawWeightJumpAfterHyperjump { get; init; } = 0.98;
        public double PrecisionRawWeightJumps { get; init; } = 0.97;
        public double PrecisionDelayedWeight { get; init; } = 0.94;

        public double PrecisionStrainAmplitude { get; init; } = 33.2;
        public double PrecisionStrainShift { get; init; } = -7.0;
        public double PrecisionStrainPace { get; init; } = 35.0;
        public double PrecisionStrainMultiplier { get; init; } = 52.0;

        public double HighPrecisionThreshold { get; init; }  = 32.0;
        public double HighPrecisionPace { get; init; } = 3.0;
        public double HighPrecisionPower { get; init; } = 1.3;

        public double MaxPrecisionCorrection { get; init; } = 1.3;

        public double SpeedSnapAmplitude { get; init; } = 19.1;
        public double SpeedSnapShift { get; init; } = -10.0;
        public double SpeedSnapPace { get; init; } = 50.0;
        public double SpeedSnapMultiplier { get; init; } = 0.92;

        public double SpeedBurstAmplitude { get; init; } = 19.1;
        public double SpeedBurstShift { get; init; } = -10.0;
        public double SpeedBurstPace { get; init; } = 50.0;
        public double SpeedBurstMultiplier { get; init; } = 0.98;

        public double SpeedConsistencyAmplitude { get; init; } = 19.1;
        public double SpeedConsistencyShift { get; init; } = -10.0;
        public double SpeedConsistencyPace { get; init; } = 52.0;
        public double SpeedConsistencyMultiplier { get; init; } = 1.07;

        public double ReadingHighCsPower { get; init; } = 2.0;
        public double ReadingHighCsRate { get; init; } = 0.18;
        public double ReadingHighCsPenaltyHypers { get; init; } = 0.8;

        public double ReadingLocalRhythmPenalty { get; init; } = 0.95;
        public double ReadingExplicitRhythmPenalty { get; init; } = 0.94;
        public double ReadingImplicitRhythmPenalty { get; init; } = 0.98;
        public double ReadingSimilarDistancePenalty { get; init; } = 0.84;
        public double ReadingAlternatingDistancePenalty { get; init; } = 0.97;
        public double ReadingHyperchainPenalty { get; init; } = 0.95;
        public double ReadingNonHyperchainPenalty { get; init; } = 0.96;
        public double ReadingHighVelocityNerf { get; init; } = 0.0;
        public double ReadingHighDistanceBuff { get; init; } = 0.14;
        public double ReadingHighDistancePower { get; init; } = 1.3;
        public double ReadingFuturePrecisionBuff { get; init; } = 0.19;

        public double StandingWidthAdditiveConstant { get; init; } = 1.24;

        public double PrecisionCorrectionDistanceExponent { get; init; } = 0.6;
        public double PrecisionCorrectionTimeExponent { get; init; } = 1.25;
        public double PrecisionCorrectionDistanceWeight { get; init; } = 0.3;

        public double DoubleTimeNerf = 0.03;

        public double FinalPPMultiplier { get; init; } = 1.48;
    }
}
