using System;
using System.IO;
using Qlz.Model;
using Scpsl.ProjectMer.Authoring;
using Scpsl.ProjectMer.Authoring.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scp294Authoring
{
    public static class DrinkMachinePreview
    {
        public static string Build(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            Scene scene = SceneManager.GetSceneByName("SCP294Preview");
            if (!scene.IsValid()) scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (GameObject old in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("SCP294-CoffeeMachine");
            Shader shader = Shader.Find("Standard");
            foreach (DrinkMachineGeometry.Part part in DrinkMachineGeometry.Parts)
            {
                GameObject go = GameObject.CreatePrimitive(part.Type);
                go.name = part.Name;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = part.Position;
                go.transform.localEulerAngles = part.Rotation;
                go.transform.localScale = part.Scale;
                var material = new Material(shader) { color = part.Color };
                material.SetFloat("_Glossiness", 0.27f);
                go.GetComponent<Renderer>().sharedMaterial = material;
                var metadata = go.AddComponent<ProjectMerExportMetadata>();
                metadata.Collidable = part.Collidable;
                if (!part.Collidable) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            foreach (DrinkMachineGeometry.Label label in DrinkMachineGeometry.Labels)
            {
                var go = new GameObject(label.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = label.Position;
                go.transform.localScale = Vector3.one * label.Scale;
                var metadata = go.AddComponent<ProjectMerExportMetadata>();
                metadata.BlockKind = MerBlockKind.Text;
                metadata.DisplaySize = new Vector2(label.Units, 40f);
                metadata.Text = "<align=center><size=" + label.FontSize + ">" + label.Text + "</size></align>";
                // Unity's TextMesh is a preview of world text; native TextToy is reviewed in-game.
                var text = go.AddComponent<TextMesh>();
                text.text = label.Text;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = label.FontSize * 0.05f * 10f / text.fontSize;
                text.color = Color.white;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            }
            var result = ProjectMerSceneExporter.ExportHierarchyToFile(root, Path.Combine(outputDirectory, "scp294.mer.json"));
            if (!result.Success) throw new InvalidOperationException(string.Join("; ", result.Errors));
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/SCP294/CoffeeMachine.prefab");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SCP294Preview-Floor";
            floor.transform.position = new Vector3(0, -0.07f, 0);
            floor.transform.localScale = new Vector3(7, 0.1f, 7);
            floor.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(0.15f, 0.17f, 0.18f) };
            var light = new GameObject("SCP294Preview-Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            var camera = new GameObject("SCP294Preview-Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(-2.5f, 2.1f, -4.4f);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.fieldOfView = 34;
            camera.backgroundColor = new Color(0.065f, 0.08f, 0.10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Restrict this preview camera to the model's own scene using an isolated layer.
            foreach (Transform t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            floor.layer = light.gameObject.layer = 30;
            camera.cullingMask = 1 << 30;
            light.cullingMask = 1 << 30;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1.1f, 0), camera.transform.rotation, 2.1f);
            var texture = new RenderTexture(1000, 1000, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var pixels = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(outputDirectory, "unity-preview.png"), pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.SaveScene(scene, "Assets/SCP294/SCP294Preview.unity");
            return result.BlockCount + " blocks; " + string.Join("; ", result.Warnings);
        }
    }
}
