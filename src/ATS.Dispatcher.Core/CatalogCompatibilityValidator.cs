namespace ATS.Dispatcher.Core;

/// <summary>
/// Reports compatibility issues against imported definitions. A clean
/// result does not guarantee the job will spawn in ATS.
/// </summary>
public static class CatalogCompatibilityValidator
{
    public static IReadOnlyList<string> Check(DispatchJob job, GameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(catalog);
        var issues = DispatchValidator.Validate(job).ToList();
        static bool Matches(string input, string id, string name) =>
            string.Equals(input.Trim(), id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input.Trim(), name, StringComparison.OrdinalIgnoreCase);

        var origin = catalog.Cities.FirstOrDefault(x => Matches(job.OriginCity, x.Id, x.Name));
        var destination = catalog.Cities.FirstOrDefault(x => Matches(job.DestinationCity, x.Id, x.Name));
        var cargo = catalog.Cargo.FirstOrDefault(x => Matches(job.Cargo, x.Id, x.Name));
        if (origin is null) issues.Add("Pickup city was not found in the extracted catalog.");
        if (destination is null) issues.Add("Destination city was not found in the extracted catalog.");
        if (cargo is null) issues.Add("Cargo was not found in the extracted catalog.");

        if (origin is not null && cargo is not null)
            CheckDepot(origin, cargo, job.OriginCompany, catalog.Depots, true, issues);
        if (destination is not null && cargo is not null)
            CheckDepot(destination, cargo, job.DestinationCompany, catalog.Depots, false, issues);

        if (cargo is not null && !string.IsNullOrWhiteSpace(job.Trailer))
        {
            var configurations = catalog.Trailers.Where(x =>
                string.Equals(x.CargoId, cargo.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
            var requested = job.Trailer.Trim();
            var matchesBodyType = cargo.BodyTypes.Any(x =>
                string.Equals(x, requested, StringComparison.OrdinalIgnoreCase));
            var matchesConfiguration = configurations.Any(x =>
                string.Equals(x.Id, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.BodyType, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.TrailerUnit, requested, StringComparison.OrdinalIgnoreCase));
            if ((cargo.BodyTypes.Count > 0 || configurations.Length > 0) &&
                !matchesBodyType && !matchesConfiguration)
                issues.Add("Trailer doesn't match the cargo's extracted body types/configurations.");
        }
        foreach (var waypoint in job.Waypoints ?? [])
        {
            if (!catalog.Cities.Any(x => Matches(waypoint, x.Id, x.Name)))
                issues.Add("Route waypoint is not in the extracted catalog: " + waypoint);
        }
        return issues;
    }

    private static void CheckDepot(CatalogCity city, CatalogCargo cargo, string company,
        IReadOnlyList<CatalogDepot> depots, bool origin, List<string> issues)
    {
        var candidates = depots.Where(x =>
            string.Equals(x.CityId, city.Id, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(company) ||
             string.Equals(x.CompanyId, company.Trim(), StringComparison.OrdinalIgnoreCase) ||
             string.Equals(x.CompanyName, company.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
        var label = origin ? "Pickup" : "Destination";
        if (candidates.Length == 0)
        {
            issues.Add(label + " has no matching company depot in extracted definitions.");
            return;
        }
        if (!candidates.Any(x =>
            (origin ? x.OutgoingCargo : x.IncomingCargo).Any(id =>
                string.Equals(id, cargo.Id, StringComparison.OrdinalIgnoreCase))))
            issues.Add(label + " depot doesn't " + (origin ? "produce" : "accept") +
                " this cargo in extracted definitions.");
    }
}
