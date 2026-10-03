using System.Collections.Generic;
using UnityEngine;

namespace Qlz.Model
{
    // Shared by the native toy spawner and the Unity authoring preview.
    internal static class DrinkMachineGeometry
    {
        internal sealed class Part
        {
            internal string Name = "";
            internal PrimitiveType Type;
            internal Vector3 Position, Scale, Rotation;
            internal Color Color;
            internal bool Collidable;
        }

        internal sealed class Label
        {
            internal string Name = "", Text = "";
            internal Vector3 Position;
            internal float Width, Units;
            internal int FontSize;
            internal float Scale => Width / (Units * 0.05f);
        }

        internal static readonly Vector3 InteractionPosition = new Vector3(0f, 1.14f, -0.405f);
        internal static readonly Vector3 InteractionSize = new Vector3(1.24f, 1.92f, 0.025f);
        internal static readonly IReadOnlyList<Part> Parts = Build();
        internal static readonly Label[] Labels =
        {
            Text("nameplate", "<color=#EBE5D4><b>SCP-294</b></color>", 0f, 2.09f, -0.371f, 1.03f, 100f, 16),
            Text("display-title", "<color=#342C27><b>咖啡 · 饮料</b></color>", -0.13f, 1.92f, -0.381f, 0.83f, 100f, 12),
            Text("keyboard-numbers", "1  2  3  4  5  6  7  8  9  0", -0.13f, 1.36f, -0.391f, 0.72f, 150f, 9),
            Text("keyboard-qwerty", "Q  W  E  R  T  Y  U  I  O  P", -0.13f, 1.295f, -0.391f, 0.72f, 150f, 9),
            Text("keyboard-home", "A  S  D  F  G  H  J  K  L", -0.115f, 1.23f, -0.391f, 0.66f, 150f, 9),
            Text("keyboard-bottom", "Z  X  C  V  B  N  M", -0.105f, 1.165f, -0.391f, 0.52f, 150f, 9),
            Text("coin-display", "<color=#C2E09D>就绪</color>", 0.465f, 1.58f, -0.393f, 0.20f, 80f, 14),
            Text("instructions", "<color=#DFE4D4>按住互动键领取</color>", -0.11f, 0.92f, -0.374f, 0.76f, 110f, 12),
            Text("quota", "<color=#C4C9CA>每条生命\n限领一瓶</color>", 0.17f, 0.61f, -0.374f, 0.34f, 70f, 11),
        };

        private static Label Text(string name, string text, float x, float y, float z, float width, float units, int size)
            => new Label { Name = name, Text = text, Position = new Vector3(x, y, z), Width = width, Units = units, FontSize = size };

