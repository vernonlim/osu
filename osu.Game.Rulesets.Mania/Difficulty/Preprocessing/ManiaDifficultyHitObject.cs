// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        private readonly List<DifficultyHitObject>[] perColumnDifficultyHitObjects;

        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        private readonly int columnIndex;

        public readonly int Column;

        // The local context needed to assess the difficulty of the note
        // The lists are in order of how close each note is to the current - i.e index 0 is the closest note
        public readonly List<ManiaDifficultyHitObject>[] PreviousHitObjects;
        public readonly List<ManiaDifficultyHitObject>[] NextHitObjects;

        // The deltaTime from the previous note in Time normalized to seconds
        public readonly double NormalizedDeltaTime;

        // The deltaTime from the previous note in Column normalized to seconds
        public readonly double NormalizedColumnDeltaTime;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            perColumnDifficultyHitObjects = perColumnObjects;

            int totalColumns = perColumnDifficultyHitObjects.Length;

            Column = BaseObject.Column;
            columnIndex = perColumnDifficultyHitObjects[Column].Count;

            PreviousHitObjects = new List<ManiaDifficultyHitObject>[totalColumns];

            for (int i = 0; i < totalColumns; i++)
            {
                // Adds the previous hit object in each column
                PreviousHitObjects[i] = new List<ManiaDifficultyHitObject>();
                if (perColumnDifficultyHitObjects[i].Count > 0)
                {
                    PreviousHitObjects[i].Add((ManiaDifficultyHitObject)perColumnDifficultyHitObjects[i].Last());
                }

                // If another object is within 499ms of the region, add it as part of the local context as well
                for (int j = perColumnDifficultyHitObjects[i].Count - 2; j >= 0; j--)
                {
                    if (StartTime - perColumnDifficultyHitObjects[i][j].EndTime < 500)
                    {
                        PreviousHitObjects[i].Add((ManiaDifficultyHitObject)perColumnDifficultyHitObjects[i][j]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // This cannot be filled yet, because it requires perColumnDifficultyHitObjects to be fully constructed
            NextHitObjects = new List<ManiaDifficultyHitObject>[totalColumns];
            for (int i = 0; i < NextHitObjects.Length; i++)
            {
                NextHitObjects[i] = new List<ManiaDifficultyHitObject>();
            }

            NormalizedDeltaTime = 0.001 * DeltaTime;

            ManiaDifficultyHitObject? prev = PrevInColumn(0);
            NormalizedColumnDeltaTime = prev is not null ? 0.001 * (StartTime - prev.StartTime) : 10e9;
        }

        // Should only run after perColumnObjects is fully constructed with all details
        public void InitializeNextHitObjects()
        {
            for (int i = 0; i < NextHitObjects.Length; i++)
            {
                ManiaDifficultyHitObject? prevObject = PreviousHitObjects[i].FirstOrDefault();

                ManiaDifficultyHitObject? nextObject;

                int futureIndex;

                // If it's the column of the current note, find the next note
                if (i == Column)
                {
                    futureIndex = columnIndex + 1;
                }
                // If there is no previous object, the next one is the first in the column
                else if (prevObject == null)
                {
                    futureIndex = 0;
                }
                // Finally, if there is a previous note in another column, the next note is the note after that
                else
                {
                    futureIndex = prevObject.columnIndex + 1;
                }

                nextObject = futureIndex < perColumnDifficultyHitObjects[i].Count
                    ? (ManiaDifficultyHitObject?)perColumnDifficultyHitObjects[i][futureIndex]
                    : default;

                if (nextObject is not null)
                {
                    NextHitObjects[i].Add(nextObject);

                    // If another object is within 500ms of the region, add it as part of the local context as well
                    for (int j = nextObject.columnIndex + 1; j < perColumnDifficultyHitObjects[i].Count; j++)
                    {
                        // Notably, this includes notes within an LN's time range
                        if (perColumnDifficultyHitObjects[i][j].StartTime - EndTime <= 500)
                        {
                            NextHitObjects[i].Add((ManiaDifficultyHitObject)perColumnDifficultyHitObjects[i][j]);
                        }
                    }
                }
            }
        }

        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return (ManiaDifficultyHitObject?)(
                index >= 0 && index < perColumnDifficultyHitObjects[Column].Count
                ? perColumnDifficultyHitObjects[Column][index]
                : default);
        }

        public ManiaDifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return (ManiaDifficultyHitObject?)(
                index >= 0 && index < perColumnDifficultyHitObjects[Column].Count
                ? perColumnDifficultyHitObjects[Column][index]
                : default);
        }

        // For testing purposes
        public void PrintInformation()
        {
            string noteType = StartTime == EndTime ? "Regular" : "Long";
            Console.WriteLine($"{noteType} Note: {Index}, Time: {StartTime}-{EndTime}, Column: {Column + 1}");
            Console.WriteLine($"Previous:");
            for (int i = 0; i < PreviousHitObjects.Length; i++)
            {
                Console.WriteLine($"Column {i + 1}:");
                foreach (var note in PreviousHitObjects[i])
                {
                    string noteTypeShort = note.StartTime == note.EndTime ? "RN" : "LN";
                    Console.WriteLine($"{noteTypeShort} {note.Index} of distance {StartTime - note.EndTime}");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"Future:");
            for (int i = 0; i < NextHitObjects.Length; i++)
            {
                Console.WriteLine($"Column {i + 1}:");
                foreach (var note in NextHitObjects[i])
                {
                    string noteTypeShort = note.StartTime == note.EndTime ? "RN" : "LN";
                    Console.WriteLine($"{noteTypeShort} {note.Index} of distance {note.StartTime - EndTime}");
                }
            }
            Console.WriteLine();
        }
    }
}
