// COPYRIGHT 2009 - 2024 by the Open Rails project.
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
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ORTS.Common
{
    public static class ApplicationInfo
    {
#if WINDOWS || NET6_0_WINDOWS
        public static string ProcessFile => Process.GetCurrentProcess().MainModule.FileName;
        public static string ProcessDirectory => Path.GetDirectoryName(ProcessFile);
        static FileVersionInfo VersionInfo => FileVersionInfo.GetVersionInfo(ProcessFile);
        public static string ProductName => VersionInfo.ProductName;
        public static string ApplicationName => VersionInfo.FileDescription;
#else
        public static string ProcessFile => Assembly.GetEntryAssembly()?.Location ?? typeof(ApplicationInfo).Assembly.Location;
        public static string ProcessDirectory => AppContext.BaseDirectory;
        public static string ProductName => typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Open Rails";
        public static string ApplicationName => typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? ProductName;
#endif
    }
}
