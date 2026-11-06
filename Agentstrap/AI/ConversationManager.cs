using System.Text.Json;

namespace Agentstrap.AI
{
    public static class ConversationManager
    {
        private static string FilePath => Path.Combine(Paths.LocalAppData, App.ProjectName, "AI", "Conversations.json");

        private static List<AIConversation> _conversations = new();
        private static bool _loaded = false;

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _conversations = JsonSerializer.Deserialize<List<AIConversation>>(json) ?? new();
                }
                else _conversations = new();
            }
            catch { _conversations = new(); }
            _loaded = true;
        }

        public static void Save()
        {
            try
            {
                if (!App.AISettings.Prop.SaveConversationHistory) return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_conversations, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static List<AIConversation> All
        {
            get
            {
                if (!_loaded) Load();
                return _conversations;
            }
        }

        public static AIConversation Create()
        {
            var c = new AIConversation();
            _conversations.Insert(0, c);
            Save();
            return c;
        }

        public static void Delete(string id)
        {
            _conversations.RemoveAll(c => c.Id == id);
            Save();
        }

        public static void ClearAll()
        {
            _conversations.Clear();
            Save();
        }

        public static AIConversation? Get(string id) => _conversations.FirstOrDefault(c => c.Id == id);
    }
}
