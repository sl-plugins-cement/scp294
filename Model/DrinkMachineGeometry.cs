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
            internal bool Collidable, Visible = true;
        }
        internal sealed class Label
        {
            internal string Name = "", Text = "";
            internal Vector3 Position, Rotation;
            internal float Width, Units;
            internal int FontSize;
            internal float Scale => Width / (Units * 0.05f);
        }
        private static readonly Quaternion KeyboardRotation = Quaternion.Euler(55, 0, 0);
        private static readonly Vector3 KeyboardCentre = new Vector3(-0.13f, 1.25f, -0.535f);
        private static readonly string[] KeyRows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        internal static readonly Vector3 InteractionPosition = new Vector3(0, 1.14f, -0.755f);
        internal static readonly Vector3 InteractionSize = new Vector3(1.24f, 1.96f, 0.03f);
        internal static readonly IReadOnlyList<Part> Parts = Build();
        internal static readonly Label[] Labels = BuildLabels();
        private static Label[] BuildLabels()
        {
            var labels = new List<Label>
            {
                Text("nameplate", "<color=#E9E6D7><b>SCP-294</b></color>", new Vector3(0, 2.09f, -0.562f), 1.03f, 100, 16),
                Text("display-title", "<color=#D8DED5>咖啡 · 饮料</color>", new Vector3(-0.13f, 1.91f, -0.559f), 0.76f, 100, 10),
                Text("coin-display", "<color=#C2E09D>就绪</color>", new Vector3(0.465f, 1.80f, -0.632f), 0.18f, 80, 13),
                Text("instructions", "<color=#D8DED5>按住互动键领取</color>", new Vector3(-0.13f, 1.00f, -0.540f), 0.66f, 110, 11),
                Text("quota", "<color=#D8DED5>每条生命\n限领一瓶</color>", new Vector3(0.17f, 0.69f, -0.531f), 0.34f, 70, 11),
            };
            // Individual legends stay centred despite differences in native font spacing.
            for (int row = 0; row < KeyRows.Length; row++)
                for (int key = 0; key < KeyRows[row].Length; key++)
                {
                    Label label = Text("key-legend-" + row + "-" + key, KeyRows[row][key].ToString(), KeyPosition(row, key, -0.069f), 0.065f, 20, 11);
                    label.Rotation = KeyboardRotation.eulerAngles;
                    labels.Add(label);
                }
            return labels.ToArray();
        }
        private static Label Text(string name, string text, Vector3 position, float width, float units, int size)
            => new Label { Name = name, Text = text, Position = position, Width = width, Units = units, FontSize = size };
        private static Vector3 KeyPosition(int row, int key, float depth)
            => KeyboardCentre + KeyboardRotation * new Vector3((key - (KeyRows[row].Length - 1) / 2f) * 0.073f, 0.125f - row * 0.07f, depth);
        private static IReadOnlyList<Part> Build()
        {
            var parts = new List<Part>();
            Color black = new Color(0.045f, 0.052f, 0.05f);
            Color steel = new Color(0.38f, 0.41f, 0.39f);
            Color chrome = new Color(0.61f, 0.64f, 0.59f);
            Color bay = new Color(0.018f, 0.022f, 0.021f);
            Color lining = new Color(0.16f, 0.20f, 0.19f);
            void Box(string name, Vector3 position, Vector3 scale, Color color, Vector3 rotation = default, bool collision = false, bool visible = true)
                => parts.Add(new Part { Name = name, Type = PrimitiveType.Cube, Position = position, Scale = scale, Rotation = rotation, Color = color, Collidable = collision, Visible = visible });
            void B(string name, float x, float y, float z, float w, float h, float d, Color color)
                => Box(name, new Vector3(x, y, z), new Vector3(w, h, d), color);
            void Cylinder(string name, Vector3 position, float diameter, float height, Color color, Vector3 rotation = default)
                => parts.Add(new Part { Name = name, Type = PrimitiveType.Cylinder, Position = position, Scale = new Vector3(diameter, height / 2f, diameter), Rotation = rotation, Color = color });
            void C(string name, float x, float y, float z, float diameter, float height, Color color, bool front = false)
                => Cylinder(name, new Vector3(x, y, z), diameter, height, color, front ? new Vector3(90, 0, 0) : Vector3.zero);

            // Leave genuine openings; one invisible box keeps player collision simple.
            Box("cabinet-collision", new Vector3(0, 1.1f, 0), new Vector3(1.3f, 2.12f, 0.88f), black, collision: true, visible: false);
            B("rear-shell", 0, 1.1f, 0.425f, 1.3f, 2.12f, 0.035f, black);
            B("left-shell", -0.625f, 1.1f, 0, 0.05f, 2.12f, 0.85f, black);
            B("right-shell", 0.625f, 1.1f, 0, 0.05f, 2.12f, 0.85f, black);
            B("top-shell", 0, 2.19f, -0.015f, 1.3f, 0.065f, 0.90f, black);
            B("bottom-shell", 0, 0.185f, -0.01f, 1.25f, 0.27f, 0.88f, black);
            foreach (int side in new[] { -1, 1 })
            {
                B("side-service-" + side, side * 0.653f, 1.03f, 0.045f, 0.014f, 1.45f, 0.63f, new Color(0.065f, 0.072f, 0.068f));
                for (int i = 0; i < 5; i++) B("side-vent-" + side + "-" + i, side * 0.664f, 1.74f - i * 0.045f, 0.075f, 0.016f, 0.017f, 0.34f, bay);
                C("rounded-frame-" + side, side * 0.623f, 1.13f, -0.47f, 0.038f, 2.12f, chrome);
                B("frame-shadow-" + side, side * 0.596f, 1.13f, -0.451f, 0.023f, 2.10f, 0.048f, bay);
            }
            B("top-hood", 0, 2.085f, -0.462f, 1.25f, 0.21f, 0.18f, black);
            Box("hood-bevel", new Vector3(0, 2.20f, -0.465f), new Vector3(1.26f, 0.025f, 0.115f), chrome, new Vector3(20, 0, 0));
            B("brand-plate", 0, 2.09f, -0.555f, 1.08f, 0.16f, 0.012f, new Color(0.07f, 0.078f, 0.074f));
            foreach (float x in new[] { -0.56f, 0.56f })
                foreach (float y in new[] { 2.13f, 2.04f }) C("plate-screw-" + x + "-" + y, x, y, -0.562f, 0.014f, 0.012f, chrome, true);

            // The coffee advertisement is a shadowbox with sculpted cups and handles.
            B("display-back", -0.13f, 1.74f, -0.30f, 0.90f, 0.51f, 0.022f, new Color(0.075f, 0.105f, 0.11f));
            B("display-top", -0.13f, 1.98f, -0.43f, 0.94f, 0.055f, 0.23f, steel);
            B("display-bottom", -0.13f, 1.49f, -0.43f, 0.94f, 0.045f, 0.23f, steel);
            B("display-left", -0.58f, 1.735f, -0.43f, 0.04f, 0.49f, 0.23f, steel);
            B("display-right", 0.32f, 1.735f, -0.43f, 0.04f, 0.49f, 0.23f, steel);
            B("display-light-strip", -0.13f, 1.95f, -0.522f, 0.80f, 0.012f, 0.021f, new Color(0.65f, 0.76f, 0.72f));
            for (int i = 0; i < 3; i++)
            {
                float x = -0.40f + i * 0.255f;
                float y = i == 1 ? 1.66f : 1.72f;
                Color cup = i == 0 ? new Color(0.24f, 0.43f, 0.45f) : i == 1 ? new Color(0.64f, 0.62f, 0.50f) : new Color(0.45f, 0.30f, 0.22f);
                C("display-saucer-" + i, x, y - 0.10f, -0.434f, 0.21f, 0.012f, cup);
                C("display-cup-base-" + i, x, y - 0.051f, -0.431f, 0.13f, 0.075f, cup);
                C("display-cup-body-" + i, x, y + 0.018f, -0.431f, 0.17f, 0.075f, cup);
                C("display-cup-rim-" + i, x, y + 0.061f, -0.431f, 0.18f, 0.012f, chrome);
                C("display-coffee-" + i, x, y + 0.069f, -0.431f, 0.15f, 0.003f, new Color(0.16f, 0.075f, 0.026f));
                for (int segment = 0; segment < 6; segment++)
                {
                    float a = (90f + segment * 60f) * Mathf.Deg2Rad;
                    float b = a + 60f * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(x + 0.11f + Mathf.Cos(a) * 0.044f, y + Mathf.Sin(a) * 0.055f, -0.431f);
                    Vector3 q = new Vector3(x + 0.11f + Mathf.Cos(b) * 0.044f, y + Mathf.Sin(b) * 0.055f, -0.431f);
                    Cylinder("display-handle-" + i + "-" + segment, (p + q) / 2f, 0.012f, (q - p).magnitude + 0.006f, cup, Quaternion.FromToRotation(Vector3.up, q - p).eulerAngles);
                }
            }
            Box("keyboard-console", KeyboardCentre, new Vector3(0.94f, 0.41f, 0.075f), new Color(0.22f, 0.24f, 0.22f), KeyboardRotation.eulerAngles);
            foreach (float x in new[] { -0.59f, 0.33f }) B("keyboard-support-" + x, x, 1.23f, -0.46f, 0.035f, 0.27f, 0.34f, black);
            for (int row = 0; row < KeyRows.Length; row++)
                for (int key = 0; key < KeyRows[row].Length; key++)
                    Box("key-" + row + "-" + key, KeyPosition(row, key, -0.052f), new Vector3(0.061f, 0.049f, 0.028f), black, KeyboardRotation.eulerAngles);
            Box("space-key", KeyboardCentre + KeyboardRotation * new Vector3(0, -0.168f, -0.055f), new Vector3(0.265f, 0.04f, 0.03f), black, KeyboardRotation.eulerAngles);
            Cylinder("console-front-lip", new Vector3(-0.13f, 1.13f, -0.706f), 0.03f, 0.94f, steel, new Vector3(0, 0, 90));

            // Door sections surround an open delivery cavity.
            B("door-top", -0.13f, 1.025f, -0.47f, 0.94f, 0.16f, 0.115f, steel);
            B("door-bottom", -0.13f, 0.36f, -0.47f, 0.94f, 0.12f, 0.115f, steel);
            B("door-left", -0.565f, 0.695f, -0.47f, 0.075f, 0.54f, 0.115f, steel);
            B("door-right", 0.145f, 0.695f, -0.47f, 0.39f, 0.54f, 0.115f, steel);
            B("instruction-plate", -0.13f, 1.00f, -0.534f, 0.75f, 0.075f, 0.008f, black);
            B("bay-back", -0.315f, 0.69f, -0.015f, 0.43f, 0.51f, 0.025f, bay);
            B("bay-left", -0.515f, 0.695f, -0.26f, 0.025f, 0.51f, 0.49f, lining);
            B("bay-right", -0.105f, 0.695f, -0.26f, 0.025f, 0.51f, 0.49f, lining);
            B("bay-ceiling", -0.315f, 0.928f, -0.275f, 0.43f, 0.035f, 0.52f, lining);
            B("bay-hood", -0.315f, 0.94f, -0.548f, 0.47f, 0.045f, 0.075f, chrome);
            B("bay-light", -0.315f, 0.903f, -0.31f, 0.31f, 0.012f, 0.026f, new Color(0.57f, 0.75f, 0.66f));
            C("nozzle-stem", -0.315f, 0.851f, -0.245f, 0.047f, 0.12f, chrome);
            C("nozzle-tip", -0.315f, 0.786f, -0.245f, 0.060f, 0.025f, black);
            B("drip-tray", -0.315f, 0.444f, -0.385f, 0.44f, 0.028f, 0.65f, black);
            B("tray-front-lip", -0.315f, 0.465f, -0.701f, 0.44f, 0.034f, 0.025f, chrome);
            for (int i = 0; i < 7; i++) B("tray-rib-" + i, -0.475f + i * 0.053f, 0.464f, -0.385f, 0.018f, 0.009f, 0.58f, steel);
            C("paper-cup-base", -0.315f, 0.51f, -0.36f, 0.095f, 0.075f, new Color(0.67f, 0.65f, 0.55f));
            C("paper-cup-middle", -0.315f, 0.565f, -0.36f, 0.115f, 0.045f, new Color(0.72f, 0.70f, 0.59f));
            C("paper-cup-top", -0.315f, 0.605f, -0.36f, 0.13f, 0.045f, new Color(0.75f, 0.73f, 0.63f));
            C("paper-cup-rim", -0.315f, 0.631f, -0.36f, 0.139f, 0.009f, new Color(0.87f, 0.85f, 0.75f));
            C("coffee", -0.315f, 0.637f, -0.36f, 0.116f, 0.003f, new Color(0.12f, 0.05f, 0.018f));

            B("coin-column", 0.473f, 1.40f, -0.40f, 0.265f, 1.16f, 0.35f, black);
            B("coin-face", 0.473f, 1.53f, -0.586f, 0.245f, 0.72f, 0.025f, steel);
            B("coin-lcd-bezel", 0.465f, 1.80f, -0.61f, 0.213f, 0.105f, 0.027f, black);
            B("coin-lcd", 0.465f, 1.80f, -0.626f, 0.19f, 0.075f, 0.009f, new Color(0.065f, 0.13f, 0.081f));
            C("coin-dial-ring", 0.448f, 1.63f, -0.616f, 0.118f, 0.029f, chrome, true);
            C("coin-dial", 0.448f, 1.63f, -0.648f, 0.089f, 0.047f, black, true);
            B("coin-dial-grip", 0.448f, 1.63f, -0.677f, 0.018f, 0.072f, 0.016f, chrome);
            B("coin-slot-bezel", 0.551f, 1.64f, -0.615f, 0.033f, 0.113f, 0.031f, chrome);
            B("coin-slot", 0.551f, 1.64f, -0.634f, 0.013f, 0.085f, 0.010f, bay);
            for (int i = 0; i < 6; i++) B("coin-list-" + i, 0.435f, 1.49f - i * 0.032f, -0.603f, 0.095f, 0.007f, 0.006f, bay);
            B("return-back", 0.48f, 0.58f, -0.432f, 0.13f, 0.095f, 0.025f, bay);
            foreach (float x in new[] { 0.405f, 0.555f }) B("return-side-" + x, x, 0.58f, -0.48f, 0.018f, 0.12f, 0.11f, chrome);
            Box("return-ledge", new Vector3(0.48f, 0.525f, -0.49f), new Vector3(0.15f, 0.016f, 0.13f), chrome, new Vector3(15, 0, 0));
            B("return-hood", 0.48f, 0.637f, -0.48f, 0.17f, 0.024f, 0.11f, black);
            B("door-seam", 0.346f, 0.73f, -0.522f, 0.014f, 0.81f, 0.018f, bay);
            foreach (float y in new[] { 0.48f, 0.81f, 1.02f }) C("door-hinge-" + y, -0.589f, y, -0.55f, 0.027f, 0.055f, chrome);
            foreach (float x in new[] { -0.47f, 0.47f })
                foreach (float z in new[] { -0.26f, 0.26f }) C("foot-" + x + "-" + z, x, 0.031f, z, 0.095f, 0.06f, bay);
            return parts;
        }
    }
}
