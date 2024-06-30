/* Test skill as an attempt to port sunny's rework */

using System;
using osu.Framework.Utils;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using Note = (int column, double startTime, double endTime);
using TimeColumn = (double time, int column);
using Time = double;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        private double lambda_n = 4.0;
        private double lambda_1 = 0.11;
        private double lambda_2 = 5.0;
        private double lambda_3 = 8;
        private double lambda_4 = 0.1;
        private double w_0 = 0.46;
        private double w_1 = 2.5;
        private double p_1 = 1.5;
        private double w_2 = 0.25;
        private double p_0 = 1.2;
        private double hitLeniency;
        private int totalColumns;
        private int objectCount;

        // misses the first note
        private ManiaDifficultyHitObject[] hitObjects;
        // sorted by start time, then column
        private SortedList<TimeColumn, Note> note_seq;
        // grouped by column, sorted by start time
        private SortedList<TimeColumn, Note>[] note_seq_by_column;
        // sorted by end time, then column
        private SortedList<TimeColumn, Note> tail_seq;
        // grouped by column, sorted by start time
        private SortedList<TimeColumn, Note>[] ln_seq_by_column;
        // the index here represents time (in ms), while the value is the number of LNs present at said time
        // I think I will use a granularity?
        private double granularity = 1;
        private int timeSlots;
        private double[] ln_bodies;

        private int Quantize(Time time)
        {
            return (int) Math.Floor(time / granularity);
        }

        private double[] Smooth(double[] list)
        {
            double[] listBar = new double[timeSlots];
            double windowSum = 0;
            for (int i = 0; i < Math.Min(500, timeSlots); i++)
            {
                windowSum += list[i];
            }

            for (int ts = 0; ts < timeSlots; ts++)
            {
                listBar[ts] = 0.001 * windowSum;
                if (ts + 500 < timeSlots)
                {
                    windowSum += list[ts + 500];
                }
                if (ts - 500 >= 0)
                {
                    windowSum -= list[ts - 500];
                }
            }

            return listBar;
        }

        private double[] Smooth2(double[] list)
        {
            double[] listBar = new double[timeSlots];
            double windowSum = 0;
            for (int i = 0; i < Math.Min(500, timeSlots); i++)
            {
                windowSum += list[i];
            }
            double windowLen = Math.Min(500, timeSlots);

            for (int ts = 0; ts < timeSlots; ts++)
            {
                listBar[ts] = windowSum / windowLen;
                if (ts + 500 < timeSlots)
                {
                    windowSum += list[ts + 500];
                    windowLen += 1;
                }
                if (ts - 500 >= 0)
                {
                    windowSum -= list[ts - 500];
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

        public SunnySkill(Mod[] mods, int totalColumns, int objectCount, double lastObjectTime, double od)
            : base(mods)
        {
            // initializing things
            hitLeniency = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3.0)) / 500.0, 0.5);

            this.totalColumns = totalColumns;
            this.objectCount = objectCount;

            hitObjects = new ManiaDifficultyHitObject[objectCount - 1];

            note_seq = [];
            note_seq_by_column = new SortedList<TimeColumn, Note>[totalColumns];
            for (int i = 0; i < note_seq_by_column.Length; i++)
            {
                note_seq_by_column[i] = new SortedList<TimeColumn, Note>();
            }
            tail_seq = [];
            ln_seq_by_column = new SortedList<TimeColumn, Note>[totalColumns];
            for (int i = 0; i < ln_seq_by_column.Length; i++)
            {
                ln_seq_by_column[i] = new SortedList<TimeColumn, Note>();
            }
            timeSlots = (int) Math.Ceiling(lastObjectTime / granularity) + 1;
            ln_bodies = new double[timeSlots];
        }

        public override void Process(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            hitObjects[maniaCurrent.Index] = maniaCurrent;

            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            if (startTime == endTime)
            {
                endTime = -1;
            }
            int column = maniaCurrent.BaseObject.Column;

            Note note = (column, startTime, endTime);
            TimeColumn startTimeColumn = (startTime, column);
            TimeColumn endTimeColumn = (endTime, column);

            note_seq.Add(startTimeColumn, note);
            note_seq_by_column[column].Add(startTimeColumn, note);
            if (endTime != -1)
            {
                tail_seq.Add(endTimeColumn, note);
                ln_seq_by_column[column].Add(startTimeColumn, note);

                double time = Math.Min(startTime + 80, endTime);
                for (
                    int i = Quantize(startTime);
                    i < Quantize(time);
                    i++
                )
                {
                    ln_bodies[i] += 0.5;
                }
                for (
                    int i = Quantize(time);
                    i < Quantize(endTime);
                    i++
                )
                {
                    ln_bodies[i] += 1;
                }
            }
        }

        public override double DifficultyValue()
        {
            // calculating sameColumnPressure (J_ks)
            double[][] sameColumnPressure_ks = new double[totalColumns][];
            double[][] delta_ks = new double[totalColumns][];
            double[][] sameColumnPressureBar_ks = new double[totalColumns][];

            // initializing some variables
            for (int column = 0; column < totalColumns; column++)
            {
                sameColumnPressure_ks[column] = new double[timeSlots];
                sameColumnPressureBar_ks[column] = new double[timeSlots];
                delta_ks[column] = new double[timeSlots];
                for (int ts = 0; ts < timeSlots; ts++)
                {
                    delta_ks[column][ts] = Math.Pow(10, 9);
                }
            }

            // calculation
            for (int col = 0; col < totalColumns; col++)
            {
                for (int note = 0; note < note_seq_by_column[col].Count - 1; note++)
                {
                    Note after = note_seq_by_column[col].GetValueAtIndex(note + 1);
                    Note current = note_seq_by_column[col].GetValueAtIndex(note);

                    double delta = 0.001 * (after.startTime - current.startTime);
                    double val = Math.Pow(delta, -1) * Math.Pow(delta + lambda_1 * Math.Pow(hitLeniency, 1.0 / 4.0), -1.0);
                    for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                    {
                        delta_ks[col][ts] = delta;
                        sameColumnPressure_ks[col][ts] = val;
                    }
                }
            }

            // smoothing it out to get the "Bar" version
            for (int col = 0; col < totalColumns; col++)
            {
                sameColumnPressureBar_ks[col] = Smooth(sameColumnPressure_ks[col]);
            }

            // the main calculation to get J
            double[] sameColumnPressureBar = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                double[] sameColumnPressure_ks_vals = new double[totalColumns];
                double[] weights = new double[totalColumns];

                // so this part of the code used an unassigned variable k, while the loop variable was i
                // which was simply the last column of the map
                // I fixed it
                for (int col = 0; col < totalColumns; col++)
                {
                    sameColumnPressure_ks_vals[col] = sameColumnPressureBar_ks[col][ts];
                    weights[col] = 1.0 / delta_ks[col][ts];
                }

                double sumValLambdanWeight = 0;
                double sumWeights = 0;
                for (int col = 0; col < totalColumns; col++)
                {
                    double weight = weights[col];
                    sumWeights += weight;
                }
                for (int col = 0; col < totalColumns; col++)
                {
                    double val = sameColumnPressure_ks_vals[col];
                    double weight = weights[col];

                    sumValLambdanWeight += Math.Pow(val, lambda_n) * weight;
                }

                double firstPart = sumValLambdanWeight / Math.Max(Math.Pow(10, -9), sumWeights);

                double weightedAverage = Math.Pow(
                    firstPart,
                    1.0 / lambda_n
                );

                sameColumnPressureBar[ts] = weightedAverage;
            }

            // J/sameColumnPressure is now calculated

            // calculating X/Cross-Column Pressure
            double[][] crossColumnPressure_ks = new double[totalColumns + 1][];
            for (int col = 0; col < totalColumns + 1; col++)
            {
                crossColumnPressure_ks[col] = new double[timeSlots];
            }

            for (int col = 0; col < totalColumns + 1; col++)
            {
                SortedList<TimeColumn, Note> notes_in_pair;
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

                for (int note = 1; note < notes_in_pair.Count - 1; note++)
                {
                    Note after = notes_in_pair.GetValueAtIndex(note + 1);
                    Note current = notes_in_pair.GetValueAtIndex(note);
                    Note before = notes_in_pair.GetValueAtIndex(note - 1);

                    double delta = 0.001 * (current.startTime - before.startTime);
                    double val = 0.1 * Math.Pow(Math.Max(hitLeniency, delta), -2);
                    for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                    {
                        crossColumnPressure_ks[col][ts] = val;
                    }
                }
            }
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

            double[] crossColumnPressure = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                double total = 0;
                for (int col = 0; col < totalColumns + 1; col++)
                {
                    total += crossColumnPressure_ks[col][ts] * cross_matrix[totalColumns][col];
                }

                crossColumnPressure[ts] = total;
                Console.WriteLine(total);
            }

            double[] crossColumnPressureBar = Smooth(crossColumnPressure);

            // X/crossColumnPressure complete

            // calculating P/Pressing Intensity
            double[] pressingIntensity = new double[timeSlots];
            for (int note = 0; note < note_seq.Count - 1; note++)
            {
                Note after = note_seq.GetValueAtIndex(note + 1);
                Note current = note_seq.GetValueAtIndex(note);

                double delta = 0.001 * (after.startTime - current.startTime);
                if (delta < Math.Pow(10, -9))
                {
                    Console.WriteLine($"THE QUANT: {Quantize(current.startTime)}");
                    Console.WriteLine($"MAX: {timeSlots}");
                    pressingIntensity[Quantize(current.startTime)] += Math.Pow(0.02 * ((4 / hitLeniency) - lambda_3), 1.0 / 4.0);
                }
                else
                {
                    double total = 0;
                    for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                    {
                        total += ln_bodies[ts];
                    }
                    double v = 1 + lambda_2 * 0.001 * total;
                    if (delta < (2 * hitLeniency / 3.0))
                    {
                        for (int ts = Quantize(current.startTime); ts < Quantize(after.startTime); ts++)
                        {
                            pressingIntensity[ts] =
                                Math.Pow(delta, -1.0) *
                                    Math.Pow(0.08 *
                                        Math.Pow(delta, -1.0) * (1 - lambda_3 *
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
                            pressingIntensity[ts] =
                                Math.Pow(delta, -1.0) *
                                    Math.Pow(0.08 *
                                        Math.Pow(delta, -1.0) * (1 - lambda_3 *
                                            Math.Pow(hitLeniency, -1.0) *
                                            Math.Pow(hitLeniency / 6.0, 2.0)
                                        ), (1.0 / 4.0)
                                    ) * streamBooster(delta) * v;
                        }
                    }
                }
            }

            double[] pressingIntensityBar = Smooth(pressingIntensity);
            // P/pressingIntensity done

            // calculating A (unevenness), dks
            double[][] dks = new double[totalColumns - 1][];
            for (int col = 0; col < totalColumns - 1; col++)
            {
                dks[col] = new double[timeSlots];

                for (int ts = 0; ts < timeSlots; ts++)
                {
                    dks[col][ts] = Math.Abs(delta_ks[col][ts] - delta_ks[col + 1][ts]) + Math.Max(0, Math.Max(delta_ks[col + 1][ts], delta_ks[col][ts]) - 0.3);
                }
            }

            double[] unevenness = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                unevenness[ts] = 1;
            }

            for (int ts = 0; ts < timeSlots; ts++)
            {
                for (int col = 0; col < totalColumns - 1; col++)
                {
                    if (dks[col][ts] < 0.02)
                    {
                        unevenness[ts] *= Math.Min(0.75 + 0.5 * Math.Max(delta_ks[col + 1][ts], delta_ks[col][ts]), 1);
                    }
                    else if (dks[col][ts] < 0.07)
                    {
                        unevenness[ts] *= Math.Min(0.65 + 5 * dks[col][ts] + 0.5 * Math.Max(delta_ks[col + 1][ts], delta_ks[col][ts]), 1);
                    }
                    else
                    {
                        // nothing
                    }
                }
            }

            double[] unevennessBar = Smooth2(unevenness);
            // A/unevenness done

            // calculating I, R
            double[] headSpacingIndex = new double[tail_seq.Count];
            for (int note = 0; note < tail_seq.Count; note++)
            {
                Note currentNote = tail_seq.GetValueAtIndex(note);
                Note nextNote = findNextNoteInColumn(currentNote, note_seq_by_column);

                double currentI = 0.001 * Math.Abs(currentNote.endTime - currentNote.startTime - 80.0) / hitLeniency;
                double nextI = 0.001 * Math.Abs(nextNote.endTime - nextNote.startTime - 80.0) / hitLeniency;

                headSpacingIndex[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75))) + Math.Exp(-5 * (nextI - 0.75));
            }

            double[] headSpacingTimes = new double[timeSlots];
            double[] releaseFactor = new double[timeSlots];
            for (int note = 0; note < tail_seq.Count - 1; note++)
            {
                Note currentNote = tail_seq.GetValueAtIndex(note);
                Note nextNote = tail_seq.GetValueAtIndex(note + 1);

                double delta_r = 0.001 * (nextNote.endTime - currentNote.endTime);
                for (int ts = Quantize(currentNote.endTime); ts < Quantize(nextNote.endTime); ts++)
                {
                    headSpacingTimes[ts] = 1 + headSpacingIndex[note];
                    releaseFactor[ts] = 0.08 * Math.Pow(delta_r, -1.0 / 2.0) * Math.Pow(hitLeniency, -1.0) * (1 + lambda_4 * (headSpacingIndex[note] + headSpacingIndex[note + 1]));
                }
            }

            double[] releaseFactorBar = Smooth(releaseFactor);

            // final calculations, starting with C
            double[] C = new double[timeSlots];
            int start = 0;
            int end = 0;
            for (int ts = 0; ts < timeSlots; ts++)
            {
                while (start < note_seq.Count && note_seq.GetValueAtIndex(start).startTime < ts - 500)
                {
                    start += 1;
                }
                while (end < note_seq.Count && note_seq.GetValueAtIndex(end).startTime < ts + 500)
                {
                    end += 1;
                }
                C[ts] = end - start;
            }

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
                if (Jbar[ts] < 0)
                {
                    Jbar[ts] = 0;
                }
                if (Xbar[ts] < 0)
                {
                    Xbar[ts] = 0;
                }
                if (Pbar[ts] < 0)
                {
                    Pbar[ts] = 0;
                }
                if (Abar[ts] < 0)
                {
                    Abar[ts] = 0;
                }
                if (Rbar[ts] < 0)
                {
                    Rbar[ts] = 0;
                }
                if (C[ts] < 0)
                {
                    C[ts] = 0;
                }
            }

            double[] S = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                S[ts] = Math.Pow(w_0 * Math.Pow(Math.Pow(Abar[ts], 1.0 / 2.0) * Jbar[ts], 1.5) + (1 - w_0) * Math.Pow(Math.Pow(Abar[ts], 2.0 / 3.0) * (Pbar[ts] + Rbar[ts]), 1.5), 2.0 / 3.0);
            }
            double[] T = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                T[ts] = (Xbar[ts]) / (Xbar[ts] + S[ts] + 1);
            }
            double[] D = new double[timeSlots];
            for (int ts = 0; ts < timeSlots; ts++)
            {
                D[ts] = w_1 * Math.Pow(S[ts], 1.0 / 2.0) * Math.Pow(T[ts], p_1) + S[ts] * w_2;
            }

            double sum1 = 0;
            double sum2 = 0;
            for (int ts = 0; ts < timeSlots; ts++)
            {
                sum1 += Math.Pow(D[ts], lambda_n) * C[ts];

                sum2 += C[ts];
            }

            double starRating = Math.Pow(sum1 / sum2, 1.0 / lambda_n);
            starRating = Math.Pow(starRating, p_0) / Math.Pow(8, p_0) * 8;

            return starRating;
        }
    }
}
