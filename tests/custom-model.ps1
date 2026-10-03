param($Context)
# Standard tier: server arranges the actor; all dispense attempts use native E input.
function Machine-State {
    $reply=(Invoke-LabServer '/scp294fixture state') -join "`n"
    if ($reply -notmatch 'MODEL_STATE (\{[^\r\n]+\})') { throw "Missing model state: $reply" }
    return $Matches[1] | ConvertFrom-Json
}
function Fixture($verb, $id) {
    $reply=(Invoke-LabServer "/scp294fixture $verb $id") -join "`n"
    if ($reply -notmatch "FIXTURE_OK $verb") { throw "Fixture setup failed: $reply" }
}
$null=Invoke-LabServer '/forcestart'
$deadline=(Get-Date).AddSeconds(20)
do { Start-Sleep -Milliseconds 500; $roundReply=(Invoke-LabServer '/roundtime') -join "`n" }
until ($roundReply -match 'Round time:' -or (Get-Date) -gt $deadline)
if ($roundReply -notmatch 'Round time:') { throw 'Round did not start' }
$null=Invoke-LabServer '/scp294 spawn'
$machineState=Machine-State
if ($machineState.roots -ne 1 -or $machineState.toys -ne 176) { throw 'Expected one complete static model' }
$actorId=@(Observe -ClientsOnly)[0].id
$null=Invoke-LabServer "/labplacecheck $($machineState.x) $($machineState.baseY + 1.04) $($machineState.z - 1.3) Tutorial"
Fixture 'place' $actorId
$null=Invoke-LabInput @{id='model-settle';frames=90}
$actor=@(Observe -ClientsOnly)[0]
if (!$actor.ready -or $actor.role -ne 'Tutorial' -or !$actor.godMode) { throw 'Actor arrangement incomplete' }
$null=Invoke-LabSetup 'graphics-max'
Fixture 'view' $actorId
$null=Invoke-LabInput @{id='model-front-settle';frames=60}
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-full-front'
$null=Invoke-LabInput @{id='model-full-front';frames=180;capture=$true}
Fixture 'angle' $actorId
$null=Invoke-LabInput @{id='model-angle-settle';frames=60}
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-three-quarter'
$null=Invoke-LabInput @{id='model-three-quarter';frames=180;capture=$true}
Fixture 'place' $actorId
$null=Invoke-LabInput @{id='model-interaction-settle';frames=60}
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-front'
Fixture 'fill' $actorId
$null=Invoke-LabInput @{id='model-inventory-full';frames=90;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 8) { throw 'Full inventory changed' }
Fixture 'empty' $actorId
$null=Invoke-LabInput @{id='model-cancel';frames=90;inputFrames=2;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 0) { throw 'Cancelled search dispensed a bottle' }
$null=Invoke-LabInput @{id='model-dispense';frames=90;keys=@(101);capture=$true}
$after=@(Observe -ClientsOnly)[0]
if (@($after.items).Count -ne 1 -or $after.items[0] -notmatch 'SCP207') { throw "Native hold did not dispense exactly one SCP207: $($after | ConvertTo-Json -Compress -Depth 6)" }
$null=Invoke-LabInput @{id='model-quota-reject';frames=90;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 1) { throw 'Second hold bypassed per-life quota' }
$null=Invoke-LabScreenshot -Name 'scp294-quota'
$null=Invoke-LabServer '/scp294 clear'
$cleared=Machine-State
if ($cleared.roots -ne 0 -or $cleared.survivingToys -ne 0) { throw 'Machine toys survived clear' }
$null=Invoke-LabScreenshot -Name 'scp294-cleared'
$null=Invoke-LabServer '/scp294 spawn'
$replacement=Machine-State
if ($replacement.roots -ne 1 -or $replacement.toys -ne $machineState.toys -or [Math]::Abs($replacement.baseY - $machineState.baseY) -gt 0.02) { throw 'Replacement model count or floor placement changed' }
$null=Invoke-LabInput @{id='model-respawn-quota';frames=90;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 1) { throw 'Machine replacement reset quota' }
$null=Invoke-LabServer '/scp294 clear'
$cleared=Machine-State
if ($cleared.roots -ne 0 -or $cleared.survivingToys -ne 0) { throw 'Final cleanup failed' }
