using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCVGui
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Set up global exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            // Set up task scheduler exception handler for background tasks
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fatal error starting application: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Unobserved task exception in Program: {e.Exception.Message}");
            foreach (var innerException in e.Exception.InnerExceptions)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {innerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {innerException.StackTrace}");
            }
            
            // Mark as observed to prevent app crash
            e.SetObserved();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {exception.StackTrace}");
                
                if (exception.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {exception.InnerException.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception (non-Exception): {e.ExceptionObject}");
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
