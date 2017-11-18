import subprocess

# Script similar to GitVersion, but simplier and focused on our use case.

curtag = subprocess.check_output(["hg", "id", "-t", "-r", ".^"])
if curtag.strip() != "":
    print (curtag.strip().decode("utf-8"))
else:
    lasttag = subprocess.check_output(["hg", "id", "-t", "-r", "ancestors(.) and tag()"])
    parts = lasttag.decode('utf-8').rstrip().split(".")
    patch = int(parts[2]) + 1
    print (parts[0] + "." + parts[1] + "." + str(patch))
