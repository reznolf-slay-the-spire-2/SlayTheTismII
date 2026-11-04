using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SlayTheTismII.SlayTheTismIICode;

/// <summary>
/// Replaces the main-menu title logo with a PNG shipped next to this DLL.
///
/// The vanilla logo is not a single texture: <c>MainMenuBg/BgContainer/Control/Logo</c> is a
/// <c>SpineSprite</c> playing a skeletal animation. Its slots are drawn by <c>SpineMesh2D</c> child
/// nodes, so hiding the sprite itself would hide our replacement too. Instead a discard shader is
/// pushed into the sprite's blend materials: it drops the atlas' text region (UV.x below the fire
/// column) and keeps the little potion flames, which the skeleton scatters around the title.
///
/// The replacement PNG is drawn over the text's bounds. Those bounds are re-read every frame,
/// because the logo drops into place while its intro animation plays, and the flames need to stay
/// glued to the art.
/// </summary>
internal static class TitleLogoPatch
{
    private const string LogoFileName = "Title.png";
    private const string ConfigFileName = "logo.json";

    /// <summary>Atlas X (0..1) where the text region ends and the fire column starts.</summary>
    private const float FireColumnX = 0.88f;

    private const string DiscardShader = """
        shader_type canvas_item;

        uniform float min_uv_x : hint_range(0.0, 1.0) = 0.88;

        void fragment() {
            if (UV.x < min_uv_x) {
                discard;
            }
        }
        """;

    private static Texture2D? _texture;
    private static bool _triedToLoad;

