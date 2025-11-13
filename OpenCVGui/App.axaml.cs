using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OpenCVGui.ViewModels;
using OpenCVGui.Views;
using System;
using System.Threading.Tasks;

namespace OpenCVGui
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Set up UI thread exception handler
            Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;
            
            // Set up task scheduler exception handler for background tasks
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Line below is needed to remove Avalonia data validation.
                // Without this line you will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
        
        private void OnUIThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"UI thread unhandled exception: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception.StackTrace}");
            
            if (e.Exception.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {e.Exception.InnerException.Message}");
            }
            
            // Mark as handled to prevent app crash, but log the error
            e.Handled = true;
        }
        
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception.Message}");
            foreach (var innerException in e.Exception.InnerExceptions)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {innerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {innerException.StackTrace}");
            }
            
            // Mark as observed to prevent app crash
            e.SetObserved();
        }
    }
}