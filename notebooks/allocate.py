import re
import pandas as pd
import subprocess
import json
import urllib
import os
import plotly.figure_factory as ff
import plotly.graph_objs as go

# Compied and modified from https://github.com/pandas-dev/pandas/pull/19065 until new pandas release
iso_pater = re.compile(r"""P
                        (?:(?P<days>-?[0-9]*)D)?
                        T
                        (?:(?P<hours>[0-9]{1,2}H))?
                        (?:(?P<minutes>[0-9]{1,2}M))?
                        (?:
                          (?P<seconds>[0-9]{0,2})?
                          (\.
                          (?P<milliseconds>[0-9]{1,3})
                          (?P<microseconds>[0-9]{0,3})
                          (?P<nanoseconds>[0-9]{0,3})
                          )?S
                        )?""", re.VERBOSE)


def parse_iso_format_string(iso_fmt):
    """Parse ISO formatted string into a timedelta"""
    t = pd.Timedelta(0)
    match = re.match(iso_pater, iso_fmt)
    if match:
        match_dict = match.groupdict(default='0')
        for comp in ['milliseconds', 'microseconds', 'nanoseconds']:
            match_dict[comp] = '{:0<3}'.format(match_dict[comp])
        for k, v in match_dict.items():
            t += pd.Timedelta(v, unit=k)
    else:
        raise ValueError("Invalid ISO 8601 Duration format - "
                         "{}".format(iso_fmt))
    return t

def encode_bookings(bookings, prev_parts):
    blst = []
    for b in bookings.groupby("BookingId"):
        blst.append({"BookingId":b[0],
                     "DueDate":b[1]["DueDate"].iloc[0].isoformat(),
                     "Priority":b[1]["Priority"].iloc[0].item(),
                     "Parts": [{"BookingId":b[0], "Part":p["Part"], "Quantity":p["Quantity"]}
                               for _,p in b[1].iterrows()]})
    return json.dumps({"UnscheduledBookings": blst, "ScheduledParts": prev_parts})

def encode_bookings_as_workorders(bookings):
    if bookings is None: return []
    wlst = []
    for index, b in bookings.iterrows():
        wlst.append({"WorkorderId": b["BookingId"],
                     "Part": b["Part"],
                     "Quantity": b["Quantity"],
                     "DueDate": b["DueDate"].isoformat(),
                     "Priority": b["Priority"]})
    return wlst

def print_result_summary(results):
    print("Simulation from {} to {} UTC".format(results["Jobs"][0]["RouteStartUTC"], results["Jobs"][0]["RouteEndUTC"]))
    for j in results["Jobs"]:
        print("* {} ".format(j["PartName"]))
        print("    Priority: {}".format(j["Priority"]))
        procCntr = 1
        for proc in j["ProcsAndPaths"]:
            print("    Proc {}".format(procCntr))
            pathCntr = 0
            for path in proc["paths"]:
                print("        Path {}".format(pathCntr))
                if procCntr == 1:
                    print("            Completed {}".format(j["CyclesOnFirstProcess"][pathCntr]))
                print("            Pallets {}".format(",".join(path["Pallets"])))
                print("            Loads {}".format(",".join([str(l) for l in path["Load"]])))
                stops = []
                for s in path["Stops"]:
                    stops.append(",".join([s["StationGroup"] + n for n in s["Stations"].keys()]))
                print("            Stops {}".format("->".join(stops)))
                print("            Unload {}".format(",".join([str(u) for u in path["Unload"]])))
                pathCntr += 1
            procCntr += 1

