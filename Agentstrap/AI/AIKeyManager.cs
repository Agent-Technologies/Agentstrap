using Agentstrap.Accounts;

namespace Agentstrap.AI
{
    public static class AIKeyManager
    {
        private static SecureCredentialStore? _store;
        private static SecureCredentialStore Store
        {
            get
            {
                if (_store is null)
                {
                    string dir = Path.Combine(Paths.LocalAppData, App.ProjectName, "AI", "Keys");
                    _store = new SecureCredentialStore(dir);
                }
                return _store;
            }
        }

        public static void Save(AIProviderId provider, string apiKey)
        {
            Store.Save($"ai:{provider}", apiKey);
        }

        public static string? Load(AIProviderId provider)
        {
            return Store.Load($"ai:{provider}");
        }

        public static bool Remove(AIProviderId provider)
        {
            return Store.Remove($"ai:{provider}");
        }

        public static bool Exists(AIProviderId provider)
        {
            return Store.Exists($"ai:{provider}");
        }

        public static AIProviderId? Parse(string id)
        {
            return Enum.TryParse<AIProviderId>(id, out var p) ? p : null;
        }
    }
}
