using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FH.LanguageComboTool.Wpf;

public partial class QingDialog : Window
{
    private readonly QingDialogButtons _buttons;

    private QingDialog(
        Window owner,
        string message,
        string title,
        QingDialogButtons buttons,
        QingDialogKind kind)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        _buttons = buttons;

        if (buttons == QingDialogButtons.YesNo)
        {
            PrimaryButton.Text = "是";
            SecondaryButton.Text = "否";
            SecondaryButton.Visibility = Visibility.Visible;
        }

        ApplyKind(kind);
    }

    public QingDialogResult Result { get; private set; } = QingDialogResult.None;

    public static QingDialogResult Show(
        Window owner,
        string message,
        string title,
        QingDialogButtons buttons = QingDialogButtons.Ok,
        QingDialogKind kind = QingDialogKind.Information)
    {
        var dialog = new QingDialog(owner, message, title, buttons, kind);
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void ApplyKind(QingDialogKind kind)
    {
        switch (kind)
        {
            case QingDialogKind.Error:
                TitleBar.SetResourceReference(BackgroundProperty, "ColorBrushRedLight");
                PrimaryButton.ColorType = QING.UIKIT.MyButton.ColorState.Red;
                break;
            case QingDialogKind.Warning:
                TitleBar.Background = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                break;
            default:
                TitleBar.SetResourceReference(BackgroundProperty, "ColorBrush4");
                break;
        }
    }

    private void Primary_Click(object sender, MouseButtonEventArgs e)
    {
        CompletePrimary();
    }

    private void CompletePrimary()
    {
        Result = _buttons == QingDialogButtons.YesNo
            ? QingDialogResult.Yes
            : QingDialogResult.Ok;
        Close();
    }

    private void Secondary_Click(object sender, MouseButtonEventArgs e)
    {
        Result = QingDialogResult.No;
        Close();
    }

    private void Close_Click(object sender, EventArgs e)
    {
        Result = _buttons == QingDialogButtons.YesNo
            ? QingDialogResult.No
            : QingDialogResult.Ok;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Dialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close_Click(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CompletePrimary();
            e.Handled = true;
        }
    }
}

public enum QingDialogButtons
{
    Ok,
    YesNo
}

public enum QingDialogKind
{
    Information,
    Warning,
    Error,
    Question
}

public enum QingDialogResult
{
    None,
    Ok,
    Yes,
    No
}
