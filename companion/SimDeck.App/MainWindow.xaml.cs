using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SimDeck.App;
public partial class MainWindow : Window
{
    CompanionHost? host;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    bool closing;
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
        SourceLabel.Text = t.Source == "demo" ? "ДЕМОНСТРАЦИЯ · не данные игры" : host.Profile.Name + (fresh ? " · данные поступают" : " · нет свежих данных");
        SpeedLabel.Text = fresh ? (t.Data!.SpeedMps * 3.6).ToString("0") : "—";
        RpmLabel.Text = fresh ? $"{t.Data!.Rpm:0} RPM   /   {t.Data.GearDisplay}" : "— RPM   /   —";
        TelemetryHelp.Text = fresh ? host.Profile.Id is "f1-24" or "f1-25" ? "UDP F1 · DRS / ERS / лимитер — по данным игры" : t.Data!.Headlights is null
            ? "Старый поток без состояния кнопок. Перезапустите BeamNG после обновления мода."
            : "Состояния кнопок поступают · " + (t.Data.Headlights == 2 ? "дальний свет" : t.Data.Headlights == 1 ? "ближний свет" : "фары выключены") : host.TelemetryDiagnostic;
        InputStatus.Text = host.Backend.Demo ? "Демонстрация · ввод отключён" : !host.Backend.Enabled ? "Выключен · установите галочку ниже" : host.Backend.CanInject ? "Готов · окно игры активно" : "Включён · ждёт активного окна игры";
        LastCommand.Text = host.LastCommand;
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
        ProfileHelp.Text = host.Profile.Id is "f1-24" or "f1-25"
            ? $"{host.Profile.Name} · SimDeck 75: выберите изменённый Keyboard Preset 2. MFD — B, лимитер — P. 69 кнопок в трёх разделах; без нампада."
            : "BeamNG: мод SimDeck → 127.0.0.1:4444. Подсветка берётся из машины. Для новых клавиш, которых нет в телеметрии, отображается только физическое нажатие.";
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
        e.Cancel = true; timer.Stop(); IsEnabled = false;
        if (host is not null) await host.DisposeAsync();
        closing = true; Close();
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
