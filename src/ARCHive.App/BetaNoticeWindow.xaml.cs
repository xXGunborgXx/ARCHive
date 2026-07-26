using System.Windows;
using System.Windows.Input;

namespace ARCHive.App;

public partial class BetaNoticeWindow : Window
{
    public BetaNoticeWindow(
        string title,
        string message,
        string buttonText)
    {
        InitializeComponent();
        NoticeTitle.Text = title;
        NoticeMessage.Text = message;
        ContinueButton.Content = buttonText;
    }

    private void OnTitleBarMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) =>
        Close();
}
