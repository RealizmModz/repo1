using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ATS.Dispatcher.Core;

public sealed record CargoFlowModResult(
    string PackagePath, string OriginCompanyId, string DestinationCompanyId, string CargoId);

/// <summary>
/// Builds an independent .scs ZIP containing two cargo_def associations.
/// Adds an existing cargo to shipping/receiving company lists; does not
/// force a route or create a Freight Market job. Does not install the mod.
/// </summary>
public static class CargoFlowModBuilder
{
    private static readonly Regex SafeId = new(
        "^[a-zA-Z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CargoFlowModResult Create(
        GameCatalog catalog, DispatchJob job, string outputFile)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);

        var issues = DispatchValidator.Validate(job);
        if (issues.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, issues));

        if (string.IsNullOrWhiteSpace(job.OriginCompany) ||
            string.IsNullOrWhiteSpace(job.DestinationCompany))
            throw new InvalidDataException(
                "Select both a pickup company and a destination company.");

        static bool Match(string input, string id, string name) =>
            string.Equals(input.Trim(), id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input.Trim(), name, StringComparison.OrdinalIgnoreCase);

        CatalogCity? ResolveCity(string value)
        {
            var candidates = catalog.Cities.Where(x => Match(value, x.Id, x.Name)).ToArray();
            return candidates.Length == 1 ? candidates[0] : null;
        }

        var origin = ResolveCity(job.OriginCity)
            ?? throw new InvalidDataException("Pickup city is unknown or ambiguous in catalog.");
        var destination = ResolveCity(job.DestinationCity)
            ?? throw new InvalidDataException("Destination city is unknown or ambiguous in catalog.");

        CatalogDepot ResolveDepot(CatalogCity city, string company, string direction)
        {
            var matches = catalog.Depots.Where(x =>
                string.Equals(x.CityId, city.Id, StringComparison.OrdinalIgnoreCase) &&
                Match(company, x.CompanyId, x.CompanyName))
                .DistinctBy(x => x.CompanyId, StringComparer.OrdinalIgnoreCase).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException(direction +
                    " company is missing or ambiguous at selected city.");
            return matches[0];
        }

        var shipper = ResolveDepot(origin, job.OriginCompany, "Pickup");
        var receiver = ResolveDepot(destination, job.DestinationCompany, "Destination");
        var matchesCargo = catalog.Cargo.Where(x => Match(job.Cargo, x.Id, x.Name)).ToArray();
        if (matchesCargo.Length != 1)
            throw new InvalidDataException("Cargo is missing or ambiguous in imported definitions.");
        var cargo = matchesCargo[0];

        // Early beta conservatively rejects non-file-safe token IDs.
        foreach (var id in new[] { shipper.CompanyId, receiver.CompanyId, cargo.Id })
        {
            if (!SafeId.IsMatch(id))
                throw new InvalidDataException(
                    "Company/cargo IDs must contain only ASCII letters, numbers or underscore.");
        }

        var knownConfigurations = catalog.Trailers.Where(x =>
            string.Equals(x.CargoId, cargo.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var trailer = job.Trailer.Trim();
        if (cargo.BodyTypes.Count > 0 || knownConfigurations.Length > 0)
        {
            var valid = cargo.BodyTypes.Any(x => string.Equals(x, trailer,
                StringComparison.OrdinalIgnoreCase)) || knownConfigurations.Any(x =>
                    string.Equals(x.Id, trailer, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.BodyType, trailer, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.TrailerUnit, trailer, StringComparison.OrdinalIgnoreCase));
            if (!valid)
                throw new InvalidDataException(
                    "Trailer selection is not supported by this cargo's extracted definitions.");
        }

        var target = Path.GetFullPath(outputFile);
        if (!string.Equals(Path.GetExtension(target), ".scs", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Output must be an .scs mod archive.");
        if (File.Exists(target))
            throw new IOException("Refusing to overwrite an existing .scs file.");

        // Complete the ZIP away from the requested output name before publishing.
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Add(zip, "manifest.sii", """
                    SiiNunit
                    {
                    mod_package : .package_name {
                      package_version: "0.1"
                      display_name: "ATS Dispatcher - Cargo Flow Test"
                      author: "ATS Dispatcher"
                      category[]: "cargo_pack"
                      description_file: "description.txt"
                    }
                    }
                    """);
                Add(zip, "description.txt",
                    "Generated cargo flow test package.\n" +
                    "Adds an existing cargo to the shipping and receiving lists of selected companies.\n" +
                    $"From: {shipper.CompanyName} ({origin.Name})\n" +
                    $"To: {receiver.CompanyName} ({destination.Name})\n" +
                    $"Cargo: {cargo.Name}\n\n" +
                    "This does NOT force a city-to-city delivery, GPS route, trailer or payout.\n" +
                    "Activate only in a disposable ATS profile; verify game.log.txt and mod load order.\n" +
                    "Revert by disabling/removing this mod and refreshing the economy as needed.\n");
                var association = "SiiNunit\n{\ncargo_def : .ats_dispatch {\n" +
                    "  cargo: \"cargo." + cargo.Id + "\"\n}\n}\n";
                Add(zip, $"def/company/{shipper.CompanyId}/out/{cargo.Id}.sii",
                    association);
                Add(zip, $"def/company/{receiver.CompanyId}/in/{cargo.Id}.sii",
                    association);
            }

            using (var zip = ZipFile.OpenRead(temporary))
            {
                if (zip.Entries.Count != 4 || zip.GetEntry("manifest.sii") is null ||
                    zip.GetEntry($"def/company/{shipper.CompanyId}/out/{cargo.Id}.sii") is null ||
                    zip.GetEntry($"def/company/{receiver.CompanyId}/in/{cargo.Id}.sii") is null)
                    throw new InvalidDataException("Generated mod archive is incomplete.");
            }

            File.Move(temporary, target, overwrite: false);
            return new CargoFlowModResult(target, shipper.CompanyId, receiver.CompanyId, cargo.Id);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void Add(ZipArchive zip, string entryName, string value)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(value);
    }
}
