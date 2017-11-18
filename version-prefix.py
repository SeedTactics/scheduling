import subprocess

curtag = subprocess.check_output("hg id -t -r '.^'", shell=True)
if curtag == "":
    print (curtag)
else:
    lasttag = subprocess.check_output("hg id -t -r 'ancestors(.) and tag()'", shell=True)
    parts = lasttag.decode('utf-8').rstrip().split(".")
    patch = int(parts[2]) + 1
    print (parts[0] + "." + parts[1] + "." + str(patch))
