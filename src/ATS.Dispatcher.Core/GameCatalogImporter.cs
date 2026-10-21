namespace ATS.Dispatcher.Core;

/// <summary>
/// Reads loose files from an already-extracted /def tree. Never opens .scs
/// archives, edits save games or assumes a mod/DLC is actually enabled.
/// </summary>
public static class GameCatalogImporter
{
    public static GameCatalog Import(string selectedFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedFolder);
        var root = Path.GetFullPath(selectedFolder);
        var defs = string.Equals(
            Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            "def", StringComparison.OrdinalIgnoreCase) ? root : Path.Combine(root, "def");
        if (!Directory.Exists(defs))
            throw new DirectoryNotFoundException("Select an extracted folder containing 'def', or 'def' itself.");

        int filesRead = 0;
        IReadOnlyDictionary<string, List<string>> Read(string file)
        {
            filesRead++;
            return SiiPropertyReader.Read(file);
        }
        var cities = new Dictionary<string, CatalogCity>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FindFiles(Path.Combine(defs, "city")))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var data = Read(file);
            cities[id] = new CatalogCity(id,
                SiiPropertyReader.First(data, "city_name",
                    SiiPropertyReader.First(data, "name", id)));
        }
        var cargo = new Dictionary<string, CatalogCargo>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FindFiles(Path.Combine(defs, "cargo")))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var data = Read(file);
            cargo[id] = new CatalogCargo(id, SiiPropertyReader.First(data, "name", id),
                SiiPropertyReader.All(data, "body_types")
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        var trailers = new List<CatalogTrailer>();
        var cargoRoot = Path.Combine(defs, "cargo");
        foreach (var folder in FindDirectories(cargoRoot))
        {
            foreach (var file in FindFiles(folder))
            {
                var data = Read(file);
                trailers.Add(new CatalogTrailer(Path.GetFileName(folder),
                    Path.GetFileNameWithoutExtension(file),
                    SiiPropertyReader.First(data, "body_type"),
                    SiiPropertyReader.First(data, "trailer")));
            }
        }
        var depots = new List<CatalogDepot>();
        var companyRoot = Path.Combine(defs, "company");
        foreach (var folder in FindDirectories(companyRoot))
        {
            var companyId = Path.GetFileName(folder);
            var nameFile = Path.Combine(companyRoot, companyId + ".sii");
            var companyName = File.Exists(nameFile)
                ? SiiPropertyReader.First(Read(nameFile), "name", companyId) : companyId;
            var outgoing = ReadCargoLinks(Path.Combine(folder, "out"), Read);
            var incoming = ReadCargoLinks(Path.Combine(folder, "in"), Read);

            foreach (var file in FindFiles(Path.Combine(folder, "editor")))
            {
                var data = Read(file);
                var cityId = TokenId(SiiPropertyReader.First(data, "city",
                    Path.GetFileNameWithoutExtension(file)), "city.");
                if (!cities.ContainsKey(cityId))
                    cities[cityId] = new CatalogCity(cityId, cityId.Replace('_', ' '));
                depots.Add(new CatalogDepot(companyId, companyName, cityId, outgoing, incoming));
            }
        }
        return new GameCatalog
        {
            SourceDirectory = defs,
            DefinitionFilesRead = filesRead,
            Cities = cities.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Cargo = cargo.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Depots = depots.OrderBy(x => x.CityId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase).ToArray(),
            Trailers = trailers.OrderBy(x => x.CargoId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static IReadOnlyList<string> ReadCargoLinks(string directory,
        Func<string, IReadOnlyDictionary<string, List<string>>> read)
    {
        var cargo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FindFiles(directory))
        {
            var raw = SiiPropertyReader.First(read(file), "cargo",
                Path.GetFileNameWithoutExtension(file));
            cargo.Add(TokenId(raw, "cargo."));
        }
        return cargo.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static string TokenId(string raw, string prefix) =>
        raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..] : raw;

    private static IEnumerable<string> FindFiles(string path) =>
        Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.sii", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase) : [];
    private static IEnumerable<string> FindDirectories(string path) =>
        Directory.Exists(path)
            ? Directory.EnumerateDirectories(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            : [];
}
