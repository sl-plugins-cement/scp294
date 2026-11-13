param($Context)
# Two real clients: one in audible range, one at a native LCZ spawn.
function Audio-State {
    $reply=(Invoke-LabServer '/scp294fixture audio') -join "`n"
    if ($reply -notmatch 'AUDIO_STATE (\{[^\r\n]+\})') { throw "Missing audio state: $reply" }
    $state=$Matches[1] | ConvertFrom-Json
    $state | ConvertTo-Json -Compress | Add-Content "$($Context.Evidence)/cabinet-audio.jsonl"
    return $state
}
function Fixture($verb, $id) {
    $reply=(Invoke-LabServer "/scp294fixture $verb $id") -join "`n"
    if ($reply -notmatch "FIXTURE_OK $verb") { throw "Fixture failed: $reply" }
}
function Expect-Listeners($expected) {
    $state=Audio-State
    if (($state.eligible -join ',') -ne ($expected -join ',') -or $state.listeners -ne @($expected).Count) {
        throw "Unexpected listeners: $($state | ConvertTo-Json -Compress); expected $expected"
    }
    return $state
}
if (@($Context.Actors).Count -ne 2) { throw 'Run with --clients 2 --audio --bandwidth' }
$near=$Context.Actors[0].id; $far=$Context.Actors[1].id
$null=Invoke-LabServer '/forcestart'
$deadline=(Get-Date).AddSeconds(20)
do { Start-Sleep -Milliseconds 500; $round=(Invoke-LabServer '/roundtime') -join "`n" }
until ($round -match 'Round time:' -or (Get-Date) -gt $deadline)
if ($round -notmatch 'Round time:') { throw 'Round did not start' }
$null=Invoke-LabServer @('/decontamination disable','/wave forcepause NTF true','/wave forcepause Chaos true','/scp294 spawn')
$reply=(Invoke-LabServer '/scp294fixture state') -join "`n"
if ($reply -notmatch 'MODEL_STATE (\{[^\r\n]+\})') { throw $reply }
$machine=$Matches[1] | ConvertFrom-Json
$machine | ConvertTo-Json | Set-Content "$($Context.Evidence)/cabinet-model.json"
if ($Context.Options.productionPlacement -and
    ([Math]::Abs($machine.baseX - 63) -gt 0.01 -or [Math]::Abs($machine.baseZ + 51) -gt 0.01 -or
     [Math]::Abs($machine.yaw - 180) -gt 0.01)) { throw 'Production cabinet placement was not reproduced' }
# Use the actual custom model transform, including its rotation, for the approach.
$reply=(Invoke-LabServer "/labplacecheck $($machine.approachX) $($machine.approachY) $($machine.approachZ) Tutorial") -join "`n"
$reply | Set-Content "$($Context.Evidence)/cabinet-placement.txt"
if ($reply -notmatch 'OFFLINE_LAB_PLACEMENT (\{[^\r\n]+\})') { throw "Placement check missing: $reply" }
$placement=$Matches[1] | ConvertFrom-Json
if (@($placement.warnings).Count) { throw "Cabinet approach has placement warnings: $reply" }
Fixture 'place' $near
Fixture 'audio-far' $far
$null=Invoke-LabInput @{id='cabinet-settle';frames=120}
$null=Set-LabAim -Target @{x=$machine.x;y=$machine.y;z=$machine.z}
$startingVolume=if ($Context.Options.productionPlacement) { 0.6 } else { 1 }
$before=Expect-Listeners @($near)
if ([Math]::Abs($before.volume - $startingVolume) -gt 0.01) { throw 'Starting cabinet volume differs from config' }
if ($Context.Options.productionPlacement) {
    $null=Invoke-LabInput @{id='cabinet-saved-volume';frames=300;capture=$true;audio=$true;expectAudio=$true}
    $null=Invoke-LabServer '/scp294fixture audio-volume 1'
    $null=Invoke-LabInput @{id='cabinet-volume-settle';frames=60}
}
$before=Expect-Listeners @($near)
if ([Math]::Abs($before.localX) -gt 0.01 -or [Math]::Abs($before.localY - 1.1) -gt 0.01 -or
    [Math]::Abs($before.localZ + 0.4) -gt 0.01 -or [Math]::Abs($before.volume - 1) -gt 0.01 -or
    [Math]::Abs($before.minDistance - 2.5) -gt 0.01) { throw "Speaker alignment/level incorrect: $($before | ConvertTo-Json -Compress)" }
