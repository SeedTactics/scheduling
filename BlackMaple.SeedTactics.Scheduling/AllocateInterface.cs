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
using System.Linq;
using BlackMaple.FMSInsight.API;

#nullable enable

namespace BlackMaple.SeedTactics.Scheduling
{
  ///Marks that a collection of stations is down for a given time period during the allocation.
  public class StationDowntime
  {
    public DateTime StartOfDowntimeUTC { get; set; }
    public DateTime EndOfDowntimeUTC { get; set; }
    ///An empty list means the entire cell - all stations
    public IReadOnlyCollection<FlexibilityStation>? Station { get; set; }
  }

  public enum BookingFillMethod
  {
    FillInAnyOrder,
    FillOnlyByDueDate
  }

  // An allocation algorithm will receive an AllocateRequest as JSON on standard input and
  // must produce a value of type BlackMaple.FMSInsight.API.NewJobs as JSON on standard output
  public class AllocateRequest
  {
    public string ScheduleId { get; set; } = CreateScheduleId.Create();

    public DateTime StartUTC { get; set; }

    public DateTime EndUTC { get; set; }

    public PlannedSchedule? PreviousSchedule { get; set; }

    public CurrentStatus? CurrentStatus { get; set; }

    public IEnumerable<SeedOrders.Booking>? UnscheduledBookings { get; set; }

    public IEnumerable<SeedOrders.ScheduledPartWithoutBooking>? ScheduledParts { get; set; }

    public IEnumerable<SeedOrders.Casting>? AvailableCastings { get; set; }

    public FlexPlan FlexPlan { get; set; } = new FlexPlan();

    public BookingFillMethod FillMethod { get; set; } = BookingFillMethod.FillInAnyOrder;

    public IEnumerable<StationDowntime>? Downtimes { get; set; }
  }

  public class AllocateResult
  {
    public IEnumerable<JobPlan> Jobs { get; set; } = Enumerable.Empty<JobPlan>();
    public IEnumerable<SimulatedStationUtilization> SimStations { get; set; } = Enumerable.Empty<SimulatedStationUtilization>();
    public IReadOnlyDictionary<string, int> NewExtraParts { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, QueueSize> QueueSizes { get; set; } = new Dictionary<string, QueueSize>();
    public IReadOnlyDictionary<string, string>? DebugData { get; set; }
  }

}