        private static IReadOnlyList<Part> Build()
        {
            var parts = new List<Part>();
            Color black = new Color(0.055f, 0.062f, 0.06f);
            Color steel = new Color(0.50f, 0.52f, 0.50f);
            Color edge = new Color(0.73f, 0.75f, 0.70f);
            Color bay = new Color(0.022f, 0.028f, 0.03f);
            void Box(string name, float x, float y, float z, float w, float h, float d, Color color, bool collision = false)
                => parts.Add(new Part { Name = name, Type = PrimitiveType.Cube, Position = new Vector3(x, y, z), Scale = new Vector3(w, h, d), Color = color, Collidable = collision });
            void Cylinder(string name, float x, float y, float z, float diameter, float height, Color color, bool front = false)
                => parts.Add(new Part { Name = name, Type = PrimitiveType.Cylinder, Position = new Vector3(x, y, z), Scale = new Vector3(diameter, height / 2f, diameter), Rotation = front ? new Vector3(90f, 0f, 0f) : Vector3.zero, Color = color });

            Box("cabinet", 0, 1.10f, 0, 1.30f, 2.12f, 0.70f, black, true);
            Box("left-frame", -0.635f, 1.14f, -0.355f, 0.026f, 2.16f, 0.028f, edge);
            Box("right-frame", 0.635f, 1.14f, -0.355f, 0.026f, 2.16f, 0.028f, edge);
            Box("top-frame", 0, 2.20f, -0.355f, 1.30f, 0.026f, 0.028f, edge);
            Box("nameplate", 0, 2.085f, -0.356f, 1.23f, 0.195f, 0.025f, black);
            Box("screen-frame", -0.13f, 1.70f, -0.359f, 0.94f, 0.575f, 0.023f, steel);
            Box("screen", -0.13f, 1.70f, -0.374f, 0.865f, 0.525f, 0.009f, new Color(0.60f, 0.71f, 0.70f));
            Box("keyboard-panel", -0.13f, 1.255f, -0.362f, 0.94f, 0.35f, 0.025f, new Color(0.24f, 0.25f, 0.24f));
            int[] counts = { 10, 10, 9, 7 };
            for (int row = 0; row < counts.Length; row++)
                for (int key = 0; key < counts[row]; key++)
                    Box("key-" + row + "-" + key, -0.13f + (key - (counts[row] - 1) / 2f) * 0.073f, 1.36f - row * 0.065f, -0.38f, 0.061f, 0.049f, 0.020f, black);
            Box("space-key", -0.13f, 1.10f, -0.38f, 0.265f, 0.04f, 0.018f, black);
            Box("metal-front", -0.13f, 0.71f, -0.356f, 0.94f, 0.68f, 0.028f, steel);
            Box("cup-recess", -0.30f, 0.61f, -0.375f, 0.32f, 0.365f, 0.025f, bay);
            Box("bay-top", -0.30f, 0.792f, -0.399f, 0.36f, 0.04f, 0.05f, new Color(0.24f, 0.28f, 0.27f));
            Box("drip-tray", -0.30f, 0.428f, -0.431f, 0.36f, 0.035f, 0.13f, black);
            for (int i = 0; i < 7; i++) Box("tray-rib-" + i, -0.438f + i * 0.046f, 0.449f, -0.431f, 0.017f, 0.008f, 0.11f, steel);
            Cylinder("cup", -0.30f, 0.512f, -0.423f, 0.12f, 0.12f, new Color(0.90f, 0.88f, 0.77f));
            Cylinder("cup-rim", -0.30f, 0.576f, -0.423f, 0.127f, 0.009f, new Color(0.98f, 0.97f, 0.89f));
            Cylinder("coffee", -0.30f, 0.581f, -0.423f, 0.105f, 0.003f, new Color(0.11f, 0.055f, 0.026f));
            Box("nozzle", -0.30f, 0.742f, -0.419f, 0.055f, 0.075f, 0.05f, steel);
            Box("coin-frame", 0.465f, 1.38f, -0.363f, 0.26f, 0.53f, 0.024f, steel);
            Box("coin-panel", 0.465f, 1.38f, -0.378f, 0.235f, 0.50f, 0.012f, black);
            Box("coin-lcd", 0.465f, 1.58f, -0.387f, 0.203f, 0.06f, 0.008f, new Color(0.095f, 0.14f, 0.1f));
            Cylinder("coin-dial", 0.465f, 1.46f, -0.402f, 0.072f, 0.015f, edge, true);
            Box("coin-slot", 0.555f, 1.46f, -0.396f, 0.012f, 0.066f, 0.014f, bay);
            for (int i = 0; i < 6; i++) Box("coin-list-" + i, 0.417f, 1.33f - i * 0.026f, -0.387f, 0.089f, 0.006f, 0.005f, steel);
            Box("return-slot", 0.50f, 0.435f, -0.37f, 0.10f, 0.08f, 0.012f, steel);
            Box("return-opening", 0.50f, 0.435f, -0.381f, 0.076f, 0.045f, 0.012f, bay);
            Box("base-plinth", 0, 0.185f, -0.008f, 1.22f, 0.27f, 0.69f, black);
            Box("left-foot", -0.45f, 0.028f, 0, 0.15f, 0.055f, 0.45f, bay);
            Box("right-foot", 0.45f, 0.028f, 0, 0.15f, 0.055f, 0.45f, bay);
            // Flat primitive cup illustrations stand in for the wiki photograph.
            for (int i = 0; i < 3; i++)
            {
                float x = -0.39f + i * 0.26f;
                Box("display-cup-" + i, x, 1.67f - (i == 1 ? 0.055f : 0), -0.385f, 0.17f, 0.15f, 0.008f, new Color(0.84f - i * 0.08f, 0.84f - i * 0.09f, 0.76f - i * 0.04f));
                Box("display-rim-" + i, x, 1.75f - (i == 1 ? 0.055f : 0), -0.391f, 0.19f, 0.019f, 0.01f, edge);
                Box("display-coffee-" + i, x, 1.755f - (i == 1 ? 0.055f : 0), -0.399f, 0.145f, 0.005f, 0.009f, new Color(0.23f, 0.14f, 0.09f));
            }
            return parts;
        }
    }
}
