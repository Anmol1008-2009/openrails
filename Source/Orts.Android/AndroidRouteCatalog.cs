using System.IO;

namespace Orts.Android;

internal sealed record AndroidRouteActivity(string RouteName, string ActivityName, string ActivityPath);

internal sealed record AndroidRouteCatalogResult(int RouteCount, IReadOnlyList<AndroidRouteActivity> Activities);

internal static class AndroidRouteCatalog
{
    public static AndroidRouteCatalogResult Read(string installationPath)
    {
        var routesPath = MSTS.MSTSPath.ResolvePath(Path.Combine(installationPath, "ROUTES"));
        if (!Directory.Exists(routesPath))
            throw new DirectoryNotFoundException($"The imported installation has no ROUTES folder: {routesPath}");

        var routes = Directory.EnumerateDirectories(routesPath).ToArray();
        var activities = new List<AndroidRouteActivity>();
        foreach (var routePath in routes)
        {
            var activityPath = MSTS.MSTSPath.ResolvePath(Path.Combine(routePath, "ACTIVITIES"));
            if (!Directory.Exists(activityPath))
                continue;

            foreach (var file in Directory.EnumerateFiles(activityPath)
                .Where(path => Path.GetExtension(path).Equals(".act", StringComparison.OrdinalIgnoreCase)))
            {
                activities.Add(new AndroidRouteActivity(
                    Path.GetFileName(routePath),
                    Path.GetFileNameWithoutExtension(file),
                    file));
            }
        }

        return new AndroidRouteCatalogResult(
            routes.Length,
            activities.OrderBy(activity => activity.RouteName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(activity => activity.ActivityName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }
}
