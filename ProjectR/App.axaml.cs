// App.axaml.cs styrer opstarten af hele programmet, den loader xaml ressourcer og herefter opretter den hovedvinduet.
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ProjectR;

// App class - dette er vores overordnede "container" for vores application (programmet der starter xaml.).
// Det betyder at den styrer opstart af programmet, livscyklus og hvilket vindue der først skal åbnes.

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
// Her tjekkes for hvilken slags application der åbner.
// Vores er er classic desktop style application, som man også kan se når man runner vores program.
// Det er vigtigt at forstå vi egentligt har bygget videre på login og application som underviser har udarbejdet til undervisning.
// Den application, har vi så bygget videre på. 
// Mainwindow = hovedvinduet.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
