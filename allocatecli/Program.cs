using System;
using System.Collections.Generic;
using System.IO;

using CommandLine;
using BlackMaple.SeedOrders;
using BlackMaple.SeedTactics.Scheduling;
using Newtonsoft.Json;

namespace AllocateCli
{
    class Options
    {
        [Option('p', "plugin", Required=true, HelpText="Plugin to test")]
        public string Plugin {get;set;}

        [Option('b', "bookings", HelpText="Path to bookings json (defaults to standard input)")]
        public string BookingsJsonFile {get;set;}

        [Option('f', "flex", Required=true, HelpText="Path to flexibility json file")]
        public string FlexJsonFile {get;set;}

        [Option('s', "start", HelpText="Start date and time (defaults to 2016-11-05 7AM)")]
        public DateTime? StartUTC {get;set;}

        [Option('e', "end", HelpText="End date and time (defaults to 2016-11-06 7AM")]
        public DateTime? EndUTC {get;set;}

        [Option("fill", HelpText="Fill method", Default=BookingFillMethod.FillInAnyOrder)]
        public BookingFillMethod FillMethod {get;set;}

        [Option("downtimes", HelpText="Path to downtimes json file (defaults to no downtimes)")]
        public string DowntimeJsonFile {get;set;}
    }

    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            if (result.Tag == ParserResultType.NotParsed)
                return 1;

            var options = ((Parsed<Options>)result).Value;
            if (!options.StartUTC.HasValue)
                options.StartUTC = new DateTime(2016, 11, 5, 7, 0, 0, DateTimeKind.Utc);
            if (!options.EndUTC.HasValue)
                options.EndUTC = new DateTime(2016, 11, 6, 7, 0, 0, DateTimeKind.Utc);

            //load inputs

            var flex = JsonConvert.DeserializeObject<FlexPlan>(File.ReadAllText(options.FlexJsonFile));

            IEnumerable<StationDowntime> downtime;
            if (string.IsNullOrEmpty(options.DowntimeJsonFile))
                downtime = new StationDowntime[] {};
            else
                downtime = JsonConvert.DeserializeObject<List<StationDowntime>>(
                    File.ReadAllText(options.DowntimeJsonFile)
                );


            UnscheduledStatus status;
            if (string.IsNullOrEmpty(options.BookingsJsonFile))
            {
                using (var reader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding))
                {
                    var s = new JsonSerializer();
                    status = s.Deserialize<UnscheduledStatus>(new JsonTextReader(reader));
                }

            } else {
                status = JsonConvert.DeserializeObject<UnscheduledStatus>(
                    File.ReadAllText(options.BookingsJsonFile)
                );
            }

            //run allocation
            var allocate = LoadPlugin(options);
            if (allocate == null) return 1;

            var results = allocate.Allocate(
                status,
                default(BlackMaple.MachineWatchInterface.JobsAndExtraParts),
                flex,
                options.StartUTC.Value,
                options.EndUTC.Value,
                options.FillMethod,
                "schId1234",
                downtime);

            //print results

            System.Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));

            return 0;
        }

        private static IAllocateInterface LoadPlugin(Options options)
        {
            var a = System.Reflection.Assembly.LoadFrom(options.Plugin);
            foreach (var t in a.GetTypes())
            {
                foreach (var i in t.GetInterfaces())
                {
                    if (i == typeof(IAllocateInterface))
                    {
                        return (IAllocateInterface)Activator.CreateInstance(t);
                    }
                }
            }
            System.Console.Error.WriteLine("Plugin does not contain implementation of IAllocateInterface");
            return null;
        }
    }
}
