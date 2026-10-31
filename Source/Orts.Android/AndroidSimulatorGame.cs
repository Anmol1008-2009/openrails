using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using System.Collections.Concurrent;
using System.IO;

namespace Orts.Android;

internal sealed class AndroidSimulatorGame : Game
{
    SpriteBatch? spriteBatch;
    Texture2D? statusTexture;
    Texture2D? mapPixel;
    volatile Simulator? simulator;
    RouteMapSnapshot? routeMap;
    readonly ConcurrentQueue<Action<Simulator>> pendingControls = new();
    readonly object statusLock = new();
    string contentStatus = "Choose an MSTS folder to discover route folders.";
    string? pendingContentStatus;
    TimeSpan lastSimulationStatusUpdate;

    public event Action? SimulationUpdateStopped;
    public event Action<int, int, string>? PlayerControlStateChanged;

    public AndroidSimulatorGame()
    {
        new GraphicsDeviceManager(this) { IsFullScreen = true };
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 30d);
    }

    public void SetContentStatus(string status)
    {
        lock (statusLock)
            pendingContentStatus = status;
    }

    public void SetSimulator(Simulator? activeSimulator, RouteMapSnapshot? map)
    {
        while (pendingControls.TryDequeue(out _))
        {
        }
        routeMap = map;
        simulator = activeSimulator;
    }

    public void SetThrottlePercent(int percent)
    {
        pendingControls.Enqueue(activeSimulator =>
        {
            GetPlayerLocomotive(activeSimulator).SetThrottlePercent(Math.Clamp(percent, 0, 100));
        });
    }

    public void SetTrainBrakePercent(int percent)
    {
        pendingControls.Enqueue(activeSimulator =>
        {
            GetPlayerLocomotive(activeSimulator).SetTrainBrakePercent(Math.Clamp(percent, 0, 100));
        });
    }

    public void ToggleDirection()
    {
        pendingControls.Enqueue(activeSimulator =>
        {
            var locomotive = GetPlayerLocomotive(activeSimulator);
            if (locomotive.ThrottleController.CurrentValue > 0.01f)
                throw new InvalidOperationException("Set throttle to zero before changing direction.");
            if (Math.Abs(locomotive.Train.SpeedMpS) > 0.3f)
                throw new InvalidOperationException("The train must be nearly stopped before changing direction.");
            locomotive.SetDirection(locomotive.Direction == ORTS.Common.Direction.Forward
                ? ORTS.Common.Direction.Reverse
                : ORTS.Common.Direction.Forward);
        });
    }

    static MSTSLocomotive GetPlayerLocomotive(Simulator activeSimulator)
    {
        return activeSimulator.PlayerLocomotive as MSTSLocomotive
            ?? throw new InvalidOperationException("The selected activity has no controllable player locomotive.");
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        mapPixel = new Texture2D(GraphicsDevice, 1, 1);
        mapPixel.SetData(new[] { Color.White });
        statusTexture = CreateStatusTexture(contentStatus);
        base.LoadContent();
    }

    Texture2D CreateStatusTexture(string status)
    {
        var argb8888 = global::Android.Graphics.Bitmap.Config.Argb8888
            ?? throw new InvalidOperationException("Android did not provide an ARGB_8888 bitmap configuration.");
        using var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(1024, 240, argb8888);
        using var canvas = new global::Android.Graphics.Canvas(bitmap);
        using var background = new global::Android.Graphics.Paint { Color = new global::Android.Graphics.Color(24, 37, 49) };
        using var text = new global::Android.Graphics.Paint
        {
            AntiAlias = true,
            Color = global::Android.Graphics.Color.White,
            TextSize = 34,
        };
        using var title = new global::Android.Graphics.Paint
        {
            AntiAlias = true,
            Color = global::Android.Graphics.Color.White,
            TextSize = 46,
        };
        title.SetTypeface(global::Android.Graphics.Typeface.Create("sans-serif", global::Android.Graphics.TypefaceStyle.Bold));

        canvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, background);
        canvas.DrawText("Open Rails Android", 24, 48, title);
        var statusLines = WrapStatus(status, text, 960);
        for (int i = 0; i < statusLines.Count; i++)
            canvas.DrawText(statusLines[i], 24, 98 + 36 * i, text);
        canvas.DrawText("2D route map and basic controls; 3D viewer is not integrated.", 24, 220, text);

        using var image = new MemoryStream();
        var png = global::Android.Graphics.Bitmap.CompressFormat.Png
            ?? throw new InvalidOperationException("Android did not provide PNG bitmap compression.");
        if (!bitmap.Compress(png, 100, image))
            throw new InvalidOperationException("Could not encode the Android host status image.");
        image.Position = 0;
        return Texture2D.FromStream(GraphicsDevice, image);
    }

    static IReadOnlyList<string> WrapStatus(string status, global::Android.Graphics.Paint paint, float maxWidth)
    {
        var lines = new List<string>();
        var currentLine = string.Empty;
        foreach (var word in status.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);
            currentLine = word;
            if (lines.Count == 3)
            {
                lines[2] += "...";
                return lines;
            }
        }

        if (currentLine.Length > 0 && lines.Count < 3)
            lines.Add(currentLine);
        return lines;
    }

    protected override void Update(GameTime gameTime)
    {
        var currentSimulator = simulator;
        if (currentSimulator != null)
        {
            try
            {
                while (pendingControls.TryDequeue(out var command))
                {
                    try
                    {
                        command(currentSimulator);
                    }
                    catch (Exception error)
                    {
                        global::Android.Util.Log.Error(nameof(AndroidSimulatorGame), error.ToString());
                        SetContentStatus($"Could not apply driving control: {error.Message}");
                    }
                }

                var elapsedRealSeconds = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.1);
                currentSimulator.Update(currentSimulator.GetElapsedClockSeconds(elapsedRealSeconds));
                if (gameTime.TotalGameTime - lastSimulationStatusUpdate >= TimeSpan.FromMilliseconds(500))
                {
                    var locomotive = currentSimulator.PlayerLocomotive as MSTSLocomotive;
                    var speedKph = Math.Abs(locomotive?.Train.SpeedMpS ?? 0) * 3.6;
                    var throttlePercent = locomotive?.ThrottleController.CurrentValue * 100 ?? 0;
                    var brakePercent = locomotive?.TrainBrakeController.CurrentValue * 100 ?? 0;
                    var direction = locomotive?.Direction.ToString() ?? "unknown";
                    SetContentStatus($"Driving: {speedKph:F1} km/h | power {throttlePercent:F0}% | brake {brakePercent:F0}% | {direction}. 3D viewer is not integrated.");
                    PlayerControlStateChanged?.Invoke(
                        (int)Math.Round(throttlePercent),
                        (int)Math.Round(brakePercent),
                        direction);
                    lastSimulationStatusUpdate = gameTime.TotalGameTime;
                }
            }
            catch (Exception error)
            {
                global::Android.Util.Log.Error(nameof(AndroidSimulatorGame), error.ToString());
                currentSimulator.Stop();
                if (ReferenceEquals(simulator, currentSimulator))
                {
                    simulator = null;
                    SetContentStatus($"Simulation update stopped: {error.Message}");
                    SimulationUpdateStopped?.Invoke();
                }
            }
        }
        else
        {
            while (pendingControls.TryDequeue(out _))
            {
            }
        }

        string? updatedStatus;
        lock (statusLock)
        {
            updatedStatus = pendingContentStatus;
            pendingContentStatus = null;
        }
        if (updatedStatus != null)
        {
            contentStatus = updatedStatus;
            var previousTexture = statusTexture;
            statusTexture = CreateStatusTexture(contentStatus);
            previousTexture?.Dispose();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(16, 25, 30));
        if (spriteBatch != null)
        {
            var viewport = GraphicsDevice.Viewport;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            var simulatorSnapshot = simulator;
            var mapSnapshot = routeMap;
            if (simulatorSnapshot != null && mapSnapshot != null && mapPixel != null)
            {
                var mapTop = Math.Min(190, viewport.Height / 4);
                var mapArea = new Rectangle(12, mapTop, Math.Max(1, viewport.Width - 24), Math.Max(1, viewport.Height - mapTop - 12));
                DrawRouteMap(simulatorSnapshot, mapSnapshot, mapArea);
            }
            if (statusTexture != null)
            {
                var statusHeight = Math.Min(190, Math.Max(120, viewport.Height / 4));
                spriteBatch.Draw(statusTexture, new Rectangle(12, 12, Math.Max(1, viewport.Width - 24), statusHeight), Color.White);
            }
            spriteBatch.End();
        }
        base.Draw(gameTime);
    }

    void DrawRouteMap(Simulator activeSimulator, RouteMapSnapshot map, Rectangle area)
    {
        var pixel = mapPixel;
        var batch = spriteBatch;
        if (pixel == null || batch == null || activeSimulator.PlayerLocomotive == null)
            return;

        var scaleX = (area.Width - 32f) / (float)Math.Max(map.Width, 1);
        var scaleZ = (area.Height - 32f) / (float)Math.Max(map.Height, 1);
        var scale = Math.Min(scaleX, scaleZ);
        if (!float.IsFinite(scale) || scale <= 0)
            return;
        var drawnWidth = (float)map.Width * scale;
        var drawnHeight = (float)map.Height * scale;
        var offsetX = area.X + (area.Width - drawnWidth) / 2f;
        var offsetY = area.Y + (area.Height - drawnHeight) / 2f;
        Vector2 Project(double x, double z) => new(
            offsetX + (float)(x - map.MinX) * scale,
            offsetY + (float)(map.MaxZ - z) * scale);

        foreach (var segment in map.Segments)
        {
            var start = Project(segment.StartX, segment.StartZ);
            var end = Project(segment.EndX, segment.EndZ);
            var delta = end - start;
            var length = delta.Length();
            if (length < 0.5f)
                continue;
            batch.Draw(pixel, start, null, new Color(120, 166, 157), MathF.Atan2(delta.Y, delta.X),
                Vector2.Zero, new Vector2(length, 2), SpriteEffects.None, 0);
        }

        var traveller = activeSimulator.PlayerLocomotive.Train.FrontTDBTraveller;
        var trainPosition = Project((double)traveller.TileX * 2048 + traveller.X, (double)traveller.TileZ * 2048 + traveller.Z);
        batch.Draw(pixel, new Rectangle((int)trainPosition.X - 7, (int)trainPosition.Y - 7, 14, 14), new Color(255, 190, 75));
    }

    protected override void UnloadContent()
    {
        statusTexture?.Dispose();
        mapPixel?.Dispose();
        spriteBatch?.Dispose();
        base.UnloadContent();
    }
}

