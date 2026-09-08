using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace MarkOne;

public partial class RecoveryWindow : Window
{
    private readonly ObservableCollection<RecoveryEntry> _entries;

    /// <summary>Die vom Benutzer gewählte Fassung, sonst null.</summary>
    public RecoveryEntry? Restored { get; private set; }

    public RecoveryWindow(IEnumerable<RecoveryEntry> entries)
    {
        InitializeComponent();
        TitleBar.Attach(this);
        _entries = new ObservableCollection<RecoveryEntry>(entries);
        Entries.ItemsSource = _entries;
        if (_entries.Count > 0) Entries.SelectedIndex = 0;
    }

    private void OnRestore(object sender, RoutedEventArgs e)
    {
        if (Entries.SelectedItem is not RecoveryEntry entry) return;
        Restored = entry;
        Recovery.Delete(entry);
        Close();
    }

    private void OnDiscard(object sender, RoutedEventArgs e)
    {
        if (Entries.SelectedItem is not RecoveryEntry entry) return;

        var answer = MessageBox.Show(this,
            $"Die gesicherte Fassung von {entry.DisplayName} endgültig verwerfen?",
            "MarkOne", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        Recovery.Delete(entry);
        _entries.Remove(entry);
        if (_entries.Count == 0) Close();
        else Entries.SelectedIndex = 0;
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();
}
