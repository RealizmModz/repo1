using ATS.Dispatcher.Core;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ATS.Dispatcher.App;

public partial class MainWindow : Window
{
    private GameCatalog? _catalog;
    private SaveOfferTemplate? _selectedTemplate;
    private IReadOnlyList<AtsSaveSlot> _saveSlots = [];

    public MainWindow()
    {
        InitializeComponent();
        RefreshSaves();
    }

    private void ImportCatalogClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select extracted ATS game data folder (containing def)"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = GameCatalogImporter.Import(dialog.FolderName);
            if (imported.Cities.Count == 0 || imported.Cargo.Count == 0)
                throw new InvalidDataException(
                    "No usable city/cargo definitions were found. Select an extracted game 'def' folder.");

            _catalog = imported;
            SetSuggestions(OriginInput, imported.Cities.Select(x => x.Name));
            SetSuggestions(DestinationInput, imported.Cities.Select(x => x.Name));
            SetSuggestions(CargoInput, imported.Cargo.Select(x => x.Name));
            SetSuggestions(OriginCompanyInput, imported.Depots.Select(x => x.CompanyName));
            SetSuggestions(DestinationCompanyInput, imported.Depots.Select(x => x.CompanyName));
            SetSuggestions(TrailerInput, imported.Trailers.Select(x => x.Id)
                .Concat(imported.Trailers.Select(x => x.BodyType))
                .Concat(imported.Cargo.SelectMany(x => x.BodyTypes)));
            CatalogStatus.Text = imported.Cities.Count + " cities, " +
                imported.Cargo.Count + " cargo types, " + imported.Depots.Count +
                " company-city depots, " + imported.Trailers.Count +
                " trailer configurations (from " + imported.DefinitionFilesRead + " definition files).";
            StatusMessage.Text = "Catalog loaded. Use Check compatibility before exporting.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot import definitions",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetSuggestions(ComboBox box, IEnumerable<string> values)
    {
        var previous = box.Text;
        box.ItemsSource = values.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        box.Text = previous;
    }