def allocate(bookings, flex_file, plugin, allocatecli, prev_parts=[], downtimes=[], start_utc=None, end_utc=None, schid=None):
    bookings_json = encode_bookings(bookings, prev_parts)
    downtime_json = json.dumps(downtimes)
    args =[ "dotnet", "run", "-p", allocatecli, "--",
           "-f", flex_file, "-p", plugin, "--downtimes", downtime_json
          ]
    if start_utc != None:
        args.append("--start")
        args.append(start_utc)
    if end_utc != None:
        args.append("--end")
        args.append(end_utc)
    if schid != None:
        args.append("--schid")
        args.append(schid)
    env = os.environ.copy()
    env["TERM"] = "xterm"
    proc = subprocess.run(args=args,
                          input=bookings_json,
                          encoding="utf-8",
                          stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE,
                          env=env)
    if proc.stderr != "":
        print(proc.stderr)
    if proc.returncode != 0:
        raise Exception()
    results_json = proc.stdout
    results = json.loads(results_json)
    results["OriginalJson"] = results_json
    return results

def newjobs(results, bookings=None):
    newJobs = {
      "Jobs": results["Jobs"],
      "StationUse": results["SimStations"],
      "ExtraParts": {},
      "ArchiveCompletedJobs": True,
      "ScheduleId": results["ScheduleId"],
      "QueueSizes": results["QueueSizes"],
      "CurrentUnfilledWorkorders": encode_bookings_as_workorders(bookings)
    }
    for p in results["NewExtraParts"]:
        newJobs["ExtraParts"][p["Part"]] = p["Quantity"]
    return newJobs

def download(results, computer, bookings=None):
    newJobs = newjobs(results, bookings)
    req = urllib.request.Request(url="http://" + computer + "/api/v1/jobs/add",
                                 data=json.dumps(newJobs).encode('utf-8'),
                                 headers={'content-type': 'application/json'},
                                 method='POST')
    urllib.request.urlopen(req)

def simstat(results):
    simstat = pd.DataFrame(results["SimStations"])
    simstat["StartUTC"] = pd.to_datetime(simstat["StartUTC"])
    simstat["EndUTC"] = pd.to_datetime(simstat["EndUTC"])
    simstat["PlannedDownTime"] = simstat["PlannedDownTime"].apply(parse_iso_format_string)
    simstat["UtilizationTime"] = simstat["UtilizationTime"].apply(parse_iso_format_string)
    #simstat["PlannedDownTime"] = pd.to_timedelta(simstat["PlannedDownTime"])
    #simstat["UtilizationTime"] = pd.to_timedelta(simstat["UtilizationTime"])
    return simstat

def plot_simstat(simstat):
    s = simstat.copy()
    s["Task"] = s["StationGroup"] + s["StationNum"].apply(str)
    s["Start"] = s["StartUTC"]
    s["Finish"] = s["EndUTC"]
    return ff.create_gantt(s, index_col="StationGroup", group_tasks=True)

def simprod(results):
    simprod = pd.DataFrame()
    for j in results["Jobs"]:
        procCntr = 1
        for proc in j["ProcsAndPaths"]:
            pathCntr = 0
            for path in proc["paths"]:
                p = pd.DataFrame(path["SimulatedProduction"])
                p["Part"] = j["PartName"]
                p["Process"] = procCntr
                p["Path"] = pathCntr
                simprod = simprod.append(p)
                pathCntr += 1
            procCntr += 1
    return simprod

def plot_simprod(results):
    plots = []
    for j in results["Jobs"]:
        procCntr = 1
        for proc in j["ProcsAndPaths"]:
            pathCntr = 0
            for path in proc["paths"]:
                p = pd.DataFrame(path["SimulatedProduction"])
                plots.append(go.Scatter(
                    x=p["TimeUTC"],
                    y=p["Quantity"],
                    name=j["PartName"] + " " + str(procCntr) + ":" + str(pathCntr)))
                pathCntr += 1
            procCntr += 1
    return plots

def create_scenario(results, bookings, path, prev_parts=[], downtimes=[]):
    if not os.path.exists(path):
        os.mkdir(path)
    bookings_json = encode_bookings(bookings,prev_parts)
    with open(os.path.join(path, "bookings.json"), "w") as f:
        f.write(bookings_json)
    with open(os.path.join(path, "results.json"), "w") as f:
        f.write(results["OriginalJson"])
    if len(downtimes) > 0:
        with open(os.path.join(path, "downtimes.json"), "w") as f:
            f.write(json.dumps(downtimes))