    [HarmonyPatch(typeof(NMainMenuBg), nameof(NMainMenuBg._Ready))]
    private static class NMainMenuBgReady
    {
        [HarmonyPostfix]
        private static void Postfix(NMainMenuBg __instance)
        {
            try
            {
                Node2D? logo = __instance.GetNodeOrNull<Node2D>("%Logo");
                if (logo is null)
                {
                    MainFile.Logger.Warn("Title logo: '%Logo' was not found on the main menu background.");
                    return;
                }

                LogoConfig config = LogoConfig.Load();

                if (config.HideVanilla)
                {
                    HideVanilla(logo, config.KeepFires);
                }

                Texture2D? texture = GetTexture();
                if (texture is null)
                {
                    return;
                }

                // The skeleton loads asynchronously; bounds are only meaningful once it is ready.
                var spine = new MegaSprite(logo);
                __instance.RunWhenSpineReady(spine, _ => Attach(__instance, logo, spine, texture, config));
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"Title logo: replacement failed: {e}");
            }
        }
    }

    private static void Attach(NMainMenuBg menu, Node2D logo, MegaSprite spine, Texture2D texture, LogoConfig config)
    {
        try
        {
            var replacement = new Sprite2D
            {
                Name = "TismLogo",
                Texture = texture,
                Centered = true,
                Modulate = new Color(1f, 1f, 1f, 0f),
            };

            logo.AddChild(replacement);
            // Draw under the skeleton's own slot meshes so the kept flames sit on top.
            logo.MoveChild(replacement, 0);

            // Follow the drop-in animation instead of sampling once and hoping it settled.
            SceneTree tree = menu.GetTree();
            void Sync()
            {
                if (!GodotObject.IsInstanceValid(logo) || !GodotObject.IsInstanceValid(replacement))
                {
                    tree.ProcessFrame -= Sync;
                    return;
                }

                SyncTransform(spine, replacement, texture, config);
            }

            tree.ProcessFrame += Sync;
            Sync();

            Tween fade = replacement.CreateTween();
            fade.TweenProperty(replacement, "modulate:a", 1f, Math.Max(0.01f, config.Fade))
                .SetDelay(Math.Max(0f, config.Delay))
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);

            MainFile.Logger.Info(
                $"Title logo: attached (delay {config.Delay}, fade {config.Fade}, " +
                $"scale {config.Scale}, offset {config.Offset}).");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Title logo: could not attach the replacement: {e}");
        }
    }

    /// <summary>
    /// Drops the vanilla text by discarding fragments from the atlas' text region. Keeping the fire
    /// column means the skeleton's potion flames (including the ones sitting on the "i" dots) survive.
    /// </summary>
    private static void HideVanilla(Node2D logo, bool keepFires)
    {
        try
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = DiscardShader } };
            material.SetShaderParameter("min_uv_x", keepFires ? FireColumnX : 1.01f);

            foreach (string setter in new[]
                     {
                         "set_normal_material", "set_additive_material",
                         "set_multiply_material", "set_screen_material",
                     })
            {
                try
                {
                    logo.Call(setter, material);
                }
                catch (Exception e)
                {
                    MainFile.Logger.Warn($"Title logo: {setter} failed: {e.Message}");
                }
            }

            MainFile.Logger.Info($"Title logo: vanilla text hidden (fires kept: {keepFires}).");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Title logo: could not hide the vanilla logo: {e}");
        }
    }

    private static void SyncTransform(MegaSprite spine, Sprite2D replacement, Texture2D texture, LogoConfig config)
    {
        Rect2 bounds = spine.GetSkeleton()?.GetBounds() ?? default;
        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
        {
            return;
        }

        float fit = Math.Min(bounds.Size.X / texture.GetWidth(), bounds.Size.Y / texture.GetHeight());

        replacement.Scale = Vector2.One * (fit * config.Scale);
        replacement.Position = bounds.Position + bounds.Size * 0.5f + config.Offset;
    }

    private static Texture2D? GetTexture()
    {
        if (_triedToLoad)
        {
            return _texture;
        }

        _triedToLoad = true;

        try
        {
            string? dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(string.IsNullOrEmpty(dllDir) ? "." : dllDir, LogoFileName);

            if (!File.Exists(path))
            {
                MainFile.Logger.Warn($"Title logo: '{path}' was not found; keeping the vanilla logo.");
                return null;
            }

            var image = new Image();
            Error error = image.LoadPngFromBuffer(File.ReadAllBytes(path));
            if (error != Error.Ok)
            {
                MainFile.Logger.Warn($"Title logo: could not decode '{path}': {error}");
                return null;
            }

            // Trim transparent padding so the art is fitted to the logo bounds, not to its canvas.
            Rect2I used = image.GetUsedRect();
            if (used.Size.X > 0 && used.Size.Y > 0 &&
                (used.Size.X != image.GetWidth() || used.Size.Y != image.GetHeight()))
            {
                MainFile.Logger.Info($"Title logo: trimming padding to {used.Size}.");
                image = image.GetRegion(used);
            }

            _texture = ImageTexture.CreateFromImage(image);
            MainFile.Logger.Info($"Title logo: loaded '{path}' ({image.GetWidth()}x{image.GetHeight()}).");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Title logo: could not load the logo image: {e}");
        }

        return _texture;
    }

    /// <summary>Optional <c>logo.json</c> next to the DLL.</summary>
    private sealed class LogoConfig
    {
        public float Delay { get; private init; } = 0f;
        public float Fade { get; private init; } = 0.6f;
        public float Scale { get; private init; } = 1f;
        public Vector2 Offset { get; private init; } = Vector2.Zero;
        public bool HideVanilla { get; private init; } = true;
        public bool KeepFires { get; private init; } = true;

        public static LogoConfig Load()
        {
            try
            {
                string? dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(string.IsNullOrEmpty(dllDir) ? "." : dllDir, ConfigFileName);
                if (!File.Exists(path))
                {
                    return new LogoConfig();
                }

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = doc.RootElement;

                float Number(string name, float fallback) =>
                    root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number
                        ? e.GetSingle()
                        : fallback;

                bool Flag(string name, bool fallback) =>
                    root.TryGetProperty(name, out JsonElement e) && e.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? e.GetBoolean()
                        : fallback;

                var config = new LogoConfig
                {
                    Delay = Number("delay", 0f),
                    Fade = Number("fade", 0.6f),
                    Scale = Number("scale", 1f),
                    Offset = new Vector2(Number("offset_x", 0f), Number("offset_y", 0f)),
                    HideVanilla = Flag("hide_vanilla", true),
                    KeepFires = Flag("keep_fires", true),
                };

                MainFile.Logger.Info(
                    $"Title logo: config delay={config.Delay}, fade={config.Fade}, scale={config.Scale}, " +
                    $"offset={config.Offset}, hide_vanilla={config.HideVanilla}, keep_fires={config.KeepFires}.");
                return config;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"Title logo: could not read {ConfigFileName}; using defaults: {e.Message}");
                return new LogoConfig();
            }
        }
    }
}
