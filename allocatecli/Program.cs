using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using CommandLine;
using BlackMaple.SeedOrders;
using BlackMaple.SeedTactics.Scheduling;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using BlackMaple.FMSInsight.API;

namespace AllocateCli
{
  class Options
  {
    [Option('p', "plugin", Required = true, HelpText = "Plugin to use")]
    public string Plugin { get; set; }

    [Option('b', "bookings", HelpText = "Path to bookings json (defaults to standard input)")]
    public string BookingsJsonFile { get; set; }

    [Option('f', "flex", Required = true, HelpText = "Path to flexibility json file")]
    public string FlexJsonFile { get; set; }

    [Option('s', "start", HelpText = "Start date and time (defaults to 2016-11-05 7AM)")]
    public DateTime? StartUTC { get; set; }

    [Option('e', "end", HelpText = "End date and time (defaults to 2016-11-06 7AM")]
    public DateTime? EndUTC { get; set; }

    [Option("fill", HelpText = "Fill method", Default = BookingFillMethod.FillInAnyOrder)]
    public BookingFillMethod FillMethod { get; set; }

    [Option("downtime-file", HelpText = "Path to downtimes json file (defaults to no downtimes)")]
    public string DowntimeJsonFile { get; set; }

    [Option("downtimes", HelpText = "Downtime json (defaults to no downtimes)")]
    public string DowntimeJson { get; set; }

    [Option("schid", HelpText = "Schedule Id (defaults to newly generated)")]
    public string ScheduleId { get; set; }

    [Option("download", HelpText = "Server to download new jobs to (defaults to just printing to stdout)")]
    public string DownloadServer {get;set;}
  }

  class Program
  {
    static async Task<int> Main(string[] args)
    {
      try {
        var result = Parser.Default.ParseArguments<Options>(args);
        if (result.Tag == ParserResultType.NotParsed)
          return 1;

        var options = ((Parsed<Options>)result).Value;
        if (!options.StartUTC.HasValue)
          options.StartUTC = (new DateTime(2016, 11, 5, 7, 0, 0, DateTimeKind.Local)).ToUniversalTime();
        if (!options.EndUTC.HasValue)
          options.EndUTC = (new DateTime(2016, 11, 6, 7, 0, 0, DateTimeKind.Local)).ToUniversalTime();

        //load inputs

        var jsonSettings = new JsonSerializerSettings();
        jsonSettings.Converters.Add(new BlackMaple.FMSInsight.API.TimespanConverter());
        jsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        jsonSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

        var flex = JsonConvert.DeserializeObject<FlexPlan>(
          System.IO.File.ReadAllText(options.FlexJsonFile), jsonSettings);

        IEnumerable<StationDowntime> downtime;
        if (!string.IsNullOrEmpty(options.DowntimeJsonFile))
          downtime = JsonConvert.DeserializeObject<List<StationDowntime>>(
              System.IO.File.ReadAllText(options.DowntimeJsonFile), jsonSettings);
        else if (!string.IsNullOrEmpty(options.DowntimeJson))
          downtime = JsonConvert.DeserializeObject<List<StationDowntime>>(
              options.DowntimeJson, jsonSettings);
        else
          downtime = new StationDowntime[] { };


        UnscheduledStatus bookings;
        if (string.IsNullOrEmpty(options.BookingsJsonFile))
        {

          using (var reader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding))
          {
            var s = JsonSerializer.Create(jsonSettings);
            bookings = s.Deserialize<UnscheduledStatus>(new JsonTextReader(reader));
          }

        }
        else
        {
          bookings = JsonConvert.DeserializeObject<UnscheduledStatus>(
              File.ReadAllText(options.BookingsJsonFile), jsonSettings);
        }

        if (string.IsNullOrEmpty(options.ScheduleId)) {
          options.ScheduleId = CreateScheduleId.Create();
        }

        //run allocation
        var loader = new AssemblyLoader(Path.GetFullPath(options.Plugin));
        var allocate = loader.LoadPlugin();
        if (allocate == null) return 1;

        var results = allocate.Allocate(
            bookings,
            default(BlackMaple.FMSInsight.API.PlannedSchedule),
            default(BlackMaple.FMSInsight.API.CurrentStatus),
            flex,
            options.StartUTC.Value,
            options.EndUTC.Value,
            options.FillMethod,
            options.ScheduleId,
            downtime);

        if (string.IsNullOrEmpty(options.DownloadServer)) {
          //print results
          System.Console.WriteLine(
              JsonConvert.SerializeObject(results, Formatting.Indented, jsonSettings));
        } else {
          // download
          var newJobs = new NewJobs();
          newJobs.ScheduleId = options.ScheduleId;
          newJobs.Jobs = new ObservableCollection<JobPlan>(results.Jobs);
          newJobs.StationUse = new ObservableCollection<SimulatedStationUtilization>(results.SimStations);
          newJobs.ExtraParts = results.NewExtraParts.ToDictionary(x => x.Part, x => x.Quantity);
          newJobs.ArchiveCompletedJobs = true;
          newJobs.QueueSizes = new Dictionary<string, QueueSize>(results.QueueSizes);

          var builder = new UriBuilder(options.DownloadServer);
          if (builder.Scheme == "") builder.Scheme = "http";
          if (builder.Port == 80) builder.Port = 5000;
          var client = new JobsClient(builder.Uri.ToString());
          await client.AddAsync(newJobs, null);
        }

        return 0;

      } catch (Exception ex) {
        System.Console.Error.WriteLine("Error during allocate. " + Environment.NewLine + ex.ToString());
        return 1;
      }
    }

    public class AssemblyLoader : AssemblyLoadContext
    {
      private string depPath;
      private string fullPath;

      public AssemblyLoader(string p)
      {
        depPath = Path.GetDirectoryName(p);
        fullPath = p;
      }

      public IAllocateInterface LoadPlugin()
      {
        try
        {
          var a = LoadFromAssemblyPath(Path.GetFullPath(fullPath));
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
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
          System.Console.Error.WriteLine(ex.ToString());
          foreach (var l in ex.LoaderExceptions)
            System.Console.Error.WriteLine(l.ToString());
        }
        return null;
      }

      protected override Assembly Load(AssemblyName assemblyName)
      {
        var deps = DependencyContext.Default;
        var compileLibs = deps.CompileLibraries.Where(d => d.Name.Contains(assemblyName.Name));
        if (compileLibs.Any())
        {
          return Assembly.Load(new AssemblyName(compileLibs.First().Name));
        }

        var depFullPath = Path.Combine(depPath, assemblyName.Name + ".dll");
        if (File.Exists(depFullPath))
        {
          return LoadFromAssemblyPath(depFullPath);
        }

        return Assembly.Load(assemblyName);
      }
    }
  }
}
