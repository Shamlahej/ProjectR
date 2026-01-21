// Vi har for at gøre det mere overskueligt at arbejde med codebehind (mainwindow.axaml.cs), lavet flere filer i katergorier.
// Dette har bare gjort det nemmmere at finde rundt i og nemmere at finde fejl i.
// CS filerne er altså vores codebehind, til vores knapper, gui og er egentligt logikken bag det hele.
// MainWindow.axaml.cs - fortæller avalonia at den skal indlæse vore layout fra vores mainwindow.axaml fil og den skal "build" vinduet.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectR;

public partial class MainWindow : Window
{
    // Loader vinduets XAML (layout + controls)
    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);
}