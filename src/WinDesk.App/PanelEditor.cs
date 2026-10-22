using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WinDesk.Core;
using Forms = System.Windows.Forms;

namespace WinDesk.App;

public sealed class PanelEditor : Window
{
    private readonly TextBox _name;
    private readonly TextBlock _error;
    private readonly List<RadioButton> _presets = new();
    private readonly TextBox _hex;
    private readonly Border _colorPreview;
    private bool _syncingColor;
    public string PanelName => _name.Text.Trim();
    public string PanelColor { get; private set; }

    public PanelEditor(PanelModel? panel, string? title = null, bool allowColor = true)
    {
        Title = title ?? (panel is null ? "Yeni panel" : "Paneli düzenle");
        Width = 400; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ShowInTaskbar = false;
        Background = ManagerWindow.Brush("#171F2C");
        Foreground = ManagerWindow.Brush("#EDF1F7");
        PanelColor = PanelModel.NormalizeColor(panel?.Color) ?? PanelModel.Colors[0];
        var body = new StackPanel { Margin = new Thickness(26) };
        body.Children.Add(new TextBlock { Text = Title, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 22) });
        body.Children.Add(new TextBlock { Text = allowColor ? "Panel adı" : "Alan adı", Margin = new Thickness(0, 0, 0, 8) });
        _name = new TextBox { Text = panel?.Name ?? "", MaxLength = 60 };
        System.Windows.Automation.AutomationProperties.SetName(_name, "Panel adı");
        body.Children.Add(_name);
        if (allowColor) body.Children.Add(new TextBlock { Text = "Vurgu rengi", Margin = new Thickness(0, 20, 0, 8) });
        var colors = new StackPanel { Orientation = Orientation.Horizontal };
        string[] names = { "Nane", "Lavanta", "Amber", "Mavi", "Pembe" };
        for (int i = 0; i < PanelModel.Colors.Length; i++)
        {
            var color = PanelModel.Colors[i];
            var radio = new RadioButton { GroupName = "Colors", IsChecked = PanelColor == color, Tag = color,
                Style = (Style)FindResource("ColorSwatch"), ToolTip = names[i], Background = ManagerWindow.Brush(color) };
            System.Windows.Automation.AutomationProperties.SetName(radio, names[i]);
            radio.Checked += (_, _) => { if (!_syncingColor) SelectColor(color); };
            _presets.Add(radio);
            colors.Children.Add(radio);
        }
        if (allowColor) body.Children.Add(colors);
        var customRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        customRow.ColumnDefinitions.Add(new ColumnDefinition());
        customRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _colorPreview = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5),
            BorderBrush = ManagerWindow.Brush("#718096"), BorderThickness = new Thickness(1), Background = ManagerWindow.Brush(PanelColor), Margin = new Thickness(0, 0, 10, 0) };
        var customContent = new StackPanel { Orientation = Orientation.Horizontal };
        customContent.Children.Add(_colorPreview);
        customContent.Children.Add(new TextBlock { Text = "Özel renk seç…", VerticalAlignment = VerticalAlignment.Center });
        var customButton = new Button { Content = customContent, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(11, 9, 11, 9) };
        System.Windows.Automation.AutomationProperties.SetName(customButton, "Özel renk seç");
        customButton.Click += (_, _) => ChooseCustomColor();
        customRow.Children.Add(customButton);
        _hex = new TextBox { Text = PanelColor, Width = 111, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"), ToolTip = "Renk kodu: #RRGGBB" };
        System.Windows.Automation.AutomationProperties.SetName(_hex, "Renk kodu");
        System.Windows.Automation.AutomationProperties.SetHelpText(_hex, "Altı haneli HEX renk kodu; örneğin #73D9C3.");
        _hex.TextChanged += (_, _) => {
            if (_syncingColor) return;
            var value = PanelModel.NormalizeColor(_hex.Text);
            if (value is not null) SelectColor(value, updateText: false);
        };
        _hex.LostKeyboardFocus += (_, _) => {
            var value = PanelModel.NormalizeColor(_hex.Text);
            if (value is not null) SelectColor(value);
        };
        Grid.SetColumn(_hex, 1); customRow.Children.Add(_hex);
        if (allowColor) body.Children.Add(customRow);
        _error = new TextBlock { Foreground = ManagerWindow.Brush("#FFABAB"), Margin = new Thickness(0, 12, 0, 8), TextWrapping = TextWrapping.Wrap };
        body.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Vazgeç", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var save = new Button { Content = panel is null ? "Oluştur" : "Kaydet", IsDefault = true, Style = (Style)FindResource("AccentButton") };
        save.Click += (_, _) => {
            if (PanelName.Length == 0) { _error.Text = "Panele bir ad verin."; _name.Focus(); return; }
            if (allowColor && PanelModel.NormalizeColor(_hex.Text) is null) { _error.Text = "Altı haneli bir renk kodu girin. Örnek: #73D9C3"; _hex.Focus(); return; }
            DialogResult = true;
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); body.Children.Add(buttons);
        Content = body;
        Loaded += (_, _) => {
            // A quiet background start may carry SW_HIDE in STARTUPINFO.
            // An explicit editor action must still open a visible dialog.
            DesktopNative.ShowWindow(new WindowInteropHelper(this).Handle, 5);
            Activate(); _name.Focus(); _name.SelectAll();
        };
    }

    private void SelectColor(string color, bool updateText = true)
    {
        _syncingColor = true;
        try
        {
            PanelColor = color;
            foreach (var preset in _presets) preset.IsChecked = (string)preset.Tag == color;
            _colorPreview.Background = ManagerWindow.Brush(color);
            if (updateText) _hex.Text = color;
            _error.Text = "";
        }
        finally { _syncingColor = false; }
    }

    private void ChooseCustomColor()
    {
        using var dialog = new Forms.ColorDialog { FullOpen = true, AnyColor = true,
            Color = System.Drawing.ColorTranslator.FromHtml(PanelColor) };
        if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK)
            SelectColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }

    private sealed class DialogOwner(IntPtr handle) : Forms.IWin32Window
    { public IntPtr Handle { get; } = handle; }
}
