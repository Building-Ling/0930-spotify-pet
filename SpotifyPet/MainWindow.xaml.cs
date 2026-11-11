using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace SpotifyPet;

public partial class MainWindow : Window
{
    private static readonly Geometry PlayIcon = Geometry.Parse("M 2,0 L 18,9 L 2,18 Z");
    private static readonly Geometry PauseIcon = Geometry.Parse("M 2,0 H 7 V 18 H 2 Z M 11,0 H 16 V 18 H 11 Z");
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer pianoTimer = new() { Interval = TimeSpan.FromMilliseconds(360) };
    private const int PianoBeatsPerGlance = 6;
    private ImageSource[,]? pianoFrames;
    private int pianoBeat;
    private bool pianoMode;
    private readonly DispatcherTimer feedingTimer = new();
    private bool feedingMode;
    private bool busy;
    private bool petAwake;
    private bool spotifyPlaying;
    private bool lampPlaying;
    private bool dragging;
    private bool reacting;
    private Point? pressPosition;
    private bool pressedPet;
    private bool canSeek;
    private bool scrubbing;
    private string? scrubTrack;
    private double trackDuration;

    public MainWindow()
    {
        InitializeComponent();
        StartSleepAnimation();
        timer.Tick += async (_, _) => await RefreshAsync();
        pianoTimer.Tick += (_, _) =>
        {
            pianoBeat = (pianoBeat + 1) % (2 * PianoBeatsPerGlance);
            PianoSprite.Source = pianoFrames![pianoBeat % 2, pianoBeat / PianoBeatsPerGlance];
        };
        feedingTimer.Tick += OnEatingTick;
        Closed += (_, _) =>
        {
            timer.Stop();
            pianoTimer.Stop();
            feedingTimer.Stop();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        if (IsLoaded)
            timer.Start();
    }

    private GlobalSystemMediaTransportControlsSession? FindSpotify() =>
        manager?.GetSessions().FirstOrDefault(session =>
            session.SourceAppUserModelId.Equals("Spotify.exe", StringComparison.OrdinalIgnoreCase) ||
            session.SourceAppUserModelId.StartsWith("SpotifyAB.SpotifyMusic_", StringComparison.OrdinalIgnoreCase));

    private async Task RefreshAsync()
    {
        if (busy || scrubbing)
            return;

        busy = true;
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = FindSpotify();
            var media = session is null ? null : await session.TryGetMediaPropertiesAsync();
            var playback = session?.GetPlaybackInfo();

            SongTitle.Text = string.IsNullOrWhiteSpace(media?.Title) ? "未播放" : media.Title;
            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var controls = playback?.Controls;
            canSeek = controls?.IsPlaybackPositionEnabled == true;
            PreviousButton.IsEnabled = controls?.IsPreviousEnabled == true;
            NextButton.IsEnabled = controls?.IsNextEnabled == true;
            PlayPauseButton.IsEnabled = isPlaying ? controls?.IsPauseEnabled == true : controls?.IsPlayEnabled == true;
            UpdateProgress(session?.GetTimelineProperties(), isPlaying, playback?.PlaybackRate ?? 1);
            spotifyPlaying = isPlaying;
            UpdatePet();
        }
        catch (Exception ex)
        {
            SongTitle.Text = "无法读取 Spotify";
            Status.Text = ex.Message;
            PreviousButton.IsEnabled = false;
            PlayPauseButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            canSeek = false;
            UpdateProgress(null, false, 1);
            spotifyPlaying = false;
            UpdatePet();
        }
        finally
        {
            busy = false;
        }
    }

    private void UpdateProgress(GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline,
        bool isPlaying, double rate)
    {
        PlayPauseIcon.Data = isPlaying ? PauseIcon : PlayIcon;
        var action = isPlaying ? "暂停" : "播放";
        PlayPauseButton.ToolTip = action;
        AutomationProperties.SetName(PlayPauseButton, action);

        if (timeline is null || timeline.EndTime <= timeline.StartTime)
        {
            trackDuration = 0;
            canSeek = false;
            SeekArea.Cursor = Cursors.Arrow;
            SongProgress.Value = 0;
            ElapsedTime.Text = TotalTime.Text = "--:--";
            return;
        }

        var duration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
        trackDuration = duration;
        SeekArea.Cursor = canSeek ? Cursors.Hand : Cursors.Arrow;
        TotalTime.Text = FormatTime(duration);
        if (scrubbing)
            return;

        var position = (timeline.Position - timeline.StartTime).TotalSeconds;
        if (isPlaying && timeline.LastUpdatedTime != default)
            position += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds) * rate;

