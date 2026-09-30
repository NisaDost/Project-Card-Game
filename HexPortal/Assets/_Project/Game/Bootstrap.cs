using UnityEngine;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    // Builds the scene from code (no manual scene setup). M0: portrait camera and a title label.
    public static class Bootstrap
    {
        const string ThemeResource = "HexPortalTheme";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            SetupCamera();
            SetupUi();
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            // Top-down view of the board origin, row 0 toward +Z (top of a portrait screen).
            cam.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        }

        static void SetupUi()
        {
            var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
            if (theme == null)
                Debug.LogError($"[Bootstrap] ThemeStyleSheet 'Resources/{ThemeResource}.tss' not found; UI text may not render.");

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 1920);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            // Assign panel settings while inactive so the document builds its root once, on activation.
            var go = new GameObject("UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            go.SetActive(true);

            var root = doc.rootVisualElement;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            var title = new Label("HexPortal M0");
            title.style.fontSize = 72;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
        }
    }
}
