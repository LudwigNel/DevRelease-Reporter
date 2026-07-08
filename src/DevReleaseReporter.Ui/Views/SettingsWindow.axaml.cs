using Avalonia.Controls;

namespace DevReleaseReporter.Ui.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
