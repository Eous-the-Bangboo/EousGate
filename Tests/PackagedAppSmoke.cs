using EousGate;
using EousGate.Infrastructure;

internal static class PackagedAppSmoke
{
    // Explicit opt-in: opens only the image files supplied by the caller.
    // Runs in STA, matching the WPF drop handler's COM apartment.
    internal static int Run(string[] files)
    {
        if (files.Length == 0 || files.Any(path => !File.Exists(path)))
        {
            Console.Error.WriteLine("Supply existing image files for --photos-smoke.");
            return 1;
        }

        var exitCode = 1;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new JsonSettingsStore().Load();
                var discovery = new AppDiscovery(() => settings);
                var candidates = discovery.GetCandidates(files);
                var photos = candidates.FirstOrDefault(candidate => string.Equals(
                    candidate.AssociationHandlerName, "Microsoft.Windows.Photos_8wekyb3d8bbwe!App", StringComparison.OrdinalIgnoreCase));
                if (photos is null) throw new InvalidOperationException("Photos is not a visible common handler for the supplied extensions.");
                Console.WriteLine($"Selected: {photos.DisplayName}; ID: {photos.AssociationHandlerName}; file count: {files.Length}");
                var result = new OpenFileService().Open(files, photos);
                Console.WriteLine(result.Success ? "Windows accepted Photos file activation. Verify the displayed image in Photos." : result.Error);
                exitCode = result.Success ? 0 : 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }
}
