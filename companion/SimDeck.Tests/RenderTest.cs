using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimDeck.App;

static class RenderTest
{
    public static void Save(string path)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App(); application.InitializeComponent();
                var window = new MainWindow();
                var profile = GameProfiles.F1();
                var picker = (ComboBox)window.FindName("ProfilePicker"); picker.ItemsSource = new[] { profile }; picker.SelectedIndex = 0;
                ((DataGrid)window.FindName("ButtonGrid")).ItemsSource = profile.Actions.Select(a => new ButtonRow(a)).ToList();
                ((TextBlock)window.FindName("SourceLabel")).Text = "ТЕСТОВЫЙ ПАКЕТ · BeamNG";
                ((TextBlock)window.FindName("SpeedLabel")).Text = "144";
                ((TextBlock)window.FindName("RpmLabel")).Text = "6234 RPM / 4";
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(1080, 900)); content.Arrange(new Rect(0, 0, 1080, 900)); content.UpdateLayout();
                ((ScrollViewer)content).ScrollToBottom(); content.UpdateLayout();
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen()) { drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(11, 17, 21)), null, new Rect(0, 0, 1080, 900)); drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, 1080, 900)); }
                var bitmap = new RenderTargetBitmap(1080, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(path); encoder.Save(file);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
