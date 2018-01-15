import re
import pandas as pd
import subprocess
import json
import os

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

def allocate(bookings, flex_file, plugin, allocatecli, prev_parts=[]):
    bookings_json = encode_bookings(bookings, prev_parts)
    proc = subprocess.run(args=["dotnet", "run", "-p", allocatecli, "--",
                                "-f", flex_file, "-p", plugin],
                          input=bookings_json,
                          encoding="utf-8",
                          stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE)
    if proc.stderr != "":
        print(proc.stderr)
    if proc.returncode != 0:
        raise Exception()
    results_json = proc.stdout
    results = json.loads(results_json)
    results["OriginalJson"] = results_json

    # Convert the results to pandas frames
    simstat = pd.DataFrame(results["SimStations"])
    simstat["StartUTC"] = pd.to_datetime(simstat["StartUTC"])
    simstat["EndUTC"] = pd.to_datetime(simstat["EndUTC"])
    simstat["PlannedDownTime"] = simstat["PlannedDownTime"].apply(parse_iso_format_string)
    simstat["UtilizationTime"] = simstat["UtilizationTime"].apply(parse_iso_format_string)
    return results, simstat

def create_scenario(results, bookings, path, prev_parts=[]):
    if not os.path.exists(path):
        os.mkdir(path)
    bookings_json = encode_bookings(bookings,prev_parts)
    with open(os.path.join(path, "bookings.json"), "w") as f:
        f.write(bookings_json)
    with open(os.path.join(path, "results.json"), "w") as f:
        f.write(results["OriginalJson"])