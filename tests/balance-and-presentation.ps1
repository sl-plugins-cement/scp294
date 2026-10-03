param($Context)
# Standard tier: native RA buff grants exercise the public grant path; client input verifies rendering and movement.
function Read-Effects {
    $reply=(Invoke-LabServer "/scp294fixture effects $actorId") -join "`n"
    if ($reply -notmatch 'EFFECT_STATE (\{[^\r\n]+\})') { throw "Missing native effect state: $reply" }
    return $Matches[1] | ConvertFrom-Json
}
function Wait-Effects($predicate, $seconds, $label) {
    $deadline=(Get-Date).AddSeconds($seconds)
    do {
        $state=Read-Effects
        if (& $predicate $state) { return $state }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $label"
}
function Arrange($verb) {
    $reply=(Invoke-LabServer "/scp294fixture $verb $actorId") -join "`n"
    if ($reply -notmatch "FIXTURE_OK $verb") { throw "Arrangement failed: $reply" }
}
$null=Invoke-LabServer '/forcestart'
$deadline=(Get-Date).AddSeconds(20)
do { Start-Sleep -Milliseconds 500; $roundReply=(Invoke-LabServer '/roundtime') -join "`n" }
until ($roundReply -match 'Round time:' -or (Get-Date) -gt $deadline)
if ($roundReply -notmatch 'Round time:') { throw 'Round did not start' }
$null=Invoke-LabServer '/scp294 spawn'
$machineReply=(Invoke-LabServer '/scp294fixture state') -join "`n"
if ($machineReply -notmatch 'MODEL_STATE (\{[^\r\n]+\})') { throw 'Model inspection unavailable' }
$machineState=$Matches[1] | ConvertFrom-Json
$actorId=@(Observe -ClientsOnly)[0].id
Arrange 'place'
$null=Invoke-LabInput @{id='balance-settle';frames=90}
$actor=@(Observe -ClientsOnly)[0]
if (!$actor.ready -or $actor.role -ne 'Tutorial' -or !$actor.godMode) { throw 'Expected ready Tutorial fixture' }
$baseline=Read-Effects
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-label-close'
$null=Invoke-LabServer "/scp294 buff give $actorId coffee 8"
$coffee=Read-Effects
if ($coffee.movement -ne 10 -or $coffee.maxHealth -ne $baseline.maxHealth) { throw 'Coffee must give only the mild speed modifier' }
$before=@(Observe -ClientsOnly)[0]
$null=Invoke-LabInput @{id='coffee-movement';frames=90;keys=@(115);capture=$true}
$after=@(Observe -ClientsOnly)[0]
$distance=[Math]::Sqrt([Math]::Pow($after.position.x-$before.position.x,2)+[Math]::Pow($after.position.z-$before.position.z,2))
if ($after.life -ne $before.life -or $distance -lt 0.2) { throw 'Native movement under coffee was not observed' }
$null=Wait-Effects { param($s) $s.movement -eq $baseline.movement } 15 'coffee expiry'
$null=Invoke-LabServer "/scp294 buff give $actorId ahead 8"
$ahead=Read-Effects
if ($ahead.movement -ne 20 -or $ahead.maxHealth -ne $baseline.maxHealth) { throw 'Ahead positive phase differs from tuned values' }
$null=Invoke-LabInput @{id='ahead-positive';frames=90;capture=$true}
$backlash=Wait-Effects { param($s) $s.slowness -eq 20 } 15 'Ahead backlash'
if ([Math]::Abs($backlash.maxHealth-$baseline.maxHealth*0.8) -gt 0.01) { throw 'Ahead backlash HP cap differs from tuned value' }
$null=Invoke-LabInput @{id='ahead-backlash';frames=480;capture=$true}
$expired=Wait-Effects { param($s) $s.slowness -eq $baseline.slowness -and $s.movement -eq $baseline.movement -and $s.maxHealth -eq $baseline.maxHealth } 15 'Ahead restoration'
if ($expired.health -gt $backlash.health+0.01) { throw 'Backlash expiry healed damage' }
Arrange 'view'
$null=Invoke-LabInput @{id='model-wide-settle';frames=90}
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-wide'
$null=Invoke-LabInput @{id='model-wide';frames=120;capture=$true}
$null=Invoke-LabServer '/scp294 clear'
