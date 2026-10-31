// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ORTS.Common
{
    sealed class SettingsStoreJson : SettingsStore
    {
        readonly string FilePath;
        readonly object SyncRoot = new object();
        readonly Dictionary<string, JsonElement> Values;

        internal SettingsStoreJson(string filePath, string registryKey, string section)
            : base(section)
        {
            if (!String.IsNullOrEmpty(filePath))
            {
                string directory = Path.GetDirectoryName(filePath);
                string name = Path.GetFileNameWithoutExtension(filePath);
                string sectionSuffix = String.IsNullOrEmpty(section) ? String.Empty : "." + MakeSafeFileName(section);
                FilePath = Path.Combine(String.IsNullOrEmpty(directory) ? Environment.CurrentDirectory : directory, name + sectionSuffix + ".json");
            }
            else if (!String.IsNullOrEmpty(registryKey))
            {
                string dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (String.IsNullOrEmpty(dataDirectory))
                    throw new InvalidOperationException("The application data directory is not available for persistent settings.");

                string name = MakeSafeFileName(registryKey + (String.IsNullOrEmpty(section) ? String.Empty : "." + section));
                FilePath = Path.Combine(dataDirectory, ApplicationInfo.ProductName, "Settings", name + ".json");
            }
            else
            {
                throw new ArgumentException("Neither 'filePath' nor 'registryKey' arguments are valid.");
            }

            Values = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, JsonElement>()
                : new Dictionary<string, JsonElement>();
        }

        public override string[] GetUserNames()
        {
            lock (SyncRoot)
                return Values.Keys.ToArray();
        }

        public override object GetUserValue(string name, Type expectedType)
        {
            AssertGetUserValueType(expectedType);
            lock (SyncRoot)
                return Values.TryGetValue(name, out JsonElement value) ? JsonSerializer.Deserialize(value.GetRawText(), expectedType) : null;
        }

        public override void SetUserValue(string name, bool value) => SaveValue(name, value);
        public override void SetUserValue(string name, int value) => SaveValue(name, value);
        public override void SetUserValue(string name, long value) => SaveValue(name, value);
        public override void SetUserValue(string name, DateTime value) => SaveValue(name, value);
        public override void SetUserValue(string name, TimeSpan value) => SaveValue(name, value);
        public override void SetUserValue(string name, string value) => SaveValue(name, value);
        public override void SetUserValue(string name, int[] value) => SaveValue(name, value);
        public override void SetUserValue(string name, string[] value) => SaveValue(name, value);

        void SaveValue<T>(string name, T value)
        {
            lock (SyncRoot)
            {
                Values[name] = JsonSerializer.SerializeToElement(value);
                Save();
            }
        }

        public override void DeleteUserValue(string name)
        {
            lock (SyncRoot)
            {
                if (Values.Remove(name))
                    Save();
            }
        }

        public override string GetStoreName() => FilePath;

        void Save()
        {
            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);

            string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Values));
                File.Move(temporaryPath, FilePath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        static string MakeSafeFileName(string name)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            return new string(name.Select(character => invalidCharacters.Contains(character) || character == '\\' || character == '/' ? '_' : character).ToArray());
        }
    }
}
