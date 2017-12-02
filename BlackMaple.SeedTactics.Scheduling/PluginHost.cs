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

namespace BlackMaple.SeedTactics.Scheduling
{
    public class PluginHost : MarshalByRefObject
    {
        private readonly IAllocateInterface _allocate;

        public PluginHost(string pluginDll)
        {
            var a = Assembly.LoadFrom(pluginDll);
            foreach (var t in a.GetTypes())
            {
                foreach (var i in t.GetInterfaces())
                {
                    if (_allocate == null && i == typeof(IAllocateInterface))
                    {
                        _allocate = (IAllocateInterface)Activator.CreateInstance(t);
                        return;
                    }
                }
            }
        }

        private static T DeserializeObject<T>(string json)
        {
            var s = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
            var m = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            return (T)s.ReadObject(m);
        }

        public static string SerializeObject<T>(T obj)
        {
            var s = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
            var m = new System.IO.MemoryStream();
            s.WriteObject(m, obj);
            var bytes = m.ToArray();
            return System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);
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
            var bookings = DeserializeObject<SeedOrders.UnscheduledStatus>(bookingsJson);
            var previousSchedule = DeserializeObject<MachineWatchInterface.JobsAndExtraParts>(previousScheduleJson);
            var plan = DeserializeObject<FlexPlan>(flexPlanJson);
            var result = _allocate.Allocate(bookings, previousSchedule, plan, startLocal, endLocal, fillMethod, scheduleId);

            return SerializeObject(result);
        }

    }
}
