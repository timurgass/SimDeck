using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using SimDeck.Core;

namespace SimDeck.App;
public partial class MainWindow : Window
{
    CompanionHost? host;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    bool closing;
    bool closeRequested;
    readonly ObservableCollection<ButtonRow> rows = [];
    public MainWindow()
    {
        InitializeComponent();
        ButtonGrid.ItemsSource = rows;
        GestureColumn.ItemsSource = new[] { "Нажатие", "Удержание", "Нажать → держать" };
        Loaded += async (_, _) =>
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var option = Array.IndexOf(args, "--data-dir");
                var directory = option >= 0 && args.Length > option + 1 ? args[option + 1] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimDeck");
                host = new(directory);
                ProfilePicker.ItemsSource = host.Store.Value.Profiles;
                ProfilePicker.SelectedValue = host.Profile.Id;
                CompatibleInput.IsChecked = host.Store.Value.UseVirtualKeyInput;
                LoadEditor();
                await host.StartAsync();
                FingerprintText.Text = host.Fingerprint;
                AddressText.Text = "Резервный адрес: " + string.Join(" / ", Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => $"{a}:{host.Store.Value.Port}"));
                PairButton.IsEnabled = true;
                if (args.Contains("--safari"))
                {
                    var infoOption = Array.IndexOf(args, "--safari-info");
                    var infoFile = infoOption >= 0 && args.Length > infoOption + 1 ? args[infoOption + 1] : null;
                    await StartBrowserAsync(infoFile);
                }
                timer.Tick += (_, _) => Refresh();
                timer.Start();
                if (args.Contains("--demo")) DemoBox.IsChecked = true;
            }
            catch (Exception ex) { ErrorLabel.Text = "Не удалось запустить Companion: " + ex.Message; ConnectionStatus.Text = "Ошибка запуска"; }
        };
    }
    void Refresh()
    {
        if (host is null) return;
        ConnectionStatus.Text = host.Status;
        DeviceLabel.Text = host.Device;
        if (!host.Pairing.IsOpen) PairCode.Text = "";
        if (host.Browser?.Pairing.IsOpen != true) BrowserCode.Text = "";
        var t = host.Telemetry.Read();
        var fresh = t.Data is not null && t.Age < 500;
        var fs25 = host.Profile.Id == "fs25" ? t.Data?.Fs25 : null;
        SourceLabel.Text = t.Source == "demo" ? "ДЕМОНСТРАЦИЯ · не данные игры" : fs25 is not null
            ? "FS25 · данные последнего сохранения" : host.Profile.Name + (fresh ? " · данные поступают" : " · нет свежих данных");
        SpeedLabel.Text = host.Profile.Id == "fs25" ? "—" : fresh ? (t.Data!.SpeedMps * 3.6).ToString("0") : "—";
        RpmLabel.Text = host.Profile.Id == "fs25" ? "СЕЙВ FS25" : fresh ? $"{t.Data!.Rpm:0} RPM   /   {t.Data.GearDisplay}" : "— RPM   /   —";
        TelemetryHelp.Text = host.Profile.Id == "fs25" ? host.TelemetryDiagnostic : fresh ? host.Profile.Id is "f1-24" or "f1-25" ? $"UDP F1 · принято {host.ReceivedPackets}, отклонено {host.InvalidPackets}" : host.Profile.Id == "beamng-default" && t.Data!.Headlights is null
            ? "Старый поток без состояния кнопок. Перезапустите BeamNG после обновления мода."
            : host.Profile.Id == "beamng-default" ? "Состояния кнопок поступают · " + (t.Data!.Headlights == 2 ? "дальний свет" : t.Data.Headlights == 1 ? "ближний свет" : "фары выключены")
            : host.Profile.Id == "acc" ? $"ACC Shared Memory · принято {host.ReceivedPackets} кадров · шины и тормоза доступны на пульте"
            : host.Profile.Id == "ets2" ? $"SCS Telemetry · принято {host.ReceivedPackets} кадров · состояния кнопок поступают на пульт"
            : "Демонстрационные данные" : host.TelemetryDiagnostic;
        var foreground = host.Backend.ForegroundProcessName;
        InputStatus.Text = host.Backend.Demo ? "Демонстрация · ввод отключён" : !host.Backend.Enabled ? "Выключен · установите галочку ниже" : host.Backend.CanInject
            ? $"Готов · {host.Profile.TargetProcess} · {host.Backend.InputModeName}"
            : $"Ожидается {host.Profile.TargetProcess} · сейчас активно: {foreground}";
        LastCommand.Text = host.LastCommand + (host.LastCommand.Contains("injected", StringComparison.Ordinal) ? " · " + host.Backend.LastSendStatus : "");
        if (host.Input.LastFault.Length > 0) ErrorLabel.Text = host.Input.LastFault;
    }
    static string Gear(int gear) => gear == -1 ? "R" : gear == 0 ? "N" : gear.ToString();
    void OpenPairing(object sender, RoutedEventArgs e) { if (host is not null) PairCode.Text = host.Pairing.Open(); }
    async void OpenBrowser(object sender, RoutedEventArgs e) => await StartBrowserAsync();
    async Task StartBrowserAsync(string? infoFile = null)
    {
        if (host is null) return;
        try {
            await host.StartBrowserAsync();
            var addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => $"http://{a}:{host.Browser!.Port}").ToArray();
            var code = host.Browser!.Pairing.Open();
            BrowserAddress.Text = "Откройте в Safari: " + string.Join("  /  ", addresses);
            BrowserCode.Text = "Код на 2 минуты: " + code;
            if (!string.IsNullOrWhiteSpace(infoFile))
                await File.WriteAllLinesAsync(infoFile, [.. addresses, code]);
        } catch (Exception ex) { ErrorLabel.Text = "Safari: " + ex.Message; }
    }
    void Revoke(object sender, RoutedEventArgs e) { host?.RevokeDevices(); }
    void SourceChanged(object sender, RoutedEventArgs e) { host?.SetDemo(DemoBox.IsChecked == true); }
    void InputChanged(object sender, RoutedEventArgs e) { if (host is not null) { host.Backend.Enabled = EnableInput.IsChecked == true; if (!host.Backend.Enabled) host.Input.ReleaseAll(); } }
    void InputModeChanged(object sender, RoutedEventArgs e) { if (host is not null) host.SetCompatibleInput(CompatibleInput.IsChecked == true); }
    void SaveBindings(object sender, RoutedEventArgs e)
    {
        if (host is null) return;
        try
        {
            ButtonGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ButtonGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var target = ProcessName.Text.Trim();
            if (target.Length == 0 || target.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("Введите имя процесса без пути.");
            EnableInput.IsChecked = false;
            host.SaveProfile(host.Profile with { TargetProcess = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target[..^4] : target, Actions = rows.Select(r => r.ToAction()).ToList() });
            ErrorLabel.Text = "Сохранено. Планшет переподключится автоматически. Включите клавиатурный ввод после проверки назначений.";
        }
        catch (Exception ex) { ErrorLabel.Text = ex.Message; }
    }
    void LoadEditor()
    {
        if (host is null) return;
        rows.Clear(); foreach (var a in host.Profile.Actions) rows.Add(new(a));
        ProcessName.Text = host.Profile.TargetProcess;
        ProfileHelp.Text = GameProfiles.Help(host.Profile);
        ImportFs25Button.Visibility = host.Profile.Id == "fs25" ? Visibility.Visible : Visibility.Collapsed;
        LoadFs25PlanButton.Visibility = host.Profile.Id == "fs25" ? Visibility.Visible : Visibility.Collapsed;
    }
    void LoadFs25Plan(object sender, RoutedEventArgs e)
    {
        if (host?.Profile.Id != "fs25") return;
        var picker = new OpenFileDialog
        {
            Title = "Выберите план сезона FS25",
            Filter = "План сезона (*.json)|*.json",
        };
        if (picker.ShowDialog(this) != true) return;
        try { ErrorLabel.Text = "План FS25 загружен: " + host.LoadFs25Plan(picker.FileName) + ". План появится на пульте после чтения сохранения."; }
        catch (Exception ex) when (ex is Fs25PlanException or UnauthorizedAccessException)
        { ErrorLabel.Text = "План FS25: " + ex.Message; }
    }
    void ImportFs25Bindings(object sender, RoutedEventArgs e)
    {
        if (host?.Profile.Id != "fs25") return;
        try
        {
            ButtonGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ButtonGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!rows.Select(r => r.ToAction()).SequenceEqual(host.Profile.Actions) || ProcessName.Text != host.Profile.TargetProcess)
                throw new InvalidOperationException("Сначала сохраните изменения текущего профиля.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var suggested = Path.Combine(documents, "My Games", "FarmingSimulator2025", "inputBinding.xml");
            var picker = new OpenFileDialog
            {
                Title = "Выберите inputBinding.xml Farming Simulator 25",
                Filter = "Настройки клавиш FS25 (inputBinding.xml)|inputBinding.xml|XML (*.xml)|*.xml",
                FileName = "inputBinding.xml",
                InitialDirectory = Path.GetDirectoryName(suggested) is { } folder && Directory.Exists(folder) ? folder : documents
            };
            if (picker.ShowDialog(this) != true) return;
            var bindings = Fs25Bindings.Load(picker.FileName);
            if (bindings.Count == 0) throw new InvalidDataException("В файле не найдены клавиши FS25. Выберите inputBinding.xml из папки игры.");
            var updated = Fs25Profile.ApplyPlayerBindings(host.Profile, bindings);
            GameProfiles.Validate(updated);
            var changed = updated.Actions.Zip(host.Profile.Actions).Count(pair => pair.First.Key != pair.Second.Key);
            if (changed == 0) { ErrorLabel.Text = "Клавиши FS25 уже совпадают с профилем. Изменений нет."; return; }
            host.Store.Backup("before-fs25-import");
            EnableInput.IsChecked = false;
            host.SaveProfile(updated);
            LoadEditor();
            ErrorLabel.Text = $"Клавиши FS25 обновлены: {changed}. Пользовательские кнопки сохранены; резервная копия настроек создана. Включите ввод после проверки.";
        }
        catch (Exception ex) { ErrorLabel.Text = "Импорт FS25: " + ex.Message; }
    }
    void ChangeProfile(object sender, RoutedEventArgs e)
    {
        if (host is null || ProfilePicker.SelectedValue is not string id || id == host.Profile.Id) return;
        ButtonGrid.CommitEdit(DataGridEditingUnit.Cell, true); ButtonGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!rows.Select(r => r.ToAction()).SequenceEqual(host.Profile.Actions) || ProcessName.Text != host.Profile.TargetProcess)
        { ErrorLabel.Text = "Сначала сохраните изменения текущего профиля."; return; }
        EnableInput.IsChecked = false; host.SelectProfile(id); LoadEditor();
        ErrorLabel.Text = "Профиль открыт. Планшет обновится автоматически; включите ввод для игры.";
    }
    void AddButton(object sender, RoutedEventArgs e)
    {
        if (rows.Count >= GameProfiles.MaxActions) { ErrorLabel.Text = "Не более 96 кнопок в профиле."; return; }
        var row = new ButtonRow(new("custom-" + Guid.NewGuid().ToString("N"), "Мои кнопки", "НОВАЯ КНОПКА", "", "F12"));
        rows.Add(row); ButtonGrid.SelectedItem = row; ButtonGrid.ScrollIntoView(row);
    }
    void DeleteButton(object sender, RoutedEventArgs e) { if (ButtonGrid.SelectedItem is ButtonRow row) rows.Remove(row); }
    void MoveUp(object sender, RoutedEventArgs e) => Move(-1);
    void MoveDown(object sender, RoutedEventArgs e) => Move(1);
    void Move(int delta) { var i = ButtonGrid.SelectedIndex; if (i >= 0 && i + delta >= 0 && i + delta < rows.Count) rows.Move(i, i + delta); }
    async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (closeRequested) return;
        closeRequested = true; timer.Stop(); IsEnabled = false;
        try { if (host is not null) await host.DisposeAsync(); }
        catch (Exception ex) { ErrorLabel.Text = "Ошибка завершения Companion: " + ex.Message; }
        finally {
            // Dispose may complete synchronously after startup failure. Defer Close
            // until WPF has left the original Closing handler; avoid recursive Close.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { closing = true; Close(); }));
        }
    }
}

public sealed class ButtonRow(DeckAction a)
{
    public string Id { get; } = a.Id;
    public string Label { get; set; } = a.Label;
    public string Page { get; set; } = a.Page;
    public string Group { get; set; } = a.Group;
    public string Key { get; set; } = a.Key;
    public string Description { get; set; } = a.Description;
    public string GestureLabel { get; set; } = a.Gesture == "hold" ? "Удержание" : a.Gesture == "tapThenHold" ? "Нажать → держать" : "Нажатие";
    public DeckAction ToAction() => new(Id, Page.Trim(), Label.Trim(), Description.Trim(), Key.Trim(), GestureLabel == "Удержание" ? "hold" : GestureLabel == "Нажать → держать" ? "tapThenHold" : "press", Group.Trim());
}
