namespace Plugin.Maui.Pulse;

sealed record PluginHook(
    string PackageId,
    string Lane,
    string[] InterfaceNames,
    string[] EventNames,
    string[] AssemblyHints);
