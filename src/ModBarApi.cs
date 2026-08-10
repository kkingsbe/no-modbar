using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;

namespace NoModBar
{
    public static class ModBarApi
    {
        internal static ManualLogSource Log;

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ModBarEntry> Entries = new Dictionary<string, ModBarEntry>();
        private static int _version;

        public static bool Register(object entry)
        {
            if (entry == null) return false;
            string id = GetProp<string>(entry, "Id");
            string name = GetProp<string>(entry, "Name");
            string tooltip = GetProp<string>(entry, "Tooltip");
            Func<bool> isVisible = GetProp<Func<bool>>(entry, "IsVisible");
            Action toggle = GetProp<Action>(entry, "Toggle");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || toggle == null)
            {
                Log?.LogWarning($"ModBar: rejected registration id='{id}' name='{name}' toggle={(toggle != null)}");
                return false;
            }

            lock (Sync)
            {
                Entries[id] = new ModBarEntry
                {
                    Id = id,
                    Name = name,
                    Tooltip = tooltip,
                    IsVisible = isVisible,
                    Toggle = toggle
                };
                _version++;
            }
            Log?.LogInfo($"ModBar: registered '{id}' ({name}), total {Count()}");
            return true;
        }

        public static bool Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            lock (Sync)
            {
                if (!Entries.Remove(id)) return false;
                _version++;
            }
            Log?.LogInfo($"ModBar: unregistered '{id}', total {Count()}");
            return true;
        }

        private static int Count()
        {
            lock (Sync) return Entries.Count;
        }

        public static bool IsRegistered(string id)
        {
            lock (Sync) return Entries.ContainsKey(id);
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Entries.Clear();
                _version++;
            }
        }

        internal static int Version
        {
            get { lock (Sync) return _version; }
        }

        internal static List<ModBarEntry> Snapshot()
        {
            lock (Sync) return new List<ModBarEntry>(Entries.Values);
        }

        private static T GetProp<T>(object o, string name)
        {
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !typeof(T).IsAssignableFrom(p.PropertyType)) return default(T);
                object v = p.GetValue(o, null);
                return v is T ? (T)v : default(T);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
