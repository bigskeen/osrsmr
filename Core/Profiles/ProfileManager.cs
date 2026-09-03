using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OsrsMr.Core.Profiles
{
    public class ProfileManager
    {
        private static ProfileManager? _instance;
        public static ProfileManager Instance => _instance ??= new ProfileManager();

        private static readonly string StoragePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "profiles.json");

        public List<AccountProfile> Profiles { get; private set; } = new();
        public AccountProfile ActiveProfile { get; set; } = new();

        public event Action<AccountProfile>? OnProfileChanged;

        public ProfileManager()
        {
            LoadProfiles();
        }

        public void LoadProfiles()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    string json = File.ReadAllText(StoragePath);
                    var loaded = JsonSerializer.Deserialize<List<AccountProfile>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        Profiles = loaded;
                        ActiveProfile = Profiles[0];
                        return;
                    }
                }
            }
            catch { }

            // Default fallback
            Profiles = new List<AccountProfile>
            {
                new() { ProfileName = "Default Main Profile", AccountName = "Main Account", PreferredWorld = 301 },
                new() { ProfileName = "F2P Skiller Profile", AccountName = "Skiller", PreferredWorld = 308 }
            };
            ActiveProfile = Profiles[0];
            SaveProfiles();
        }

        public void SaveProfiles()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Profiles, options);
                File.WriteAllText(StoragePath, json);
            }
            catch { }
        }

        public void SetActiveProfile(AccountProfile profile)
        {
            ActiveProfile = profile;
            OnProfileChanged?.Invoke(profile);
        }

        public void AddProfile(AccountProfile profile)
        {
            Profiles.Add(profile);
            SaveProfiles();
        }

        public void RemoveProfile(AccountProfile profile)
        {
            if (Profiles.Count <= 1) return; // Keep at least one
            Profiles.Remove(profile);
            if (ActiveProfile == profile)
            {
                ActiveProfile = Profiles[0];
                OnProfileChanged?.Invoke(ActiveProfile);
            }
            SaveProfiles();
        }
    }
}
