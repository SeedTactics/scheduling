using System;
using System.Collections.Generic;
using BlackMaple.MachineWatchInterface;

namespace BlackMaple.SeedTactics.Scheduling
{
    public class AllocateResult
    {
        public string ScheduleId { get; set; }
        public List<JobPlan> Jobs { get; set; }
        public List<SimulatedStationUtilization> SimStations { get; set; }
        public List<string> NewScheduledOrders { get; set; }
        public List<SeedOrders.ScheduledPartWithoutBooking> NewExtraParts { get; set; }
    }

    public struct FlexPlan
    {
        public Newtonsoft.Json.Linq.JObject FlexJson { get; set; }
        public object MastModel { get; set; }
    }

    public enum BookingFillMethod
    {
        FillInAnyOrder,
        FillOnlyByDueDate
    }

    public interface IAllocateInterface
    {
        AllocateResult Allocate(
            SeedOrders.UnscheduledStatus bookings,
            JobsAndExtraParts previousSchedule,
            FlexPlan flexPlan,
            DateTime startLocal,
            DateTime endLocal,
            BookingFillMethod fillMethod,
            string scheduleId);
    }
}
