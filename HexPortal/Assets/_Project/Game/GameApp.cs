using HexPortal.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexPortal.Game
{
    /// <summary>Runtime root: owns the MatchController, the views and the UI, and ticks the T-09 clock.</summary>
    public sealed class GameApp : MonoBehaviour
    {
        const string UiResource = "HexPortalUI/Main";
        const string StyleResource = "HexPortalUI/MainStyle";
        const string ThemeResource = "HexPortalTheme";

        public static GameApp Instance { get; private set; }

        public MatchController Match { get; private set; }
        public GameUi Ui { get; private set; }

        public static void Create()
        {
            var go = new GameObject("HexPortal");
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            Instance = this;
            Match = new MatchController();
            var cam = CameraRig.Create();
            CameraRig.Face(cam, PlayerId.A);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            uiGo.SetActive(false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.visualTreeAsset = Resources.Load<VisualTreeAsset>(UiResource);
            uiGo.SetActive(true);
            var root = doc.rootVisualElement;
            var style = Resources.Load<StyleSheet>(StyleResource);
            if (style == null) Debug.LogError("[GameApp] Missing Resources/" + StyleResource + ".uss");
            else root.styleSheets.Add(style);

            var board = BoardView.Create(transform);
            var units = UnitsView.Create(transform, root.Q("overlay"), cam, board);
            Ui = new GameUi(Match, root, board, units, cam);
        }

        void Update()
        {
            Match.Tick(Time.deltaTime);
            Ui.TickClock();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
