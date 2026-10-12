using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kiso11.Services;

namespace Kiso11.Views;

/// Dark replacement for the system message box. Buttons are shown in the order given.
public partial class ThemedDialog : Window
{
    /// Index of the button that was clicked, or -1 when the dialog was closed with Escape.
    public int Choice { get; private set; } = -1;

    public ThemedDialog(string title, string message, IReadOnlyList<DialogButton> buttons)
    {
        InitializeComponent();

        TitleText.Text = title;
        TitleText.Visibility = string.IsNullOrWhiteSpace(title) ? Visibility.Collapsed : Visibility.Visible;
        MessageText.Text = message;

        for (int i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i].Text,
                Style = StyleFor(buttons[i].Kind),
                MinWidth = 104,
                Margin = i == 0 ? new Thickness(0) : new Thickness(8, 0, 0, 0)
            };

            button.Click += (_, _) =>
            {
                Choice = index;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }
    }

    private Style StyleFor(DialogButtonKind kind) => kind switch
    {
        DialogButtonKind.Primary => (Style)FindResource("PrimaryButtonStyle"),
        DialogButtonKind.Danger => (Style)FindResource("DangerButtonStyle"),
        DialogButtonKind.Ghost => (Style)FindResource("GhostButtonStyle"),
        _ => (Style)FindResource("SecondaryButtonStyle")
    };

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Choice = -1;
            Close();
        }
    }

    /// Lets the user drag the dialog by its body, but not when a button is pressed.
    private void OnCardMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ButtonPanel.IsAncestorOf(source))
        {
            return;
        }

        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
