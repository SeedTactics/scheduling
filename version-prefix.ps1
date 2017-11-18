$currenttag = hg id -t -r '.^'
If ($currenttag) {
  $currenttag
} else {
  $lasttag = hg id -t -r 'ancestors(.) and tag()'
  $parts = $lasttag.Split(".")
  $patch = [convert]::ToInt32($parts[2], 10)
  $parts[0] + "." + $parts[1] + "." + (($patch + 1) -as [string])
}