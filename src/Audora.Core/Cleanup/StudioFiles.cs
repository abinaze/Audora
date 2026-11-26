namespace Audora.Core.Cleanup;

/// <summary>Finds the files the Studio engine needs: the DeepFilterNet library and its model, in a "studio" folder beside the app.</summary>
public static class StudioFiles
{
    public const string FolderName = "studio";
    public const string LibraryName = "deepfilter.dll";
    public const string ModelName = "DeepFilterNet3_onnx.tar.gz";

    /// <summary>The folder the Studio files belong in, for an app that lives in <paramref name="appFolder"/>.</summary>
    public static string FolderIn(string appFolder) => Path.Combine(appFolder, FolderName);

    /// <summary>The library and the model in <paramref name="folder"/>, or null unless both are there.</summary>
    public static (string Library, string Model)? Locate(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var library = Path.Combine(folder, LibraryName);
        var model = Path.Combine(folder, ModelName);
        return File.Exists(library) && File.Exists(model) ? (library, model) : null;
    }
}
