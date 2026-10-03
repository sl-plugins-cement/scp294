# SCP-294 coffee machine model

`DrinkMachineGeometry.cs` is the shared authoritative geometry for the native toy spawner and Unity preview. `Authoring/DrinkMachinePreview.cs` creates the editable Unity hierarchy and exports `scp294.mer.json`; do not edit the generated schematic directly. The runtime builds native primitives and world text from the same geometry, with no ProjectMER dependency or client asset download.

The cabinet follows the layout of the SCP Wiki's SCP-294 reference: dark frame, metal front, recessed display with sculpted coffee cups, angled QWERTY keyboard with raised keys, protruding coin controls and an open paper-cup bay with nozzle and drip tray. In-game interaction uses native hold-to-search on the front panel. The keyboard and coin controls are visual details; the plugin continues to dispense its existing random drinks.

The authored cabinet is approximately 1.35 m wide, 2.22 m high and 1.2 m deep including the controls and tray. `machine_scale: 10` retains this default size; other values scale it proportionally. The configured position is projected onto the floor below it, and the model faces local negative Z. The model uses 175 static network toys, including its invisible root, collision shell and interaction target; a single collidable cabinet gives it a physical body. Clearing or replacing the machine destroys its replicated root and children.

To regenerate in the Unity playground, copy `DrinkMachineGeometry.cs` and `Authoring/DrinkMachinePreview.cs` into `Assets/SCP294`, refresh the Editor, then run `Scp294Authoring.DrinkMachinePreview.Build(outputDirectory)` through Unity CLI. The playground needs the existing ProjectMER authoring package. Native text sizing and interaction require a real-client check.

## Attribution

This original primitive model adapts the layout of [SCP-294](https://scp-wiki.wikidot.com/scp-294) and its reference image, **SCP-294** by [StaticFactory](https://www.deviantart.com/staticfactory/art/SCP-294-298815062). The article's current credit names Arcibi. The adapted geometry and generated schematic are licensed under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). The reference image is not bundled or used as a texture.