        position = Math.Clamp(position, 0, duration);
        SongProgress.Value = position / duration;
        ElapsedTime.Text = FormatTime(position);
    }

    private static string FormatTime(double seconds)
    {
        var elapsed = TimeSpan.FromSeconds(seconds);
        return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
    }

    private void UpdatePet()
    {
        UpdateLamp();
        PerformanceSprite.Visibility = feedingMode ? Visibility.Collapsed : Visibility.Visible;
        FeedingScene.Visibility = feedingMode ? Visibility.Visible : Visibility.Collapsed;
        var awake = spotifyPlaying || dragging || reacting;
        var playingPiano = spotifyPlaying && pianoMode && !feedingMode;
        UpdatePianoSprite(playingPiano);
        SingingSprite.Visibility = spotifyPlaying && !playingPiano ? Visibility.Visible : Visibility.Collapsed;
        AwakeSprite.Visibility = awake && !spotifyPlaying ? Visibility.Visible : Visibility.Collapsed;
        SleepBubble.Visibility = awake || feedingMode ? Visibility.Collapsed : Visibility.Visible;

        // Refreshing Spotify metadata must not restart an ongoing animation.
        if (petAwake == awake)
            return;

        petAwake = awake;
        SleepSprite.Visibility = awake ? Visibility.Collapsed : Visibility.Visible;

        PetRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        PetBreathing.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SleepBubble.BeginAnimation(OpacityProperty, null);
        PetRotation.Angle = awake ? 0 : -4;
        if (awake)
        {
            PetRotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(-5, 5, TimeSpan.FromSeconds(0.9))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
        }
        else
        {
            StartSleepAnimation();
        }
    }

    private void UpdatePianoSprite(bool playingPiano)
    {
        PianoSprite.Visibility = playingPiano ? Visibility.Visible : Visibility.Collapsed;
        if (!playingPiano)
        {
            pianoTimer.Stop();
            return;
        }

        if (pianoTimer.IsEnabled)
            return;

        pianoFrames ??= LoadPianoFrames();
        pianoBeat = 0;
        PianoSprite.Source = pianoFrames[0, 0];
        pianoTimer.Start();
    }

    private static ImageSource[,] LoadPianoFrames()
    {
        var basePose = new BitmapImage(new Uri("pack://application:,,,/Assets/0930-piano.png"));
        var alternatePose = new BitmapImage(new Uri("pack://application:,,,/Assets/0930-piano-alternate.png"));
        var frames = new ImageSource[2, 2];
        var canvas = new Rect(0, 0, basePose.PixelWidth, basePose.PixelHeight);
        // Overlay only the moving regions so the approved silhouette stays fixed.
        var eyes = new Rect(215, 395, 445, 160);
        var keyboard = new Rect(218, 1058, 571, 174);
        for (var paw = 0; paw < 2; paw++)
        {
            for (var gaze = 0; gaze < 2; gaze++)
            {
                var drawing = new DrawingGroup();
                using (var context = drawing.Open())
                {
                    context.DrawImage(basePose, canvas);
                    if (paw == 1)
                    {
                        context.PushClip(new RectangleGeometry(keyboard));
                        context.DrawImage(alternatePose, canvas);
                        context.Pop();
                    }
                    if (gaze == 1)
                    {
                        context.PushClip(new RectangleGeometry(eyes));
                        context.DrawImage(alternatePose, canvas);
                        context.Pop();
                    }
                }
                drawing.Freeze();
                frames[paw, gaze] = new DrawingImage(drawing);
            }
        }
        return frames;
    }

    private void OnPianoModeChanged(object sender, RoutedEventArgs e)
    {
        pianoMode = ((MenuItem)sender).IsChecked;
        if (pianoMode)
        {
            FeedingModeMenu.IsChecked = false;
            feedingMode = false;
            ResetFeeding();
        }
        UpdatePet();
    }

    private void OnFeedingModeChanged(object sender, RoutedEventArgs e)
    {
        feedingMode = ((MenuItem)sender).IsChecked;
        if (feedingMode)
        {
            PianoModeMenu.IsChecked = false;
            pianoMode = false;
        }
        else
        {
            ResetFeeding();
        }
        UpdatePet();
    }

    private void OnFeedPot(object sender, RoutedEventArgs e)
    {
        if (!feedingMode || !FeedPotButton.IsEnabled)
            return;

        FeedPotButton.IsEnabled = false;
        FeedPotButton.ToolTip = "0930 正在吃牛肉丸";
        MovingMeatball.Visibility = Visibility.Visible;
        feedingTimer.Interval = TimeSpan.FromMilliseconds(440);
        feedingTimer.Start();
    }

    private void OnEatingTick(object? sender, EventArgs e)
    {
        if (EatingMouth.Visibility == Visibility.Visible)
        {
            ResetFeeding();
            return;
        }

        feedingTimer.Interval = TimeSpan.FromMilliseconds(420);
        EatingMouth.Visibility = Visibility.Visible;
        var bite = TimeSpan.FromMilliseconds(330);
        MovingMeatball.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(37.5, bite));
        MovingMeatball.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(58.5, bite));
        var swallow = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120))
        {
            BeginTime = TimeSpan.FromMilliseconds(250),
        };
        MeatballScale.BeginAnimation(ScaleTransform.ScaleXProperty, swallow);
        MeatballScale.BeginAnimation(ScaleTransform.ScaleYProperty, swallow);
    }

    private void ResetFeeding()
    {
        feedingTimer.Stop();
        MovingMeatball.Visibility = Visibility.Hidden;
        EatingMouth.Visibility = Visibility.Hidden;
        MovingMeatball.BeginAnimation(Canvas.LeftProperty, null);
        MovingMeatball.BeginAnimation(Canvas.TopProperty, null);
        MeatballScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        MeatballScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FeedPotButton.IsEnabled = true;
        FeedPotButton.ToolTip = "投喂牛肉丸";
    }

    private void UpdateLamp()
    {
        var useStageLights = spotifyPlaying && !feedingMode;
        if (lampPlaying == useStageLights)
            return;

        lampPlaying = useStageLights;
        SideStageBeams.Visibility = lampPlaying ? Visibility.Visible : Visibility.Collapsed;
        LeftStageLens.Opacity = RightStageLens.Opacity = lampPlaying ? 1 : 0.16;
        CenterStageBeam.Opacity = lampPlaying ? 0.9 : 0.65;
        LeftStageColor.BeginAnimation(SolidColorBrush.ColorProperty,
            lampPlaying ? CycleLamp("#FF74BE", "#B79AFF", "#FFD775", "#78E8FF") : null);
        CenterStageColor.BeginAnimation(SolidColorBrush.ColorProperty,
            lampPlaying ? CycleLamp("#78E8FF", "#FFD775", "#FF74BE", "#B79AFF") : null);
        RightStageColor.BeginAnimation(SolidColorBrush.ColorProperty,
            lampPlaying ? CycleLamp("#B79AFF", "#78E8FF", "#66FFAD", "#FF74BE") : null);
    }

    private static ColorAnimationUsingKeyFrames CycleLamp(params string[] colors)
    {
        var animation = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        for (var i = 0; i <= colors.Length; i++)
        {
            animation.KeyFrames.Add(new LinearColorKeyFrame(
                (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]),
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(i * 0.85))));
        }
        return animation;
    }

    private void StartSleepAnimation()
    {
        PetBreathing.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.98, TimeSpan.FromSeconds(1.5))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        SleepBubble.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.5))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
    }

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (feedingMode && FeedPotButton.IsMouseOver)
        {
            pressPosition = null;
            return;
        }
        if (SeekArea.IsMouseOver)
        {
            pressPosition = null;
            e.Handled = true;
            if (canSeek && !busy && SeekArea.CaptureMouse())
            {
                scrubbing = true;
                scrubTrack = SongTitle.Text;
                PreviewSeek(e.GetPosition(SeekArea).X);
            }
            return;
        }

        pressPosition = e.GetPosition(this);
        pressedPet = PetArea.IsMouseOver;
        Mouse.Capture(this, CaptureMode.SubTree);
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (scrubbing && SeekArea.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            PreviewSeek(e.GetPosition(SeekArea).X);
            return;
        }
        scrubbing = false;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            pressPosition = null;
            return;
        }

        if (pressPosition is not Point start)
            return;

        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        pressPosition = null;
        e.Handled = true;
        // Cancel the button press before handing this gesture to Windows.
        Mouse.Capture(null);
        if (Mouse.LeftButton != MouseButtonState.Pressed)
            return;

        dragging = true;
        PlayWakeAnimation();
        try
        {
            DragMove();
        }
        finally
        {
            dragging = false;
            reacting = false;
            PetJump.BeginAnimation(TranslateTransform.YProperty, null);
            UpdatePet();
        }
    }

    private async void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (scrubbing)
        {
            PreviewSeek(e.GetPosition(SeekArea).X);
            var fraction = SongProgress.Value;
            scrubbing = false;
            SeekArea.ReleaseMouseCapture();
            e.Handled = true;
            if (canSeek)
            {
                var expectedTrack = scrubTrack;
                await RunPlaybackCommandAsync(async session =>
                {
                    var media = await session.TryGetMediaPropertiesAsync();
                    if (media.Title != expectedTrack || !session.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled)
                        return false;
                    var timeline = session.GetTimelineProperties();
                    var duration = timeline.EndTime - timeline.StartTime;
                    if (duration <= TimeSpan.Zero)
                        return false;
                    return await session.TryChangePlaybackPositionAsync(
                        timeline.StartTime.Ticks + (long)(duration.Ticks * fraction));
                });
            }
            return;
        }

        var clickedPet = pressPosition.HasValue && pressedPet && PetArea.IsMouseOver;
        pressPosition = null;
        if (Mouse.Captured == this)
            ReleaseMouseCapture();
        if (clickedPet)
            PlayWakeAnimation();
    }

    private void OnSeekCaptureLost(object sender, MouseEventArgs e) => scrubbing = false;

    private void PreviewSeek(double x)
    {
        SongProgress.Value = Math.Clamp(x / SeekArea.ActualWidth, 0, 1);
        ElapsedTime.Text = FormatTime(trackDuration * SongProgress.Value);
    }

    private void PlayWakeAnimation()
    {
        reacting = true;
        UpdatePet();
        var jump = new DoubleAnimation(0, -12, TimeSpan.FromMilliseconds(160))
        {
            AutoReverse = true,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut },
        };
        jump.Completed += (_, _) =>
        {
            reacting = false;
            UpdatePet();
        };
        PetJump.BeginAnimation(TranslateTransform.YProperty, jump);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnTopmostChanged(object sender, RoutedEventArgs e)
    {
        Topmost = ((MenuItem)sender).IsChecked;
    }

    private async void OnPrevious(object sender, RoutedEventArgs e) =>
        await RunPlaybackCommandAsync(async session => await session.TrySkipPreviousAsync());

    private async void OnPlayPause(object sender, RoutedEventArgs e) =>
        await RunPlaybackCommandAsync(async session => spotifyPlaying
            ? await session.TryPauseAsync()
            : await session.TryPlayAsync());

    private async void OnNext(object sender, RoutedEventArgs e) =>
        await RunPlaybackCommandAsync(async session => await session.TrySkipNextAsync());

    private async Task RunPlaybackCommandAsync(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command)
    {
        if (busy)
            return;

        busy = true;
        PreviousButton.IsEnabled = false;
        PlayPauseButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        try
        {
            var session = FindSpotify();
            Status.Text = session is not null && await command(session)
                ? ""
                : "Spotify 当前无法执行此操作。";
        }
        catch (Exception ex)
        {
            Status.Text = $"播放控制失败。{ex.Message}";
        }
        finally
        {
            busy = false;
        }

        await RefreshAsync();
    }
}
