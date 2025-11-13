using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenCVGui.Utils;
using System.Linq;

namespace OpenCVGui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            ScreenPosition.Save(new ScreenPosition()
            {
                Left = Position.X,
                Top = Position.Y,
                Width = Width,
                Height = Height,
                WindowState = WindowState
            });
            base.OnClosing(e);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            // somehow this offset a little sometimes??
            var pos = ScreenPosition.Load();
            if (pos != null)
            {
                switch (pos.WindowState)
                {
                    case WindowState.Normal:
                        var newPos = new PixelPoint(pos.Left, pos.Top);
                        // check if on screen 
                        if (Screens.All.FirstOrDefault(x => x.WorkingArea.Contains(newPos)) == null)
                            return;
                        Width = pos.Width;
                        Height = pos.Height;
                        Position = new PixelPoint(pos.Left, pos.Top);
                        break;
                    case WindowState.Minimized:
                        break;
                    case WindowState.Maximized:
                        break;
                    case WindowState.FullScreen:
                        break;
                    default:
                        break;
                }
                WindowState = pos.WindowState;
            }
            base.OnLoaded(e);
        }
    }
}