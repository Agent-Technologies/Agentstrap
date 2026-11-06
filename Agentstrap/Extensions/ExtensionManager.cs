using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace Agentstrap.Extensions
{
    public sealed class InstalledExtension
    {
        public ExtensionManifest Manifest { get; set; } = new();
        public string InstallPath { get; set; } = "";
        public bool Enabled { get; set; }
        public DateTime InstalledUtc { get; set; } = DateTime.UtcNow;
        public string? Error { get; set; }
    }

    /// <summary>Manages declarative packages and contributes them to Agentstrap's existing prelaunch overlay.</summary>
    public sealed class ExtensionManager
    {
        public string ExtensionsDirectory { get; }
        private readonly string _modificationsDirectory;
        private readonly string? _clientRoot;
        private string StateDirectory => Path.Combine(ExtensionsDirectory, ".state");

        public ExtensionManager(string extensionsDirectory, string? modificationsDirectory = null, string? clientRoot = null)
        {
            ExtensionsDirectory = Path.GetFullPath(extensionsDirectory);
            string? defaultModifications = string.IsNullOrWhiteSpace(Paths.Modifications) ? Path.Combine(ExtensionsDirectory, ".overlay") : Paths.Modifications;
            _modificationsDirectory = Path.GetFullPath(modificationsDirectory ?? defaultModifications);
            _clientRoot = clientRoot is null ? null : Path.GetFullPath(clientRoot);
            Directory.CreateDirectory(ExtensionsDirectory);
            Directory.CreateDirectory(StateDirectory);
        }

        public string GetExtensionDirectory(string id) => Path.Combine(ExtensionsDirectory, Sanitize(id));

        public InstalledExtension InstallBuiltIn(ExtensionManifest manifest, IReadOnlyDictionary<string, byte[]> files)
        {
            if (!manifest.IsValid(out var errors)) throw new InvalidDataException(string.Join("; ", errors));
            ValidateMappedAssets(manifest, files.Keys);
            return InstallFiles(manifest, files.Select(x => (x.Key, x.Value)).Append(("manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest))));
        }

        public IReadOnlyList<InstalledExtension> ListInstalled()
        {
            var result = new List<InstalledExtension>();
            foreach (var dir in Directory.GetDirectories(ExtensionsDirectory))
            {
                if (Path.GetFileName(dir).Equals(".state", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string path = Path.Combine(dir, "manifest.json");
                    if (!File.Exists(path)) continue;
                    if (!ExtensionPackageValidator.ValidateManifestJson(File.ReadAllText(path), out var manifest, out _) || manifest is null) continue;
                    bool enabled = File.Exists(StatePath(manifest.Id));
                    string? compatibilityError = null;
                    if (Version.TryParse(manifest.MinimumAgentstrapVersion, out var requiredVersion) && Version.TryParse(App.Version, out var currentVersion) && currentVersion < requiredVersion)
                        compatibilityError = $"This extension requires Agentstrap {requiredVersion} or later.";
                    if (compatibilityError is null && manifest.AssetMappings.Count > 0)
                    {
                        var managedAssets = ReadAssetState(manifest.Id).ToDictionary(x => x.Target, StringComparer.OrdinalIgnoreCase);
                        foreach (string target in manifest.AssetMappings.Values)
                        {
                            try
                            {
                                if (!File.Exists(GetActiveClientPath(target))) { compatibilityError = $"This Roblox version does not contain {target}; the extension will be skipped at launch."; break; }
                                string overlay = SafeCombine(_modificationsDirectory, target);
                                if (!managedAssets.TryGetValue(target, out var managed) || !File.Exists(overlay) || Hash(overlay) != managed.AppliedHash)
                                { compatibilityError = $"Managed asset {target} changed after activation. It will be skipped until the conflict is resolved."; break; }
                            }
                            catch (Exception ex) { compatibilityError = ex.Message; break; }
                        }
                    }
                    result.Add(new InstalledExtension { Manifest = manifest, InstallPath = dir, Enabled = enabled, InstalledUtc = Directory.GetCreationTimeUtc(dir), Error = compatibilityError });
                }
                catch { /* corrupted packages remain untouched and are reported by import/diagnostics */ }
            }
            return result;
        }

        public InstalledExtension InstallFromDirectory(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException(sourceDirectory);
            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links and special directories are not supported.");
            var all = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            if (all.Length > ExtensionPackageValidator.MaxEntries) throw new InvalidDataException("Package contains too many files.");
            long total = 0;
            foreach (string file in all)
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links and special files are not supported.");
                string rel = Path.GetRelativePath(sourceDirectory, file);
                if (!ExtensionPackageValidator.IsSafeRelativePath(rel, out _) || !ExtensionPackageValidator.IsAllowedPackageFile(rel)) throw new InvalidDataException("Unsafe or executable package file: " + rel);
                long length = new FileInfo(file).Length;
                if (length > 16 * 1024 * 1024) throw new InvalidDataException("Package file exceeds the per-file size limit: " + rel);
                total += length;
                if (total > ExtensionPackageValidator.MaxExpandedBytes) throw new InvalidDataException("Package exceeds the expanded size limit.");
            }
            string manifestPath = Path.Combine(sourceDirectory, "manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidDataException("manifest.json not found.");
            string json = File.ReadAllText(manifestPath);
            if (!ExtensionPackageValidator.ValidateManifestJson(json, out var manifest, out var errors) || manifest is null) throw new InvalidDataException(string.Join("; ", errors));
            ValidateMappedAssets(manifest, all.Select(f => Path.GetRelativePath(sourceDirectory, f)));
            return InstallFiles(manifest, all.Select(f => (Path.GetRelativePath(sourceDirectory, f), File.ReadAllBytes(f))));
        }

        public InstalledExtension InstallFromZip(string zipPath)
        {
            var info = new FileInfo(zipPath);
            if (!info.Exists) throw new FileNotFoundException(zipPath);
            if (info.Length > ExtensionPackageValidator.MaxArchiveBytes) throw new InvalidDataException("Package exceeds the archive size limit.");
            using var archive = ZipFile.OpenRead(zipPath);
            if (!ExtensionPackageValidator.ValidateArchiveEntries(archive.Entries.Select(e => e.FullName), out var errors)) throw new InvalidDataException(string.Join("; ", errors));
            long expanded = 0;
            foreach (var e in archive.Entries)
            {
                // Unix file type bits: reject symlinks, devices and other special entries.
                int unixType = (e.ExternalAttributes >> 16) & 0xF000;
                if (unixType != 0 && unixType != 0x8000 && unixType != 0x4000) throw new InvalidDataException("Archive contains a link or special file.");
                expanded += e.Length;
                if (expanded > ExtensionPackageValidator.MaxExpandedBytes) throw new InvalidDataException("Package exceeds the expanded size limit.");
            }
            var entry = archive.Entries.SingleOrDefault(e => e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("manifest.json not found at the archive root.");
            if (entry.Length > 128 * 1024) throw new InvalidDataException("Manifest exceeds the size limit.");
            string json; using (var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();
            if (!ExtensionPackageValidator.ValidateManifestJson(json, out var manifest, out var manifestErrors) || manifest is null) throw new InvalidDataException(string.Join("; ", manifestErrors));
            ValidateMappedAssets(manifest, archive.Entries.Select(e => e.FullName));
            return InstallFiles(manifest, archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => (e.FullName, ReadEntry(e))));
        }

        public bool Enable(string id)
        {
            var ext = Get(id) ?? throw new InvalidOperationException("Extension is not installed.");
            if (Version.TryParse(ext.Manifest.MinimumAgentstrapVersion, out var requiredVersion) && Version.TryParse(App.Version, out var currentVersion) && currentVersion < requiredVersion)
                throw new InvalidOperationException($"This extension requires Agentstrap {requiredVersion} or later.");
            string activeVersion = _clientRoot is null ? App.PlayerState.Prop.VersionGuid : Path.GetFileName(_clientRoot);
            string activeRoot = _clientRoot ?? Path.Combine(Paths.Versions, activeVersion);
            if (string.IsNullOrWhiteSpace(activeVersion) || !File.Exists(Path.Combine(activeRoot, "RobloxPlayerBeta.exe")))
                throw new InvalidOperationException("Install Roblox through Agentstrap before enabling extensions.");
            if (ext.Manifest.AssetMappings.Count > 0)
            {
                foreach (var other in ListInstalled().Where(x => x.Enabled && x.Manifest.Id != id))
                {
                    EnsureNotDeclaredConflict(ext.Manifest, other.Manifest);
                    if (!string.IsNullOrWhiteSpace(ext.Manifest.ConflictGroup) && ext.Manifest.ConflictGroup.Equals(other.Manifest.ConflictGroup, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Conflict group '{ext.Manifest.ConflictGroup}' is already active through {other.Manifest.Name}.");
                    var targets = other.Manifest.AssetMappings.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var conflict = ext.Manifest.AssetMappings.Values.FirstOrDefault(targets.Contains);
                    if (conflict is not null) throw new InvalidOperationException($"File conflict: {conflict} is already owned by {other.Manifest.Name}. Disable it first.");
                }
            }
            else
            {
                foreach (var other in ListInstalled().Where(x => x.Enabled && x.Manifest.Id != id))
                {
                    EnsureNotDeclaredConflict(ext.Manifest, other.Manifest);
                    if (!string.IsNullOrWhiteSpace(ext.Manifest.ConflictGroup) && ext.Manifest.ConflictGroup.Equals(other.Manifest.ConflictGroup, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Conflict group '{ext.Manifest.ConflictGroup}' is already active through {other.Manifest.Name}.");
                    if (ext.Manifest.Configuration.Keys.Intersect(other.Manifest.Configuration.Keys, StringComparer.Ordinal).Any())
                        throw new InvalidOperationException($"A setting used by {other.Manifest.Name} is already managed. Disable it first.");
                }
            }
            foreach (var pair in ext.Manifest.Configuration)
                if (!ExtensionPackageValidator.IsAllowedConfiguration(pair.Key, pair.Value)) throw new InvalidDataException("Unsupported FastFlag or value: " + pair.Key);
            foreach (var mapping in ext.Manifest.AssetMappings)
            {
                string source = SafeCombine(ext.InstallPath, mapping.Key);
                string destination = SafeCombine(_modificationsDirectory, mapping.Value);
                if (!File.Exists(source)) throw new InvalidDataException("Mapped asset is missing: " + mapping.Key);
                string clientPath = GetActiveClientPath(mapping.Value);
                if (!File.Exists(clientPath)) throw new InvalidOperationException($"This client version does not contain the mapped asset path: {mapping.Value}. The extension was not applied.");
                ValidateAsset(source, mapping.Value);
                if (File.Exists(destination)) throw new InvalidOperationException("The target already exists in Agentstrap's mod overlay. Remove or change that mod first: " + mapping.Value);
            }
            var installed = new List<(string target, string? prior, string applied)>();
            try
            {
                if (ext.Manifest.Configuration.Count > 0 && !ListInstalled().Any(x => x.Enabled && x.Manifest.Configuration.Count > 0))
                    File.WriteAllText(UseFastFlagManagerBackupPath, App.Settings.Prop.UseFastFlagManager.ToString());
                foreach (var mapping in ext.Manifest.AssetMappings)
                {
                    string source = SafeCombine(ext.InstallPath, mapping.Key), destination = SafeCombine(_modificationsDirectory, mapping.Value);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    installed.Add((mapping.Value, null, Hash(source)));
                    File.Copy(source, destination, true);
                }
                foreach (var pair in ext.Manifest.Configuration)
                {
                    string path = "ClientSettings\\" + Path.GetFileName(App.FastFlags.FileLocation);
                    if (App.FastFlags.Prop.TryGetValue(pair.Key, out var old)) StoreOriginal(ext.Manifest.Id, pair.Key, old.ToString());
                    else StoreOriginal(ext.Manifest.Id, pair.Key, null);
                    App.FastFlags.SetValue(pair.Key, pair.Value);
                }
                if (ext.Manifest.Configuration.Count > 0) App.Settings.Prop.UseFastFlagManager = true;
                WriteExtensionState(ext.Manifest.Id, installed);
                if (ext.Manifest.Configuration.Count > 0) { App.FastFlags.Save(); App.Settings.Save(); }
                return true;
            }
            catch
            {
                foreach (var item in installed.AsEnumerable().Reverse())
                {
                    string path = SafeCombine(_modificationsDirectory, item.target);
                    if (File.Exists(path) && Hash(path) == item.applied && item.prior is null) File.Delete(path);
                }
                foreach (string key in ext.Manifest.Configuration.Keys) App.FastFlags.SetValue(key, ReadOriginal(ext.Manifest.Id, key));
                DeleteOriginals(ext.Manifest.Id);
                if (ext.Manifest.Configuration.Count > 0 && !ListInstalled().Any(x => x.Enabled && x.Manifest.Configuration.Count > 0) && File.Exists(UseFastFlagManagerBackupPath)
                    && bool.TryParse(File.ReadAllText(UseFastFlagManagerBackupPath), out bool originalManagerSetting))
                {
                    App.Settings.Prop.UseFastFlagManager = originalManagerSetting;
                    File.Delete(UseFastFlagManagerBackupPath);
                    App.FastFlags.Save(); App.Settings.Save();
                }
                throw;
            }
        }

        public bool Disable(string id)
        {
            var ext = Get(id); if (ext is null) return false;
            string statePath = StatePath(id);
            if (File.Exists(statePath))
            {
                var state = JsonSerializer.Deserialize<List<ManagedAssetState>>(File.ReadAllText(statePath)) ?? new();
                bool restorationBlocked = false;
                foreach (var asset in state)
                {
                    string target = SafeCombine(_modificationsDirectory, asset.Target);
                    if (!File.Exists(target)) continue;
                    if (Hash(target) != asset.AppliedHash) { restorationBlocked = true; continue; }
                    if (asset.PreviousHash is null) File.Delete(target);
                    else throw new InvalidOperationException($"Cannot safely restore {asset.Target}; a managed file changed. Recovery state retained at {statePath}.");
                }
                if (restorationBlocked) throw new InvalidOperationException($"One or more managed files changed after activation. They were left untouched and recovery state is retained at {statePath}.");
                File.Delete(statePath);
            }
            foreach (string key in ext.Manifest.Configuration.Keys)
            {
                var original = ReadOriginal(id, key);
                App.FastFlags.SetValue(key, original);
            }
            DeleteOriginals(id);
            if (ext.Manifest.Configuration.Count > 0 && !ListInstalled().Any(x => x.Enabled && x.Manifest.Configuration.Count > 0))
            {
                if (File.Exists(UseFastFlagManagerBackupPath) && bool.TryParse(File.ReadAllText(UseFastFlagManagerBackupPath), out bool originalManagerSetting))
                    App.Settings.Prop.UseFastFlagManager = originalManagerSetting;
                File.Delete(UseFastFlagManagerBackupPath);
            }
            if (ext.Manifest.Configuration.Count > 0) { App.FastFlags.Save(); App.Settings.Save(); }
            return true;
        }

        public bool Remove(string id)
        {
            var ext = Get(id); if (ext is null) return false;
            if (ext.Enabled) Disable(id);
            Directory.Delete(ext.InstallPath, true);
            return true;
        }

        public InstalledExtension? Get(string id) => ListInstalled().FirstOrDefault(e => e.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Targets whose built-in client paths disappeared in a newer Roblox version.</summary>
        public IReadOnlySet<string> GetIncompatibleOverlayTargets(string versionDirectory)
        {
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extension in ListInstalled().Where(x => x.Enabled))
            foreach (string target in extension.Manifest.AssetMappings.Values)
            {
                bool valid = File.Exists(SafeCombine(versionDirectory, target));
                var state = ReadAssetState(extension.Manifest.Id).FirstOrDefault(x => x.Target.Equals(target, StringComparison.OrdinalIgnoreCase));
                string overlay = SafeCombine(_modificationsDirectory, target);
                valid &= state is not null && File.Exists(overlay) && Hash(overlay) == state.AppliedHash;
                if (!valid) blocked.Add(target.Replace('/', '\\'));
            }
            return blocked;
        }

        private InstalledExtension InstallFiles(ExtensionManifest manifest, IEnumerable<(string path, byte[] bytes)> files)
        {
            string dest = GetExtensionDirectory(manifest.Id);
            if (Directory.Exists(dest)) throw new IOException("An extension with this ID is already installed.");
            string staging = dest + ".installing";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            try
            {
                foreach (var (path, bytes) in files)
                {
                    if (!ExtensionPackageValidator.IsSafeRelativePath(path, out var normalized)) throw new InvalidDataException("Unsafe package path: " + path);
                    string target = SafeCombine(staging, normalized); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, bytes);
                }
                Directory.Move(staging, dest);
            }
            catch { if (Directory.Exists(staging)) Directory.Delete(staging, true); throw; }
            return Get(manifest.Id)!;
        }

        private static void ValidateMappedAssets(ExtensionManifest manifest, IEnumerable<string> paths)
        {
            var set = paths.Select(p => p.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in manifest.AssetMappings)
                if (!set.Contains(mapping.Key.Replace('\\', '/'))) throw new InvalidDataException("Mapped asset missing from package: " + mapping.Key);
            if (!string.IsNullOrWhiteSpace(manifest.Icon) && !set.Contains(manifest.Icon.Replace('\\', '/'))) throw new InvalidDataException("Icon image missing from package: " + manifest.Icon);
        }
        private static void EnsureNotDeclaredConflict(ExtensionManifest current, ExtensionManifest other)
        {
            if (current.Conflicts.Contains(other.Id, StringComparer.OrdinalIgnoreCase) || other.Conflicts.Contains(current.Id, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Extension conflict: {current.Name} conflicts with {other.Name}.");
        }
        private static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            if (entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("Package file exceeds the per-file size limit: " + entry.FullName);
            using var s = entry.Open(); using var m = new MemoryStream(); var buffer = new byte[81920]; int read;
            while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (m.Length + read > 16 * 1024 * 1024) throw new InvalidDataException("Expanded package file exceeds the size limit: " + entry.FullName);
                m.Write(buffer, 0, read);
            }
            return m.ToArray();
        }
        private static string SafeCombine(string root, string relative)
        {
            if (!ExtensionPackageValidator.IsSafeRelativePath(relative, out var normalized)) throw new InvalidDataException("Unsafe relative path.");
            string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, normalized));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes its root.");
            return full;
        }
        private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        private string GetActiveClientPath(string relative)
        {
            string version = _clientRoot is null ? App.PlayerState.Prop.VersionGuid : Path.GetFileName(_clientRoot);
            if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("Roblox is not installed through Agentstrap yet.");
            string root = _clientRoot ?? Path.Combine(Paths.Versions, version);
            if (!File.Exists(Path.Combine(root, "RobloxPlayerBeta.exe"))) throw new InvalidOperationException("Agentstrap could not find the active Roblox client version.");
            return SafeCombine(root, relative);
        }
        private string StatePath(string id) => Path.Combine(StateDirectory, Sanitize(id) + ".json");
        private sealed record ManagedAssetState(string Target, string? PreviousHash, string OriginalClientHash, string AppliedHash, string ClientVersion);
        private List<ManagedAssetState> ReadAssetState(string id)
        {
            string path = StatePath(id);
            if (!File.Exists(path)) return new();
            try { return JsonSerializer.Deserialize<List<ManagedAssetState>>(File.ReadAllText(path)) ?? new(); }
            catch { return new(); }
        }
        private void WriteExtensionState(string id, List<(string target, string? prior, string applied)> assets)
        {
            string version = _clientRoot is null ? App.PlayerState.Prop.VersionGuid : Path.GetFileName(_clientRoot);
            var rows = assets.Select(x => new ManagedAssetState(x.target, x.prior, Hash(GetActiveClientPath(x.target)), x.applied, version)).ToList();
            File.WriteAllText(StatePath(id), JsonSerializer.Serialize(rows));
        }
        private string OriginalPath(string id) => Path.Combine(StateDirectory, Sanitize(id) + ".original.json");
        private string UseFastFlagManagerBackupPath => Path.Combine(StateDirectory, "fast-flag-manager.original");
        private void StoreOriginal(string id, string key, string? value)
        {
            Dictionary<string, string?> d = File.Exists(OriginalPath(id)) ? JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(OriginalPath(id))) ?? new() : new();
            d[key] = value; File.WriteAllText(OriginalPath(id), JsonSerializer.Serialize(d));
        }
        private string? ReadOriginal(string id, string key)
        {
            if (!File.Exists(OriginalPath(id))) return null;
            var d = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(OriginalPath(id)));
            return d is not null && d.TryGetValue(key, out var value) ? value : null;
        }
        private void DeleteOriginals(string id) { if (File.Exists(OriginalPath(id))) File.Delete(OriginalPath(id)); }
        private static string Sanitize(string id) => System.Text.RegularExpressions.Regex.Replace(id, "[^a-zA-Z0-9._-]", "_");
        private static void ValidateAsset(string path, string target)
        {
            long length = new FileInfo(path).Length;
            if (length == 0 || length > 16 * 1024 * 1024) throw new InvalidDataException("Asset size is invalid: " + target);
            string ext = Path.GetExtension(target).ToLowerInvariant(); byte[] b = File.ReadAllBytes(path).Take(16).ToArray();
            bool ok = ext switch
            {
                ".png" or ".jpg" or ".jpeg" => ValidateImage(path),
                ".mp3" => b.Length >= 3 && (b[0] == 73 && b[1] == 68 && b[2] == 51 || b[0] == 255 && (b[1] & 0xE0) == 0xE0),
                ".ogg" => b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "OggS",
                ".ttf" => b.Length >= 4 && b[0] == 0 && b[1] == 1 && b[2] == 0 && b[3] == 0,
                ".otf" => b.Length >= 4 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "OTTO",
                ".json" => Path.GetFileName(path).EndsWith(".json", StringComparison.OrdinalIgnoreCase) && JsonDocument.Parse(File.ReadAllText(path)).RootElement.ValueKind == JsonValueKind.Object,
                _ => false
            };
            if (!ok) throw new InvalidDataException("Asset content does not match its supported file type: " + target);
        }

        private static bool ValidateImage(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.FirstOrDefault();
                string ext = Path.GetExtension(path);
                bool matchingCodec = decoder.CodecInfo.FileExtensions.Split(';').Any(x => x.Replace("*", "").Equals(ext, StringComparison.OrdinalIgnoreCase));
                return matchingCodec && frame is not null && frame.PixelWidth is > 0 and <= 8192 && frame.PixelHeight is > 0 and <= 8192;
            }
            catch { return false; }
        }
    }
}
