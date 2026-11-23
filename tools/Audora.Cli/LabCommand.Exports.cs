using Audora.Core.Diagnostics;

namespace Audora.Cli;

// Reading the function names of a native library without loading it.
internal static partial class LabCommand
{
    private static int Exports(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("That file does not exist.");
            return 1;
        }

        try
        {
            var names = NativeExports.List(path);
            Console.WriteLine($"{names.Count} named functions in {Path.GetFileName(path)}. Nothing was loaded or run.");
            foreach (var name in names)
            {
                Console.WriteLine($"  {name}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read it: {ex.Message}");
            return 1;
        }
    }
}