    private void ValidateCatalogClick(object sender, RoutedEventArgs e)
    {
        if (_catalog is null)
        {
            MessageBox.Show("Import extracted game definitions first.",
                "Catalog required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var issues = CatalogCompatibilityValidator.Check(BuildJob(), _catalog);
            var message = issues.Count == 0
                ? "No issues found in the imported definitions. This does NOT prove the job will work in ATS."
                : string.Join(Environment.NewLine, issues.Take(20)) +
                  (issues.Count > 20 ? Environment.NewLine + "(More issues omitted)" : "");
            MessageBox.Show(message, "Catalog compatibility",
                MessageBoxButton.OK, issues.Count == 0
                    ? MessageBoxImage.Information : MessageBoxImage.Warning);
            StatusMessage.Text = issues.Count == 0
                ? "Catalog check passed (in-game compatibility unverified)."
                : issues.Count + " potential compatibility issue(s) detected.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Compatibility check failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InspectDecodedSaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a COPY of a decoded ATS game.sii",
            Filter = "Decoded ATS save (*.sii;*.txt)|*.sii;*.txt|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var inspection = SaveOfferInspector.Inspect(dialog.FileName);
            ReferenceOfferInput.ItemsSource = inspection.Offers;
            ReferenceOfferInput.SelectedIndex = inspection.Offers.Count > 0 ? 0 : -1;
            _selectedTemplate = null;
            ReferenceOfferStatus.Text = inspection.OfferCount + " offers scanned; " +
                inspection.Offers.Count + " usable templates" +
                (inspection.ResultsTruncated ? " (list limited to first 25,000)" : "") + ".";
            StatusMessage.Text = "Inspected decoded SII in read-only mode. No changes made.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not inspect decoded save",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UseOfferTemplateClick(object sender, RoutedEventArgs e)
    {
        if (ReferenceOfferInput.SelectedItem is not SaveOfferTemplate offer)
        {
            MessageBox.Show("Inspect a decoded save and select an existing job offer first.",
                "Select a freight template", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _selectedTemplate = offer;
        var cargoId = offer.CargoUnit.StartsWith("cargo.", StringComparison.OrdinalIgnoreCase)
            ? offer.CargoUnit["cargo.".Length..] : offer.CargoUnit;
        CargoInput.Text = _catalog?.Cargo.FirstOrDefault(x =>
            string.Equals(x.Id, cargoId, StringComparison.OrdinalIgnoreCase))?.Name ?? cargoId;
        if (!string.IsNullOrWhiteSpace(offer.TrailerVariant))
            TrailerInput.Text = offer.TrailerVariant;

        // Only resolve the target city/company if extracted definitions support
        // that exact ID. The game save target alone is not authoritative for a UI city.
        var period = offer.Target.IndexOf('.');
        if (_catalog is not null && period > 0 && period < offer.Target.Length - 1)
        {
            var companyId = offer.Target[..period];
            var cityId = offer.Target[(period + 1)..];
            var knownCity = _catalog.Cities.FirstOrDefault(x =>
                string.Equals(x.Id, cityId, StringComparison.OrdinalIgnoreCase));
            var knownDepot = _catalog.Depots.FirstOrDefault(x =>
                string.Equals(x.CityId, cityId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.CompanyId, companyId, StringComparison.OrdinalIgnoreCase));
            if (knownCity is not null && knownDepot is not null)
            {
                DestinationInput.Text = knownCity.Name;
                DestinationCompanyInput.Text = knownDepot.CompanyName;
            }
        }
        NameInput.Text = "Template: " + cargoId;
        ReferenceOfferStatus.Text = "Using reference offer: " + offer.UnitId +
            ". Pickup, destination, trailer and payout must still be validated.";
        StatusMessage.Text = "Copied reference cargo/trailer into draft; no game save was modified.";
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
            if (dialog.ShowDialog(this) != true) return;
            JobFileService.Export(dialog.FileName, job);
            StatusMessage.Text = "Exported job draft: " + dialog.FileName +
                                 " (ATS save was not changed)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot export job",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open ATS job draft",
            Filter = "Job draft (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var job = JobFileService.Import(dialog.FileName);
            _selectedTemplate = null;
            ReferenceOfferInput.SelectedIndex = -1;
            NameInput.Text = job.Name;
            OriginInput.Text = job.OriginCity;
            OriginCompanyInput.Text = job.OriginCompany;
            DestinationInput.Text = job.DestinationCity;
            DestinationCompanyInput.Text = job.DestinationCompany;
            CargoInput.Text = job.Cargo;
            TrailerInput.Text = job.Trailer;
            PaymentInput.Text = job.PaymentUsd?.ToString(CultureInfo.CurrentCulture) ?? "";
            WaypointsInput.Text = string.Join(Environment.NewLine, job.Waypoints);
            StatusMessage.Text = "Loaded draft: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot open draft",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        _selectedTemplate = null;
        ReferenceOfferInput.SelectedIndex = -1;
        NameInput.Clear();
        OriginInput.Text = "";
        OriginCompanyInput.Text = "";
        DestinationInput.Text = "";
        DestinationCompanyInput.Text = "";
        CargoInput.Text = "";
        TrailerInput.Text = "";
        PaymentInput.Clear();
        WaypointsInput.Clear();
        StatusMessage.Text = "Cleared draft.";
    }

    private void BackupSaveClick(object sender, RoutedEventArgs e)
    {
        var selected = SavesList.SelectedIndex;
        if (selected < 0 || selected >= _saveSlots.Count)
        {
            MessageBox.Show("Select a save slot first, then choose a backup folder.",
                "Select a save", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Where should the independent ATS save backup go?" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var saved = SaveBackupService.CreateVerifiedBackup(
                _saveSlots[selected].FilePath, dialog.FolderName);
            StatusMessage.Text = "Created verified save backup: " + saved.BackupFile;
            MessageBox.Show("Backup created and SHA-256 verified.\n\n" +
                saved.BackupFile + "\n\nSHA-256: " + saved.Sha256,
                "Save safely backed up", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Backup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshSavesClick(object sender, RoutedEventArgs e) => RefreshSaves();

    private void RefreshSaves()
    {
        SavesList.Items.Clear();
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _saveSlots = GameProfileScanner.Scan(documents);
            var slots = _saveSlots;
            if (slots.Count == 0) SavesList.Items.Add("No local saves found.");

            foreach (var slot in slots)
                SavesList.Items.Add(slot.SlotName + "  •  " + slot.Format +
                    Environment.NewLine + slot.ProfileId +
                    Environment.NewLine + slot.LastModifiedUtc.ToLocalTime().ToString("g"));
            StatusMessage.Text = "Found " + slots.Count +
                                 " readable save-file locations (read-only).";
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
            OriginCompany = OriginCompanyInput.Text.Trim(),
            DestinationCity = DestinationInput.Text.Trim(),
            DestinationCompany = DestinationCompanyInput.Text.Trim(),
            Cargo = CargoInput.Text.Trim(),
            Trailer = TrailerInput.Text.Trim(),
            PaymentUsd = requestedPayment,
            ReferenceOfferUnit = _selectedTemplate?.UnitId ?? "",
            ReferenceTarget = _selectedTemplate?.Target ?? "",
            ReferenceCargoUnit = _selectedTemplate?.CargoUnit ?? "",
            ReferenceTrailerVariant = _selectedTemplate?.TrailerVariant ?? "",
            ReferenceTrailerDefinition = _selectedTemplate?.TrailerDefinition ?? "",
            ReferenceCompanyTruck = _selectedTemplate?.CompanyTruck ?? "",
            ReferenceUnitsCount = _selectedTemplate?.UnitsCount,
            Waypoints = WaypointsInput.Text.Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList()
        };
    }
}
