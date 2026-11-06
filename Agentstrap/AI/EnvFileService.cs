namespace Agentstrap.AI
{
    public static class EnvFileService
    {
        private static string FilePath => Path.Combine(Paths.LocalAppData, App.ProjectName, "AI", ".env");

        public static void Save(string provider, string model, string? apiKey)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var lines = new List<string>
                {
                    $"AI_PROVIDER={provider}",
                    $"AI_MODEL={model}"
                };
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    string keyName = provider.ToUpperInvariant().Replace(" ", "_") + "_API_KEY";
                    lines.Add($"{keyName}={apiKey}");
                }
                File.WriteAllLines(FilePath, lines);
            }
            catch { }
        }

        public static void OpenInEditor()
        {
            try
            {
                if (!File.Exists(FilePath))
                    Save(App.AISettings.Prop.SelectedProvider, App.AISettings.Prop.SelectedModel, null);
                Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
            }
            catch { }
        }

        public static string PathValue => FilePath;
    }
}
