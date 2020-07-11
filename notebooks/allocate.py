import re
import pandas as pd
import subprocess
import json
import urllib
import os
import matplotlib.pyplot as plt

# Compied and modified from https://github.com/pandas-dev/pandas/pull/19065
# The merged code doesn't allow optional days
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

def encode_bookings(bookings):
    blst = []
    for b in bookings.groupby("BookingId"):
        blst.append({"BookingId":b[0],
                     "DueDate":b[1]["DueDate"].iloc[0].isoformat(),
                     "Priority":b[1]["Priority"].iloc[0].item(),
                     "Parts": [{"BookingId":b[0], "Part":p["Part"], "Quantity":p["Quantity"]}
                               for _,p in b[1].iterrows()]})
    return blst

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
                    stops.append(",".join([s["StationGroup"] + str(n) for n in s["StationNums"]]))
                print("            Stops {}".format("->".join(stops)))
                print("            Unload {}".format(",".join([str(u) for u in path["Unload"]])))
                pathCntr += 1
            procCntr += 1

def allocate(bookings, flex_file, plugin, prev_parts=[], downtimes=[], start_utc=None, end_utc=None, schid=None):
    with open(flex_file) as f:
        flexplan = json.load(f)

    request = {
        "ScheduleId": schid,
        "StartUTC": start_utc.isoformat() if start_utc else "2016-11-05T01:00:00Z",
        "EndUTC": end_utc.isoformat() if end_utc else "2016-11-06T01:00:00Z",
        "UnscheduledBookings": encode_bookings(bookings),
        "ScheduledParts": prev_parts,
        "FlexPlan": flexplan,
        "FillMethod": "FillInAnyOrder",
        "Downtimes": downtimes
    }

    proc = subprocess.run(
        ["dotnet", "run", "--framework", "netcoreapp3.1", "-p", plugin],
        input=json.dumps(request),
        text=True,
        stdout=subprocess.PIPE,
    )

    if proc.stderr != "":
        print(proc.stderr)
    if proc.returncode != 0:
        raise Exception()

    results_json = proc.stdout
    results = json.loads(results_json)
    results["NewJobs"] = {
        "ScheduleId": results["Jobs"][0]["ScheduleId"],
        "Jobs": results["Jobs"],
        "StationUse": results["SimStations"],
        "ExtraParts": results["NewExtraParts"],
        "CurrentUnfilledWorkorders": [],
        "QueueSizes": results["QueueSizes"],
        "ArchiveCompletedJobs": True,
        "CurrentUnfilledWorkorders": encode_bookings_as_workorders(bookings)
    }

    simstat = pd.DataFrame(results["SimStations"])
    simstat["StartUTC"] = pd.to_datetime(simstat["StartUTC"])
    simstat["EndUTC"] = pd.to_datetime(simstat["EndUTC"])
    simstat["PlannedDownTime"] = simstat["PlannedDownTime"].apply(parse_iso_format_string)
    simstat["UtilizationTime"] = simstat["UtilizationTime"].apply(parse_iso_format_string)

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

    return results, simstat, simprod

def download(results, computer):
    req = urllib.request.Request(url="http://" + computer + "/api/v1/jobs/add",
                                 data=json.dumps(results["NewJobs"]).encode('utf-8'),
                                 headers={'content-type': 'application/json'},
                                 method='POST')
    urllib.request.urlopen(req)

def plot_simstat(simstat):
    plt.figure()
    i = 0
    start = simstat["StartUTC"].min()
    cmap = plt.cm.get_cmap("hsv", 10)
    stations = []
    for stat, group in simstat.groupby(["StationGroup", "StationNum"]):
        x = [
            ((s - start).total_seconds() / 60, (e - s).total_seconds() / 60)
            for (s, e) in zip(group["StartUTC"], group["EndUTC"])
        ]
        plt.broken_barh(xranges=x, yrange=(i * 10, 5), label=stat, color=cmap(i))
        stations.append(stat)
        i += 1
    plt.gca().legend(ncol=len(stat), bbox_to_anchor=(0, 1), loc="lower left")
    plt.show()

def plot_simprod(results):
    plt.figure()
    ax = plt.gca()
    for j in results["Jobs"]:
        procCntr = 1
        for proc in j["ProcsAndPaths"]:
            pathCntr = 0
            for path in proc["paths"]:
                p = pd.DataFrame(path["SimulatedProduction"])
                p["TimeUTC"] = pd.to_datetime(p["TimeUTC"])
                p.plot(
                    ax=ax,
                    x="TimeUTC",
                    y="Quantity",
                    label=j["PartName"] + " " + str(procCntr) + ":" + str(pathCntr),
                )
                pathCntr += 1
            procCntr += 1
    plt.show()

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
