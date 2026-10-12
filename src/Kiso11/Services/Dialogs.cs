using System.Windows;
using Kiso11.Views;

namespace Kiso11.Services;

public enum DialogButtonKind
{
    Primary,
    Secondary,
    Danger,
    Ghost
}

public readonly record struct DialogButton(string Text, DialogButtonKind Kind = DialogButtonKind.Secondary);

/// Shows themed dialogs over the main window, or centred on screen when the window is hidden in the tray.
public static class Dialogs
{
    /// Returns the index of the clicked button, or -1 when the dialog was closed.
    public static int Show(string title, string message, params DialogButton[] buttons)
    {
        var dialog = new ThemedDialog(title, message, buttons);

        var owner = Application.Current?.MainWindow;
        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Choice;
    }

    /// Returns true when the confirm button is clicked.
    public static bool Confirm(string title, string message, string confirmText, string cancelText, bool danger = false)
    {
        var confirmKind = danger ? DialogButtonKind.Danger : DialogButtonKind.Primary;
        return Show(
            title,
            message,
            new DialogButton(cancelText, DialogButtonKind.Secondary),
            new DialogButton(confirmText, confirmKind)) == 1;
    }

    public static void Info(string title, string message, string okText)
    {
        Show(title, message, new DialogButton(okText, DialogButtonKind.Primary));
    }
}
