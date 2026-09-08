using System.Windows;
using System.Windows.Input;

namespace MarkOne;

public partial class FindReplaceWindow : Window
{
    private readonly MainWindow _editor;

    public FindReplaceWindow(MainWindow editor)
    {
        InitializeComponent();
        TitleBar.Attach(this);
        _editor = editor;
    }

    public void FocusInput(bool replaceMode)
    {
        if (replaceMode && FindBox.Text.Length > 0)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }
        else
        {
            FindBox.Focus();
            FindBox.SelectAll();
        }
    }

    private void OnFindNext(object sender, RoutedEventArgs e)
    {
        bool found = _editor.FindNext(FindBox.Text, MatchCase.IsChecked == true);
        Info.Text = found ? string.Empty : "Nicht gefunden.";
    }

    private void OnFindNextCmd(object sender, ExecutedRoutedEventArgs e) => OnFindNext(sender, e);

    private void OnReplaceOne(object sender, RoutedEventArgs e)
    {
        bool more = _editor.ReplaceOne(FindBox.Text, ReplaceBox.Text, MatchCase.IsChecked == true);
        Info.Text = more ? string.Empty : "Keine weitere Fundstelle.";
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        int count = _editor.ReplaceAll(FindBox.Text, ReplaceBox.Text, MatchCase.IsChecked == true);
        Info.Text = count switch
        {
            0 => "Nichts gefunden.",
            1 => "1 Fundstelle ersetzt.",
            _ => $"{count} Fundstellen ersetzt.",
        };
    }

    private void OnCloseCmd(object sender, ExecutedRoutedEventArgs e) => Close();
}
