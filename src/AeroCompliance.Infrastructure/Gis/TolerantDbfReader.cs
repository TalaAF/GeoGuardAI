using System.Text;

namespace AeroCompliance.Infrastructure.Gis;

/// <summary>
/// A deliberately permissive dBASE III/IV attribute reader.
/// </summary>
/// <remarks>
/// The supplied municipal DBFs are not clean: <c>parcels_fixed.dbf</c> declares
/// <c>PARCEL_ARE</c> as numeric(24,15) but stores raw binary in it for some records, and no file
/// carries a <c>.cpg</c> or a language-driver byte, so the text encoding is undeclared. A strict
/// reader throws on the first such record and loses all 13,436 parcels.
///
/// This reader therefore does the only thing that is safe on dirty data: it reads every field as
/// raw bytes and returns text. Nothing is coerced to a number, so no record can fail to parse.
/// Callers that want a number convert it themselves and decide what an unconvertible value means.
/// It reads only the fields asked for, never the whole row.
/// </remarks>
public sealed class TolerantDbfReader : IDisposable
{
    private readonly FileStream stream;
    private readonly Encoding encoding;
    private readonly int headerSize;
    private readonly int recordSize;
    private readonly List<(string Name, int Offset, int Length)> fields = [];

    public int RecordCount { get; }
    public IReadOnlyList<string> FieldNames => fields.Select(f => f.Name).ToList();

    public TolerantDbfReader(string dbfPath, string? cpgPath = null)
    {
        stream = File.OpenRead(dbfPath);
        var header = new byte[32];
        ReadExactly(header, 32);

        RecordCount = BitConverter.ToInt32(header, 4);
        headerSize = BitConverter.ToUInt16(header, 8);
        recordSize = BitConverter.ToUInt16(header, 10);
        encoding = ResolveEncoding(header[29], cpgPath);

        // Field descriptors: 32 bytes each, terminated by 0x0D.
        var offset = 1; // Byte 0 of each record is the deletion flag.
        var descriptor = new byte[32];
        while (stream.Position < headerSize - 1)
        {
            var first = stream.ReadByte();
            if (first < 0 || first == 0x0D) break;
            descriptor[0] = (byte)first;
            ReadExactly(descriptor.AsSpan(1), 31);

            var name = Encoding.ASCII.GetString(descriptor, 0, 11).TrimEnd('\0', ' ');
            int length = descriptor[16];
            if (!string.IsNullOrEmpty(name)) fields.Add((name, offset, length));
            offset += length;
        }
    }

    /// <summary>
    /// Reads the requested fields for record <paramref name="index"/> (0-based) as trimmed text.
    /// A field that is absent, empty, or unreadable simply yields null.
    /// </summary>
    public Dictionary<string, string?> ReadRecord(int index, IReadOnlyCollection<string> wanted)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (index < 0 || index >= RecordCount) return result;

        var buffer = new byte[recordSize];
        stream.Position = headerSize + (long)index * recordSize;
        var read = stream.Read(buffer, 0, recordSize);
        if (read < recordSize) return result;

        foreach (var (name, fieldOffset, length) in fields)
        {
            if (!wanted.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            if (fieldOffset + length > buffer.Length) continue;

            var raw = encoding.GetString(buffer, fieldOffset, length);
            // Strip control characters that binary-in-a-text-field produces, then trim padding.
            var cleaned = new string(raw.Where(c => !char.IsControl(c)).ToArray()).Trim();
            result[name] = cleaned.Length == 0 ? null : cleaned;
        }
        return result;
    }

    /// <summary>
    /// Picks a text encoding: an explicit .cpg wins, then the DBF language-driver byte, then
    /// Latin-1 — which maps every byte to a character and so can never throw on unknown bytes.
    /// </summary>
    private static Encoding ResolveEncoding(byte languageDriverId, string? cpgPath)
    {
        Encoding fallback;
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            fallback = Encoding.GetEncoding("ISO-8859-1");
        }
        catch
        {
            fallback = Encoding.UTF8;
        }

        if (cpgPath is not null && File.Exists(cpgPath))
        {
            var text = File.ReadAllText(cpgPath).Trim();
            try
            {
                return int.TryParse(text, out var codePage) ? Encoding.GetEncoding(codePage) : Encoding.GetEncoding(text);
            }
            catch { /* fall through to the language-driver byte */ }
        }

        try
        {
            return languageDriverId switch
            {
                0x01 => Encoding.GetEncoding(437),
                0x02 => Encoding.GetEncoding(850),
                0x03 => Encoding.GetEncoding(1252),
                0x57 => Encoding.GetEncoding(1252),
                0x59 => Encoding.GetEncoding(1252),
                0x64 => Encoding.GetEncoding(852),
                0x65 => Encoding.GetEncoding(866),
                0x6A => Encoding.GetEncoding(737),
                0x7C => Encoding.GetEncoding(874),
                0x78 => Encoding.GetEncoding(950),
                0x79 => Encoding.GetEncoding(949),
                0x7B => Encoding.GetEncoding(932),
                0x7A => Encoding.GetEncoding(936),
                0xC8 => Encoding.GetEncoding(1250),
                0xC9 => Encoding.GetEncoding(1251),
                0xCB => Encoding.GetEncoding(1253),
                0xCC => Encoding.GetEncoding(1254),
                0x87 => Encoding.GetEncoding(1256), // Arabic — the plausible case for this data.
                _ => fallback,
            };
        }
        catch
        {
            return fallback;
        }
    }

    private void ReadExactly(byte[] buffer, int count) => ReadExactly(buffer.AsSpan(), count);

    private void ReadExactly(Span<byte> buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer[total..count]);
            if (read <= 0) throw new EndOfStreamException("Unexpected end of DBF header.");
            total += read;
        }
    }

    public void Dispose() => stream.Dispose();
}
