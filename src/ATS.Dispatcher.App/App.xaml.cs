using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ATS.Dispatcher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!e.Args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            base.OnStartup(e);
            return;
        }

        // CI-only: load the real packaged WPF window, exercise draft bindings,
        // then exit. No dialogs, profile scans, save reads/writes or network.
        StartupUri = null;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        try
        {
            var window = new FreightMarketWindow();
            MainWindow = window;
            window.Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    try
                    {
                        T Control<T>(string name) where T : FrameworkElement =>
                            window.FindName(name) as T ??
                            throw new InvalidOperationException("Missing window control: " + name);
                        void Check(bool condition)
                        {
                            if (!condition) throw new InvalidOperationException("Packaged WPF smoke assertion failed.");
                        }

                        Check(Control<ListBox>("FreightList").Items.Count > 0);
                        Check(!Control<Button>("CreateJobButton").IsEnabled);
                        Check(!Control<Button>("ReferenceReportButton").IsEnabled);
                        Check(!string.IsNullOrWhiteSpace(Control<TextBox>("NameBox").Text));
                        Control<TextBox>("DestinationBox").Text = "Smoke Test Delivery";
                        Check(Control<TextBlock>("MapDestinationLabel").Text == "Smoke Test Delivery");
                        Check(Control<TextBlock>("RouteSummary").Text.Contains("Smoke Test Delivery"));
                        Control<TextBox>("WaypointsBox").Text = "Stop One\nStop Two";
                        Check(Control<TextBlock>("RouteSummary").Text.Contains("Stop One"));
                        Control<TextBox>("SearchBox").Text = "Grain";
                        Check(Control<ListBox>("FreightList").Items.Count == 1);
                        Check(!Control<Button>("CreateJobButton").IsEnabled);
                        Shutdown(0);
                    }
                    catch
                    {
                        Shutdown(1);
                    }
                }));
            window.Show();
        }
        catch
        {
            Shutdown(1);
        }
    }
}
