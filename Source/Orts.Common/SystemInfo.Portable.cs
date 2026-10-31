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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ORTS.Common
{
    public static class SystemInfo
    {
        static readonly Regex NameAndVersionRegex = new Regex("^(.*?) +([0-9.]+)$");

        static SystemInfo()
        {
            Application = new Platform
            {
                Name = ApplicationInfo.ProductName,
                Version = VersionInfo.VersionOrBuild,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            };

            var runtime = NameAndVersionRegex.Match(RuntimeInformation.FrameworkDescription.Trim());
            Runtime = new Platform
            {
                Name = runtime.Success ? runtime.Groups[1].Value : RuntimeInformation.FrameworkDescription,
                Version = runtime.Success ? runtime.Groups[2].Value : String.Empty,
            };

            OperatingSystem = new Platform
            {
                Name = RuntimeInformation.OSDescription,
                Version = Environment.OSVersion.Version.ToString(),
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                Languages = new[] { CultureInfo.CurrentUICulture.IetfLanguageTag },
            };

            InstalledMemoryMB = ReadInstalledMemoryMB();
        }

        public static readonly Platform Application;
        public static readonly Platform Runtime;
        public static readonly Platform OperatingSystem;
        public static readonly int InstalledMemoryMB;
        public static readonly List<CPU> CPUs = new List<CPU>();
        public static readonly List<GPU> GPUs = new List<GPU>();
        public static readonly List<string> Direct3DFeatureLevels = new List<string>();

        static int ReadInstalledMemoryMB()
        {
            const string memoryInfoPath = "/proc/meminfo";
            if (!File.Exists(memoryInfoPath))
                throw new PlatformNotSupportedException("Cannot determine installed memory because /proc/meminfo is unavailable.");

            string memoryLine = File.ReadLines(memoryInfoPath).FirstOrDefault(line => line.StartsWith("MemTotal:", StringComparison.Ordinal));
            if (memoryLine == null)
                throw new InvalidDataException("Cannot determine installed memory because /proc/meminfo has no MemTotal entry.");

            string[] parts = memoryLine.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !String.Equals(parts[2], "kB", StringComparison.Ordinal) ||
                !Int64.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long memoryKilobytes))
                throw new InvalidDataException("Cannot parse the MemTotal entry in /proc/meminfo.");

            return checked((int)(memoryKilobytes / 1024));
        }

        public static void WriteSystemDetails(TextWriter output)
        {
            output.WriteLine("Date/time   = {0} ({1:u})", DateTime.Now, DateTime.UtcNow);
            output.WriteLine("Application = {0} {1} ({2})", Application.Name, Application.Version, Application.Architecture);
            output.WriteLine("Runtime     = {0} {1}", Runtime.Name, Runtime.Version);
            output.WriteLine("System      = {0} {1} ({2}; {3}; {4})", OperatingSystem.Name, OperatingSystem.Version, OperatingSystem.Architecture, OperatingSystem.Language, string.Join(",", OperatingSystem.Languages ?? new string[0]));
            output.WriteLine("Memory      = {0:N0} MB", InstalledMemoryMB);
            foreach (var cpu in CPUs) output.WriteLine("CPU         = {0} ({1}; {2} threads; {3:N0} MHz)", cpu.Name, cpu.Manufacturer, cpu.ThreadCount, cpu.MaxClockMHz);
            foreach (var gpu in GPUs) output.WriteLine("GPU         = {0} ({1}; {2:N0} MB)", gpu.Name, gpu.Manufacturer, gpu.MemoryMB);
            output.WriteLine("Direct3D    = {0}", string.Join(",", Direct3DFeatureLevels));
        }

        public struct Platform
        {
            public string Name;
            public string Version;
            public string Architecture;
            public string Language;
            public string[] Languages;
        }

        public struct CPU
        {
            public string Name;
            public string Manufacturer;
            public uint ThreadCount;
            public uint MaxClockMHz;
        }

        public struct GPU
        {
            public string Name;
            public string Manufacturer;
            public uint MemoryMB;
        }
    }
}
