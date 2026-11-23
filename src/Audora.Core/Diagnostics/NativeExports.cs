namespace Audora.Core.Diagnostics;

/// <summary>
/// Lists the function names that a native Windows library (.dll) offers, by reading its file. Nothing is loaded or run,
/// so it is safe to use on a library you have not decided to trust yet.
/// </summary>
public static class NativeExports
{
    public static IReadOnlyList<string> List(string path) => Parse(File.ReadAllBytes(path));

    public static IReadOnlyList<string> Parse(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < 0x40 || file[0] != (byte)'M' || file[1] != (byte)'Z')
        {
            throw new InvalidDataException("This is not a Windows program file.");
        }

        try
        {
            return ReadNames(file);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("The file is damaged or cut short.", ex);
        }
    }

    private static IReadOnlyList<string> ReadNames(byte[] file)
    {
        var pe = BitConverter.ToInt32(file, 0x3C);
        if (pe < 0 || pe + 24 > file.Length || file[pe] != (byte)'P' || file[pe + 1] != (byte)'E' || file[pe + 2] != 0 || file[pe + 3] != 0)
        {
            throw new InvalidDataException("The file header is not valid.");
        }

        var sectionCount = BitConverter.ToUInt16(file, pe + 6);
        var optionalSize = BitConverter.ToUInt16(file, pe + 20);
        var optional = pe + 24;
        var magic = BitConverter.ToUInt16(file, optional);
        var directory = optional + (magic == 0x20B ? 112 : 96);
        var exportRva = BitConverter.ToUInt32(file, directory);
        if (exportRva == 0)
        {
            return Array.Empty<string>();
        }

        var sections = optional + optionalSize;

        int ToOffset(uint rva)
        {
            for (var i = 0; i < sectionCount; i++)
            {
                var entry = sections + (i * 40);
                var virtualSize = BitConverter.ToUInt32(file, entry + 8);
                var virtualAddress = BitConverter.ToUInt32(file, entry + 12);
                var rawSize = BitConverter.ToUInt32(file, entry + 16);
                var rawPointer = BitConverter.ToUInt32(file, entry + 20);
                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                {
                    return checked((int)(rva - virtualAddress + rawPointer));
                }
            }

            throw new InvalidDataException("The export table points outside the file.");
        }

        var table = ToOffset(exportRva);
        var nameCount = BitConverter.ToUInt32(file, table + 24);
        var names = ToOffset(BitConverter.ToUInt32(file, table + 32));
        var result = new List<string>();
        for (var i = 0; i < nameCount; i++)
        {
            var start = ToOffset(BitConverter.ToUInt32(file, names + (4 * i)));
            var end = Array.IndexOf(file, (byte)0, start);
            if (end < 0)
            {
                throw new InvalidDataException("A function name is cut short.");
            }

            result.Add(System.Text.Encoding.ASCII.GetString(file, start, end - start));
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
