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

namespace AllocateCli
{
  class Options
  {
    [Option('p', "plugin", Required = true, HelpText = "Plugin to test")]
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

    [Option("schid", HelpText = "Schedule Id", Default = "schId1234")]
    public string ScheduleId { get; set; }
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
        options.StartUTC = (new DateTime(2016, 11, 5, 7, 0, 0, DateTimeKind.Local)).ToUniversalTime();
      if (!options.EndUTC.HasValue)
        options.EndUTC = (new DateTime(2016, 11, 6, 7, 0, 0, DateTimeKind.Local)).ToUniversalTime();

      //load inputs

      var flex = ReadJsonFile<FlexPlan>(options.FlexJsonFile);

      IEnumerable<StationDowntime> downtime;
      if (!string.IsNullOrEmpty(options.DowntimeJsonFile))
        downtime = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StationDowntime>>(
            System.IO.File.ReadAllText(options.DowntimeJsonFile));
      else if (!string.IsNullOrEmpty(options.DowntimeJson))
        downtime = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StationDowntime>>(
            options.DowntimeJson);
      else
        downtime = new StationDowntime[] { };


      UnscheduledStatus bookings;
      if (string.IsNullOrEmpty(options.BookingsJsonFile))
      {

        using (var reader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding))
        {
          var s = new Newtonsoft.Json.JsonSerializer();
          bookings = s.Deserialize<UnscheduledStatus>(new Newtonsoft.Json.JsonTextReader(reader));
        }

      }
      else
      {
        bookings = Newtonsoft.Json.JsonConvert.DeserializeObject<UnscheduledStatus>(
            File.ReadAllText(options.BookingsJsonFile));
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

      //print results

      System.Console.WriteLine(
          Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));

      return 0;
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

    private static T ReadJsonFile<T>(string fileName)
    {
      var json = File.ReadAllText(fileName);
      return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json, new TimespanConverter());
    }
  }

  public class TimespanConverter : Newtonsoft.Json.JsonConverter
  {
    public override bool CanConvert(Type objectType)
    {
      return objectType == typeof(TimeSpan);
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
      if (objectType != typeof(TimeSpan))
        throw new ArgumentException();

      var spanString = reader.Value as string;
      if (spanString == null)
        return null;
      if (TimeSpan.TryParse(spanString, out TimeSpan result))
        return result;
      return System.Xml.XmlConvert.ToTimeSpan(spanString);
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
    {
      var duration = (TimeSpan)value;
      writer.WriteValue(System.Xml.XmlConvert.ToString(duration));
    }
  }
}
