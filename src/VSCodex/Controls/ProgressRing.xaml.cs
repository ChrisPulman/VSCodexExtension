// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace VSCodex.Controls;

/// <summary>Lightweight progress ring based on the CrissCross WPF ProgressRing API surface.</summary>
public partial class ProgressRing : UserControl
{
    /// <summary>Stores the progress Property.</summary>
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(ProgressRing),
        new(50D, OnProgressChanged));

    /// <summary>Stores the is Indeterminate Property.</summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(ProgressRing),
        new(false, OnIsIndeterminateChanged));

    /// <summary>Stores the is Active Property.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(ProgressRing),
        new(false, OnIsActiveChanged));

    /// <summary>Stores the cover Ring Stroke Property.</summary>
    public static readonly DependencyProperty CoverRingStrokeProperty = DependencyProperty.Register(
        nameof(CoverRingStroke),
        typeof(Brush),
        typeof(ProgressRing),
        new(Brushes.Transparent));

    /// <summary>Stores the cover Ring Visibility Property.</summary>
    public static readonly DependencyProperty CoverRingVisibilityProperty = DependencyProperty.Register(
        nameof(CoverRingVisibility),
        typeof(Visibility),
        typeof(ProgressRing),
        new(Visibility.Visible));

    /// <summary>Named number used by this type.</summary>
    private const double Numeric10D = 10D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric100D = 100D;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric16 = 16;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric180D = 180D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric2D = 2D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric3Point6D = 3.6D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric359D = 359D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric360D = 360D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric4D = 4D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric75D = 75D;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric90D = 90D;

    /// <summary>Stores the indeterminate Timer.</summary>
    private readonly DispatcherTimer _indeterminateTimer;

    /// <summary>Stores the indeterminate Angle.</summary>
    private double _indeterminateAngle;

    /// <summary>Initializes a new instance of the <see cref="ProgressRing"/> class.</summary>
    public ProgressRing()
    {
        InitializeComponent();

        _indeterminateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Numeric16) };
        _indeterminateTimer.Tick += OnIndeterminateTimerTick;
        SizeChanged += (_, _) => UpdateProgressVisual();
        Loaded += (_, _) => UpdateIndeterminateState();
        Unloaded += (_, _) => _indeterminateTimer.Stop();
    }

    /// <summary>Gets or sets the progress.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Gets or sets the is Indeterminate.</summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Gets or sets the is Active.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets or sets the cover Ring Stroke.</summary>
    public Brush CoverRingStroke
    {
        get => (Brush)GetValue(CoverRingStrokeProperty);
        set => SetValue(CoverRingStrokeProperty, value);
    }

    /// <summary>Gets or sets the cover Ring Visibility.</summary>
    public Visibility CoverRingVisibility
    {
        get => (Visibility)GetValue(CoverRingVisibilityProperty);
        set => SetValue(CoverRingVisibilityProperty, value);
    }

    /// <summary>Performs the point On Circle operation.</summary>
    /// <param name="center">The center.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="angle">The angle.</param>
    /// <returns>The point On Circle result.</returns>
    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = Math.PI * angle / Numeric180D;
        return new(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }

    /// <summary>Handles the progress Changed event.</summary>
    /// <param name="d">The d.</param>
    /// <param name="_">The unused event data.</param>
    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is not ProgressRing control)
        {
            return;
        }

        control.UpdateProgressVisual();
    }

    /// <summary>Handles the is Indeterminate Changed event.</summary>
    /// <param name="d">The d.</param>
    /// <param name="e">The e.</param>
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

    /// <summary>Handles the is Active Changed event.</summary>
    /// <param name="d">The d.</param>
    /// <param name="e">The e.</param>
    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProgressRing control || e.NewValue is not bool isActive)
        {
            return;
        }

        if (control.IsIndeterminate == isActive)
        {
            return;
        }

        control.SetCurrentValue(IsIndeterminateProperty, isActive);
    }

    /// <summary>Handles the indeterminate Timer Tick event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnIndeterminateTimerTick(object? sender, EventArgs e)
    {
        _indeterminateAngle = (_indeterminateAngle + Numeric4D) % Numeric360D;
        ProgressArcRotation.Angle = _indeterminateAngle;
    }

    /// <summary>Updates indeterminate State.</summary>
    private void UpdateIndeterminateState()
    {
        if (IsIndeterminate && IsLoaded)
        {
            SetProgressArc(Numeric75D);
            _indeterminateTimer.Start();
            return;
        }

        _indeterminateTimer.Stop();
        ProgressArcRotation.Angle = 0D;
        UpdateProgressVisual();
    }

    /// <summary>Updates progress Visual.</summary>
    private void UpdateProgressVisual()
    {
        if (IsIndeterminate)
        {
            SetProgressArc(Numeric75D);
            return;
        }

        var progress = Math.Max(0D, Math.Min(Numeric100D, Progress));
        SetProgressArc(progress);
    }

    /// <summary>Sets progress Arc.</summary>
    /// <param name="progress">The progress.</param>
    private void SetProgressArc(double progress)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0D)
        {
            return;
        }

        var thickness = Math.Max(Numeric2D, size / Numeric10D);
        CoverRing.StrokeThickness = thickness;
        ProgressArc.StrokeThickness = thickness;

        if (progress <= 0D)
        {
            ProgressArc.Data = null;
            return;
        }

        var angle = Math.Min(Numeric359D, Numeric3Point6D * progress);
        var radius = Math.Max(0D, (size - thickness) / Numeric2D);
        var center = new Point(ActualWidth / Numeric2D, ActualHeight / Numeric2D);
        var startPoint = PointOnCircle(center, radius, -Numeric90D);
        var endPoint = PointOnCircle(center, radius, angle - Numeric90D);

        var segment = new ArcSegment(
            endPoint,
            new Size(radius, radius),
            0D,
            angle > Numeric180D,
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
}
