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
using System.IO;
using MSTS;
using ORTS.Common;
using Xunit;

namespace Tests.Orts.Content
{
    public class PortableCoreTests
    {
        [Fact]
        public void AddOnPathsUsePlatformSeparatorsAndAcceptWindowsSeparators()
        {
            string originalLocation = MSTSPath.DefaultLocation;
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                MSTSPath.DefaultLocation = root;

                Assert.Equal(Path.Combine(root, "ROUTES", "Route", "Subroute"), MSTSPath.RouteFolder(@"Route\Subroute"));
                Assert.Equal(Path.Combine(root, "ROUTES", "Route", "Subroute", "ACTIVITIES"), MSTSPath.GetActivityFolder(@"Route\Subroute"));
                Assert.Equal(Path.Combine(root, "TRAINS", "CONSISTS", "Freight.con"), MSTSPath.GetConPath("Freight"));
                Assert.Equal(Path.Combine(root, "TRAINS", "TRAINSET", "Locomotive", "Engine.eng"), MSTSPath.GetEngPath("Engine", "Locomotive"));
                Assert.Equal(Path.Combine(root, "TRAINS", "TRAINSET", "Locomotive", "Wagon.wag"), MSTSPath.GetWagPath("Wagon", "Locomotive"));
                Assert.Equal(Path.Combine(root, "ROUTES", "Route", "SERVICES", "Service.srv"), MSTSPath.GetSrvPath("Service", Path.Combine(root, "ROUTES", "Route")));
                Assert.Equal(Path.Combine(root, "ROUTES", "Route", "TRAFFIC", "Traffic.trf"), MSTSPath.GetTrfPath("Traffic", Path.Combine(root, "ROUTES", "Route")));
                Assert.Equal(Path.Combine(root, "ROUTES", "Route", "PATHS", "Path.pat"), MSTSPath.GetPatPath("Path", Path.Combine(root, "ROUTES", "Route")));
                Assert.Equal(Path.Combine(root, "SOUND", "Engine", "idle.sms"), MSTSPath.SMSSoundPath(Path.Combine(root, "SOUND", "Engine", "locomotive.sms"), @"Engine\idle.sms"));
            }
            finally
            {
                MSTSPath.DefaultLocation = originalLocation;
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void AddOnPathResolutionIsCaseInsensitiveOnCaseSensitivePlatforms()
        {
            string originalLocation = MSTSPath.DefaultLocation;
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string routeFolder = Path.Combine(root, "Routes", "MixedCaseRoute");
            Directory.CreateDirectory(routeFolder);
            string trackFile = Path.Combine(routeFolder, "MixedCaseRoute.TRK");
            File.WriteAllText(trackFile, "route");

            try
            {
                MSTSPath.DefaultLocation = root;

                Assert.Equal(routeFolder, MSTSPath.RouteFolder("mixedcaseroute"));
                Assert.Equal(trackFile, MSTSPath.GetTRKFileName(Path.Combine(root, "routes", "mixedcaseroute")));
                Assert.Equal(trackFile, MSTSPath.ResolvePath(Path.Combine(root, "routes", "mixedcaseroute", "mixedcaseroute.trk")));
                Assert.Equal(Path.Combine(routeFolder, "Missing.act"), MSTSPath.ResolvePath(Path.Combine(root, "routes", "mixedcaseroute", "Missing.act")));
            }
            finally
            {
                MSTSPath.DefaultLocation = originalLocation;
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void PortableSettingsStorePersistsTypedValues()
        {
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string settingsPath = Path.Combine(root, "settings.ini");
            DateTime timestamp = new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Utc);

            try
            {
                SettingsStore store = SettingsStore.GetSettingStore(settingsPath, null, "Simulation");
                store.SetUserValue("enabled", true);
                store.SetUserValue("count", 42);
                store.SetUserValue("duration", TimeSpan.FromMinutes(3));
                store.SetUserValue("timestamp", timestamp);
                store.SetUserValue("names", new[] { "route", "train" });

                SettingsStore reloaded = SettingsStore.GetSettingStore(settingsPath, null, "Simulation");

                Assert.Equal(true, reloaded.GetUserValue("enabled", typeof(bool)));
                Assert.Equal(42, reloaded.GetUserValue("count", typeof(int)));
                Assert.Equal(TimeSpan.FromMinutes(3), reloaded.GetUserValue("duration", typeof(TimeSpan)));
                Assert.Equal(timestamp, reloaded.GetUserValue("timestamp", typeof(DateTime)));
                Assert.Equal(new[] { "route", "train" }, (string[])reloaded.GetUserValue("names", typeof(string[])));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
