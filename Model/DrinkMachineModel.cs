using System;
using PrimitiveFlags = AdminToys.PrimitiveFlags;
using InvisibleInteractableToy = AdminToys.InvisibleInteractableToy;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace Qlz.Model
{
    internal sealed class DrinkMachineModel : IDisposable
    {
        private PrimitiveObjectToy? root;
        internal InteractableToy Target { get; private set; } = null!;
        internal bool IsDestroyed => root == null || root.IsDestroyed;
        internal Vector3 Position => root?.Position ?? Vector3.zero;
        internal int ToyCount => DrinkMachineGeometry.Parts.Count + DrinkMachineGeometry.Labels.Length + 2;

        internal static DrinkMachineModel Spawn(Vector3 position, Quaternion rotation, float legacyScale)
        {
            // Keep existing machine_scale: 10 configurations at the authored two-metre size.
            float scale = float.IsNaN(legacyScale) || float.IsInfinity(legacyScale) ? 1f : Mathf.Clamp(legacyScale / 10f, 0.1f, 5f);
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position.y = floor.point.y + 0.015f;
            var model = new DrinkMachineModel();
            try
            {
                model.root = PrimitiveObjectToy.Create(position, rotation, Vector3.one * scale, networkSpawn: false);
                model.root.GameObject.name = "SCP294-CoffeeMachine";
                model.root.Flags = PrimitiveFlags.None;
                model.root.IsStatic = true;
                model.root.Spawn();
                foreach (DrinkMachineGeometry.Part part in DrinkMachineGeometry.Parts)
                {
                    PrimitiveObjectToy toy = PrimitiveObjectToy.Create(part.Position, Quaternion.Euler(part.Rotation), part.Scale, model.root.Transform, false);
                    toy.Type = part.Type;
                    toy.Color = part.Color;
                    toy.Flags = PrimitiveFlags.Visible | (part.Collidable ? PrimitiveFlags.Collidable : PrimitiveFlags.None);
                    toy.IsStatic = true;
                    toy.Spawn();
                }
                foreach (DrinkMachineGeometry.Label label in DrinkMachineGeometry.Labels)
                {
                    TextToy text = TextToy.Create(label.Position, Quaternion.identity, Vector3.one * label.Scale, model.root.Transform, false);
                    text.DisplaySize = new Vector2(label.Units, 40f);
                    text.TextFormat = "<align=center><size=" + label.FontSize + ">" + label.Text + "</size></align>";
                    text.IsStatic = true;
                    text.Spawn();
                }
                model.Target = InteractableToy.Create(DrinkMachineGeometry.InteractionPosition, Quaternion.identity,
                    DrinkMachineGeometry.InteractionSize, model.root.Transform, false);
                model.Target.Shape = InvisibleInteractableToy.ColliderShape.Box;
                model.Target.InteractionDuration = 0.5f;
                model.Target.IsLocked = false;
                model.Target.IsStatic = true;
                model.Target.Spawn();
                return model;
            }
            catch { model.Dispose(); throw; }
        }

        public void Dispose()
        {
            // Native AdminToy destruction cascades to the replicated child hierarchy.
            if (root != null && !root.IsDestroyed)
            {
                // Unity defers destruction; a same-frame replacement must not raycast onto the old cabinet.
                foreach (Collider collider in root.GameObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
                root.Destroy();
            }
            root = null;
        }
    }
}
