/* Copyright (c) 2020, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;

#nullable enable

namespace BlackMaple.SeedTactics.Scheduling
{
  public struct FlexibilityStation : IEquatable<FlexibilityStation>
  {
    public string GroupName { get; set; }
    public int StationNumber { get; set; }

    public bool Equals(FlexibilityStation other)
        => GroupName == other.GroupName && StationNumber == other.StationNumber;
    public override int GetHashCode()
        => (GroupName.GetHashCode(), StationNumber.GetHashCode()).GetHashCode();
  }

  public class FlexRouteStop
  {
    public string MachineGroup { get; set; } = "MC";

    public string Program { get; set; } = "";

    public long? Revision { get; set; }

    public HashSet<int> Machines { get; private set; } = new HashSet<int>();

    public TimeSpan? ExpectedCycleTime { get; set; }
  }

  public class FlexPath
  {
    public HashSet<int> LoadStations { get; private set; } = new HashSet<int>();

    public TimeSpan ExpectedLoadTime { get; set; }

    public IList<FlexRouteStop> Stops { get; private set; } = new List<FlexRouteStop>();

    public HashSet<int> UnloadStations { get; private set; } = new HashSet<int>();

    public TimeSpan ExpectedUnloadTime { get; set; }

    public HashSet<int> Pallets { get; private set; } = new HashSet<int>();

    public string? Fixture { get; set; }

    public int Face { get; set; }

    public int QuantityOnFace { get; set; }

    // Optional input queue. If given, only allow loading when the queue contains a
    // piece of material.
    public string? InputQueue { get; set; }

    // Optional output queue.  If given, place completed material into this queue
    // and also do not unload if the queue is full.
    public string? OutputQueue { get; set; }

    // Option casting (only for process = 1).  If given, the input queue is searched
    // for castings matching the given name
    public string? Casting { get; set; }

    // inspections which happen after this path is completed.
    public IList<FlexInspection>? Inspections { get; set; }
  }

  public class FlexInspection
  {
    public string InspectionType { get; set; } = "Default";

    //There are three possible ways of triggering an exception: counts, random, and time interval.
    // The system maintains a database of counters which consist of an Id, a part completed count, and
    // the time of the last triggered inspection.  The CounterIdTemplate is used at the time a part
    // is completed; the template is replaced with the specific pallets and machines for the just completed
    // part to find the counter to use.
    public string CounterIdTemplate { get; set; } = StationFormatFlag(1, 1);

    // Each time a part is completed, the counter is incremented and if it reaches a given value an inspection
    // is triggered and the counter is reset to zero.  Multiple parts could share the same counter, or a single
    // part could use multiple counters via replacement strings.
    public int MaxVal { get; set; }

    // Each time a part is completed, the counter is checked for the time of the last inspection.  If
    // the given amount of time has passed, the inspection is triggered.
    // This can be disabled by using TimeSpan.Zero
    public TimeSpan? TimeInterval { get; set; }

    // If this is non-zero, the part is inspected with the given frequency (number between 0 and 1).  Nothing
    // about counters or time is used, this is purely memoryless Bernoulli process.
    public double? RandomFreq { get; set; }

    public TimeSpan? ExpectedInspectionTime { get; set; }

    //The final counter string is determined by replacing following substrings in the counter template
    public static string PalletFormatFlag(int proc)
    {
      return "%pal" + proc.ToString() + "%";
    }
    public static string LoadFormatFlag(int proc)
    {
      return "%load" + proc.ToString() + "%";
    }
    public static string UnloadFormatFlag(int proc)
    {
      return "%unload" + proc.ToString() + "%";
    }
    public static string StationFormatFlag(int proc, int stopNum)
    {
      return "%stat" + proc.ToString() + "," + stopNum.ToString() + "%";
    }
  }

  public class FlexProcess
  {
    public int ProcessNumber { get; set; }

    public IList<FlexPath> Paths { get; private set; } = new List<FlexPath>();
  }

  public enum PartReadiness
  {
    ProductionReady,
    ProveOutOnly
  }

  public class FlexPart
  {
    public string Name { get; set; } = "part";

    public PartReadiness Readiness { get; set; }

    public IList<FlexProcess> Processes { get; private set; } = new List<FlexProcess>();

    public bool Wash { get; set; }

    public TimeSpan? ExpectedWashTime { get; set; }

    public string? GroupColor { get; set; }
  }

  public class FlexLaborTeam
  {
    public string TeamName { get; set; } = "team";
    public int NumberOfOperators { get; set; }
    public IList<int> LoadStations { get; set; } = new List<int>();
  }

  public class FlexQueueSize
  {
    //once an output queue grows to this size, stop unloading parts
    //and keep them in the buffer inside the cell
    public int? MaxSizeBeforeStopUnloading { get; set; }
  }

  public class FlexPlan
  {
    ///All the parts in the flexibility plan
    public IList<FlexPart> Parts { get; private set; } = new List<FlexPart>();

    ///All the labor teams which are assigned to stations.  If the list is empty, it is assumed
    ///that each station has a dedicated labor operator.
    public IList<FlexLaborTeam> LaborTeams { get; private set; } = new List<FlexLaborTeam>();

    ///Queue sizes (if in-process queues are used)
    public IDictionary<string, FlexQueueSize> QueueSizes { get; private set; } = new Dictionary<string, FlexQueueSize>();

    ///Maps kanban names to a list of pallet numbers
    public IDictionary<string, IList<int>>? Kanbans { get; set; }

    public int? NumLoadStations { get; set; }

    ///Maps a machine group to the machine numbers for that group
    public IDictionary<string, IList<int>>? MachineNumbers { get; set; }

    ///The list of machine groups in-order of stops (repeats are allowed)
    public IList<string>? MachineRouting { get; set; }

    ///Cell efficiency as a percentage between 0 and 1
    public double CellEfficiency { get; set; } = 1.0;

    ///Travel time of the cart between two points (average)
    public TimeSpan ExpectedCartTravelTime { get; set; } = TimeSpan.FromMinutes(1);

    ///Time for a rotary swap from machine queue to machine worktable
    public TimeSpan ExpectedRotarySwapTime { get; set; } = TimeSpan.FromMinutes(0.5);

    public string? SeedtacticPlanningToken { get; set; }

    public string? OriginalMastModelFileName { get; set; }
  }
}