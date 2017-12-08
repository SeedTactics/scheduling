/* Copyright (c) 2017, John Lenz

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

namespace BlackMaple.SeedTactics.Scheduling
{
    public struct FlexibilityStation : IEquatable<FlexibilityStation>
    {
        public string GroupName {get;set;}        
        public int StationNumber {get;set;}

        public bool Equals(FlexibilityStation other)
            => GroupName == other.GroupName && StationNumber == other.StationNumber;
        public override int GetHashCode()
            => (GroupName.GetHashCode(), StationNumber.GetHashCode()).GetHashCode();
    }

    public class FlexRouteStop
    {
        public string MachineGroup {get;set;}
        public string Program {get;set;}
        public HashSet<int> Machines {get;} = new HashSet<int>();
        public TimeSpan ExpectedCycleTime {get;set;}
    }

    public class FlexPath
    {
        public HashSet<int> LoadStations {get;} = new HashSet<int>();
        public TimeSpan ExpectedLoadTime {get;set;}

        public IList<FlexRouteStop> Stops {get;} = new List<FlexRouteStop>();

        public HashSet<int> UnloadStations {get;} = new HashSet<int>();
        public TimeSpan ExpectedUnloadTime {get;set;}

        public HashSet<string> Pallets {get;} = new HashSet<string>();

        public string Fixture {get;set;}
        public int Face {get;set;}
        public int QuantityOnFace {get;set;}
    }

    public class FlexProcess
    {
        public int ProcessNumber {get;set;}
        public IList<FlexPath> Paths {get;} = new List<FlexPath>();
    }

    public enum PartReadiness
    {
        ProductionReady,
        ProveOutOnly
    }

    public class FlexPart
    {
        public string Name {get;set;}
        public PartReadiness Readiness {get;set;}
        public IList<FlexProcess> Processes {get;} = new List<FlexProcess>();
    }

    public class FlexPlan
    {
        ///All the parts in the flexibility plan
        public IList<FlexPart> Parts {get;} = new List<FlexPart>();
        
        ///Cell efficiency as a percentage between 0 and 1
        public double CellEfficiency {get;set;} = 1.0;

        ///Travel time of the cart between two points (average)
        public TimeSpan ExpectedCartTravelTime {get;set;} = TimeSpan.FromMinutes(1);

        ///Time for a rotary swap from machine queue to machine worktable
        public TimeSpan ExpectedRotarySwapTime {get;set;} = TimeSpan.FromMinutes(0.5);

        public string OriginalSeedtacticPlanningJson { get; set; }
        public string OriginalMastModelFileName { get; set; }
    }
 }