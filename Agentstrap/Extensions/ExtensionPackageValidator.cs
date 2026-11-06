namespace Agentstrap.Extensions
{
    public static class ExtensionPackageValidator
    {
        public const long MaxArchiveBytes = 32 * 1024 * 1024;
        public const long MaxExpandedBytes = 64 * 1024 * 1024;
        public const int MaxEntries = 256;

        // Snapshot published by Roblox on 2025-09-29. Roblox states that the list can change.
        private static readonly HashSet<string> AllowedFlags = new(StringComparer.Ordinal)
        {
            "DFFlagTextureQualityOverrideEnabled", "DFIntTextureQualityOverride", "FIntDebugForceMSAASamples",
            "DFFlagDisableDPIScale", "FFlagDebugGraphicsPreferD3D11", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL"
        };

        private static readonly HashSet<string> DeniedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".com", ".scr", ".msi", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".lua", ".luau", ".js", ".mjs", ".cjs", ".vbs", ".wsf", ".py", ".pyc", ".sh", ".rb", ".jar", ".class", ".so", ".dylib", ".asi", ".rbxl", ".rbxlx", ".rbxm", ".rbxmx" };

        public static bool IsAllowedConfiguration(string key, string value)
        {
            if (!AllowedFlags.Contains(key) || !FastFlagValidator.TryValidate(key, value, out _)) return false;
            if (key.StartsWith("FFlag", StringComparison.Ordinal) || key.StartsWith("DFFlag", StringComparison.Ordinal)) return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(value, out int number)) return false;
            return key switch
            {
                "DFFlagTextureQualityOverrideEnabled" or "DFFlagDisableDPIScale" or "FFlagDebugGraphicsPreferD3D11" or "FFlagDebugGraphicsPreferVulkan" or "FFlagDebugGraphicsPreferOpenGL" => true,
                "DFIntTextureQualityOverride" => number is >= 0 and <= 3,
                "FIntDebugForceMSAASamples" => number is 0 or 1 or 2 or 4,
                _ => false
            };
        }

        public static bool IsAllowedAssetTarget(string target)
        {
            if (!IsSafeRelativePath(target, out var normalized)) return false;
            string path = normalized.Replace('\\', '/');
            return path.StartsWith("content/textures/cursors/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("content/sounds/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("content/fonts/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("platformcontent/pc/textures/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSafeRelativePath(string path, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(path) || path.Length > 240 || path.Contains(':') || path.Contains('\0')) return false;
            if (Path.IsPathRooted(path) || path.StartsWith('/') || path.StartsWith('\\')) return false;
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Any(p => p is ".." or "." || p.EndsWith('.') || p.EndsWith(' '))) return false;
            normalized = Path.Combine(parts);
            return true;
        }

        public static bool IsAllowedPackageFile(string path)
        {
            string ext = Path.GetExtension(path);
            return !DeniedExtensions.Contains(ext) && !path.Equals("disabled.flag", StringComparison.OrdinalIgnoreCase)
                && (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".json", StringComparison.OrdinalIgnoreCase) || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".md", StringComparison.OrdinalIgnoreCase) || ext.Equals(".license", StringComparison.OrdinalIgnoreCase));
        }

        public static bool ValidateManifestJson(string json, out ExtensionManifest? manifest, out List<string> errors)
        {
            errors = new(); manifest = null;
            try { manifest = JsonSerializer.Deserialize<ExtensionManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }); }
            catch (Exception ex) { errors.Add("Manifest JSON is invalid: " + ex.Message); return false; }
            if (manifest is null) { errors.Add("Manifest is empty."); return false; }
            return manifest.IsValid(out errors);
        }

        public static bool ValidateArchiveEntries(IEnumerable<string> entryNames, out List<string> errors)
        {
            errors = new(); var names = entryNames.ToList();
            if (names.Count > MaxEntries) errors.Add($"Archive exceeds the {MaxEntries} file limit.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                if (!IsSafeRelativePath(name, out var normalized)) { errors.Add("Unsafe archive entry path: " + name); continue; }
                if (!seen.Add(normalized)) errors.Add("Duplicate archive path: " + name);
                if (!IsAllowedPackageFile(normalized)) errors.Add("Executable or unsupported package file: " + name);
            }
            return errors.Count == 0;
        }
    }
}
