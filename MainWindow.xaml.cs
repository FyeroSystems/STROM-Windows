using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using STROM.Models;
using STROM.Services;
using System.Text.Json.Nodes;

namespace STROM;

public sealed partial class MainWindow : Window
{
    private readonly DirigeraClient _client = new();
    private List<Outlet> _outlets = new();
    private List<ControllerInfo> _controllers = new();
    private List<DiscoveredDevice> _discovered = new();
    private HashSet<string> _selectedIds = new();
    private string _authCode = "";
    private string _codeVerifier = "";
    private StackPanel? _manageContainer;

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "STRÖM";

        // Fenstergröße setzen (WinUI 3 erlaubt Width/Height nicht direkt im XAML)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(800, 700));

        LoadData();
        BuildSettingsUI();
        RefreshControlUI();
        RefreshControllerUI();
    }

    private void LoadData()
    {
        var (ip, token, outlets) = SettingsService.Load();
        _client.HubIp = ip;
        _client.Token = token;
        _outlets = outlets;
    }

    private void SaveData()
    {
        SettingsService.Save(_client.HubIp, _client.Token, _outlets);
    }

    private void RefreshControlUI()
    {
        ControlPanel.Children.Clear();
        ControlPanel.Children.Add(new TextBlock { Text = "STRÖM", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        if (_outlets.Count == 0)
        {
            ControlPanel.Children.Add(new TextBlock { Text = "Noch keine Geräte angelegt – im Einstellungen-Tab hinzufügen oder suchen.", Opacity = 0.7 });
        }
        else
        {
            var groups = _outlets.GroupBy(o => string.IsNullOrEmpty(o.Group) ? "Allgemein" : o.Group).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                ControlPanel.Children.Add(new TextBlock { Text = g.Key, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 4) });
                foreach (var o in g)
                    ControlPanel.Children.Add(CreateOutletControl(o));
            }
        }

        var refreshBtn = new Button { Content = "Status aktualisieren", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 12, 0, 0) };
        refreshBtn.Click += async (_, _) => await LoadStatesAsync();
        ControlPanel.Children.Add(refreshBtn);
    }

    private FrameworkElement CreateOutletControl(Outlet o)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        var header = new Grid();
        header.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(o.Name) ? "Unbenannt" : o.Name, VerticalAlignment = VerticalAlignment.Center });
        var toggle = new ToggleSwitch { IsOn = o.IsOn, HorizontalAlignment = HorizontalAlignment.Right, OffContent = "", OnContent = "" };
        toggle.Toggled += async (_, _) =>
        {
            o.IsOn = toggle.IsOn;
            try { await _client.SetOnAsync(o.Id, o.IsOn); } catch { }
            SaveData();
        };
        header.Children.Add(toggle);
        panel.Children.Add(header);

        if (o.LightLevel.HasValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = "☀", VerticalAlignment = VerticalAlignment.Center });
            var slider = new Slider { Minimum = 1, Maximum = 100, Value = o.LightLevel.Value, Width = 220 };
            slider.ValueChanged += async (_, e) =>
            {
                o.LightLevel = (int)e.NewValue;
                try { await _client.SetLightLevelAsync(o.Id, o.LightLevel.Value); } catch { }
                SaveData();
            };
            row.Children.Add(slider);
            row.Children.Add(new TextBlock { Text = $"{o.LightLevel}%", Width = 40, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }

        if (o.ColorTemperature.HasValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = "🌡", VerticalAlignment = VerticalAlignment.Center });
            var slider = new Slider { Minimum = 2200, Maximum = 4000, Value = o.ColorTemperature.Value, Width = 220 };
            var label = new TextBlock { Text = ColorTempLabel(o.ColorTemperature.Value), Width = 60, VerticalAlignment = VerticalAlignment.Center };
            slider.ValueChanged += async (_, e) =>
            {
                o.ColorTemperature = (int)e.NewValue;
                label.Text = ColorTempLabel(o.ColorTemperature.Value);
                try { await _client.SetColorTemperatureAsync(o.Id, o.ColorTemperature.Value); } catch { }
                SaveData();
            };
            row.Children.Add(slider);
            row.Children.Add(label);
            panel.Children.Add(row);
        }

        if (o.ColorHue.HasValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = "🎨", VerticalAlignment = VerticalAlignment.Center });
            var slider = new Slider { Minimum = 0, Maximum = 360, Value = o.ColorHue.Value, Width = 220 };
            slider.ValueChanged += async (_, e) =>
            {
                o.ColorHue = e.NewValue;
                try { await _client.SetColorAsync(o.Id, o.ColorHue.Value, o.ColorSaturation ?? 1.0); } catch { }
                SaveData();
            };
            row.Children.Add(slider);
            row.Children.Add(new TextBlock { Text = "Hue", Width = 40, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }

        if (o.ColorSaturation.HasValue)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = "💧", VerticalAlignment = VerticalAlignment.Center });
            var slider = new Slider { Minimum = 0, Maximum = 1, Value = o.ColorSaturation.Value, Width = 220, StepFrequency = 0.01 };
            var label = new TextBlock { Text = $"{(int)(o.ColorSaturation.Value * 100)}%", Width = 40, VerticalAlignment = VerticalAlignment.Center };
            slider.ValueChanged += async (_, e) =>
            {
                o.ColorSaturation = e.NewValue;
                label.Text = $"{(int)(o.ColorSaturation.Value * 100)}%";
                try { await _client.SetColorAsync(o.Id, o.ColorHue ?? 0, o.ColorSaturation.Value); } catch { }
                SaveData();
            };
            row.Children.Add(slider);
            row.Children.Add(label);
            panel.Children.Add(row);
        }

        return new Border { Child = panel, BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8) };
    }

    private string ColorTempLabel(int k) => k < 2700 ? "Warm" : k > 3500 ? "Kalt" : "Neutral";

    private async Task LoadStatesAsync()
    {
        try
        {
            var devices = await _client.GetDevicesAsync();
            foreach (var d in devices)
            {
                var id = d["id"]?.ToString();
                if (id == null) continue;
                var outlet = _outlets.FirstOrDefault(o => o.Id == id);
                if (outlet == null) continue;
                var attr = d["attributes"]?.AsObject();
                if (attr == null) continue;
                if (attr["isOn"] is JsonValue isOn) outlet.IsOn = isOn.GetValue<bool>();
                if (attr["lightLevel"] is JsonValue ll) outlet.LightLevel = ll.GetValue<int>();
                if (attr["colorTemperature"] is JsonValue ct) outlet.ColorTemperature = ct.GetValue<int>();
                if (attr["colorHue"] is JsonValue ch) outlet.ColorHue = ch.GetValue<double>();
                if (attr["colorSaturation"] is JsonValue cs) outlet.ColorSaturation = cs.GetValue<double>();
            }
            SaveData();
            RefreshControlUI();
        }
        catch { }
    }

    private void RefreshControllerUI()
    {
        ControllerPanel.Children.Clear();
        ControllerPanel.Children.Add(new TextBlock { Text = "Taster & Zuweisungen", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var loadBtn = new Button { Content = "Taster laden" };
        loadBtn.Click += async (_, _) => await LoadControllersAsync();
        ControllerPanel.Children.Add(loadBtn);

        if (_controllers.Count == 0)
        {
            ControllerPanel.Children.Add(new TextBlock { Text = "Noch keine Taster geladen oder keine vorhanden.", Opacity = 0.7 });
            return;
        }

        foreach (var c in _controllers)
        {
            var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 8) };
            var nameBox = new TextBox { Text = c.Name, PlaceholderText = "Name" };
            nameBox.LostFocus += async (_, _) =>
            {
                c.Name = nameBox.Text;
                try { await _client.SetControllerNameAsync(c.Id, c.Name); } catch { }
            };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(nameBox);
            if (c.BatteryPercentage.HasValue)
                header.Children.Add(new TextBlock { Text = $"{c.BatteryPercentage}%", VerticalAlignment = VerticalAlignment.Center, Foreground = c.BatteryPercentage < 20 ? new SolidColorBrush(Microsoft.UI.Colors.Red) : null });
            panel.Children.Add(header);

            foreach (var linked in c.LinkedDeviceIDs.ToList())
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var name = _outlets.FirstOrDefault(o => o.Id == linked)?.Name ?? linked.Substring(0, Math.Min(12, linked.Length)) + "…";
                row.Children.Add(new TextBlock { Text = $"→ {name}", VerticalAlignment = VerticalAlignment.Center });
                var removeBtn = new Button { Content = "−", Padding = new Thickness(8, 2, 8, 2) };
                var lid = linked;
                removeBtn.Click += async (_, _) =>
                {
                    await UpdateLinkAsync(c.Id, lid, remove: true);
                    c.LinkedDeviceIDs.Remove(lid);
                    RefreshControllerUI();
                };
                row.Children.Add(removeBtn);
                panel.Children.Add(row);
            }

            if (_outlets.Count > 0)
            {
                var menu = new MenuFlyout();
                foreach (var o in _outlets.Where(x => !c.LinkedDeviceIDs.Contains(x.Id)))
                {
                    var item = new MenuFlyoutItem { Text = string.IsNullOrEmpty(o.Name) ? o.Id : o.Name };
                    var oid = o.Id;
                    item.Click += async (_, _) =>
                    {
                        await UpdateLinkAsync(c.Id, oid, remove: false);
                        if (!c.LinkedDeviceIDs.Contains(oid)) c.LinkedDeviceIDs.Add(oid);
                        RefreshControllerUI();
                    };
                    menu.Items.Add(item);
                }
                var addBtn = new Button { Content = "Gerät hinzufügen" };
                addBtn.Flyout = menu;
                panel.Children.Add(addBtn);
            }

            ControllerPanel.Children.Add(new Border { Child = panel, BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8) });
        }
    }

    private async Task LoadControllersAsync()
    {
        try
        {
            var devices = await _client.GetDevicesAsync();
            var map = new Dictionary<string, (string name, int? bat)>();
            var links = new Dictionary<string, List<string>>();

            foreach (var d in devices)
            {
                var id = d["id"]?.ToString();
                var type = d["type"]?.ToString();
                if (id == null) continue;
                if (type is "controller" or "remote")
                {
                    var attr = d["attributes"]?.AsObject();
                    var name = attr?["customName"]?.ToString() ?? "Taster";
                    int? bat = attr?["batteryPercentage"] is JsonValue bv ? bv.GetValue<int>() : null;
                    map[id] = (name, bat);
                }
                else if (type is "light" or "outlet")
                {
                    if (d["remoteLinks"] is JsonArray arr)
                    {
                        foreach (var link in arr)
                        {
                            var cid = link?.ToString();
                            if (cid == null) continue;
                            if (!links.ContainsKey(cid)) links[cid] = new();
                            links[cid].Add(id);
                        }
                    }
                }
            }

            _controllers = map.Select(kv => new ControllerInfo
            {
                Id = kv.Key,
                Name = kv.Value.name,
                BatteryPercentage = kv.Value.bat,
                LinkedDeviceIDs = links.GetValueOrDefault(kv.Key) ?? new()
            }).OrderBy(c => c.Name).ToList();

            RefreshControllerUI();
        }
        catch { }
    }

    private async Task UpdateLinkAsync(string controllerId, string deviceId, bool remove)
    {
        try
        {
            var devices = await _client.GetDevicesAsync();
            var device = devices.FirstOrDefault(d => d["id"]?.ToString() == deviceId);
            if (device == null) return;
            var current = device["remoteLinks"]?.AsArray().Select(n => n!.ToString()).ToList() ?? new List<string>();
            if (remove) current.Remove(controllerId);
            else if (!current.Contains(controllerId)) current.Add(controllerId);
            await _client.UpdateRemoteLinksAsync(deviceId, current);
        }
        catch { }
    }

    private void BuildSettingsUI()
    {
        SettingsPanel.Children.Clear();
        SettingsPanel.Children.Add(new TextBlock { Text = "Einstellungen", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        SettingsPanel.Children.Add(new TextBlock { Text = "IP-Adresse des Hubs", Opacity = 0.7 });
        var ipBox = new TextBox { Text = _client.HubIp, PlaceholderText = "192.168.1.XX" };
        ipBox.TextChanged += (_, _) => { _client.HubIp = ipBox.Text; SaveData(); };
        SettingsPanel.Children.Add(ipBox);

        SettingsPanel.Children.Add(new TextBlock { Text = "Token", Opacity = 0.7 });
        var tokenBox = new TextBox { Text = _client.Token, PlaceholderText = "Bearer Token" };
        tokenBox.TextChanged += (_, _) => { _client.Token = tokenBox.Text; SaveData(); };
        SettingsPanel.Children.Add(tokenBox);

        var startTokenBtn = new Button { Content = "Neuen Token erzeugen" };
        var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange) };
        var finishBtn = new Button { Content = "Taste gedrückt – Token holen", Visibility = Visibility.Collapsed };
        startTokenBtn.Click += async (_, _) =>
        {
            statusText.Text = "Starte Authentifizierung…";
            try
            {
                var (code, verifier) = await _client.StartTokenGenerationAsync();
                _authCode = code;
                _codeVerifier = verifier;
                statusText.Text = "Challenge gesendet. Bitte Taste am Hub drücken.";
                finishBtn.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { statusText.Text = "Fehler: " + ex.Message; }
        };
        finishBtn.Click += async (_, _) =>
        {
            statusText.Text = "Hole Token…";
            try
            {
                var token = await _client.FinishTokenGenerationAsync(_authCode, _codeVerifier);
                if (!string.IsNullOrEmpty(token))
                {
                    _client.Token = token;
                    tokenBox.Text = token;
                    statusText.Text = "Token erfolgreich erzeugt und gespeichert!";
                    finishBtn.Visibility = Visibility.Collapsed;
                    SaveData();
                }
                else statusText.Text = "Token konnte nicht gelesen werden.";
            }
            catch (Exception ex) { statusText.Text = "Fehler: " + ex.Message; }
        };
        SettingsPanel.Children.Add(startTokenBtn);
        SettingsPanel.Children.Add(statusText);
        SettingsPanel.Children.Add(finishBtn);

        var discoverBtn = new Button { Content = "Geräte suchen" };
        var discoveredPanel = new StackPanel();
        var importBtn = new Button { Content = "Ausgewählte übernehmen", IsEnabled = false };
        discoverBtn.Click += async (_, _) =>
        {
            discoveredPanel.Children.Clear();
            _selectedIds.Clear();
            try
            {
                var devices = await _client.GetDevicesAsync();
                _discovered.Clear();
                foreach (var d in devices)
                {
                    var type = d["type"]?.ToString();
                    if (type is not ("light" or "outlet")) continue;
                    var id = d["id"]?.ToString() ?? "";
                    var attr = d["attributes"]?.AsObject();
                    var name = attr?["customName"]?.ToString() ?? "Unbenannt";
                    _discovered.Add(new DiscoveredDevice
                    {
                        Id = id,
                        Name = name,
                        Type = type ?? "",
                        IsOn = attr?["isOn"] is JsonValue v && v.GetValue<bool>(),
                        LightLevel = type == "light" && attr?["lightLevel"] is JsonValue ll ? ll.GetValue<int>() : null,
                        ColorTemperature = type == "light" && attr?["colorTemperature"] is JsonValue ct ? ct.GetValue<int>() : null,
                        ColorHue = type == "light" && attr?["colorHue"] is JsonValue ch ? ch.GetValue<double>() : null,
                        ColorSaturation = type == "light" && attr?["colorSaturation"] is JsonValue cs ? cs.GetValue<double>() : null
                    });
                }
                foreach (var dev in _discovered.OrderBy(d => d.Name))
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    var cb = new CheckBox();
                    var did = dev.Id;
                    cb.Checked += (_, _) => { _selectedIds.Add(did); importBtn.IsEnabled = _selectedIds.Count > 0; };
                    cb.Unchecked += (_, _) => { _selectedIds.Remove(did); importBtn.IsEnabled = _selectedIds.Count > 0; };
                    if (_outlets.Any(o => o.Id == did)) cb.IsChecked = true;
                    row.Children.Add(cb);
                    var info = $"{dev.Type}{(dev.LightLevel.HasValue ? " • dimmbar" : "")}{(dev.ColorTemperature.HasValue ? " • Farbtemp." : "")}{(dev.ColorHue.HasValue ? " • RGB" : "")} • {dev.Id[..Math.Min(8, dev.Id.Length)]}…";
                    row.Children.Add(new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = dev.Name },
                            new TextBlock { Text = info, Opacity = 0.6, FontSize = 12 }
                        }
                    });
                    discoveredPanel.Children.Add(row);
                }
            }
            catch (Exception ex) { statusText.Text = "Suche fehlgeschlagen: " + ex.Message; }
        };
        importBtn.Click += (_, _) =>
        {
            foreach (var dev in _discovered.Where(d => _selectedIds.Contains(d.Id)))
            {
                if (!_outlets.Any(o => o.Id == dev.Id))
                {
                    _outlets.Add(new Outlet
                    {
                        Id = dev.Id,
                        Name = dev.Name,
                        Group = "Allgemein",
                        IsOn = dev.IsOn,
                        LightLevel = dev.LightLevel,
                        ColorTemperature = dev.ColorTemperature,
                        ColorHue = dev.ColorHue,
                        ColorSaturation = dev.ColorSaturation
                    });
                }
            }
            SaveData();
            discoveredPanel.Children.Clear();
            importBtn.IsEnabled = false;
            RefreshControlUI();
            BuildManageUI();
        };
        SettingsPanel.Children.Add(discoverBtn);
        SettingsPanel.Children.Add(discoveredPanel);
        SettingsPanel.Children.Add(importBtn);

        SettingsPanel.Children.Add(new TextBlock { Text = "Geräte verwalten", Margin = new Thickness(0, 16, 0, 0), Opacity = 0.7 });
        _manageContainer = new StackPanel { Spacing = 6 };
        SettingsPanel.Children.Add(_manageContainer);
        BuildManageUI();

        var addManual = new Button { Content = "Manuell hinzufügen" };
        addManual.Click += (_, _) =>
        {
            _outlets.Add(new Outlet { Id = "", Name = "Neue Steckdose", Group = "Allgemein" });
            SaveData();
            BuildManageUI();
            RefreshControlUI();
        };
        SettingsPanel.Children.Add(addManual);
    }

    private void BuildManageUI()
    {
        if (_manageContainer == null) return;
        _manageContainer.Children.Clear();
        foreach (var o in _outlets.ToList())
        {
            var row = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 4) };
            var nameBox = new TextBox { Text = o.Name, PlaceholderText = "Name" };
            nameBox.TextChanged += (_, _) => { o.Name = nameBox.Text; SaveData(); RefreshControlUI(); };
            var groupBox = new TextBox { Text = o.Group, PlaceholderText = "Gruppe", Width = 120 };
            groupBox.TextChanged += (_, _) => { o.Group = groupBox.Text; SaveData(); RefreshControlUI(); };
            var idBox = new TextBox { Text = o.Id, PlaceholderText = "Geräte-ID", FontSize = 12 };
            idBox.TextChanged += (_, _) => { o.Id = idBox.Text; SaveData(); };
            var delBtn = new Button { Content = "🗑" };
            delBtn.Click += (_, _) =>
            {
                _outlets.Remove(o);
                SaveData();
                BuildManageUI();
                RefreshControlUI();
            };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(nameBox);
            header.Children.Add(groupBox);
            header.Children.Add(delBtn);
            row.Children.Add(header);
            row.Children.Add(idBox);
            _manageContainer.Children.Add(row);
        }
    }
}
