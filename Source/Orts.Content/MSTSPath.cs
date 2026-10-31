// COPYRIGHT 2009, 2010, 2013 by the Open Rails project.
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
using System.Linq;
using System.Text;
using System.Diagnostics;
#if WINDOWS || NET6_0_WINDOWS
using Microsoft.Win32;
#endif
using System.IO;

namespace MSTS
{
    // TODO: Replace this with a full-on ORTS.Content.ContentManager.
    /// <summary>
    /// Utility functions to access the various directories in an MSTS install.
    /// </summary>
    public class MSTSPath
    {

        //TODO - make all msts classes use this.
        public static string DefaultLocation;   // MSTS default path.

        /// <summary>
        /// Returns the base path of the MSTS installation
        /// </summary>
        /// <returns>no trailing directory separator</returns>
        public static string Base()
        /* Throws
         *		System.Exception( "Can't find MSTS" );
         */
        {

#if WINDOWS || NET6_0_WINDOWS
			if (DefaultLocation == null)
			{
				DefaultLocation = "c:\\program files\\microsoft games\\train simulator";

				using (RegistryKey RK = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft Games\Train Simulator\1.0")
					?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Microsoft Games\Train Simulator\1.0"))
					if (RK != null)
						DefaultLocation = (string)RK.GetValue("Path", DefaultLocation);
			}
#endif

            if (DefaultLocation == null)
                throw new InvalidOperationException("MSTSPath.DefaultLocation must be set to the MSTS installation directory on this platform.");

            if (!Directory.Exists(DefaultLocation))
                throw new FileNotFoundException("MSTS directory '" + DefaultLocation + "' does not exist.", DefaultLocation);

            return DefaultLocation;
        }  //


        /// <summary>
        /// Returns the route folder with out trailing \
        /// </summary>
        /// <param name="route"></param>
        /// <returns></returns>
        public static string RouteFolder(string route)
        {
            return ResolvePath(Path.Combine(Base(), "ROUTES", NormalizePath(route)));
        }

        public static string ConsistFolder()
        {
            return ResolvePath(Path.Combine(Base(), "TRAINS", "CONSISTS"));
        }

        public static string TrainsetFolder()
        {
            return ResolvePath(Path.Combine(Base(), "TRAINS", "TRAINSET"));
        }

        public static string GlobalSoundFolder()
        {
            return ResolvePath(Path.Combine(Base(), "SOUND"));
        }

        public static string GetActivityFolder(string routeFolderName)
        {
            return ResolvePath(Path.Combine(RouteFolder(routeFolderName), "ACTIVITIES"));
        }

        public static string GetTRKFileName(string routeFolderPath)
        {
            routeFolderPath = ResolvePath(routeFolderPath);
            if (!Directory.Exists(routeFolderPath))
                throw new DirectoryNotFoundException(routeFolderPath);
            var trkFileName = Directory.EnumerateFiles(routeFolderPath)
                .FirstOrDefault(file => Path.GetExtension(file).Equals(".trk", StringComparison.OrdinalIgnoreCase));
            if (trkFileName == null)
                throw new FileNotFoundException("TRK file not found in '" + routeFolderPath + "'.", Path.Combine(routeFolderPath, Path.GetFileName(routeFolderPath)));
            return trkFileName;
        }

        /// <summary>
        /// Resolves an MSTS path using case-insensitive lookup on case-sensitive filesystems.
        /// </summary>
        public static string ResolvePath(string path)
        {
            string fullPath = Path.GetFullPath(NormalizePath(path));
            string currentPath = Path.GetPathRoot(fullPath) ?? string.Empty;
            if (string.IsNullOrEmpty(currentPath))
                return fullPath;

            var pathParts = fullPath[currentPath.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pathParts.Length; i++)
            {
                string candidate = Path.Combine(currentPath, pathParts[i]);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    currentPath = candidate;
                    continue;
                }

                if (Directory.Exists(currentPath))
                {
                    string actualName = Directory.EnumerateFileSystemEntries(currentPath)
                        .FirstOrDefault(entry => Path.GetFileName(entry).Equals(pathParts[i], StringComparison.OrdinalIgnoreCase));
                    if (actualName != null)
                    {
                        currentPath = actualName;
                        continue;
                    }
                }

                return Path.Combine(currentPath, Path.Combine(pathParts[i..]));
            }

            return currentPath;
        }

        /// <summary>
        /// Given a soundfile reference in a wag or eng file, return the path the sound file
        /// </summary>
        /// <param name="wagfilename"></param>
        /// <param name="soundfile"></param>
        /// <returns></returns>
        public static string TrainSoundPath(string wagfilename, string soundfile)
        {
            string trainsetSoundPath = Path.Combine(Path.GetDirectoryName(wagfilename), "SOUND", NormalizePath(soundfile));
            string globalSoundPath = Path.Combine(MSTSPath.GlobalSoundFolder(), NormalizePath(soundfile));

            trainsetSoundPath = ResolvePath(trainsetSoundPath);
            return File.Exists(trainsetSoundPath) ? trainsetSoundPath : ResolvePath(globalSoundPath);
        }


        /// <summary>
        /// Given a soundfile reference in a cvf file, return the path to the sound file
        /// </summary>
        public static string SMSSoundPath(string smsfilename, string soundfile)
        {
            string smsSoundPath = Path.Combine(Path.GetDirectoryName(smsfilename), NormalizePath(soundfile));
            string globalSoundPath = Path.Combine(MSTSPath.GlobalSoundFolder(), NormalizePath(soundfile));

            smsSoundPath = ResolvePath(smsSoundPath);
            return File.Exists(smsSoundPath) ? smsSoundPath : ResolvePath(globalSoundPath);
        }

        public static string TITFilePath(string route)
        {
            string normalizedRoute = NormalizePath(route);
            return ResolvePath(Path.Combine(RouteFolder(route), normalizedRoute + ".TIT"));
        }

        public static string GetConPath(string conName)
        {
            return ResolvePath(Path.Combine(ConsistFolder(), NormalizePath(conName) + ".con"));
        }

        public static string GetSrvPath(string srvName, string routeFolderPath)
        {
            return ResolvePath(Path.Combine(routeFolderPath, "SERVICES", NormalizePath(srvName) + ".srv"));
        }

        public static string GetTrfPath(string trfName, string routeFolderPath)
        {
            return ResolvePath(Path.Combine(routeFolderPath, "TRAFFIC", NormalizePath(trfName) + ".trf"));
        }

        public static string GetPatPath(string patName, string routeFolderPath)
        {
            return ResolvePath(Path.Combine(routeFolderPath, "PATHS", NormalizePath(patName) + ".pat"));
        }

        public static string GetWagPath(string name, string folder)
        {
            return ResolvePath(Path.Combine(TrainsetFolder(), NormalizePath(folder), NormalizePath(name) + ".wag"));
        }

        public static string GetEngPath(string name, string folder)
        {
            return ResolvePath(Path.Combine(TrainsetFolder(), NormalizePath(folder), NormalizePath(name) + ".eng"));
        }

        static string NormalizePath(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

    } // class MSTSPath
}
