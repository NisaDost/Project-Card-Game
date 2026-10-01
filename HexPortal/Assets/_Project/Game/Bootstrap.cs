using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace HexPortal.Game
{
    // Builds the scene from code (no manual scene setup): orientation, touch input, then the GameApp root.
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SetupOrientation();
            EnhancedTouchSupport.Enable(); // touch for the Input System; UI Toolkit routes touch and mouse to the UI
            Application.targetFrameRate = 60;
            GameApp.Create();
        }

        // Landscape only (GDD UX-01): auto-rotate between Landscape Left and Right, never portrait.
        static void SetupOrientation()
        {
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
