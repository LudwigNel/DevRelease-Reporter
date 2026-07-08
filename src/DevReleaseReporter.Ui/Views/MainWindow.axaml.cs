using Avalonia.Controls;

namespace DevReleaseReporter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenSettingsDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = DataContext,
        };

        await settingsWindow.ShowDialog(this);
    }
}