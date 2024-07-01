/*
    A port of sunny's rework
    This is only meant for testing of values and experimentation with the formula
    Not meant as an attempt to write usable pp calculation code
 */

using System;
using osu.Framework.Utils;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

using Note = (int column, double startTime, double endTime);

// a tuple of Time, then Column, used for sorting
using TimeColumn = (double time, int column);

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public readonly record struct SunnyParams(
        double lambda_n,
        double lambda_1,
        double lambda_2,
        double lambda_3,
        double lambda_4,
        double w_0,
        double w_1,
        double w_2,
        double p_0,
        double p_1
    );
    public class SunnySkill : Skill
    {
        // grouping all parameters together
        private readonly SunnyParams pr = new(4.0, 0.11, 5.0, 8, 0.1, 0.46, 2.5, 0.25, 1.2, 1.5);
        // a value set based on map OD
        private double hitLeniency;
        private int totalColumns;

        // sorted by start time, then column
        private SortedList<TimeColumn, Note> note_seq;
        // grouped by column, sorted by start time
        private SortedList<TimeColumn, Note>[] note_seq_by_column;
        // sorted by end time, then column
        private SortedList<TimeColumn, Note> tail_seq;
        // grouped by column, sorted by start time
        private SortedList<TimeColumn, Note>[] ln_seq_by_column;
        // the number of LN bodies present at each point in time
        private double[] ln_bodies;
        // the 'granularity' of the time precision.
        // set to 1, this means that notes are processed in 1ms windows
        // set to 20, that would mean that notes would be processed in 20ms windows
        private readonly double granularity = 1;
        // the number of points in time that are being processed
        // equal to total time divided by granularity
        private readonly int timeSlots;

        // instance of this class created at the beginning for each map processed
        public SunnySkill(Mod[] mods, int totalColumns, int objectCount, double lastObjectTime, double od)
            : base(mods)
        {
            // setting hit leniency based on OD
            hitLeniency = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3.0)) / 500.0, 0.5);

            this.totalColumns = totalColumns;

            // initializing the lists needed for calculation
            note_seq = new();

            note_seq_by_column = new SortedList<TimeColumn, Note>[totalColumns];
            for (int i = 0; i < note_seq_by_column.Length; i++)
            {
                note_seq_by_column[i] = new SortedList<TimeColumn, Note>();
            }

            tail_seq = new();

            ln_seq_by_column = new SortedList<TimeColumn, Note>[totalColumns];
            for (int i = 0; i < ln_seq_by_column.Length; i++)
            {
                ln_seq_by_column[i] = new SortedList<TimeColumn, Note>();
            }

            // the number of points in time being processed
            // 1 is added due to 0ms being a valid point (perhaps? I just added this to avoid an out of bounds error)
            timeSlots = (int)Math.Ceiling(lastObjectTime / granularity) + 1;

            ln_bodies = new double[timeSlots];
        }

        // this method is called once for every hitobject in order
        // in strain.cs, it's used to calculate difficulty as the map progresses
        // however, here it's only used to fill up the lists of hitobjects needed for calculation
        // with the actual SR calculation performed when the SR is requested in the DifficultyValue() method
        // if I'm not mistaken, that's a rather horrible abuse of lazer's code but oh well
        public override void Process(DifficultyHitObject current)
        {
            // the current hit object
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;

            // regular notes have an endTime identical to their startTime
            if (startTime == endTime)
            {
                // following the convention set by Sunny's code
                endTime = -1;
            }

            int column = maniaCurrent.BaseObject.Column;

            // if the current hit object overlaps with one in note_seq (the array containing all notes), skip it
            if (note_seq.ContainsKey((startTime, column)))
            {
                return;
            }

            Note note = (column, startTime, endTime);
            TimeColumn startTimeColumn = (startTime, column);
            TimeColumn endTimeColumn = (endTime, column);

            note_seq.Add(startTimeColumn, note);

            note_seq_by_column[column].Add(startTimeColumn, note);

            // if the note is an LN
            if (endTime != -1)
            {
                tail_seq.Add(endTimeColumn, note);

                ln_seq_by_column[column].Add(startTimeColumn, note);

                // finding LN bodies within a certain window
                double time = Math.Min(startTime + 80, endTime);

                for (int i = Quantize(startTime); i < Quantize(time); i++)
                {
                    ln_bodies[i] += 0.5;
                }
                for (int i = Quantize(time); i < Quantize(endTime); i++)
                {
                    ln_bodies[i] += 1;
                }
            }
        }

        // where the actual difficulty calculation is performed (at the wrong time probably, again, abuse of lazer's code)
        public override double DifficultyValue()
        {
            if (timeSlots == 1)
            {
                return 0;
            }

            /*
                Calculating sameColumnPressure (J)
            */

            // initializing some variables
            // each of these is first indexed by column, then by 'time slot'
            double[][] sameColumnPressure_col_ts = new double[totalColumns][];
            double[][] delta_col_ts = new double[totalColumns][];
            double[][] sameColumnPressureBar_col_ts = new double[totalColumns][];
            for (int col = 0; col < totalColumns; col++)
            {
                sameColumnPressure_col_ts[col] = new double[timeSlots];
                sameColumnPressureBar_col_ts[col] = new double[timeSlots];
                delta_col_ts[col] = new double[timeSlots];
                for (int ts = 0; ts < timeSlots; ts++)
                {
                    delta_col_ts[col][ts] = Math.Pow(10, 9);
                }
            }

            // calculation for each time slot
            for (int col = 0; col < totalColumns; col++)
            {
                for (int note = 0; note < note_seq_by_column[col].Count - 1; note++)
                {
                    Note next = note_seq_by_column[col].GetValueAtIndex(note + 1);
                    Note current = note_seq_by_column[col].GetValueAtIndex(note);

                    double delta = 0.001 * (next.startTime - current.startTime);
                    double val = Math.Pow(delta, -1) * Math.Pow(delta + pr.lambda_1 * Math.Pow(hitLeniency, 1.0 / 4.0), -1.0);

                    // the variables created earlier are filled with delta/val
                    for (int ts = Quantize(current.startTime); ts < Quantize(next.startTime); ts++)
                    {
                        delta_col_ts[col][ts] = delta;
                        sameColumnPressure_col_ts[col][ts] = val;
                    }
                }
            }

            // smoothing it out to get the "Bar" version
            for (int col = 0; col < totalColumns; col++)
            {
                sameColumnPressureBar_col_ts[col] = Smooth(sameColumnPressure_col_ts[col]);
            }

            // finally, a flattened array is created (not by column)
            double[] sameColumnPressureBar = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                // sum(weight)
                double sumWeights = 0;
                // sum((val ** lambda_n) * weight)
                double sumValLambdaWeight = 0;
                for (int col = 0; col < totalColumns; col++)
                {
                    double val = sameColumnPressureBar_col_ts[col][ts];
                    double weight = 1.0 / delta_col_ts[col][ts];

                    sumWeights += weight;
                    sumValLambdaWeight += Math.Pow(val, pr.lambda_n) * weight;
                }

                // sumValLambdaWeight / max(10**(-9), sum(weights))
                double firstPart = sumValLambdaWeight / Math.Max(Math.Pow(10, -9), sumWeights);

                double weightedAverage = Math.Pow(
                    firstPart,
                    1.0 / pr.lambda_n
                );

                sameColumnPressureBar[ts] = weightedAverage;
            }


            /*
                calculating crossColumnPressure (X)
            */

            // we suppose that there is an extra column at the edges that is just empty
            double[][] crossColumnPressure_col_ts = new double[totalColumns + 1][];
            for (int col = 0; col < totalColumns + 1; col++)
            {
                crossColumnPressure_col_ts[col] = new double[timeSlots];
            }

            for (int col = 0; col < totalColumns + 1; col++)
            {
                SortedList<TimeColumn, Note> notes_in_pair;

                // at the edges, the only notes considered paired are the ones in the same column...?
                if (col == 0)
                {
                    notes_in_pair = note_seq_by_column[0];
                }
                else if (col == totalColumns)
                {
                    notes_in_pair = note_seq_by_column[col - 1];
                }
                else
                {
                    // merges two columns together, forming pairs of notes adjacent in time
                    SortedList<TimeColumn, Note> mergedList = new();
                    foreach (var i in note_seq_by_column[col - 1])
                    {
                        mergedList.Add(i.Key, i.Value);
                    }
                    foreach (var i in note_seq_by_column[col])
                    {
                        mergedList.Add(i.Key, i.Value);
                    }
                    notes_in_pair = mergedList;
                }

                // calculates a value based on distance between
                // before -> current
                // and then adjusts the pressure from current to after, based on it
                for (int note = 1; note < notes_in_pair.Count - 1; note++)
                {
                    Note after = notes_in_pair.GetValueAtIndex(note + 1);
                    Note current = notes_in_pair.GetValueAtIndex(note);
                    Note before = notes_in_pair.GetValueAtIndex(note - 1);

                    double delta = 0.001 * (current.startTime - before.startTime);
                    double val = 0.1 * Math.Pow(Math.Max(hitLeniency, delta), -2);
                    for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                    {
                        crossColumnPressure_col_ts[col][ts] = val;
                    }
                }
            }

            // weights for each column (plus the extra one)
            double[][] cross_matrix = [
                [-1],
                [0.075, 0.075],
                [0.125, 0.05, 0.125],
                [0.125, 0.125, 0.125, 0.125],
                [0.175, 0.25, 0.05, 0.25, 0.175],
                [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
                [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
                [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
                [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
                [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
                [0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325]
            ];

            // consolidates the values
            double[] crossColumnPressure = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                double total = 0;
                for (int col = 0; col < totalColumns + 1; col++)
                {
                    total += crossColumnPressure_col_ts[col][ts] * cross_matrix[totalColumns][col];
                }

                crossColumnPressure[ts] = total;
            }

            // smooths it out
            double[] crossColumnPressureBar = Smooth(crossColumnPressure);


            /*
                calculates pressingIntensity (P)
                roughly equivalent to ordinary osu! mania density
            */

            double[] pressingIntensity = new double[timeSlots];
            for (int note = 0; note < note_seq.Count - 1; note++)
            {
                Note after = note_seq.GetValueAtIndex(note + 1);
                Note current = note_seq.GetValueAtIndex(note);

                double delta = 0.001 * (after.startTime - current.startTime);
                if (delta < Math.Pow(10, -9))
                {
                    // (0.02 * ((4 / x) - lambda_3)**(1/4)
                    pressingIntensity[Quantize(current.startTime)] += Math.Pow(0.02 * ((4 / hitLeniency) - pr.lambda_3), 1.0 / 4.0);
                }
                else
                {
                    double lnCount = 0;
                    for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                    {
                        lnCount += ln_bodies[ts];
                    }

                    double v = 1 + pr.lambda_2 * 0.001 * lnCount;
                    if (delta < (2 * hitLeniency / 3.0))
                    {
                        for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                        {
                            // these formulas were a pain to read
                            // delta**(-1) * (0.08*(delta)**(-1) * (1 - lambda_3 * x**(-1)*(delta - x/2)**2))**(1/4)*b(delta)*v
                            pressingIntensity[ts] =
                                Math.Pow(delta, -1.0) *
                                    Math.Pow(0.08 *
                                        Math.Pow(delta, -1.0) * (1 - pr.lambda_3 *
                                            Math.Pow(hitLeniency, -1.0) *
                                            Math.Pow(delta - hitLeniency / 2.0, 2.0)
                                        ), (1.0 / 4.0)
                                    ) * streamBooster(delta) * v;
                        }
                    }
                    else
                    {
                        for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                        {
                            // delta**(-1) * (0.08*(delta)**(-1) * (1 - lambda_3*x**(-1)*(x/6)**2))**(1/4)*b(delta)*v
                            pressingIntensity[ts] =
                                Math.Pow(delta, -1.0) *
                                    Math.Pow(0.08 *
                                        Math.Pow(delta, -1.0) * (1 - pr.lambda_3 *
                                            Math.Pow(hitLeniency, -1.0) *
                                            Math.Pow(hitLeniency / 6.0, 2.0)
                                        ), (1.0 / 4.0)
                                    ) * streamBooster(delta) * v;
                        }
                    }
                }
            }

            double[] pressingIntensityBar = Smooth(pressingIntensity);


            /*
                calculating unevenness (A)
            */

            // some sort of value representing distance between notes in different columns
            double[][] dks = new double[totalColumns - 1][];
            for (int col = 0; col < totalColumns - 1; col++)
            {
                dks[col] = new double[timeSlots];

                for (int ts = 0; ts < timeSlots; ts++)
                {
                    dks[col][ts] = Math.Abs(delta_col_ts[col][ts] - delta_col_ts[col + 1][ts]) + Math.Max(0, Math.Max(delta_col_ts[col + 1][ts], delta_col_ts[col][ts]) - 0.3);
                }
            }

            // initializes this to 1
            double[] unevenness = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                unevenness[ts] = 1;
            }

            // calculates unevenness based on dks and some other values
            for (int ts = 0; ts < timeSlots; ts++)
            {
                for (int col = 0; col < totalColumns - 1; col++)
                {
                    if (dks[col][ts] < 0.02)
                    {
                        unevenness[ts] *= Math.Min(0.75 + 0.5 * Math.Max(delta_col_ts[col + 1][ts], delta_col_ts[col][ts]), 1);
                    }
                    else if (dks[col][ts] < 0.07)
                    {
                        unevenness[ts] *= Math.Min(0.65 + 5 * dks[col][ts] + 0.5 * Math.Max(delta_col_ts[col + 1][ts], delta_col_ts[col][ts]), 1);
                    }
                    else
                    {
                        // nothing
                    }
                }
            }

            double[] unevennessBar = Smooth2(unevenness);


            /*
                calculating releaseFactor (R)
            */

            // value used to calculate R
            // some value calculated from LN spacing within the same column
            double[] headSpacingIndex = new double[tail_seq.Count];
            for (int note = 0; note < tail_seq.Count; note++)
            {
                Note currentNote = tail_seq.GetValueAtIndex(note);
                Note nextNote = findNextNoteInColumn(currentNote, note_seq_by_column);

                double currentI = 0.001 * Math.Abs(currentNote.endTime - currentNote.startTime - 80.0) / hitLeniency;
                double nextI = 0.001 * Math.Abs(nextNote.startTime - currentNote.endTime - 80.0) / hitLeniency;

                headSpacingIndex[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));
            }

            // uh, what is headSpacingTimes/Is even for? it's never used
            // double[] headSpacingTimes = new double[timeSlots];
            double[] releaseFactor = new double[timeSlots];
            for (int note = 0; note < tail_seq.Count - 1; note++)
            {
                Note currentNote = tail_seq.GetValueAtIndex(note);
                Note nextNote = tail_seq.GetValueAtIndex(note + 1);

                double delta_r = 0.001 * (nextNote.endTime - currentNote.endTime);
                for (int ts = Quantize(currentNote.endTime); ts < Quantize(nextNote.endTime); ts++)
                {
                    // headSpacingTimes[ts] = 1 + headSpacingIndex[note];
                    releaseFactor[ts] = 0.08 * Math.Pow(delta_r, -1.0 / 2.0) * Math.Pow(hitLeniency, -1.0) * (1 + pr.lambda_4 * (headSpacingIndex[note] + headSpacingIndex[note + 1]));
                }
            }

            double[] releaseFactorBar = Smooth(releaseFactor);


            /*
                calculating C
            */

            double[] C = new double[timeSlots];
            int start = 0;
            int end = 0;
            for (int ts = 0; ts < timeSlots; ts++)
            {
                while (start < note_seq.Count && Quantize(note_seq.GetValueAtIndex(start).startTime) < ts - Quantize(500))
                {
                    start += 1;
                }
                while (end < note_seq.Count && Quantize(note_seq.GetValueAtIndex(end).startTime) < ts + Quantize(500))
                {
                    end += 1;
                }
                C[ts] = end - start;
            }

            /*
                SR calculation
            */

            // renaming variables so the formulas don't take up 20 lines (and it's easier to copy over)
            // Jbar/sameColumnPressureBar
            // Xbar/crossColumnPressureBar
            // Pbar/pressingIntensityBar
            // Abar/unevennessBar
            // Rbar/releaseFactorBar
            // C/C
            var Jbar = sameColumnPressureBar;
            var Xbar = crossColumnPressureBar;
            var Pbar = pressingIntensityBar;
            var Abar = unevennessBar;
            var Rbar = releaseFactorBar;

            // clipping values <0 to 0
            for (int ts = 0; ts < timeSlots; ts++)
            {
                Jbar[ts] = Math.Max(Jbar[ts], 0);
                Xbar[ts] = Math.Max(Xbar[ts], 0);
                Pbar[ts] = Math.Max(Pbar[ts], 0);
                Abar[ts] = Math.Max(Abar[ts], 0);
                Rbar[ts] = Math.Max(Rbar[ts], 0);
                C[ts] = Math.Max(C[ts], 0);
            }

            // the calculations below are performed per-row/ts, hence the loops
            // calculating S
            double[] S = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                S[ts] = Math.Pow(pr.w_0 * Math.Pow(Math.Pow(Abar[ts], 1.0 / 2.0) * Jbar[ts], 1.5) + (1 - pr.w_0) * Math.Pow(Math.Pow(Abar[ts], 2.0 / 3.0) * (Pbar[ts] + Rbar[ts]), 1.5), 2.0 / 3.0);
            }

            // calculating T
            double[] T = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                T[ts] = (Xbar[ts]) / (Xbar[ts] + S[ts] + 1);
            }

            // calculating D
            double[] D = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                D[ts] = pr.w_1 * Math.Pow(S[ts], 1.0 / 2.0) * Math.Pow(T[ts], pr.p_1) + S[ts] * pr.w_2;
            }

            // sum(df['D']**lambda_n * df['C'])
            double sum1 = 0;
            // sum(df['C'])
            double sum2 = 0;
            for (int ts = 0; ts < timeSlots; ts++)
            {
                sum1 += Math.Pow(D[ts], pr.lambda_n) * C[ts];

                sum2 += C[ts];
            }

            // Console.WriteLine($"JBAR: {Jbar.Average()}");
            // Console.WriteLine($"XBAR: {Xbar.Average()}");
            // Console.WriteLine($"PBAR: {Pbar.Average()}");
            // Console.WriteLine($"ABAR: {Abar.Average()}");
            // Console.WriteLine($"RBAR: {Rbar.Average()}");
            // Console.WriteLine($"C: {C.Average()}");

            // (sum1 / sum2)**(1/lambda_n)
            double starRating = Math.Pow(sum1 / sum2, 1.0 / pr.lambda_n);
            // (SR**(p_0)) / (8**p_0) * 8
            starRating = Math.Pow(starRating, pr.p_0) / Math.Pow(8, pr.p_0) * 8;

            return starRating;
        }

        // used to convert times in milliseconds into times in sizes of granularity
        private int Quantize(double time)
        {
            return (int) Math.Floor(time / granularity);
        }

        // smoothing function 1
        private double[] Smooth(double[] list)
        {
            double[] listBar = new double[timeSlots];
            double windowSum = 0;
            for (int i = 0; i < Math.Min(Quantize(500), timeSlots); i++)
            {
                windowSum += list[i];
            }

            for (int ts = 0; ts < timeSlots; ts++)
            {
                listBar[ts] = (0.001 * granularity) * windowSum;
                if (ts + Quantize(500) < timeSlots)
                {
                    windowSum += list[ts + Quantize(500)];
                }
                if (ts - Quantize(500) >= 0)
                {
                    windowSum -= list[ts - Quantize(500)];
                }
            }

            return listBar;
        }

        // smoothing function 2
        private double[] Smooth2(double[] list)
        {
            double[] listBar = new double[timeSlots];
            double windowSum = 0;
            for (int i = 0; i < Math.Min(Quantize(500), timeSlots); i++)
            {
                windowSum += list[i];
            }
            double windowLen = Math.Min(Quantize(500), timeSlots);

            for (int ts = 0; ts < timeSlots; ts++)
            {
                listBar[ts] = windowSum / windowLen;
                if (ts + Quantize(500) < timeSlots)
                {
                    windowSum += list[ts + Quantize(500)];
                    windowLen += 1;
                }
                if (ts - Quantize(500) >= 0)
                {
                    windowSum -= list[ts - Quantize(500)];
                    windowLen -= 1;
                }
            }

            return listBar;
        }

        private Note findNextNoteInColumn(Note note, SortedList<TimeColumn, Note>[] note_seq_by_column)
        {
            // // The below is for if the note isn't present in the map
            // for (int i = 0; i < note_seq_by_column[note.column].Count; i++)
            // {
            //     Note currentNote = note_seq_by_column[note.column].GetValueAtIndex(i);
            //     if (currentNote.startTime > note.startTime)
            //     {
            //         return currentNote;
            //     }
            // }

            var columnNotes = note_seq_by_column[note.column];
            var key = (note.startTime, note.column);
//
            if (columnNotes.ContainsKey(key))
            {
                int nextNoteIndex = columnNotes.IndexOfKey(key) + 1;
                if (!(nextNoteIndex == columnNotes.Count))
                {
                    return columnNotes.GetValueAtIndex(nextNoteIndex);
                }
            }

            return (0, Math.Pow(10, 9), Math.Pow(10, 9));
        }

        public double streamBooster(double delta)
        {
            double val = 15.0 / delta;
            if (val > 180 && val < 340)
            {
                return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
            }

            return 1;
        }
    }
}
