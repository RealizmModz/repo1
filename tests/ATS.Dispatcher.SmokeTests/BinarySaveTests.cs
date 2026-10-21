using ATS.Dispatcher.Core;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal static class BinarySaveTests
{
    public static void Run()
    {
        var original = Fixture();
        var expected = original.ToArray();
        var parsed = BinarySaveDocument.Read(original);
        var inspection = parsed.ToInspection();
        Check(inspection.OfferCount == 2 && inspection.Offers.Count == 1,
            "Empty reserved offer slot must be counted but not advertised");
        Check(inspection.MissingRequiredFieldsCount == 1, "Missing slot accounting");
        Check(inspection.Offers[0].CargoUnit == "cargo.test", "Binary cargo token");
        Check(inspection.Offers[0].UnitsCount == 12 &&
              inspection.Offers[0].ShortestDistanceKm == 221, "Binary numeric fields");
        Check(inspection.ObservedUnitTypes["company.volatile.test.city"] == "company",
            "Company ownership index");
        Check(inspection.SourceSha256 == Convert.ToHexString(SHA256.HashData(original)),
            "Hash must describe exact source bytes");
        Check(original.SequenceEqual(expected), "Inspection changed caller bytes");
        var encrypted = Envelope(original);
        var decoded = BinarySaveDocument.Read(encrypted).ToInspection();
        Check(decoded.Format == GameSaveFormat.CompressedScs &&
              decoded.Offers[0] == inspection.Offers[0], "ScsC offer parity");
        Check(decoded.SourceSha256 == Convert.ToHexString(SHA256.HashData(encrypted)),
            "Encoded source fingerprint");
        var overDeclared = encrypted.ToArray();
        BitConverter.GetBytes(uint.MaxValue).CopyTo(overDeclared, 52);
        Reject(overDeclared, "Huge decoded length");
        Reject(original[..^1], "Missing terminator");
        Reject(original.Concat(new byte[] { 0 }).ToArray(), "Trailing data");
        var wrongVersion = original.ToArray(); wrongVersion[4] = 4;
        Reject(wrongVersion, "Unknown binary version");
        Reject(Fixture(dangling: true), "Dangling offer owner reference");
        Reject(Fixture(duplicate: true), "Duplicate unit ID");
        Reject(Fixture(unknownType: true), "Unknown future field type");
        var root = Path.Combine(Path.GetTempPath(), "ats-binary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "game-copy.sii");
            File.WriteAllBytes(path, encrypted);
            Check(SaveOfferInspector.Inspect(path).Offers.Count == 1,
                "Inspector must accept encoded copies without external decoder");
            Check(File.ReadAllBytes(path).SequenceEqual(encrypted), "File inspection wrote save");
        }
        finally { Directory.Delete(root, true); }
    }

    private static byte[] Fixture(bool dangling = false, bool duplicate = false, bool unknownType = false)
    {
        using var memory = new MemoryStream();
        using var w = new BinaryWriter(memory, Encoding.UTF8, true);
        void Text(string value) { var b = Encoding.UTF8.GetBytes(value); w.Write(b.Length); w.Write(b); }
        void Pointer(string value)
        {
            if (value == "null") { w.Write((byte)0); return; }
            var parts = value.Split('.'); w.Write((byte)parts.Length);
            foreach (var part in parts)
            {
                ulong n = 0, factor = 1;
                const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz_";
                foreach (char c in part) { n += (ulong)(alphabet.IndexOf(c) + 1) * factor; factor *= 38; }
                w.Write(n);
            }
        }
        void Schema(uint id, string type, params (uint Type, string Name)[] fields)
        {
            w.Write(0u); w.Write((byte)1); w.Write(id); Text(type);
            foreach (var field in fields) { w.Write(field.Type); Text(field.Name); }
            w.Write(0u);
        }
        w.Write("BSII"u8); w.Write(3u);
        Schema(1, "company", (58, "job_offer"), (39, "future_opaque_number"));
        Schema(2, "job_offer_data", (1, "target"), (39, "expiration_time"),
            (61, "cargo"), (61, "trailer_variant"), (61, "trailer_definition"),
            (1, "company_truck"), (39, "units_count"), (43, "shortest_distance_km"));
        if (unknownType) Schema(3, "future_unit", (999, "new_kind"));
        w.Write(1u); Pointer("company.volatile.test.city"); w.Write(2u);
        Pointer(dangling ? "absent" : "offer"); Pointer("empty"); w.Write(123456789u);
        void Offer(string id, bool empty)
        {
            w.Write(2u); Pointer(id); Text(empty ? "" : "receiver.city"); w.Write(830u);
            Pointer(empty ? "null" : "cargo.test"); Pointer("trailer.test"); Pointer("trailer_def.test");
            Text("truck.test"); w.Write(12u); w.Write((ushort)221);
        }
        Offer("offer", false); Offer(duplicate ? "offer" : "empty", true);
        w.Write(0u); w.Write((byte)0);
        return memory.ToArray();
    }

    private static byte[] Envelope(byte[] payload)
    {
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true)) z.Write(payload);
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString("2A5FCB1791D22FB60245B3D8369ED0B2C27371563FBF1F3C9EDF6B11825A5D0A");
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var encoder = aes.CreateEncryptor();
        var source = compressed.ToArray();
        var cipher = encoder.TransformFinalBlock(source, 0, source.Length);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write("ScsC"u8); writer.Write(new byte[32]); writer.Write(aes.IV);
        writer.Write((uint)payload.Length); writer.Write(cipher);
        return output.ToArray();
    }

    private static void Reject(byte[] bytes, string label)
    {
        try { BinarySaveDocument.Read(bytes).ToInspection(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(label + " was accepted");
    }
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
    }
}
