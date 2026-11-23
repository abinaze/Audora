using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class NativeExportsTests
{
    private static void PutU16(byte[] b, int at, int value)
    {
        b[at] = (byte)value;
        b[at + 1] = (byte)(value >> 8);
    }

    private static void PutU32(byte[] b, int at, int value)
    {
        for (var i = 0; i < 4; i++)
        {
            b[at + i] = (byte)(value >> (8 * i));
        }
    }

    // A tiny 64-bit library file with one section that holds an export table.
    private static byte[] FakeLibrary(bool withExports, params string[] names)
    {
        var file = new byte[0x600];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        PutU32(file, 0x3C, 0x80);
        file[0x80] = (byte)'P';
        file[0x81] = (byte)'E';
        PutU16(file, 0x86, 1);
        PutU16(file, 0x94, 240);
        var optional = 0x80 + 24;
        PutU16(file, optional, 0x20B);
        PutU32(file, optional + 112, withExports ? 0x1000 : 0);
        PutU32(file, optional + 116, 0x100);
        var section = optional + 240;
        PutU32(file, section + 8, 0x200);
        PutU32(file, section + 12, 0x1000);
        PutU32(file, section + 16, 0x200);
        PutU32(file, section + 20, 0x400);

        var body = 0x400;
        PutU32(file, body + 24, names.Length);
        PutU32(file, body + 32, 0x1000 + 0x40);
        var position = 0x80;
        for (var i = 0; i < names.Length; i++)
        {
            PutU32(file, body + 0x40 + (4 * i), 0x1000 + position);
            var bytes = System.Text.Encoding.ASCII.GetBytes(names[i]);
            Array.Copy(bytes, 0, file, body + position, bytes.Length);
            position += bytes.Length + 1;
        }

        return file;
    }

    [Fact]
    public void ItFindsTheNamesALibraryOffersInOrder()
    {
        var names = NativeExports.Parse(FakeLibrary(true, "df_process_frame", "df_create"));

        Assert.Equal(new[] { "df_create", "df_process_frame" }, names);
    }

    [Fact]
    public void ALibraryWithoutAnExportTableHasNoNames()
    {
        Assert.Empty(NativeExports.Parse(FakeLibrary(false)));
    }

    [Fact]
    public void AFileThatIsNotAProgramIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => NativeExports.Parse(new byte[200]));
    }

    [Fact]
    public void AFileThatIsCutShortIsRefused()
    {
        var whole = FakeLibrary(true, "df_create");
        var cut = new byte[0x150];
        Array.Copy(whole, cut, cut.Length);

        Assert.Throws<InvalidDataException>(() => NativeExports.Parse(cut));
    }

    [Fact]
    public void ItReadsARealWindowsLibrary()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");

        var names = NativeExports.List(path);

        Assert.Contains("CreateFileW", names);
    }
}
