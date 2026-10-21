using ATS.Dispatcher.Core;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ATS.Dispatcher.App;

/// <summary>
/// New ATS-inspired presentation layer. The route graphic is intentionally
/// schematic, and there is no enabled dispatch/write-to-save action.
/// Legacy read-only tools remain accessible via Advanced tools.
/// </summary>
public partial class FreightMarketWindow : Window
{
    private IReadOnlyList<FreightBoardCard> _cards = FreightBoardService.Demonstration();
    private GameCatalog? _catalog;
    private SaveOfferTemplate? _selectedOffer;
    private bool _ready;

    public FreightMarketWindow()
    {
        InitializeComponent();
        _ready = true;
        ShowCards();
        FreightList.SelectedIndex = 0;
        RefreshDraftPreview();
    }

    private void ShowCards()
    {
        var matches = FreightBoardService.Search(_cards, SearchBox.Text);
        FreightList.ItemsSource = matches;
        OfferCountLabel.Text = _cards.Count > 0 && _cards.All(x => x.IsDemo)
            ? "DEMO ENTRIES" : matches.Count + " SAVED OFFERS";
        if (matches.Count == 0)
            CurrentCardStatus.Text = "No matching offers; import a decoded save or clear the search.";
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) ShowCards();
    }

    private void FreightSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || FreightList.SelectedItem is not FreightBoardCard card)
            return;

        _ready = false;
        try
        {
            _selectedOffer = card.Source;
            if (card.IsDemo)
            {
                var (origin, destination, cargo, trailer) = card.Id switch
                {
                    "demo-grain" => ("Denver", "Phoenix", "Grain", "Hopper"),
                    "demo-steel" => ("Las Vegas", "Los Angeles", "Steel", "Flatbed"),
                    _ => ("Rapid City", "Denver", "Heavy machinery", "Lowboy")
                };
                NameBox.Text = "Demo: " + cargo;
                OriginBox.Text = origin;
                DestinationBox.Text = destination;
                CargoBox.Text = cargo;
                TrailerBox.Text = trailer;
                OriginCompanyBox.Text = "";
                DestinationCompanyBox.Text = "";
            }
            else if (card.Source is { } offer)
            {
                var shortCargo = FreightBoardService.ShortToken(offer.CargoUnit, "cargo.");
                var resolvedCargo = _catalog?.Cargo.FirstOrDefault(x =>
                    x.Id.Equals(shortCargo, StringComparison.OrdinalIgnoreCase));
                NameBox.Text = "Reference: " + shortCargo;
                CargoBox.Text = resolvedCargo?.Name ?? shortCargo;
                TrailerBox.Text = FreightBoardService.ShortToken(card.Trailer, "trailer.");
                OriginBox.Text = "";
                OriginCompanyBox.Text = "";
                DestinationBox.Text = "";
                DestinationCompanyBox.Text = "";
                var lastDot = offer.Target.LastIndexOf('.');
                if (lastDot > 0 && _catalog is not null)
                {
                    var companyToken = offer.Target[..lastDot];
                    var cityToken = offer.Target[(lastDot + 1)..];
                    var matchDepot = _catalog.Depots.FirstOrDefault(x =>
                        x.CompanyId.Equals(companyToken, StringComparison.OrdinalIgnoreCase) &&
                        x.CityId.Equals(cityToken, StringComparison.OrdinalIgnoreCase));
                    if (matchDepot is not null)
                    {
                        DestinationBox.Text = _catalog.Cities.FirstOrDefault(x =>
                            x.Id.Equals(matchDepot.CityId, StringComparison.OrdinalIgnoreCase))
                            ?.Name ?? matchDepot.CityId;
                        DestinationCompanyBox.Text = matchDepot.CompanyName;
                    }
                }
            }

            WaypointsBox.Clear();
            PayoutBox.Clear();
            CurrentCardStatus.Text = card.IsDemo
                ? "Example only. Change pickup, destination and cargo in the editor."
                : "Existing game offer used as a template; its source save is unchanged.";
        }
        finally
        {
            _ready = true;
            RefreshDraftPreview();
        }
    }

    private void DraftChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) RefreshDraftPreview();
    }

    private void RefreshDraftPreview()
    {
        MapOriginLabel.Text = string.IsNullOrWhiteSpace(OriginBox.Text)
            ? "Select pickup" : OriginBox.Text.Trim();
        MapDestinationLabel.Text = string.IsNullOrWhiteSpace(DestinationBox.Text)
            ? "Select destination" : DestinationBox.Text.Trim();
        var via = WaypointCities();
        MapViaLabel.Text = FreightBoardService.SchematicStops(via);
        var stops = via.Count == 0 ? " → " :
            " → " + string.Join(" → ", via) + " → ";
        RouteSummary.Text = MapOriginLabel.Text + stops + MapDestinationLabel.Text;

        try
        {
            var issues = DispatchValidator.Validate(CreateDraft());
            EditorValidation.Text = issues.Count == 0
                ? "Draft passes basic checks. In-game compatibility is NOT verified."
                : issues[0] + (issues.Count > 1 ? " (+" + (issues.Count - 1) + " more)" : "");
        }
        catch (InvalidDataException ex)
        {
            EditorValidation.Text = ex.Message;
        }
    }

    private List<string> WaypointCities() =>
        WaypointsBox.Text.Split(["\r\n", "\n", "\r"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private DispatchJob CreateDraft()
    {
        decimal? payment = null;
        if (!string.IsNullOrWhiteSpace(PayoutBox.Text))
        {
            if (!decimal.TryParse(PayoutBox.Text.Trim(), NumberStyles.Number,
                CultureInfo.CurrentCulture, out var amount))
                throw new InvalidDataException("Requested payout must be a number or blank.");
            payment = amount;
        }

        return new DispatchJob
        {
            Name = NameBox.Text.Trim(),
            OriginCity = OriginBox.Text.Trim(),
            OriginCompany = OriginCompanyBox.Text.Trim(),
            DestinationCity = DestinationBox.Text.Trim(),
            DestinationCompany = DestinationCompanyBox.Text.Trim(),
            Cargo = CargoBox.Text.Trim(),
            Trailer = TrailerBox.Text.Trim(),
            PaymentUsd = payment,
            Waypoints = WaypointCities(),
            ReferenceOfferUnit = _selectedOffer?.UnitId ?? "",
            ReferenceTarget = _selectedOffer?.Target ?? "",
            ReferenceCargoUnit = _selectedOffer?.CargoUnit ?? "",
            ReferenceTrailerVariant = _selectedOffer?.TrailerVariant ?? "",
            ReferenceTrailerDefinition = _selectedOffer?.TrailerDefinition ?? "",
            ReferenceCompanyTruck = _selectedOffer?.CompanyTruck ?? "",
            ReferenceUnitsCount = _selectedOffer?.UnitsCount
        };
    }

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = CreateDraft();
            var errors = DispatchValidator.Validate(draft);
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Fix job draft",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var picker = new SaveFileDialog
            {
                Title = "Export a draft, not a game save",
                Filter = "ATS job draft (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "custom-haul.json"
            };
            if (picker.ShowDialog(this) != true) return;
            JobFileService.Export(picker.FileName, draft);
            MarketStatus.Text = "Saved planning draft: " + picker.FileName +
                " • ATS was not modified.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Draft export failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportDraft_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Import a custom-haul draft",
            Filter = "ATS job draft (*.json)|*.json"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var job = JobFileService.Import(picker.FileName);
            _ready = false;
            try
            {
                FreightList.SelectedIndex = -1;
                NameBox.Text = job.Name;
                OriginBox.Text = job.OriginCity;
                OriginCompanyBox.Text = job.OriginCompany;
                DestinationBox.Text = job.DestinationCity;
                DestinationCompanyBox.Text = job.DestinationCompany;
                CargoBox.Text = job.Cargo;
                TrailerBox.Text = job.Trailer;
                PayoutBox.Text = job.PaymentUsd?.ToString(CultureInfo.CurrentCulture) ?? "";
                WaypointsBox.Text = string.Join(Environment.NewLine, job.Waypoints);
                _selectedOffer = string.IsNullOrWhiteSpace(job.ReferenceOfferUnit) ? null :
                    new SaveOfferTemplate(job.ReferenceOfferUnit, job.ReferenceTarget,
                        job.ReferenceCargoUnit, job.ReferenceTrailerVariant,
                        job.ReferenceTrailerDefinition, job.ReferenceCompanyTruck,
                        job.ReferenceUnitsCount, null, null);
            }
            finally
            {
                _ready = true;
            }
            RefreshDraftPreview();
            MarketStatus.Text = "Loaded planning draft from JSON. No ATS files changed.";
            CurrentCardStatus.Text = "Independent imported draft (no current freight card selected).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Draft import failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InspectDecoded_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Read a separately decoded copy of game.sii",
            Filter = "Decoded SII (*.sii;*.txt)|*.sii;*.txt|All files (*.*)|*.*"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var inspection = SaveOfferInspector.Inspect(picker.FileName);
            _ready = false;
            try
            {
                _cards = FreightBoardService.FromOffers(inspection.Offers);
                SearchBox.Clear();
                ShowCards();
            }
            finally
            {
                _ready = true;
            }
            FreightList.SelectedIndex = _cards.Count > 0 ? 0 : -1;
            MarketStatus.Text = inspection.OfferCount + " existing offer units read; " +
                inspection.Offers.Count + " usable references. Source file unchanged.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Offer inspection failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportCatalog_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose extracted game data / def folder" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var data = GameCatalogImporter.Import(picker.FolderName);
            if (data.Cities.Count == 0 || data.Cargo.Count == 0)
                throw new InvalidDataException("Selected folder has no usable city/cargo definitions.");
            _catalog = data;
            MarketStatus.Text = "Read " + data.Cities.Count + " cities, " +
                data.Cargo.Count + " cargo types and " + data.Depots.Count +
                " company-city entries. Compatibility is not guaranteed.";
            RefreshDraftPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Catalog import failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = CreateDraft();
            var problems = _catalog is null
                ? DispatchValidator.Validate(draft)
                : CatalogCompatibilityValidator.Check(draft, _catalog);
            var result = problems.Count == 0
                ? "No errors in available draft/catalog checks. This is NOT a dispatch-ready confirmation."
                : string.Join(Environment.NewLine, problems.Take(16));
            MessageBox.Show(result +
                "\n\nNo road graph, active profile compatibility, job injection, or ATS GPS control is present.",
                "Offline route and cargo review", MessageBoxButton.OK,
                problems.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            MarketStatus.Text = problems.Count == 0
                ? "Draft review complete; dispatch unavailable pending in-game testing."
                : problems.Count + " potential draft/catalog issue(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Preview unavailable",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReverseRoute_Click(object sender, RoutedEventArgs e)
    {
        var a = OriginBox.Text;
        OriginBox.Text = DestinationBox.Text;
        DestinationBox.Text = a;
        var company = OriginCompanyBox.Text;
        OriginCompanyBox.Text = DestinationCompanyBox.Text;
        DestinationCompanyBox.Text = company;
        var waypoints = WaypointCities();
        waypoints.Reverse();
        WaypointsBox.Text = string.Join(Environment.NewLine, waypoints);
        _selectedOffer = null;
        RefreshDraftPreview();
        MarketStatus.Text = "Reversed draft route; reference offer cleared. ATS save unchanged.";
    }

    private void FocusPickup_Click(object sender, RoutedEventArgs e) => OriginBox.Focus();
    private void FocusDestination_Click(object sender, RoutedEventArgs e) => DestinationBox.Focus();
    private void FocusWaypoint_Click(object sender, RoutedEventArgs e) => WaypointsBox.Focus();

    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        new MainWindow { Owner = this }.Show();
        MarketStatus.Text = "Advanced legacy importer and backup tools opened.";
    }
}
