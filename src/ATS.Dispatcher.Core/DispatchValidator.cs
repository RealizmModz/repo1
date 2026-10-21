namespace ATS.Dispatcher.Core;

public static class DispatchValidator
{
    public static IReadOnlyList<string> Validate(DispatchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(job.OriginCity))
            errors.Add("Enter a pickup city.");
        if (string.IsNullOrWhiteSpace(job.DestinationCity))
            errors.Add("Enter a destination city.");
        if (!string.IsNullOrWhiteSpace(job.OriginCity) &&
            string.Equals(job.OriginCity.Trim(), job.DestinationCity.Trim(), StringComparison.OrdinalIgnoreCase))
            errors.Add("Pickup and destination cannot be the same city.");
        if (string.IsNullOrWhiteSpace(job.Cargo))
            errors.Add("Enter cargo to haul.");
        if (string.IsNullOrWhiteSpace(job.Trailer))
            errors.Add("Enter a trailer type.");
        if (job.PaymentUsd < 0)
            errors.Add("Payment cannot be negative.");

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var via in job.Waypoints ?? [])
        {
            if (string.IsNullOrWhiteSpace(via))
            {
                errors.Add("Waypoints cannot be blank.");
                continue;
            }

            if (!visited.Add(via.Trim()))
                errors.Add("A waypoint appears more than once: " + via.Trim());
        }

        return errors;
    }
}
