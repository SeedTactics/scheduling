/* Copyright (c) 2018, John Lenz

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
using System.Runtime.Serialization;

namespace BlackMaple.SeedTactics.Scheduling
{
  [DataContract]
  public struct FlexibilityStation : IEquatable<FlexibilityStation>
  {
    [DataMember(IsRequired = true)] public string GroupName { get; set; }
    [DataMember(IsRequired = true)] public int StationNumber { get; set; }

    public bool Equals(FlexibilityStation other)
        => GroupName == other.GroupName && StationNumber == other.StationNumber;
    public override int GetHashCode()
        => (GroupName.GetHashCode(), StationNumber.GetHashCode()).GetHashCode();
  }

  [DataContract]
  public class FlexRouteStop
  {
    [DataMember(IsRequired = true)]
    public string MachineGroup { get; set; }

    [DataMember(IsRequired = true)]
    public string Program { get; set; }

    [DataMember(IsRequired = true)]
    public HashSet<int> Machines { get; private set; } = new HashSet<int>();

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public TimeSpan ExpectedCycleTime { get; set; }
  }

  [DataContract]
  public class FlexPath
  {
    [DataMember(IsRequired = true)]
    public HashSet<int> LoadStations { get; private set; } = new HashSet<int>();

    [DataMember(IsRequired = true)]
    public TimeSpan ExpectedLoadTime { get; set; }

    [DataMember(IsRequired = true)]
    public IList<FlexRouteStop> Stops { get; private set; } = new List<FlexRouteStop>();

    [DataMember(IsRequired = true)]
    public HashSet<int> UnloadStations { get; private set; } = new HashSet<int>();

    [DataMember(IsRequired = true)]
    public TimeSpan ExpectedUnloadTime { get; set; }

    [DataMember(IsRequired = true)]
    public HashSet<string> Pallets { get; private set; } = new HashSet<string>();

    [DataMember(IsRequired = true)]
    public string Fixture { get; set; }

    [DataMember(IsRequired = true)]
    public int Face { get; set; }

    [DataMember(IsRequired = true)]
    public int QuantityOnFace { get; set; }

    // Optional input queue. If given, only allow loading when the queue contains a
    // piece of material.
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string InputQueue { get; set; }

    // Optional output queue.  If given, place completed material into this queue
    // and also do not unload if the queue is full.
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string OutputQueue { get; set; }
  }

  [DataContract]
  public class FlexInspection
  {
    [DataMember(IsRequired = true)]
    public string InspectionType { get; set; }

    //There are three possible ways of triggering an exception: counts, random, and time interval.
    // The system maintains a database of counters which consist of an Id, a part completed count, and
    // the time of the last triggered inspection.  The CounterIdTemplate is used at the time a part
    // is completed; the template is replaced with the specific pallets and machines for the just completed
    // part to find the counter to use.
    [DataMember(IsRequired = true)] public string CounterIdTemplate { get; set; }

    // Each time a part is completed, the counter is incremented and if it reaches a given value an inspection
    // is triggered and the counter is reset to zero.  Multiple parts could share the same counter, or a single
    // part could use multiple counters via replacement strings.
    [DataMember(IsRequired = true)] public int MaxVal { get; set; }

    // Each time a part is completed, the counter is checked for the time of the last inspection.  If
    // the given amount of time has passed, the inspection is triggered.
    // This can be disabled by using TimeSpan.Zero
    [DataMember(IsRequired = true)] public TimeSpan TimeInterval { get; set; }

    // If this is non-zero, the part is inspected with the given frequency (number between 0 and 1).  Nothing
    // about counters or time is used, this is purely memoryless Bernoulli process.
    [DataMember(IsRequired = true)] public double RandomFreq { get; set; }

    [DataMember(IsRequired = true)]
    public TimeSpan ExpectedInspectionTime { get; set; }

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

  [DataContract]
  public class FlexProcess
  {
    [DataMember(IsRequired = true)]
    public int ProcessNumber { get; set; }

    [DataMember(IsRequired = true)]
    public IList<FlexPath> Paths { get; private set; } = new List<FlexPath>();

    // inspections which happen after this process is completed.
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public IList<FlexInspection> Inspections { get; private set; } = new List<FlexInspection>();
  }

  [DataContract]
  public enum PartReadiness
  {
    [EnumMember] ProductionReady,
    [EnumMember] ProveOutOnly
  }

  [DataContract]
  public class FlexPart
  {
    [DataMember(IsRequired = true)]
    public string Name { get; set; }

    [DataMember(IsRequired = true)]
    public PartReadiness Readiness { get; set; }

    [DataMember(IsRequired = true)]
    public IList<FlexProcess> Processes { get; private set; } = new List<FlexProcess>();

    [DataMember(IsRequired = true)]
    public bool Wash { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public TimeSpan ExpectedWashTime { get; set; }
  }

  [DataContract]
  public class FlexLaborTeam
  {
    [DataMember(IsRequired = true)] public string TeamName { get; set; }
    [DataMember(IsRequired = true)] public int NumberOfOperators { get; set; }
    [DataMember(IsRequired = true)] public IList<int> LoadStations { get; set; }
  }

  [DataContract]
  public class FlexQueueSize
  {
    //once an output queue grows to this size, stop unloading parts
    //and keep them in the buffer inside the cell
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int? MaxSizeBeforeStopUnloading { get; set; }
  }

  [DataContract]
  public class FlexPlan
  {
    ///All the parts in the flexibility plan
    [DataMember(IsRequired = true)]
    public IList<FlexPart> Parts { get; private set; } = new List<FlexPart>();

    ///All the labor teams which are assigned to stations.  If the list is empty, it is assumed
    ///that each station has a dedicated labor operator.
    [DataMember(IsRequired = true)]
    public IList<FlexLaborTeam> LaborTeams { get; private set; } = new List<FlexLaborTeam>();

    ///Queue sizes (if in-process queues are used)
    [DataMember(IsRequired = true)]
    public IDictionary<string, FlexQueueSize> QueueSizes { get; private set; } = new Dictionary<string, FlexQueueSize>();

    ///Cell efficiency as a percentage between 0 and 1
    [DataMember(IsRequired = true)]
    public double CellEfficiency { get; set; } = 1.0;

    ///Travel time of the cart between two points (average)
    [DataMember(IsRequired = true)]
    public TimeSpan ExpectedCartTravelTime { get; set; } = TimeSpan.FromMinutes(1);

    ///Time for a rotary swap from machine queue to machine worktable
    [DataMember(IsRequired = true)]
    public TimeSpan ExpectedRotarySwapTime { get; set; } = TimeSpan.FromMinutes(0.5);

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string SeedtacticPlanningToken { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string OriginalMastModelFileName { get; set; }
  }
}