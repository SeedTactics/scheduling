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
using System.Reflection;
using Newtonsoft.Json;

namespace BlackMaple.SeedTactics.Scheduling
{
    public class PluginHost
    {
        private readonly IAllocateInterface _allocate;

        public PluginHost(string pluginDll)
        {
            var a = Assembly.LoadFrom(pluginDll);
            foreach (var t in a.GetTypes())
            {
                foreach (var i in t.GetInterfaces())
                {
                    if (_allocate != null && i == typeof(IAllocateInterface))
                    {
                        _allocate = (IAllocateInterface)Activator.CreateInstance(t);
                        return;
                    }
                }
            }
        }

        public string Allocate(
            string bookingsJson,
            string previousScheduleJson,
            string flexPlanJson,
            DateTime startLocal,
            DateTime endLocal,
            BookingFillMethod fillMethod,
            string scheduleId)
        {
            var bookings = JsonConvert.DeserializeObject<SeedOrders.UnscheduledStatus>(bookingsJson);
            var previousSchedule = JsonConvert.DeserializeObject<MachineWatchInterface.JobsAndExtraParts>(previousScheduleJson);
            FlexPlan plan = default(FlexPlan);
            if (flexPlanJson.StartsWith("{"))
                plan.FlexJson = Newtonsoft.Json.Linq.JObject.Parse(flexPlanJson);
            else
                plan.MastModelFile = flexPlanJson;

            var result = _allocate.Allocate(bookings, previousSchedule, plan, startLocal, endLocal, fillMethod, scheduleId);

            return JsonConvert.SerializeObject(result);
        }

    }
}
