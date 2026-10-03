param($Context)
# Standard tier: public commands configure/grant buffs; native E and Mouse0/Mouse1 acquire and consume bottles.
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
function Grant($drink, $options='') {
    $reply=(Invoke-LabServer "/scp294 buff give $actorId $drink $options") -join "`n"
    if ($reply -match 'Unknown|未知|持续秒数|不接受|背包已满') { throw "Grant failed: $reply" }
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
if ($machineState.roots -ne 1 -or $machineState.toys -ne 207 -or [Math]::Abs($machineState.x-60) -gt 0.01) { throw 'Configured x=60 placement was not applied' }
$actorId=@(Observe -ClientsOnly)[0].id
$null=Invoke-LabServer "/labplacecheck $($machineState.x) $($machineState.baseY+1.04) $($machineState.z-1.65) Tutorial"
Arrange 'place'
$null=Invoke-LabInput @{id='config-settle';frames=90}
$actor=@(Observe -ClientsOnly)[0]
if (!$actor.ready -or $actor.role -ne 'Tutorial' -or !$actor.godMode) { throw 'Expected ready Tutorial fixture' }
$baseline=Read-Effects
$null=Set-LabAim -Target @{x=$machineState.x;y=$machineState.y;z=$machineState.z}
$null=Invoke-LabScreenshot -Name 'scp294-configured-position'
Arrange 'fill'
$null=Invoke-LabInput @{id='config-full-inventory';frames=90;keys=@(101);capture=$true}
$fullGrant=(Invoke-LabServer "/scp294 buff give $actorId scp207") -join "`n"
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 8 -or $fullGrant -notmatch '背包已满') { throw 'Full inventory changed or native grant failed to report rejection' }
Arrange 'empty'
# Cancellation must not consume quota; a 100% native config must give an unmodified bottle.
$null=Invoke-LabInput @{id='config-search-cancel';frames=90;inputFrames=2;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 0) { throw 'Cancelled search dispensed a bottle' }
$null=Invoke-LabInput @{id='config-native-dispense';frames=90;keys=@(101);capture=$true}
$dispensed=@(Observe -ClientsOnly)[0]
if (@($dispensed.items).Count -ne 1 -or $dispensed.items[0] -notmatch 'SCP207') { throw 'Native bottle was not dispensed' }
$null=Invoke-LabInventory -Slot 1 -Expect SCP207
Arrange 'injure'
# Begin then cancel before activation; the same native bottle must remain with no effect/healing.
$null=Invoke-LabInput @{id='native207-use-start';frames=2;keys=@(323)}
$null=Invoke-LabInput @{id='native207-use-cancel';frames=90;inputFrames=2;keys=@(324);capture=$true}
$cancelled=Read-Effects
if ($cancelled.scp207 -ne 0 -or $cancelled.health -ne 50 -or @(@(Observe -ClientsOnly)[0].items).Count -ne 1) { throw 'Native use cancellation consumed or activated the bottle' }
$null=Invoke-LabInput @{id='native207-drink';frames=480;inputFrames=2;keys=@(323);capture=$true;audio=$true;expectAudio=$true}
$first=Wait-Effects { param($s) $s.scp207 -eq 1 } 15 'first native SCP-207 stack'
if ($first.health -ne 80 -or $first.movement -ne 0 -or $first.scp207Duration -ne 0 -or @(@(Observe -ClientsOnly)[0].items).Count -ne 0) { throw 'Native bottle must heal 30, consume once, and have no custom speed/expiry' }
$null=Invoke-LabInput @{id='config-quota-after-drink';frames=90;keys=@(101);capture=$true}
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 0) { throw 'Drinking reset the machine quota' }
$status=(Invoke-LabServer "/scp294 buff status $actorId") -join "`n"
if ($status -notmatch '没有饮料 Buff') { throw 'Normal SCP-207 occupied a custom buff slot' }
Grant 'scp207'
if (@(@(Observe -ClientsOnly)[0].items).Count -ne 1) { throw 'Native admin grant did not give a bottle' }
$null=Invoke-LabInventory -Slot 1 -Expect SCP207
$null=Invoke-LabInput @{id='native207-second-stack';frames=480;inputFrames=2;keys=@(323);capture=$true;audio=$true;expectAudio=$true}
$second=Wait-Effects { param($s) $s.scp207 -eq 2 } 15 'second native stack'
if ($second.health -ne 100 -or $second.scp207Duration -ne 0) { throw 'Native stacking/healing failed' }
Arrange 'vulnerable'
$before=@(Observe -ClientsOnly)[0]
if ($before.godMode) { throw 'Health-drain fixture still invulnerable' }
$damageBefore=Read-Effects
$null=Invoke-LabInput @{id='native207-health-drain';frames=360;keys=@(115);capture=$true;audio=$true;expectAudio=$true}
$draining=Read-Effects
$after=@(Observe -ClientsOnly)[0]
$distance=[Math]::Sqrt([Math]::Pow($after.position.x-$before.position.x,2)+[Math]::Pow($after.position.z-$before.position.z,2))
if ($after.life -ne $before.life -or $distance -lt 0.2 -or $draining.scp207 -ne 2 -or $draining.stamina -lt 0.99) { throw 'Native movement or stamina failed' }
$damageAttempts=$draining.scp207DamageAttempts-$damageBefore.scp207DamageAttempts
$damageCancelled=$draining.scp207DamageCancelled-$damageBefore.scp207DamageCancelled
if ($damageAttempts -le 0) { throw 'No native SCP-207 ticking damage was observed' }
# The mirrored server's CokeNoDmg cancels native drain; observe that decision without overriding it.
if ($damageCancelled -eq $damageAttempts) {
    if ($draining.health -ne $damageBefore.health) { throw 'Cancelled native drain changed health' }
} elseif ($draining.health -ge $damageBefore.health) { throw 'Allowed native drain did not reduce health' }
# Non-default modifiers must be loaded from YAML, and custom cleanup must leave native SCP-207 intact.
Grant 'ahead' '8'
$ahead=Read-Effects
if ($ahead.movement -ne 32 -or $ahead.maxHealth -ne $baseline.maxHealth -or $ahead.scp207 -ne 2) { throw 'Configured Ahead strength was not applied' }
$null=Invoke-LabInput @{id='configured-ahead-positive';frames=90;capture=$true}
$backlash=Wait-Effects { param($s) $s.slowness -eq 25 } 15 'configured Ahead backlash'
if ([Math]::Abs($backlash.maxHealth-$baseline.maxHealth*0.7) -gt 0.01) { throw 'Configured backlash maximum HP was not applied' }
$null=Invoke-LabInput @{id='configured-ahead-backlash';frames=480;capture=$true}
$expired=Wait-Effects { param($s) $s.slowness -eq $baseline.slowness -and $s.movement -eq $baseline.movement -and $s.maxHealth -eq $baseline.maxHealth } 15 'Ahead restoration'
if ($expired.health -gt $backlash.health+0.01 -or $expired.scp207 -ne 2) { throw 'Backlash expiry healed or removed native SCP-207' }
Grant 'vodka' '8'
$vodka=Read-Effects
if ($vodka.slowness -ne 15 -or $vodka.damageReduction -ne 24) { throw 'Vodka percent conversion must produce 15% slowness and 12% damage reduction' }
$null=Invoke-LabServer "/scp294 buff clear $actorId"
Grant 'qlz' 'random 8'
$qlz=Read-Effects
if ($qlz.cardiac -ne 0 -or $qlz.movement -ne 80) { throw '0% cardiac branch/configured speed was not applied' }
$null=Invoke-LabServer "/scp294 buff clear $actorId"
Grant 'jiahao' 'random 8'
$jiahao=Read-Effects
if ($jiahao.scp207 -ne 3 -or (@(Observe -ClientsOnly)[0].role -eq 'Spectator')) { throw '100% Jiahao success branch failed' }
$null=Invoke-LabInput @{id='configured-jiahao-success';frames=240;capture=$true;audio=$true;expectAudio=$true}
$null=Invoke-LabServer "/scp294 buff clear $actorId"
$cleaned=Read-Effects
if ($cleaned.scp207 -ne 2 -or $cleaned.slowness -ne 0 -or $cleaned.damageReduction -ne 0 -or $cleaned.movement -ne 0) { throw 'Custom cleanup did not preserve ordinary SCP-207' }
$null=Invoke-LabScreenshot -Name 'scp294-native207-after-cleanup'
$null=Invoke-LabServer '/scp294 clear'
$cleared=(Invoke-LabServer '/scp294fixture state') -join "`n"
if ($cleared -notmatch '"roots":0,"survivingToys":0') { throw 'Machine cleanup failed' }