$null=Invoke-LabInput @{id='cabinet-near';frames=600;capture=$true;audio=$true;expectAudio=$true}
$after=Expect-Listeners @($near)
$seconds=($after.packets-$before.packets)*0.06
if ($seconds -lt 8 -or ($after.bytes-$before.bytes) -ne 120*($after.packets-$before.packets)) { throw 'Missing 16 kb/s native packet stream' }
$null=Invoke-LabScreenshot -Name 'cabinet-music-near'
# Back out of production's narrow alcove; the default location opens sideways.
$leaveKey=if ($Context.Options.productionPlacement) { 115 } else { 100 }
$null=Invoke-LabInput @{id='cabinet-leave-range';frames=600;keys=@($leaveKey);capture=$true;audio=$true}
Observe -ClientsOnly | ConvertTo-Json -Depth 8 | Set-Content "$($Context.Evidence)/cabinet-range-actors.json"
$before=Expect-Listeners @()
$null=Invoke-LabInput @{id='cabinet-out-of-range';frames=300;capture=$true;audio=$true}
$after=Expect-Listeners @()
if ($after.packets -ne $before.packets) { throw 'Audio packets sent with no eligible listeners' }
Fixture 'near' $near
$null=Invoke-LabInput @{id='cabinet-return';frames=300;capture=$true;audio=$true;expectAudio=$true}
$null=Expect-Listeners @($near)
# Clear/replacement must leave exactly one source and no old stream.
$null=Invoke-LabServer '/scp294 spawn'
$null=Invoke-LabInput @{id='cabinet-replaced';frames=240;capture=$true;audio=$true;expectAudio=$true}
$state=Expect-Listeners @($near)
if ($state.speakers -ne 1) { throw 'Replacement leaked a speaker' }
$null=Invoke-LabServer '/scp294 clear'
$null=Invoke-LabInput @{id='cabinet-cleared';frames=180;capture=$true;audio=$true}
$state=Audio-State
if ($state.active -or $state.speakers -ne 0) { throw 'Clear leaked music' }
# Unselected spectators receive no audio.
$null=Invoke-LabServer @("/forcerole $far Spectator",'/scp294 spawn')
$null=Invoke-LabInput @{id='cabinet-unselected-spectator';frames=120}
$null=Expect-Listeners @($near)
# Select the nearby client through the native spectator UI.
$controls=@(Get-LabUi -ClientId $Context.ClientIds[1])
$target=@($controls | Where-Object { $_.type -eq 'Button' -and $_.text -like '*OfflineLab-1*' } | Select-Object -First 1)
if (!$target.Count) { $controls | ConvertTo-Json -Depth 10 | Set-Content "$($Context.Evidence)/spectator-ui.json"; throw 'Nearby actor missing from spectator UI' }
$rect=$target[0].rect
$null=Invoke-LabInput @{id='cabinet-select-spectator';frames=120;keys=@(323);inputFrames=4;cursorX=(($rect.x0+$rect.x1)/2);cursorY=(($rect.y0+$rect.y1)/2)} -ClientId $Context.ClientIds[1]
$null=Invoke-LabInput @{id='cabinet-spectator-music';frames=600;capture=$true;audio=$true;expectAudio=$true} -ClientId $Context.ClientIds[1]
$state=Expect-Listeners @($near,$far)
$null=Invoke-LabScreenshot -Name 'cabinet-music-spectator' -ClientId $Context.ClientIds[1]
Fixture 'audio-far' $near
$null=Invoke-LabInput @{id='cabinet-spectator-far';frames=180;capture=$true;audio=$true} -ClientId $Context.ClientIds[1]
$before=Expect-Listeners @()
$null=Invoke-LabInput @{id='cabinet-no-listeners';frames=180}
$after=Expect-Listeners @()
if ($after.packets -ne $before.packets) { throw 'Spectator received out-of-range music' }
$null=Invoke-LabServer '/scp294 clear'
$state=Audio-State
if ($state.active -or $state.speakers -ne 0) { throw 'Final cleanup failed' }
