namespace Agentstrap.Extensions
{
    /// <summary>Declarative, data-only extension package. Package contents are never executed.</summary>
    public sealed class ExtensionManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string License { get; set; } = "";
        public string Category { get; set; } = "Visuals";
        public string Type { get; set; } = "AssetPack";
        public string MinimumAgentstrapVersion { get; set; } = "";
        public List<string> SupportedOperatingSystems { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public string EntryType { get; set; } = "declarative";
        public List<string> Dependencies { get; set; } = new();
        public string? EntryPath { get; set; }
        public string? ConflictGroup { get; set; }
        public List<string> Conflicts { get; set; } = new();
        public List<string> CompatibilityRequirements { get; set; } = new();
        public bool DefaultEnabled { get; set; }
        public Dictionary<string, string> AssetMappings { get; set; } = new();
        public Dictionary<string, string> Configuration { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? SourceUrl { get; set; }
        public string? Attribution { get; set; }
        public string? Icon { get; set; }

        public bool IsValid(out List<string> errors)
        {
            errors = new();
            Id ??= ""; Name ??= ""; Version ??= ""; Description ??= ""; Author ??= ""; License ??= "";
            Category ??= ""; Type ??= ""; EntryType ??= ""; MinimumAgentstrapVersion ??= "";
            AssetMappings ??= new(StringComparer.OrdinalIgnoreCase); Configuration ??= new(StringComparer.Ordinal);
            Tags ??= new(); Conflicts ??= new(); CompatibilityRequirements ??= new(); Dependencies ??= new(); Permissions ??= new(); SupportedOperatingSystems ??= new();
            if (ManifestVersion != 1) errors.Add("Unsupported manifest version.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(Id ?? "", "^[a-zA-Z0-9._-]{1,80}$")) errors.Add("Id must use letters, numbers, dot, underscore or dash.");
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100) errors.Add("Name is required and must be at most 100 characters.");
            if (string.IsNullOrWhiteSpace(Version) || Version.Length > 40) errors.Add("Version is required.");
            if (string.IsNullOrWhiteSpace(Description) || Description.Length > 1000) errors.Add("Description is required and must be at most 1000 characters.");
            if (!System.Version.TryParse(MinimumAgentstrapVersion, out _)) errors.Add("MinimumAgentstrapVersion must be a valid version.");
            if (!SupportedOperatingSystems.Contains("Windows", StringComparer.OrdinalIgnoreCase)) errors.Add("This Agentstrap build only supports Windows extension packages.");
            if (EntryType != "declarative" || !string.IsNullOrEmpty(EntryPath) || (Permissions?.Count ?? 0) != 0 || (Dependencies?.Count ?? 0) != 0)
                errors.Add("Executable entries, permissions and dependencies are not supported.");
            if (!new[] { "Visuals", "Audio", "Graphics", "Performance", "Configuration" }.Contains(Category, StringComparer.OrdinalIgnoreCase)) errors.Add("Unsupported category.");
            if (!new[] { "AssetPack", "ClientConfiguration" }.Contains(Type, StringComparer.OrdinalIgnoreCase)) errors.Add("Unsupported extension type.");
            if (AssetMappings.Count > 128) errors.Add("An extension may map at most 128 assets.");
            foreach (var pair in AssetMappings)
            {
                if (!ExtensionPackageValidator.IsSafeRelativePath(pair.Key, out _) || !ExtensionPackageValidator.IsAllowedAssetTarget(pair.Value)) errors.Add($"Invalid asset mapping: {pair.Key}.");
            }
            if (!string.IsNullOrWhiteSpace(Icon) && (!ExtensionPackageValidator.IsSafeRelativePath(Icon, out _) || !Path.GetExtension(Icon).Equals(".png", StringComparison.OrdinalIgnoreCase))) errors.Add("Icon must be a safe relative PNG path.");
            if (Configuration.Count > 32) errors.Add("An extension may configure at most 32 settings.");
            foreach (var pair in Configuration)
                if (!ExtensionPackageValidator.IsAllowedConfiguration(pair.Key, pair.Value)) errors.Add($"Unsupported client configuration setting or value: {pair.Key}.");
            string[] renderers = { "FFlagDebugGraphicsPreferD3D11", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL" };
            if (renderers.Count(key => Configuration.TryGetValue(key, out string? value) && value.Equals("true", StringComparison.OrdinalIgnoreCase)) > 1)
                errors.Add("Only one rendering backend may be selected.");
            if (Type.Equals("AssetPack", StringComparison.OrdinalIgnoreCase) && (AssetMappings.Count == 0 || Configuration.Count != 0)) errors.Add("Asset packs need asset mappings and cannot contain client configuration.");
            if (Type.Equals("ClientConfiguration", StringComparison.OrdinalIgnoreCase) && (Configuration.Count == 0 || AssetMappings.Count != 0)) errors.Add("Configuration extensions need supported settings and cannot contain assets.");
            return errors.Count == 0;
        }
    }
}
