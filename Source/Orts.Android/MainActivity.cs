using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Microsoft.Xna.Framework;
using MSTS;
using ORTS.Settings;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Orts.Android;

[Activity(Label = "Open Rails Android", MainLauncher = true, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize | global::Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class MainActivity : AndroidGameActivity
{
    const int ContentTreeRequestCode = 1;
    const string PreferencesName = "openrails";
    const string ContentTreePreference = "msts-content-tree";
    const string ImportedRootPreference = "imported-msts-root";
    AndroidSimulatorGame? game;
    Button? chooseFolderButton;
    Button? browseActivitiesButton;
    SeekBar? throttleControl;
    SeekBar? brakeControl;
    TextView? throttleValue;
    TextView? brakeValue;
    Button? directionButton;
    CancellationTokenSource? importCancellation;
    CancellationTokenSource? activityLoadCancellation;
    Orts.Simulation.Simulator? activeSimulator;
    IReadOnlyList<AndroidRouteActivity> availableActivities = Array.Empty<AndroidRouteActivity>();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        game = new AndroidSimulatorGame();
        game.SimulationUpdateStopped += () => RunOnUiThread(() =>
        {
            activeSimulator = null;
            SetDrivingControlsEnabled(false);
        });
        game.PlayerControlStateChanged += OnPlayerControlStateChanged;
        var gameView = (View?)game.Services.GetService(typeof(View));
        if (gameView == null)
            throw new InvalidOperationException("MonoGame did not create an Android game view.");

        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        layout.AddView(gameView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        chooseFolderButton = new Button(this) { Text = "Choose and import MSTS installation" };
        chooseFolderButton.Click += (_, _) => OpenContentFolderPicker();
        layout.AddView(chooseFolderButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        browseActivitiesButton = new Button(this) { Text = "Browse imported activities", Enabled = false };
        browseActivitiesButton.Click += (_, _) => BrowseActivities();
        layout.AddView(browseActivitiesButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        var throttleRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        throttleValue = new TextView(this) { Text = "Power 0%", Gravity = GravityFlags.CenterVertical };
        throttleRow.AddView(throttleValue, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent));
        throttleControl = new SeekBar(this) { Max = 100, Progress = 0, Enabled = false };
        throttleControl.ProgressChanged += (_, args) =>
        {
            if (!args.FromUser)
                return;
            throttleValue.Text = $"Power {args.Progress}%";
            game.SetThrottlePercent(args.Progress);
        };
        throttleRow.AddView(throttleControl, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        layout.AddView(throttleRow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var brakeRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        brakeValue = new TextView(this) { Text = "Brake 0%", Gravity = GravityFlags.CenterVertical };
        brakeRow.AddView(brakeValue, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent));
        brakeControl = new SeekBar(this) { Max = 100, Progress = 0, Enabled = false };
        brakeControl.ProgressChanged += (_, args) =>
        {
            if (!args.FromUser)
                return;
            brakeValue.Text = $"Brake {args.Progress}%";
            game.SetTrainBrakePercent(args.Progress);
        };
        brakeRow.AddView(brakeControl, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        layout.AddView(brakeRow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        var brakeControls = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        directionButton = new Button(this) { Text = "Change direction", Enabled = false };
        directionButton.Click += (_, _) => game.ToggleDirection();
        brakeControls.AddView(directionButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        layout.AddView(brakeControls, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        SetContentView(layout);

        var preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        var importedRoot = preferences?.GetString(ImportedRootPreference, null);
        if (!string.IsNullOrEmpty(importedRoot) && Directory.Exists(importedRoot))
        {
            MSTSPath.DefaultLocation = importedRoot;
            RefreshActivityCatalog(importedRoot);
        }

        game.Run();
    }

    protected override void OnDestroy()
    {
        activityLoadCancellation?.Cancel();
        importCancellation?.Cancel();
        if (game != null)
            game.PlayerControlStateChanged -= OnPlayerControlStateChanged;
        game?.SetSimulator(null, null);
        activeSimulator?.Stop();
        activeSimulator = null;
        SetDrivingControlsEnabled(false);
        base.OnDestroy();
    }

    void OnPlayerControlStateChanged(int throttlePercent, int brakePercent, string direction)
    {
        RunOnUiThread(() =>
        {
            if (IsFinishing || IsDestroyed || activeSimulator == null)
                return;
            if (throttleControl != null)
                throttleControl.Progress = Math.Clamp(throttlePercent, 0, 100);
            if (brakeControl != null)
                brakeControl.Progress = Math.Clamp(brakePercent, 0, 100);
            if (throttleValue != null)
                throttleValue.Text = $"Power {throttleControl?.Progress ?? 0}%";
            if (brakeValue != null)
                brakeValue.Text = $"Brake {brakeControl?.Progress ?? 0}%";
            if (directionButton != null)
                directionButton.Text = $"Direction: {direction}";
        });
    }

    void OpenContentFolderPicker()
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        StartActivityForResult(intent, ContentTreeRequestCode);
    }

    protected override void OnActivityResult(int requestCode, global::Android.App.Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != ContentTreeRequestCode || resultCode != global::Android.App.Result.Ok || data == null)
            return;

        var treeUri = data.Data;
        if (treeUri == null)
            return;
        try
        {
            var permissionFlags = data.Flags & ActivityFlags.GrantReadUriPermission;
            (ContentResolver ?? throw new IOException("Android did not provide a content resolver."))
                .TakePersistableUriPermission(treeUri, permissionFlags);
            GetSharedPreferences(PreferencesName, FileCreationMode.Private)?.Edit()
                ?.PutString(ContentTreePreference, treeUri.ToString())
                ?.Apply();
            StartImport(treeUri);
        }
        catch (IOException error)
        {
            global::Android.Util.Log.Warn(nameof(MainActivity), error.Message);
            game?.SetContentStatus("Selected folder is not a readable MSTS installation.");
        }
        catch (global::Java.Lang.SecurityException error)
        {
            global::Android.Util.Log.Warn(nameof(MainActivity), error.ToString());
            game?.SetContentStatus("Android could not retain access to that folder. Choose it again.");
        }
    }

    void StartImport(global::Android.Net.Uri treeUri)
    {
        if (game == null || chooseFolderButton == null)
            throw new InvalidOperationException("The Android host is not ready to import content.");

        importCancellation?.Dispose();
        importCancellation = new CancellationTokenSource();
        var cancellationToken = importCancellation.Token;
        chooseFolderButton.Enabled = false;
        game.SetContentStatus("Preparing to import MSTS routes, trains, consists, and shared assets.");
        var progress = new Progress<string>(status => game?.SetContentStatus(status));
        _ = ImportContentAsync(treeUri, progress, cancellationToken);
    }

    async Task ImportContentAsync(global::Android.Net.Uri treeUri, IProgress<string> progress, CancellationToken cancellationToken)
    {
        try
        {
            var contentResolver = ContentResolver ?? throw new IOException("Android did not provide a content resolver.");
            var destinationParent = Path.Combine(GetExternalFilesDir(null)?.AbsolutePath ?? FilesDir?.AbsolutePath
                ?? throw new IOException("Android did not provide app storage for imported MSTS content."), "ImportedContent");
            var result = await Task.Run(
                () => AndroidContentRootImporter.Import(contentResolver, treeUri, destinationParent, progress, cancellationToken),
                cancellationToken);
            MSTSPath.DefaultLocation = result.RootPath;
            GetSharedPreferences(PreferencesName, FileCreationMode.Private)?.Edit()
                ?.PutString(ImportedRootPreference, result.RootPath)
                ?.Apply();
            RefreshActivityCatalog(result.RootPath);
            var mebibytes = result.BytesCopied / (1024d * 1024d);
            game?.SetContentStatus($"Imported {result.FilesCopied} files ({mebibytes:F0} MiB). Select an activity to validate route loading.");
        }
        catch (System.OperationCanceledException)
        {
            game?.SetContentStatus("MSTS content import was cancelled.");
        }
        catch (IOException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"MSTS content import failed: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"MSTS content import failed: {error.Message}");
        }
        catch (global::Java.Lang.SecurityException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus("Android revoked folder access. Choose the installation folder again.");
        }
        finally
        {
            RunOnUiThread(() =>
            {
                if (chooseFolderButton != null)
                    chooseFolderButton.Enabled = true;
            });
        }
    }

    void RefreshActivityCatalog(string installationPath)
    {
        var catalog = AndroidRouteCatalog.Read(installationPath);
        availableActivities = catalog.Activities;
        if (browseActivitiesButton != null)
            browseActivitiesButton.Enabled = availableActivities.Count > 0;
        var activityLabel = availableActivities.Count == 1 ? "activity" : "activities";
        game?.SetContentStatus($"Imported content: {catalog.RouteCount} routes, {availableActivities.Count} {activityLabel}. Select one to load its route data.");
    }

    void BrowseActivities()
    {
        if (availableActivities.Count == 0)
        {
            game?.SetContentStatus("No .act activities were found in the imported routes.");
            return;
        }

        var labels = availableActivities
            .Select(activity => $"{activity.RouteName} - {activity.ActivityName}")
            .ToArray();
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("Select an activity to load");
        dialog.SetItems(labels, (_, args) => LoadActivity(availableActivities[args!.Which]));
        dialog.SetNegativeButton("Cancel", (_, _) => { });
        dialog.Show();
    }

    async void LoadActivity(AndroidRouteActivity activity)
    {
        var activityButton = browseActivitiesButton
            ?? throw new InvalidOperationException("The Android host is not ready to load an activity.");
        var importButton = chooseFolderButton
            ?? throw new InvalidOperationException("The Android host is not ready to load an activity.");
        activityButton.Enabled = false;
        importButton.Enabled = false;
        game?.SetSimulator(null, null);
        SetDrivingControlsEnabled(false);
        game?.SetContentStatus($"Starting {activity.ActivityName} from route {activity.RouteName}...");
        var cancellation = new CancellationTokenSource();
        activityLoadCancellation = cancellation;
        activeSimulator?.Stop();
        activeSimulator = null;
        try
        {
            var started = await Task.Run(() =>
            {
                var settings = new UserSettings(new[] { "skip-user-settings" });
                var simulator = new Orts.Simulation.Simulator(settings, activity.ActivityPath, useOpenRailsDirectory: false);
                try
                {
                    var simulatorCancellation = new ORTS.Common.CancellationTokenSource(
                        cancellation.Token.ThrowIfCancellationRequested);
                    simulator.SetActivity(activity.ActivityPath);
                    simulator.Start(simulatorCancellation.Token);
                    simulator.SetCommandReceivers();
                    return (Simulator: simulator, RouteMap: RouteMapSnapshot.Create(simulator));
                }
                catch
                {
                    simulator.Stop();
                    throw;
                }
            }, cancellation.Token);
            activeSimulator = started.Simulator;
            game?.SetSimulator(started.Simulator, started.RouteMap);
            SetDrivingControlsEnabled(true);
            var mapStatus = started.RouteMap == null ? "Route-map geometry is unavailable." : "The 2D route map and basic controls are active.";
            game?.SetContentStatus($"Started route {started.Simulator.RouteName}: {started.Simulator.Trains.Count} trains. {mapStatus} The 3D viewer is not integrated.");
        }
        catch (System.OperationCanceledException)
        {
            game?.SetContentStatus($"Starting {activity.ActivityName} was cancelled.");
        }
        catch (IOException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Could not load {activity.ActivityName}: {error.Message}");
        }
        catch (InvalidDataException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Could not load {activity.ActivityName}: {error.Message}");
        }
        catch (FormatException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Could not load {activity.ActivityName}: {error.Message}");
        }
        catch (ArgumentException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Could not load {activity.ActivityName}: {error.Message}");
        }
        catch (TypeInitializationException error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Open Rails could not initialize on Android: {error.InnerException?.Message ?? error.Message}");
        }
        catch (Exception error)
        {
            global::Android.Util.Log.Error(nameof(MainActivity), error.ToString());
            game?.SetContentStatus($"Could not load {activity.ActivityName}: {error.Message}");
        }
        finally
        {
            if (ReferenceEquals(activityLoadCancellation, cancellation))
                activityLoadCancellation = null;
            cancellation.Dispose();
            activityButton.Enabled = availableActivities.Count > 0;
            importButton.Enabled = true;
        }
    }

    void SetDrivingControlsEnabled(bool enabled)
    {
        if (throttleControl != null)
            throttleControl.Enabled = enabled;
        if (brakeControl != null)
            brakeControl.Enabled = enabled;
        if (!enabled)
        {
            if (throttleControl != null)
                throttleControl.Progress = 0;
            if (brakeControl != null)
                brakeControl.Progress = 0;
            if (throttleValue != null)
                throttleValue.Text = "Power 0%";
            if (brakeValue != null)
                brakeValue.Text = "Brake 0%";
            if (directionButton != null)
                directionButton.Text = "Change direction";
        }
        if (directionButton != null)
            directionButton.Enabled = enabled;
    }
}