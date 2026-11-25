using Audora.Core.Cleanup;

namespace Audora.Cli;

// The engines the benchmark runs. RNNoise is always there. DeepFilterNet is added when AUDORA_DFN_DIR points at a folder
// that holds deepfilter.dll and DeepFilterNet3_onnx.tar.gz.
internal static partial class LabCommand
{
    private static (string Name, Func<IFrameDenoiser> Factory, float Strength)[] Engines
    {
        get
        {
            var list = BaseEngines.ToList();
            var directory = Environment.GetEnvironmentVariable("AUDORA_DFN_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                var library = Path.Combine(directory, "deepfilter.dll");
                var model = Path.Combine(directory, "DeepFilterNet3_onnx.tar.gz");
                if (File.Exists(library) && File.Exists(model))
                {
                    list.Add(("DeepFilterNet3 (C library), strength 100%", () => DeepFilterDenoiser.Create(library, model), 1f));
                }
                else
                {
                    Console.WriteLine("AUDORA_DFN_DIR is set, but deepfilter.dll or DeepFilterNet3_onnx.tar.gz is missing there.");
                }
            }

            return list.ToArray();
        }
    }
}
