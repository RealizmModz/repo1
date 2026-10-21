using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ATS.Dispatcher.Core;

/// <summary>
/// Experimental, read-only decoding/indexing for copied BSII v3 saves.
/// Format layout references are documented in docs/EXPERIMENTAL_DISPATCH.md.
/// Retains the original payload and every byte span; never regenerates unknown fields.
/// </summary>
public sealed class BinarySaveDocument
{
    internal const int Limit = 64 * 1024 * 1024;
    internal readonly byte[] Payload;
    internal readonly Dictionary<uint, BinarySchema> Schemas = [];
    internal readonly Dictionary<string, BinaryUnit> Units = new(StringComparer.Ordinal);
    internal int Terminator;
    public string SourceSha256 { get; }
    public string PayloadSha256 => Convert.ToHexString(SHA256.HashData(Payload));
    public int UnitCount => Units.Count;
    public uint BinaryVersion => 3;
    public GameSaveFormat SourceFormat { get; private init; }

    private BinarySaveDocument(byte[] source, byte[] payload)
    {
        SourceSha256 = Convert.ToHexString(SHA256.HashData(source));
        Payload = payload;
        Parse();
    }

    public static BinarySaveDocument Read(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length < 8 || source.Length > Limit)
            throw new InvalidDataException("Copied save is empty, truncated or above the 64 MiB research limit.");
        var bytes = source.ToArray();
        if (bytes.AsSpan(0, 4).SequenceEqual("ScsC"u8))
        {
            if (bytes.Length < 72 || (bytes.Length - 56) % 16 != 0)
                throw new InvalidDataException("Invalid ScsC envelope.");
            var expected = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(52, 4));
            if (expected < 8 || expected > Limit)
                throw new InvalidDataException("Unsupported decoded length.");
            using var aes = Aes.Create();
            // Public community format constant, not a user credential.
            aes.Key = Convert.FromHexString("2A5FCB1791D22FB60245B3D8369ED0B2C27371563FBF1F3C9EDF6B11825A5D0A");
            aes.IV = bytes.AsSpan(36, 16).ToArray();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            byte[] packed;
            try
            {
                using var decryptor = aes.CreateDecryptor();
                packed = decryptor.TransformFinalBlock(bytes, 56, bytes.Length - 56);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidDataException("ScsC copy could not be decoded.", ex);
            }
            using var input = new MemoryStream(packed, false);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream((int)expected);
            var buffer = new byte[8192];
            int read;
            while ((read = zlib.Read(buffer)) != 0)
            {
                if (output.Length + read > expected)
                    throw new InvalidDataException("Decoded size exceeds the envelope declaration.");
                output.Write(buffer, 0, read);
            }
            bytes = output.ToArray();
            if (bytes.Length != expected)
                throw new InvalidDataException("Decoded size differs from the envelope declaration.");
            // The legacy envelope MAC algorithm is not established here. Never
            // advertise authentication or write an invented encrypted envelope.
        }
        return new BinarySaveDocument(source.ToArray(), bytes)
        {
            SourceFormat = source.AsSpan(0, 4).SequenceEqual("ScsC"u8)
                ? GameSaveFormat.CompressedScs : GameSaveFormat.BinarySii
        };
    }

    public SaveOfferInspection ToInspection()
    {
        var references = Offers();
        var usable = references.Where(x => x.Target.Length > 0 && x.CargoUnit != "null").ToArray();
        var offers = usable.Take(25000).Select(x =>
        {
            var unit = Units[x.UnitId];
            var count = Number(unit, "units_count");
            if (count > int.MaxValue) throw new InvalidDataException("Offer units exceed supported range.");
            var distance = Field(unit, "shortest_distance_km", 0x2b);
            return new SaveOfferTemplate(x.UnitId, x.Target, x.CargoUnit, x.TrailerVariant,
                x.TrailerDefinition, Text(unit, "company_truck"), (int)count,
                (int)BinaryPrimitives.ReadUInt16LittleEndian(Payload.AsSpan(distance.Start, 2)),
                x.ExpirationTime);
        }).ToArray();
        return new SaveOfferInspection(references.Count, offers, usable.Length > 25000, SourceFormat)
        {
            SourceSha256 = SourceSha256,
            MissingRequiredFieldsCount = references.Count - usable.Length,
            ObservedUnitTypes = Units.ToDictionary(x => x.Key, x => x.Value.Type, StringComparer.Ordinal),
            TopLevelUnitIndexComplete = true
        };
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private void Parse()
    {
        var r = new BinaryCursor(Payload);
        if (!r.Bytes(4).SequenceEqual("BSII"u8.ToArray()) || r.U32() != 3)
            throw new InvalidDataException("This experiment only supports copied BSII version 3 payloads.");
        bool ended = false;
        while (r.Position < Payload.Length)
        {
            int start = r.Position;
            uint kind = r.U32();
            if (kind == 0)
            {
                byte valid = r.U8();
                if (valid == 0) { Terminator = start; ended = true; break; }
                if (valid != 1) throw new InvalidDataException("Invalid schema flag.");
                uint id = r.U32();
                string name = r.String();
                var fields = new List<BinaryField>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                uint type;
                while ((type = r.U32()) != 0)
                {
                    if (fields.Count >= 512) throw new InvalidDataException("Too many schema fields.");
                    string key = r.String();
                    if (!keys.Add(key)) throw new InvalidDataException("Duplicate schema field.");
                    if (type == 0x37)
                    {
                        int count = r.Count();
                        for (int i = 0; i < count; i++) { r.U32(); r.String(); }
                    }
                    BinaryCursor.ValidateType(type);
                    fields.Add(new BinaryField(key, type));
                }
                if (id == 0 || !Schemas.TryAdd(id, new BinarySchema(name, fields)))
                    throw new InvalidDataException("Duplicate or missing schema identity.");
            }
            else
            {
                if (!Schemas.TryGetValue(kind, out var schema))
                    throw new InvalidDataException("Unit references an unknown schema.");
                int idStart = r.Position;
                string id = r.Pointer();
                int idEnd = r.Position;
                var spans = new Dictionary<string, BinarySpan>(StringComparer.Ordinal);
                foreach (var field in schema.Fields)
                {
                    int fieldStart = r.Position;
                    r.SkipValue(field.Type);
                    spans.Add(field.Name, new BinarySpan(fieldStart, r.Position, field.Type));
                }
                if (id == "null" || !Units.TryAdd(id,
                    new BinaryUnit(id, schema.Name, kind, start, r.Position, idStart, idEnd, spans)))
                    throw new InvalidDataException("Duplicate or null unit identity.");
                if (Units.Count > 100000) throw new InvalidDataException("Too many units for this experiment.");
            }
        }
        if (!ended || r.Position != Payload.Length)
            throw new InvalidDataException("Missing terminator or trailing BSII data.");
    }

    internal string Text(BinaryUnit unit, string key)
    {
        var span = Field(unit, key, 1);
        return new BinaryCursor(Payload, span.Start).String();
    }
    internal uint Number(BinaryUnit unit, string key)
    {
        var span = Field(unit, key, 0x27);
        return new BinaryCursor(Payload, span.Start).U32();
    }
    internal IReadOnlyList<string> Links(BinaryUnit unit, string key)
    {
        var span = Field(unit, key, 0x3a);
        var r = new BinaryCursor(Payload, span.Start);
        int count = r.Count();
        var links = new List<string>(count);
        for (int i = 0; i < count; i++) links.Add(r.Pointer());
        return links;
    }
    internal BinarySpan Field(BinaryUnit unit, string key, uint type)
    {
        if (!unit.Fields.TryGetValue(key, out var span) || span.Type != type)
            throw new InvalidDataException("Unsupported field schema: " + unit.Type + "." + key);
        return span;
    }

    public IReadOnlyList<BinaryOfferReference> Offers()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var company in Units.Values.Where(x => x.Type == "company"))
        {
            foreach (var link in Links(company, "job_offer"))
            {
                if (!Units.TryGetValue(link, out var offer) || offer.Type != "job_offer_data" ||
                    !owners.TryAdd(link, company.Id))
                    throw new InvalidDataException("Offer ownership is dangling or ambiguous.");
            }
        }
        var result = new List<BinaryOfferReference>();
        foreach (var unit in Units.Values.Where(x => x.Type == "job_offer_data"))
        {
            if (!owners.TryGetValue(unit.Id, out var owner))
                throw new InvalidDataException("An offer is not owned by a company.");
            string PointerField(string name)
            {
                var span = Field(unit, name, 0x3d);
                return new BinaryCursor(Payload, span.Start).Pointer();
            }
            result.Add(new BinaryOfferReference(unit.Id, owner, Text(unit, "target"),
                PointerField("cargo"), PointerField("trailer_variant"), PointerField("trailer_definition"),
                Number(unit, "expiration_time")));
        }
        return result.AsReadOnly();
    }
}

