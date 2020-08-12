#!/usr/bin/python
import json
import argparse
import datetime
import pathlib
import subprocess
import os
import sys
import urllib.request
from typing import Dict, List

parser = argparse.ArgumentParser(description="Allocate and Download")
parser.add_argument("-f", "--flex", dest="flexplan", type=str, required=True)
parser.add_argument("-p", "--plugin", dest="plugin", type=str, required=True)
parser.add_argument("-d", "--download", dest="download", type=str)
parser.add_argument("--new-jobs", dest="newjobs", action="store_true")
parser.add_argument("-v", "--verbose", dest="verbose", action="store_true")
parser.add_argument("--program-dir", dest="programs", type=str)
parser.add_argument(
    "--start",
    dest="startTime",
    type=lambda d: datetime.datetime.strptime(d, "%Y%m%d %H%M%S"),
)
parser.add_argument(
    "--end",
    dest="endTime",
    type=lambda d: datetime.datetime.strptime(d, "%Y%m%d %H%M%S"),
)
parser.add_argument("parts", nargs="+", type=str)
args = parser.parse_args()

with open(args.flexplan) as f:
    flexplan = json.load(f)

request = {
    "ScheduleId": None,
    "StartUTC": args.startTime.isoformat()
    if args.startTime
    else "2016-11-05T01:00:00Z",
    "EndUTC": args.endTime.isoformat() if args.endTime else "2016-11-06T01:00:00Z",
    "UnscheduledBookings": [],
    "ScheduledParts": [],
    "FlexPlan": flexplan,
    "FillMethod": "FillInAnyOrder",
}

mainprogs: Dict[str, List] = {}
progentries = []
if args.programs:
    for f in os.listdir(args.programs):
        p = pathlib.Path(os.path.join(args.programs, f))
        with open(p) as f:
            lines = f.read().splitlines()
        [part, proc, mc] = lines[0].split(",")
        if part in mainprogs:
            mainprogs[part].append(
                {"ProcessNumber": proc, "MachineGroup": mc, "ProgramName": p.stem}
            )
        else:
            mainprogs[part] = [
                {"ProcessNumber": proc, "MachineGroup": mc, "ProgramName": p.stem}
            ]
        progentries.append(
            {
                "ProgramName": p.stem,
                "Revision": -1,
                "Comment": "Comment for " + p.stem,
                "ProgramContent": "\n".join(lines[1:]),
            }
        )

for idx, partStr in enumerate(args.parts):
    if "," in partStr:
        [part, qty] = partStr.split(",")
    else:
        part = partStr
        qty = 5
    today = datetime.date.today().isoformat()
    bookingDemand = {
        "BookingId": "booking" + str(idx),
        "Part": part,
        "Quantity": int(qty),
    }
    if part in mainprogs:
        bookingDemand["Programs"] = mainprogs[part]
    request["UnscheduledBookings"].append(
        {
            "BookingId": "booking" + str(idx),
            "DueDate": today,
            "Priority": 100,
            "Parts": [bookingDemand],
        }
    )

if args.verbose:
    print(json.dumps(request, indent=2))
    print("")
    print("----------------")
    print("")

proc = subprocess.run(
    ["dotnet", "run", "--framework", "netcoreapp3.1", "-p", args.plugin],
    input=json.dumps(request),
    text=True,
    stdout=subprocess.PIPE,
)

if proc.returncode != 0:
    sys.exit(-1)

response = json.loads(proc.stdout)

newjobs = {
    "ScheduleId": response["Jobs"][0]["ScheduleId"],
    "Jobs": response["Jobs"],
    "StationUse": response["SimStations"],
    "ExtraParts": response["NewExtraParts"],
    "CurrentUnfilledWorkorders": [],
    "QueueSizes": response["QueueSizes"],
    "Programs": progentries,
    "ArchiveCompletedJobs": True,
}


if args.download:
    if args.verbose:
        print(json.dumps(response, indent=2))
    req = urllib.request.Request(
        args.download + "/api/v1/jobs/add?expectedPreviousScheduleId=",
        json.dumps(newjobs).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    print(urllib.request.urlopen(req).read().decode())
elif args.newjobs:
    print(json.dumps(newjobs, indent=2))
else:
    print(json.dumps(response, indent=2))