internal sealed class RouteMapSnapshot
{
    RouteMapSnapshot(IReadOnlyList<TrackSegment> segments, double minX, double maxX, double minZ, double maxZ)
    {
        Segments = segments;
        MinX = minX;
        MaxX = maxX;
        MinZ = minZ;
        MaxZ = maxZ;
    }

    public IReadOnlyList<TrackSegment> Segments { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double MinZ { get; }
    public double MaxZ { get; }
    public double Width => MaxX - MinX;
    public double Height => MaxZ - MinZ;

    public static RouteMapSnapshot? Create(Simulator simulator)
    {
        var segments = new List<TrackSegment>();
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minZ = double.PositiveInfinity;
        var maxZ = double.NegativeInfinity;
        foreach (var node in simulator.TDB.TrackDB.TrackNodes)
        {
            var sections = node?.TrVectorNode?.TrVectorSections;
            if (sections == null || sections.Length < 2)
                continue;
            for (var index = 1; index < sections.Length; index++)
            {
                var first = sections[index - 1];
                var second = sections[index];
                var startX = (double)first.TileX * 2048 + first.X;
                var startZ = (double)first.TileZ * 2048 + first.Z;
                var endX = (double)second.TileX * 2048 + second.X;
                var endZ = (double)second.TileZ * 2048 + second.Z;
                segments.Add(new TrackSegment(startX, startZ, endX, endZ));
                minX = Math.Min(minX, Math.Min(startX, endX));
                maxX = Math.Max(maxX, Math.Max(startX, endX));
                minZ = Math.Min(minZ, Math.Min(startZ, endZ));
                maxZ = Math.Max(maxZ, Math.Max(startZ, endZ));
            }
        }

        if (segments.Count == 0)
            return null;

        if (maxX - minX < 1)
        {
            minX -= 0.5;
            maxX += 0.5;
        }
        if (maxZ - minZ < 1)
        {
            minZ -= 0.5;
            maxZ += 0.5;
        }
        return new RouteMapSnapshot(segments, minX, maxX, minZ, maxZ);
    }
}

internal readonly record struct TrackSegment(double StartX, double StartZ, double EndX, double EndZ);
