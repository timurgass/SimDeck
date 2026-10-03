using System.Windows;
namespace SimDeck.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if(e.Args.Length==3 && e.Args[0]=="--ets2-map-build")
        {

            try { Ets2MapBuilder.Build(e.Args[1],e.Args[2]); Shutdown(0); }
            catch(Exception ex) when(ex is not OutOfMemoryException)
            { System.IO.File.WriteAllText(e.Args[2]+".error.txt",ex.GetType().Name+": "+ex.Message);Shutdown(2); }
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
