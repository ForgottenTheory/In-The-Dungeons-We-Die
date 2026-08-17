using System;
using Godot;

namespace Dungeons.Game.Ui;

/// <summary>
/// The developer console's code-only look, and the handful of control builders every panel
/// makes its rows out of. There are no art assets, so the palette and the styleboxes are the
/// whole theme.
///
/// <para>Extracted from <see cref="MainMvpUI"/> when the Hideout split the one screen into
/// per-station panels: the panels needed the same colours and the same <c>Row</c>/<c>Card</c>
/// vocabulary, and copying them would have been three palettes drifting apart by the second
/// change. Call sites read unchanged because the UI files import it with
/// <c>using static</c>.</para>
/// </summary>
public static class ConsoleTheme
{
    public static readonly Color BackgroundColor = new(0.11f, 0.12f, 0.15f);
    public static readonly Color PanelColor = new(0.16f, 0.18f, 0.22f);
    public static readonly Color CardColor = new(0.20f, 0.23f, 0.28f);
    public static readonly Color Accent = new(0.29f, 0.62f, 1.0f);
    public static readonly Color Positive = new(0.30f, 0.72f, 0.36f);
    public static readonly Color Danger = new(0.90f, 0.36f, 0.32f);
    public static readonly Color TextColor = new(0.84f, 0.86f, 0.90f);
    public static readonly Color Muted = new(0.55f, 0.58f, 0.64f);
    public static readonly Color Border = new(0.28f, 0.32f, 0.38f);

    public static Label SectionTitle(string text)
    {
        var label = new Label { Text = text.ToUpperInvariant() };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", Accent);
        return label;
    }

    public static HBoxContainer Row(int separation = 8)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    /// <summary>
    /// A Label that wraps on words and actually takes the width available to it.
    ///
    /// <para><b>Only put one of these in a VBoxContainer or a Card.</b> An autowrapping Label
    /// inside an HBoxContainer collapses to its minimum width and wraps a single character per
    /// line — the row gives it no width to work with.</para>
    /// </summary>
    public static Label Wrapping(Color? color = null)
    {
        var label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 0),
        };

        if (color.HasValue)
            label.AddThemeColorOverride("font_color", color.Value);

        return label;
    }

    public static PanelContainer Card(Control inner)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Flat(CardColor, 6, 10, Border, 1));
        panel.AddChild(inner);
        return panel;
    }

    public static Button MakeButton(string text, Action onPressed, Color? accentText = null)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        if (accentText.HasValue)
            button.AddThemeColorOverride("font_color", accentText.Value);
        return button;
    }

    /// <summary>A control pushed right so it reads as belonging to the row above it.</summary>
    public static Control Indent(Control control, int width = 98)
    {
        var row = new HBoxContainer();
        row.AddChild(new Control { CustomMinimumSize = new Vector2(width, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    /// <summary>
    /// Empties a container. <c>RemoveChild</c> first: <c>QueueFree</c> is deferred, so freeing
    /// alone would leave the old rows on screen for a frame and the group would look duplicated.
    /// </summary>
    public static void ClearChildren(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static StyleBoxFlat Flat(Color bg, int radius, int pad, Color? border = null, int borderWidth = 0)
    {
        var box = new StyleBoxFlat { BgColor = bg };
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(pad);
        if (border.HasValue && borderWidth > 0)
        {
            box.BorderColor = border.Value;
            box.SetBorderWidthAll(borderWidth);
        }

        return box;
    }

    public static Theme Build()
    {
        var theme = new Theme();

        theme.SetColor("font_color", "Label", TextColor);
        theme.SetFontSize("font_size", "Label", 13);

        theme.SetStylebox("normal", "Button", Flat(PanelColor, 5, 8, Border, 1));
        theme.SetStylebox("hover", "Button", Flat(CardColor, 5, 8, Accent, 1));
        theme.SetStylebox("pressed", "Button", Flat(Accent, 5, 8));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", TextColor);
        theme.SetColor("font_hover_color", "Button", new Color(1, 1, 1));
        theme.SetColor("font_pressed_color", "Button", new Color(1, 1, 1));
        theme.SetFontSize("font_size", "Button", 13);

        theme.SetStylebox("panel", "PanelContainer", Flat(PanelColor, 8, 10, Border, 1));

        theme.SetStylebox("panel", "TabContainer", Flat(PanelColor, 8, 10, Border, 1));
        theme.SetStylebox("tab_selected", "TabContainer", Flat(Accent, 5, 8));
        theme.SetStylebox("tab_unselected", "TabContainer", Flat(CardColor, 5, 8));
        theme.SetStylebox("tab_hovered", "TabContainer", Flat(CardColor, 5, 8, Accent, 1));
        theme.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());
        theme.SetColor("font_selected_color", "TabContainer", new Color(1, 1, 1));
        theme.SetColor("font_unselected_color", "TabContainer", Muted);
        theme.SetColor("font_hovered_color", "TabContainer", TextColor);

        theme.SetStylebox("background", "ProgressBar", Flat(BackgroundColor, 4, 0));
        theme.SetStylebox("fill", "ProgressBar", Flat(Accent, 4, 0));
        theme.SetColor("font_color", "ProgressBar", TextColor);

        theme.SetColor("default_color", "RichTextLabel", TextColor);
        theme.SetFontSize("normal_font_size", "RichTextLabel", 12);
        return theme;
    }
}