public sealed record BinaryOfferReference(string UnitId, string SourceCompanyUnit, string Target,
    string CargoUnit, string TrailerVariant, string TrailerDefinition, uint ExpirationTime)
{
    public string Display => SourceCompanyUnit.Replace("company.volatile.", "") +
        " → " + Target + " • " + CargoUnit + " • " + UnitId;
}
internal sealed record BinaryField(string Name, uint Type);
internal sealed record BinarySchema(string Name, IReadOnlyList<BinaryField> Fields);
internal sealed record BinarySpan(int Start, int End, uint Type);
internal sealed record BinaryUnit(string Id, string Type, uint Schema, int Start, int End,
    int IdStart, int IdEnd, IReadOnlyDictionary<string, BinarySpan> Fields);

internal sealed class BinaryCursor
{
    private readonly byte[] data;
    internal int Position { get; private set; }
    internal BinaryCursor(byte[] bytes, int position = 0) { data = bytes; Position = position; }
    internal byte[] Bytes(int count)
    {
        if (count < 0 || count > data.Length - Position)
            throw new InvalidDataException("BSII field exceeds copied input.");
        var value = data.AsSpan(Position, count).ToArray();
        Position += count;
        return value;
    }
    internal byte U8() => Bytes(1)[0];
    internal uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(4));
    internal ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Bytes(8));
    internal int Count()
    {
        uint count = U32();
        if (count > 1000000) throw new InvalidDataException("BSII array exceeds research limit.");
        return (int)count;
    }
    internal string String()
    {
        int size = Count();
        try { return new UTF8Encoding(false, true).GetString(Bytes(size)); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Invalid UTF-8 string.", ex); }
    }
    internal string Pointer()
    {
        byte parts = U8();
        if (parts == 0) return "null";
        if (parts == 255)
        {
            ulong value = U64();
            if (value == 0) return "_nameless.0";
            var words = new List<string>();
            while (value != 0)
            {
                words.Insert(0, (value & 65535).ToString(value >> 16 == 0 ? "x" : "x4"));
                value >>= 16;
            }
            return "_nameless." + string.Join(".", words);
        }
        if (parts > 16) throw new InvalidDataException("Unsupported pointer length.");
        var tokens = new List<string>();
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz_";
        for (int i = 0; i < parts; i++)
        {
            ulong value = U64() & ~(1UL << 63);
            var token = new StringBuilder();
            while (value != 0)
            {
                ulong digit = value % 38;
                if (digit == 0) throw new InvalidDataException("Invalid encoded token.");
                token.Append(alphabet[(int)digit - 1]);
                value /= 38;
            }
            if (token.Length == 0) throw new InvalidDataException("Empty pointer token.");
            tokens.Add(token.ToString());
        }
        return string.Join(".", tokens);
    }
    internal static void ValidateType(uint type)
    {
        if (type is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 9 or 10 or 17 or 18 or
            23 or 24 or 25 or 26 or 37 or 38 or 39 or 40 or 41 or 42 or 43 or 44 or
            47 or 49 or 50 or 51 or 52 or 53 or 54 or 55 or 57 or 58 or 59 or 60 or 61 or 62 or 65) return;
        throw new InvalidDataException("Unsupported BSII field type " + type);
    }
    internal void SkipValue(uint type)
    {
        ValidateType(type);
        if (type is 2 or 4 or 6 or 10 or 18 or 24 or 26 or 38 or 40 or 42 or 44 or
            50 or 52 or 54 or 58 or 60 or 62)
        {
            int count = Count();
            for (int i = 0; i < count; i++) SkipValue(type - 1);
            return;
        }
        if (type == 1) { Bytes(Count()); return; }
        if (type is 57 or 59 or 61) { Pointer(); return; }
        int size = type switch
        {
            3 or 49 or 51 or 65 => 8,
            5 or 37 or 39 or 47 or 55 => 4,
            41 or 43 => 2,
            53 => 1,
            7 => 8,
            9 or 17 => 12,
            23 => 16,
            25 => 32,
            _ => throw new InvalidDataException("Unsupported BSII scalar.")
        };
        Bytes(size);
    }
}
