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
            settings.match = 1f; // match HEIGHT: landscape phones differ in width; sizes in USS are tuned to the 1080 height (UX-01 dp)

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
            Units = units;
            SetupErrorCounter(root);
        }

        public UnitsView Units { get; private set; }

        // ---------- Development-only error counter (device proof that nothing logs errors) ----------

        Label errorLabel;
        /// <summary>Errors, exceptions and asserts logged since this app started (Development builds and the Editor).</summary>
        public int LoggedErrors { get; private set; }

        void SetupErrorCounter(VisualElement root)
        {
            errorLabel = root.Q<Label>("err-counter");
            if (errorLabel == null || !Debug.isDebugBuild) return; // Release builds: no overlay, no log hook
            errorLabel.text = Strings.ErrorCounter + "0";
            errorLabel.style.display = DisplayStyle.Flex;
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            LoggedErrors++;
            errorLabel.text = Strings.ErrorCounter + LoggedErrors;
            errorLabel.AddToClassList("has-errors");
        }

        /// <summary>AI-05: pause between AI commands so each one's animation can be followed.</summary>
        public const float AiStepSeconds = 0.5f;

        void Start() => StartCoroutine(AiLoop());

        // UX-03: a drag or board gesture never survives losing the app focus (call, notification shade, home button).
        void OnApplicationFocus(bool focus) { if (!focus) Ui?.CancelPointers(); }
        void OnApplicationPause(bool paused) { if (paused) Ui?.CancelPointers(); }

        void Update()
        {
            Match.Tick(Time.deltaTime);
            Ui.Tick();
        }

        /// <summary>Runs the AI's turn one command at a time, so the UI stays responsive (AI-05).</summary>
        System.Collections.IEnumerator AiLoop()
        {
            var wait = new WaitForSeconds(AiStepSeconds);
            while (true)
            {
                if (Match.AiToAct && !Match.Paused)
                {
                    yield return wait;
                    if (Match.AiToAct && !Match.Paused) Match.StepAi();
                }
                else yield return null;
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (Instance == this) Instance = null;
        }
    }
}
