using ATS.Dispatcher.Core;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;

namespace ATS.Dispatcher.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RefreshSaves();
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var job = BuildJob();
            var errors = DispatchValidator.Validate(job);
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, errors), "Check job details",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export ATS job draft",
                Filter = "Job draft (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "ats-job-draft.json"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            JobFileService.Export(dialog.FileName, job);
            StatusMessage.Text = "Exported job draft: " + dialog.FileName +
                                 " (ATS game save was not changed)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot export job", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open ATS job draft",
            Filter = "Job draft (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var job = JobFileService.Import(dialog.FileName);
            NameInput.Text = job.Name;
            OriginInput.Text = job.OriginCity;
            DestinationInput.Text = job.DestinationCity;
            CargoInput.Text = job.Cargo;
            TrailerInput.Text = job.Trailer;
            PaymentInput.Text = job.PaymentUsd?.ToString(CultureInfo.CurrentCulture) ?? "";
            WaypointsInput.Text = string.Join(Environment.NewLine, job.Waypoints);
            StatusMessage.Text = "Loaded draft: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot open draft", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        NameInput.Clear();
        OriginInput.Clear();
        DestinationInput.Clear();
        CargoInput.Clear();
        TrailerInput.Clear();
        PaymentInput.Clear();
        WaypointsInput.Clear();
        StatusMessage.Text = "Cleared draft.";
    }

    private void RefreshSavesClick(object sender, RoutedEventArgs e) => RefreshSaves();

    private void RefreshSaves()
    {
        SavesList.Items.Clear();
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var slots = GameProfileScanner.Scan(documents);
            if (slots.Count == 0)
                SavesList.Items.Add("No local saves found.");

            foreach (var slot in slots)
                SavesList.Items.Add(slot.SlotName + "  •  " + slot.Format +
                    Environment.NewLine + slot.ProfileId +
                    Environment.NewLine + slot.LastModifiedUtc.ToLocalTime().ToString("g"));

            StatusMessage.Text = "Found " + slots.Count + " readable save-file locations (read-only).";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = "Could not scan saves: " + ex.Message;
        }
    }

    private DispatchJob BuildJob()
    {
        decimal? requestedPayment = null;
        if (!string.IsNullOrWhiteSpace(PaymentInput.Text))
        {
            if (!decimal.TryParse(PaymentInput.Text.Trim(), NumberStyles.Number,
                    CultureInfo.CurrentCulture, out var value))
                throw new InvalidDataException("Payout must be a valid number or blank.");
            requestedPayment = value;
        }

        return new DispatchJob
        {
            Name = NameInput.Text.Trim(),
            OriginCity = OriginInput.Text.Trim(),
            DestinationCity = DestinationInput.Text.Trim(),
            Cargo = CargoInput.Text.Trim(),
            Trailer = TrailerInput.Text.Trim(),
            PaymentUsd = requestedPayment,
            Waypoints = WaypointsInput.Text.Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList()
        };
    }
}
