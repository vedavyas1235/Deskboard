using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace DeskBoard.Views;

public partial class ToastNotification : Window
{
    public ToastNotification(string message)
    {
        InitializeComponent();
        MessageText.Text = message;

        // Position: bottom-right of screen
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 24;
        Top  = screen.Bottom - 80;

        // Fade in
        this.Opacity = 0;
        Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            this.BeginAnimation(OpacityProperty, anim);
        };
    }
}
