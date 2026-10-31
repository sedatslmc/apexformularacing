using Apex.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// UI Toolkit root of the APEX // RACE CONTROL interface (docs/UI_UX_OVERHAUL.md): a UIDocument with runtime
    /// PanelSettings (1920 x 1080 reference, scaled by height), the Barlow fonts and small style helpers that turn
    /// <see cref="UiTokens"/> into UI Toolkit styles. Built entirely in code (no UXML/USS assets).
    /// <para>Pointer input (mouse, touch) is read from the Input System and hit-tested with <see cref="IPanel.Pick"/>,
    /// so the menus work without an EventSystem in the scene; keyboard and gamepad come from the game's navigation.</para>
    /// </summary>
    public sealed class ApexUi
    {
        public readonly VisualElement root;
        public readonly UIDocument document;
        public static Font Display, DisplayStrong, Body, BodyStrong;

        public ApexUi(string name, int sortingOrder)
        {
            LoadFonts();
            var go = new GameObject(name);
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = name + " Panel";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            settings.sortingOrder = sortingOrder;
            // Every element is styled in code; an empty theme keeps UI Toolkit from warning about a missing one.
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);
            root = document.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;
        }

        static void LoadFonts()
        {
            if (Display != null) return;
            Font Load(string n) => Resources.Load<Font>("Fonts/" + n);
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Display = Load("BarlowCondensed-SemiBold") ?? fallback;
            DisplayStrong = Load("BarlowCondensed-Bold") ?? Display;
            Body = Load("Barlow-Medium") ?? fallback;
            BodyStrong = Load("Barlow-SemiBold") ?? Body;
        }

        // --- Style helpers -------------------------------------------------------------------------------------

        public static Color C(UiTokens.Rgb c, float a = 1f) => new Color(c.r, c.g, c.b, a);

        public static VisualElement Box(VisualElement parent, Color background)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.backgroundColor = background;
            parent?.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string text, Font font, int size, Color colour, float letterSpacing = 0f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            if (letterSpacing != 0f) l.style.letterSpacing = letterSpacing;
            parent?.Add(l);
            return l;
        }

        /// <summary>Small upper-case label in Steel (section headings, card titles).</summary>
        public static Label Caption(VisualElement parent, string text) =>
            Text(parent, text.ToUpperInvariant(), BodyStrong, UiTokens.Label, C(UiTokens.Steel), 2f);

        public static void Place(VisualElement e, float left, float top, float width = -1f, float height = -1f)
        {
            e.style.position = Position.Absolute;
            e.style.left = left;
            e.style.top = top;
            if (width >= 0f) e.style.width = width;
            if (height >= 0f) e.style.height = height;
        }

        /// <summary>Panel: Carbon at the given opacity with a hairline border (the design system's surface).</summary>
        public static VisualElement Panel(VisualElement parent, float alpha = UiTokens.PanelStrongAlpha)
        {
            var p = Box(parent, C(UiTokens.Carbon, alpha));
            var hair = C(UiTokens.Steel, UiTokens.HairlineAlpha);
            p.style.borderLeftWidth = 1; p.style.borderRightWidth = 1; p.style.borderTopWidth = 1; p.style.borderBottomWidth = 1;
            p.style.borderLeftColor = hair; p.style.borderRightColor = hair; p.style.borderTopColor = hair; p.style.borderBottomColor = hair;
            return p;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Gradients = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>A horizontal (or vertical) alpha ramp of one colour as a texture: gradients for shades and highlights.</summary>
        public static Texture2D Ramp(Color c, float alphaStart, float alphaEnd, bool vertical = false, float hold = 0f)
        {
            string key = $"{c}{alphaStart}{alphaEnd}{vertical}{hold}";
            if (Gradients.TryGetValue(key, out var t) && t != null) return t;
            const int n = 256;
            t = vertical ? new Texture2D(1, n, TextureFormat.RGBA32, false) : new Texture2D(n, 1, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);
                float k = u <= hold ? 0f : (u - hold) / Mathf.Max(1e-4f, 1f - hold);
                var px = new Color(c.r, c.g, c.b, Mathf.Lerp(alphaStart, alphaEnd, k * k * (3f - 2f * k)));
                if (vertical) t.SetPixel(0, n - 1 - i, px); else t.SetPixel(i, 0, px);
            }
            t.Apply(false);
            Gradients[key] = t;
            return t;
        }

        public static void Background(VisualElement e, Texture2D t)
        {
            e.style.backgroundImage = new StyleBackground(t);
#pragma warning disable CS0618 // backgroundSize replaces it in Unity 6; the 2021.3 reference has only this one.
            e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#pragma warning restore CS0618
        }

        public static void Translate(VisualElement e, float x, float y) => e.style.translate = new StyleTranslate(new Translate(new Length(x), new Length(y), 0f));
        public static void Scale(VisualElement e, float s) => e.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));

        // --- Pointer -----------------------------------------------------------------------------------------

        Vector2 lastPointer = new Vector2(-1f, -1f);

        /// <summary>
        /// Pointer this frame in panel coordinates, whether it moved (hover may take the focus) and whether it was
        /// released this frame (a click or tap).
        /// </summary>
        public bool ReadPointer(out Vector2 panelPos, out bool moved, out bool released)
        {
            panelPos = default; moved = false; released = false;
            var p = Pointer.current;
            if (p == null || root.panel == null) return false;
            var screen = p.position.ReadValue();
            panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            moved = (screen - lastPointer).sqrMagnitude > 4f;
            lastPointer = screen;
            released = p.press.wasReleasedThisFrame;
            return true;
        }

        /// <summary>The nearest element with userData of type T under a panel position (walks up from the picked one).</summary>
        public T PickData<T>(Vector2 panelPos) where T : class
        {
            var e = root.panel?.Pick(panelPos);
            while (e != null)
            {
                if (e.userData is T t) return t;
                e = e.parent;
            }
            return null;
        }

        public void SetVisible(bool on) => root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
