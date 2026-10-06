param($Context)
# Standard tier: setup spawns roles, places the machine and teleports the client beside it.
# Every dispense attempt is the native E key held by the real client.
$ErrorActionPreference = 'Stop'
function Fixture($verb, $id) {
    $reply = (Invoke-LabServer "/scp294fixture $verb $id") -join "`n"
    if ($reply -notmatch "FIXTURE_OK $verb") { throw "Fixture setup failed: $reply" }
}
function Force-State($id) {
    $reply = (Invoke-LabServer "/scp294fixture force $id") -join "`n"
    if ($reply -notmatch 'FORCE_STATE (\{[^\r\n]+\})') { throw "Missing force state: $reply" }
    return $Matches[1] | ConvertFrom-Json
}
function Items { return @(@(Observe -ClientsOnly)[0].items) }
function Log($text) { $text | Out-File "$($Context.Evidence)\tau9-block-steps.txt" -Append -Encoding utf8 }

$null = Invoke-LabServer '/forcestart'
$deadline = (Get-Date).AddSeconds(20)
do { Start-Sleep -Milliseconds 500; $roundReply = (Invoke-LabServer '/roundtime') -join "`n" }
until ($roundReply -match 'Round time:' -or (Get-Date) -gt $deadline)
if ($roundReply -notmatch 'Round time:') { throw 'Round did not start' }
foreach ($command in '/wave forcepause NTF true', '/wave forcepause Chaos true', '/decontamination disable', '/reinforcements auto off') {
    Log "$command => $((Invoke-LabServer $command) -join ' | ')"
}
$null = Invoke-LabServer '/scp294 spawn'
$machineReply = (Invoke-LabServer '/scp294fixture state') -join "`n"
if ($machineReply -notmatch 'MODEL_STATE (\{[^\r\n]+\})') { throw "Model inspection unavailable: $machineReply" }
$machineState = $Matches[1] | ConvertFrom-Json
if ($machineState.roots -ne 1) { throw "Expected one machine: $machineReply" }
Log "machine $machineReply"
$actorId = @(Observe -ClientsOnly)[0].id

# Step 1, control: an ordinary Class-D completes the native search and receives a bottle.
$null = Invoke-LabServer "/forcerole $actorId ClassD"
$null = Invoke-LabServer "/god $actorId enable"
Fixture 'empty' $actorId
Fixture 'near' $actorId
$null = Invoke-LabInput @{id='control-settle';frames=90}
$control = @(Observe -ClientsOnly)[0]
if (!$control.ready -or $control.role -ne 'ClassD') { throw "Control actor is not a ready Class-D: $($control | ConvertTo-Json -Compress)" }
$state = Force-State $actorId
Log "control force state $($state | ConvertTo-Json -Compress)"
if ($state.force -ne '') { throw "Control actor unexpectedly reports force '$($state.force)'" }
$null = Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null = Invoke-LabScreenshot -Name 'control-before'
$null = Invoke-LabInput @{id='control-dispense';frames=300;inputFrames=150;keys=@(101);capture=$true}
$items = Items
Log "control items $($items -join ',')"
if ($items.Count -ne 1 -or $items[0] -notmatch 'SCP207') { throw "Control did not receive one SCP-207 bottle: $($items -join ',')" }
$null = Invoke-LabScreenshot -Name 'control-after'

# Step 2: the same client becomes a Tau-9 member on a new life through the real RS force spawn.
$null = Invoke-LabServer "/forcerole $actorId Spectator"
Start-Sleep -Seconds 2
$spawnReply = (Invoke-LabServer '/reinforcements spawn tau9') -join ' | '
Log "spawn => $spawnReply"
if ($spawnReply -notmatch 'selected 1') { throw "Tau-9 force spawn failed: $spawnReply" }
$deadline = (Get-Date).AddSeconds(150)
$ready = $false
do {
    Start-Sleep -Seconds 3
    $state = Force-State $actorId
    $actor = @(Observe -ClientsOnly)[0]
    $pace = (Invoke-LabServer '/reinforcements status pace') -join ' '
    if ($state.force -eq 'tau9' -and $pace -match 'live_cinematic_toys=0' -and $actor.position.y -lt 500) { $ready = $true }
} until ($ready -or (Get-Date) -gt $deadline)
Log "tau9 state $($state | ConvertTo-Json -Compress) pace=$pace y=$($actor.position.y)"
if (!$ready) { throw "Client never became a settled Tau-9 member: $($state | ConvertTo-Json -Compress) $pace" }
Start-Sleep -Seconds 5
$null = Invoke-LabServer "/god $actorId enable"
Fixture 'empty' $actorId
Fixture 'near' $actorId
$null = Invoke-LabInput @{id='tau9-settle';frames=90}
$tau = @(Observe -ClientsOnly)[0]
$state = Force-State $actorId
Log "tau9 pre-interaction $($state | ConvertTo-Json -Compress) role=$($tau.role) items=$(@($tau.items) -join ',') pos=$($tau.position | ConvertTo-Json -Compress)"
if ($state.force -ne 'tau9' -or !$tau.ready -or $tau.life -eq $control.life) { throw 'Client is not a ready Tau-9 member on a new life' }
if (@($tau.items).Count -ne 0) { throw "Inventory not empty: $(@($tau.items) -join ',')" }
$null = Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null = Invoke-LabScreenshot -Name 'tau9-before'
$null = Invoke-LabInput @{id='tau9-refusal';frames=300;inputFrames=150;keys=@(101);capture=$true}
$after = @(Observe -ClientsOnly)[0]
Log "tau9 after items $(@($after.items) -join ',') life=$($after.life)"
if (@($after.items).Count -ne 0) { throw "Tau-9 member received an item: $(@($after.items) -join ',')" }
$null = Invoke-LabScreenshot -Name 'tau9-after'
$null = Invoke-LabServer '/scp294 clear'
