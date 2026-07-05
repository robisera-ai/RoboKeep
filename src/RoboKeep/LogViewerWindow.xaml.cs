namespace RoboKeep;

public partial class LogViewerWindow : Wpf.Ui.Controls.FluentWindow
{
    public LogViewerWindow(string title, string text)
    {
        InitializeComponent();
        ViewerTitle.Title = title;
        Title = title;
        LogText.Text = text;
    }
}
