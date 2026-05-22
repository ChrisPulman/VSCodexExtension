using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VSCodex.Controls;

/// <summary>
/// Lightweight progress ring based on the CrissCross WPF ProgressRing API surface.
/// </summary>
public partial class ProgressRing : UserControl
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(ProgressRing),
        new PropertyMetadata(50d, OnProgressChanged));

    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(ProgressRing),
        new PropertyMetadata(false, OnIsIndeterminateChanged));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(ProgressRing),
        new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty CoverRingStrokeProperty = DependencyProperty.Register(
        nameof(CoverRingStroke),
        typeof(Brush),
        typeof(ProgressRing),
        new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty CoverRingVisibilityProperty = DependencyProperty.Register(
        nameof(CoverRingVisibility),
        typeof(Visibility),
        typeof(ProgressRing),
        new PropertyMetadata(Visibility.Visible));

    private readonly DispatcherTimer indeterminateTimer;
    private double indeterminateAngle;

    public ProgressRing()
    {
        InitializeComponent();

        indeterminateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        indeterminateTimer.Tick += OnIndeterminateTimerTick;
        SizeChanged += (_, _) => UpdateProgressVisual();
        Loaded += (_, _) => UpdateIndeterminateState();
        Unloaded += (_, _) => indeterminateTimer.Stop();
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public Brush CoverRingStroke
    {
        get => (Brush)GetValue(CoverRingStrokeProperty);
        set => SetValue(CoverRingStrokeProperty, value);
    }

    public Visibility CoverRingVisibility
    {
        get => (Visibility)GetValue(CoverRingVisibilityProperty);
        set => SetValue(CoverRingVisibilityProperty, value);
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressRing control)
        {
            control.UpdateProgressVisual();
        }
    }

    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProgressRing control || e.NewValue is not bool isIndeterminate)
        {
            return;
        }

        if (control.IsActive != isIndeterminate)
        {
            control.SetCurrentValue(IsActiveProperty, isIndeterminate);
        }

        control.UpdateIndeterminateState();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProgressRing control || e.NewValue is not bool isActive)
        {
            return;
        }

        if (control.IsIndeterminate != isActive)
        {
            control.SetCurrentValue(IsIndeterminateProperty, isActive);
        }
    }

    private void OnIndeterminateTimerTick(object? sender, EventArgs e)
    {
        indeterminateAngle = (indeterminateAngle + 4d) % 360d;
        ProgressArcRotation.Angle = indeterminateAngle;
    }

    private void UpdateIndeterminateState()
    {
        if (IsIndeterminate && IsLoaded)
        {
            SetProgressArc(75d);
            indeterminateTimer.Start();
            return;
        }

        indeterminateTimer.Stop();
        ProgressArcRotation.Angle = 0d;
        UpdateProgressVisual();
    }

    private void UpdateProgressVisual()
    {
        if (IsIndeterminate)
        {
            SetProgressArc(75d);
            return;
        }

        var progress = Math.Max(0d, Math.Min(100d, Progress));
        SetProgressArc(progress);
    }

    private void SetProgressArc(double progress)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0d)
        {
            return;
        }

        var thickness = Math.Max(2d, size / 10d);
        CoverRing.StrokeThickness = thickness;
        ProgressArc.StrokeThickness = thickness;

        if (progress <= 0d)
        {
            ProgressArc.Data = null;
            return;
        }

        var angle = Math.Min(359d, 3.6d * progress);
        var radius = Math.Max(0d, (size - thickness) / 2d);
        var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
        var startPoint = PointOnCircle(center, radius, -90d);
        var endPoint = PointOnCircle(center, radius, angle - 90d);

        var segment = new ArcSegment(
            endPoint,
            new Size(radius, radius),
            0d,
            angle > 180d,
            SweepDirection.Clockwise,
            true);

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            IsClosed = false
        };
        figure.Segments.Add(segment);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ProgressArc.Data = geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = Math.PI * angle / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
    }
}
