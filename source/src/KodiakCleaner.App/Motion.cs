using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KodiakCleaner.App;

/// <summary>
/// Whether Kodiak Cleaner plays animations: the Animations switch (Settings › Appearance) and Windows'
/// "Animation effects" setting must both be on. Styles bind to <see cref="Instance"/> (busy bars stop moving when off).
/// </summary>
public sealed class Motion : INotifyPropertyChanged
{
    public static Motion Instance { get; } = new();

    private Motion() => SystemParameters.StaticPropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Refresh();
    };

    public bool Enabled => Settings.Current.Animations && SystemParameters.ClientAreaAnimation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
}

/// <summary>The bear logo's little dance: a hop and a wiggle on hover and click, used in the sidebar and Getting Started.</summary>
public static class LogoDance
{
    /// <summary>Gives the image the transforms the dance needs and makes it dance on hover and click.</summary>
    public static void Attach(Image logo)
    {
        logo.RenderTransformOrigin = new Point(0.5, 0.85);
        logo.RenderTransform = new TransformGroup { Children = { new ScaleTransform(), new RotateTransform(), new TranslateTransform() } };
        logo.MouseEnter += (_, _) => Play(logo);
        logo.MouseLeftButtonUp += (_, _) => Play(logo);
    }

    private static readonly HashSet<Image> Dancing = new();

    public static void Play(Image logo)
    {
        if (!Motion.Instance.Enabled || logo.RenderTransform is not TransformGroup { Children.Count: 3 } group || !Dancing.Add(logo)) return;
        var scale = (ScaleTransform)group.Children[0];
        var rotate = (RotateTransform)group.Children[1];
        var move = (TranslateTransform)group.Children[2];
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        DoubleAnimationUsingKeyFrames Frames(params (double ms, double value)[] frames)
        {
            var a = new DoubleAnimationUsingKeyFrames();
            foreach (var (ms, value) in frames)
                a.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), ease));
            return a;
        }

        var wiggle = Frames((0, 0), (110, -12), (230, 12), (350, -9), (470, 7), (590, -3), (700, 0));
        wiggle.Completed += (_, _) => Dancing.Remove(logo);
        rotate.BeginAnimation(RotateTransform.AngleProperty, wiggle);
        move.BeginAnimation(TranslateTransform.YProperty, Frames((0, 0), (150, -7), (300, 0), (450, -4), (600, 0)));
        var squash = Frames((0, 1), (150, 1.08), (300, 0.96), (450, 1.04), (700, 1));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, squash);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, squash);
    }
}
