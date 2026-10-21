using ATS.Dispatcher.Core;
using System.Security.Cryptography;
using System.Text;

internal static class OfferIntegrityTests
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "ats-integrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string Wrap(string units) => "SiiNunit\n{\n" + units + "\n}\n";
            string Offer(string id, string properties = "target: receiver.city\ncargo: cargo.steel") =>
                "job_offer_data : " + id + " {\n" + properties + "\n}\n";
            string Save(string name, string text)
            {
                var path = Path.Combine(root, name + ".sii");
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return path;
            }
            void Reject(string name, string text)
            {
                var path = Save(name, text);
                var bytes = File.ReadAllBytes(path);
                Throws(() => SaveOfferInspector.Inspect(path), name + " was accepted");
                Check(File.ReadAllBytes(path).SequenceEqual(bytes), name + " changed source");
            }

            var valid = Save("valid", Wrap(Offer("one",
                "target: receiver.city\ncargo: cargo.steel\ntrailer_variant: trailer.flatbed\n" +
                "trailer_definition: trailer_def.steel\ncompany_truck: truck.test\n" +
                "units_count: 12\nshortest_distance_km: 100\nexpiration_time: 9999999999")));
            var original = File.ReadAllBytes(valid);
            var observed = SaveOfferInspector.Inspect(valid);
            Check(observed.OfferCount == 1 && observed.Offers.Count == 1, "Valid offer count");
            Check(observed.SourceSha256 == Convert.ToHexString(SHA256.HashData(original)), "Exact source hash");
            Check(observed.MissingRequiredFieldsCount == 0, "Complete offer classified incomplete");
            var offer = observed.Offers[0];
            Check(offer.Target == "receiver.city" && offer.CargoUnit == "cargo.steel" &&
                offer.TrailerVariant == "trailer.flatbed" &&
                offer.TrailerDefinition == "trailer_def.steel" && offer.CompanyTruck == "truck.test" &&
                offer.UnitsCount == 12 && offer.ShortestDistanceKm == 100 &&
                offer.ExpirationTime == 9999999999L, "Observed fields changed");
            Check(File.ReadAllBytes(valid).SequenceEqual(original), "Valid inspection changed source");

            Reject("duplicate-identical", Wrap(Offer("same") + Offer("same")));
            Reject("duplicate-conflicting", Wrap(Offer("same") +
                Offer("same", "target: other.city\ncargo: cargo.logs")));
            Reject("duplicate-incomplete", Wrap(Offer("same", "cargo: cargo.steel") + Offer("same")));
            Reject("conflicting-property", Wrap(Offer("one",
                "target: receiver.city\ncargo: cargo.steel\ncargo: cargo.logs")));
            Reject("invalid-number", Wrap(Offer("one",
                "target: receiver.city\ncargo: cargo.steel\nunits_count: twelve")));
            Reject("overflow-number", Wrap(Offer("one",
                "target: receiver.city\ncargo: cargo.steel\nunits_count: 9999999999999")));
            Reject("missing-envelope", "SiiNunit\n" + Offer("one"));
            Reject("truncated-envelope", "SiiNunit\n{\n" + Offer("one"));
            Reject("truncated-unrelated", "SiiNunit\n{\neconomy : test {\n}\n");
            Reject("truncated-offer", "SiiNunit\n{\njob_offer_data : one {\ncargo: cargo.steel\n");
            Reject("extra-close", Wrap(Offer("one")) + "}\n");
            Reject("trailing-data", Wrap(Offer("one")) + "ignored data\n");
            Reject("nested-offer", Wrap("economy : test {\n" + Offer("one") + "}\n"));
            Reject("nested-braces", Wrap(Offer("one", "target: receiver.city\ncargo: cargo.steel\n{")));
            Reject("mixed-brace-order", Wrap("}{"));
            Reject("malformed-header", Wrap("job_offer_data : broken\n{\n}\n"));
            Reject("unterminated-string", Wrap(Offer("one", "target: \"receiver.city\ncargo: cargo.steel")));
            Reject("unterminated-comment", Wrap(Offer("one")) + "/* unfinished");
            Reject("wrong-header", "SiiNonsense\n{\n}\n");

            var comments = Save("comments", Wrap(
                "// job_offer_data : fake {\n/* } { */\n" +
                "economy : test {\nlabel: \"quoted } { # // /* text\"\n}\n" +
                Offer("one", "target: \"receiver.city\" // }\n" +
                    "cargo: cargo.steel # {\ncargo: \"cargo.steel\"\n" +
                    "/* comment {\n } */\nunits_count: 12")));
            var commented = SaveOfferInspector.Inspect(comments);
            Check(commented.OfferCount == 1 && commented.Offers[0].UnitsCount == 12,
                "Comments/quoted braces or identical repeated value mishandled");

            var partial = SaveOfferInspector.Inspect(Save("partial", Wrap(
                Offer("one", "cargo: cargo.steel") +
                Offer("two", "target: receiver.city") +
                Offer("three", "cargo: \"\"\ntarget: receiver.city") + Offer("four"))));
            Check(partial.OfferCount == 4 && partial.Offers.Count == 1 &&
                partial.MissingRequiredFieldsCount == 3 && !partial.ResultsTruncated,
                "Missing required fields were silently treated as complete");
            Throws(() => OfferSnapshotDiffService.Compare(partial, observed),
                "Incomplete before snapshot compared");
            Throws(() => OfferSnapshotDiffService.Compare(observed, partial),
                "Incomplete after snapshot compared");
            Check(OfferSnapshotDiffService.Compare(observed, observed).Changes.Count == 0,
                "Unchanged snapshot should have no differences");

            var empty = SaveOfferInspector.Inspect(Save("empty", Wrap("")));
            Check(empty.OfferCount == 0 && !empty.ResultsTruncated, "Empty envelope rejected");
            var cap = new StringBuilder();
            for (int i = 0; i < 25001; i++) cap.Append(Offer("id." + i));
            var cappedPath = Save("capped", Wrap(cap.ToString()));
            var capped = SaveOfferInspector.Inspect(cappedPath);
            Check(capped.OfferCount == 25001 && capped.Offers.Count == 25000 &&
                capped.ResultsTruncated, "Record cap did not retain completeness flag");
            Throws(() => OfferSnapshotDiffService.Compare(capped, observed), "Capped snapshot compared");
            // Validation must continue beyond the display cap.
            Reject("duplicate-beyond-cap", Wrap(cap + Offer("id.25000")));
            Reject("conflict-beyond-cap", Wrap(cap + Offer("last",
                "target: receiver.city\ncargo: cargo.steel\ncargo: cargo.logs")));

            var invalidUtf8 = Path.Combine(root, "invalid-utf8.sii");
            var invalidBytes = Encoding.UTF8.GetBytes(Wrap(Offer("one"))).Concat(new byte[] { 0xff }).ToArray();
            File.WriteAllBytes(invalidUtf8, invalidBytes);
            Throws(() => SaveOfferInspector.Inspect(invalidUtf8), "Invalid UTF-8 accepted");
            Check(File.ReadAllBytes(invalidUtf8).SequenceEqual(invalidBytes), "Invalid UTF-8 source changed");

            var oversized = Path.Combine(root, "oversized.sii");
            using (var stream = File.Create(oversized)) stream.SetLength(256L * 1024 * 1024 + 1);
            Throws(() => SaveOfferInspector.Inspect(oversized), "Oversized file accepted");
            Check(new FileInfo(oversized).Length == 256L * 1024 * 1024 + 1, "Oversized source changed");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }
}
