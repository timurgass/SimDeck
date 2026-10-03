using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using SimDeck.App;

static class RenderTest
{
    public static void Save(string path, int width = 1440, int height = 940, string section = "overview")
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App(); application.InitializeComponent();
                var window = new MainWindow();
                var profile = GameProfiles.F125();
                var profiles = new[] { GameProfiles.F1(), profile, new GameProfile("beamng-default", "BeamNG.drive", "BeamNG.drive.x64", [.. BeamNgProfile.Actions]) }.Concat(AdditionalProfiles.All()).ToArray();
                var picker = (ComboBox)window.FindName("ProfilePicker"); picker.ItemsSource = profiles; picker.SelectedValue = profile.Id;
                var grid = (DataGrid)window.FindName("ButtonGrid");
                var rows = (ObservableCollection<ButtonRow>)((ICollectionView)grid.ItemsSource).SourceCollection;
                var search = (TextBox)window.FindName("ActionSearch");
                foreach (var game in profiles) {
                    rows.Clear(); foreach (var action in game.Actions) rows.Add(new ButtonRow(action));
                    var expected = rows.Select(row => row.ToAction()).ToArray();
                    search.Text = "__no_matching_action__";
                    if (grid.Items.Count != 0 || rows.Count != game.Actions.Count) throw new Exception("Search changed the profile source");
                    search.Clear();
                    if (grid.Items.Count != expected.Length || !rows.Select(row => row.ToAction()).SequenceEqual(expected)) throw new Exception("Search lost a profile action");
                }
                rows.Clear(); foreach (var action in profile.Actions) rows.Add(new ButtonRow(action));
                foreach (var nav in new[] { "DevicesNav", "ProfilesNav", "DiagnosticsNav", "SettingsNav", "OverviewNav" }) {
                    ((RadioButton)window.FindName(nav)).IsChecked = true;
                    if (rows.Count != profile.Actions.Count) throw new Exception("Navigation lost editor changes");
                }
                ((RadioButton)window.FindName(section switch { "devices" => "DevicesNav", "profiles" => "ProfilesNav", "diagnostics" => "DiagnosticsNav", "settings" => "SettingsNav", _ => "OverviewNav" })).IsChecked = true;
                ((TextBlock)window.FindName("SourceLabel")).Text = "Демонстрационные данные";
                ((TextBlock)window.FindName("SpeedLabel")).Text = "216";
                ((TextBlock)window.FindName("RpmLabel")).Text = "11240";
                ((TextBlock)window.FindName("GearLabel")).Text = "6";
                ((TextBlock)window.FindName("DeviceLabel")).Text = "Планшет подключён";
                ((TextBlock)window.FindName("TransportLabel")).Text = "USB · демонстрация";
                ((Button)window.FindName("PairButton")).IsEnabled = true;
                ((Button)window.FindName("UsbButton")).Background = new SolidColorBrush(Color.FromRgb(70,224,196));
                ((Button)window.FindName("UsbButton")).Foreground = new SolidColorBrush(Color.FromRgb(6,46,41));
                ((TextBlock)window.FindName("GameStatus")).Text = "Макет · без ввода";
                ((TextBlock)window.FindName("InputStatus")).Text = "Демонстрация · ввод отключён";
                ((TextBlock)window.FindName("LastCommand")).Text = "Радио инженера · пример команды";
                ((TextBlock)window.FindName("TelemetryAge")).Text = "Демонстрация интерфейса";
                ((TextBox)window.FindName("ProcessName")).Text = profile.TargetProcess;
                ((TextBlock)window.FindName("DiagnosticSummary")).Text = "Демонстрация · реальных пакетов и команд нет";
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
                if (grid.Columns.Count != 6 || ((TextBox)window.FindName("FingerprintText")).Text.Length != 0 || ((TextBlock)window.FindName("AddressText")).Text.Length != 0) throw new Exception("Unsafe desktop preview");
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen()) { drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 23, 28)), null, new Rect(0, 0, width, height)); drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, width, height)); }
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(path); encoder.Save(file);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
