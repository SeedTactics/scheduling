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
        [DataMember] public string GroupName {get;set;}
        [DataMember] public int StationNumber {get;set;}

        public bool Equals(FlexibilityStation other)
            => GroupName == other.GroupName && StationNumber == other.StationNumber;
        public override int GetHashCode()
            => (GroupName.GetHashCode(), StationNumber.GetHashCode()).GetHashCode();
    }

    [DataContract]
    public class FlexRouteStop
    {
        [DataMember] public string MachineGroup {get;set;}
        [DataMember] public string Program {get;set;}
        [DataMember] public HashSet<int> Machines {get; private set;} = new HashSet<int>();
        [DataMember] public TimeSpan ExpectedCycleTime {get;set;}
    }

    [DataContract]
    public class FlexPath
    {
        [DataMember] public HashSet<int> LoadStations {get; private set;} = new HashSet<int>();
        [DataMember] public TimeSpan ExpectedLoadTime {get;set;}

        [DataMember] public IList<FlexRouteStop> Stops {get; private set;} = new List<FlexRouteStop>();

        [DataMember] public HashSet<int> UnloadStations {get; private set;} = new HashSet<int>();
        [DataMember] public TimeSpan ExpectedUnloadTime {get;set;}

        [DataMember] public HashSet<string> Pallets {get; private set;} = new HashSet<string>();

        [DataMember] public string Fixture {get;set;}
        [DataMember] public int Face {get;set;}
        [DataMember] public int QuantityOnFace {get;set;}

        // Optional input queue. If given, only allow loading when the queue contains a
        // piece of material.
        [DataMember(IsRequired=false, EmitDefaultValue=false)]
        public string InputQueue {get;set;}

        // Optional output queue.  If given, place completed material into this queue
        // and also do not unload if the queue is full.
        [DataMember(IsRequired=false, EmitDefaultValue=false)]
        public string OutputQueue {get;set;}
    }

    [DataContract]
    public class FlexProcess
    {
        [DataMember] public int ProcessNumber {get;set;}
        [DataMember] public IList<FlexPath> Paths {get; private set;} = new List<FlexPath>();
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
        [DataMember] public string Name {get;set;}
        [DataMember] public PartReadiness Readiness {get;set;}
        [DataMember] public IList<FlexProcess> Processes {get; private set;} = new List<FlexProcess>();
    }

    [DataContract]
    public class FlexLaborTeam
    {
        [DataMember] public string TeamName {get;set;}
        [DataMember] public int NumberOfOperators {get;set;}
        [DataMember] public IList<int> LoadStations {get;set;}
    }

    [DataContract]
    public class FlexQueueSize
    {
        //once an output queue grows to this size, stop loading new parts
        //which are destined for this queue
        [DataMember(IsRequired=false, EmitDefaultValue=false)]
        public int? MaxSizeBeforeStopLoading {get;set;}

        //once an output queue grows to this size, stop unloading parts
        //and keep them in the buffer inside the cell
        [DataMember(IsRequired=false, EmitDefaultValue=false)]
        public int? MaxSizeBeforeStopUnloading {get;set;}
    }

    [DataContract]
    public class FlexPlan
    {
        ///All the parts in the flexibility plan
        [DataMember] public IList<FlexPart> Parts {get; private set;} = new List<FlexPart>();

        ///All the labor teams which are assigned to stations.  If the list is empty, it is assumed
        ///that each station has a dedicated labor operator.
        [DataMember] public IList<FlexLaborTeam> LaborTeams {get; private set;} = new List<FlexLaborTeam>();

        ///Queue sizes (if in-process queues are used)
        [DataMember] public IDictionary<string, FlexQueueSize> QueueSizes {get;private set;} = new Dictionary<string, FlexQueueSize>();

        ///Cell efficiency as a percentage between 0 and 1
        [DataMember] public double CellEfficiency {get;set;} = 1.0;

        ///Travel time of the cart between two points (average)
        [DataMember] public TimeSpan ExpectedCartTravelTime {get;set;} = TimeSpan.FromMinutes(1);

        ///Time for a rotary swap from machine queue to machine worktable
        [DataMember] public TimeSpan ExpectedRotarySwapTime {get;set;} = TimeSpan.FromMinutes(0.5);

        [DataMember] public string OriginalSeedtacticPlanningJson { get; set; }
        [DataMember] public string OriginalMastModelFileName { get; set; }
    }
 }