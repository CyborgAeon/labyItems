using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls.Sliders;

public partial class VitaeSlider : ContentView
{
    private const int PipCount = 10;
    private readonly List<Border> _pips = new();
    private double _panStartValue;
    private double _panStartX;

    public VitaeSlider()
    {
        InitializeComponent();
        BuildPips();
        UpdateVisuals();
    }

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(int),
            typeof(VitaeSlider),
            0,
            BindingMode.TwoWay,
            coerceValue: (b, v) => CoerceValue((int)v),
            propertyChanged: (b, o, n) => ((VitaeSlider)b).OnValueChanged((int)o, (int)n));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly BindableProperty ExactEnabledProperty =
        BindableProperty.Create(
            nameof(ExactEnabled),
            typeof(bool),
            typeof(VitaeSlider),
            false,
            propertyChanged: (b, o, n) => ((VitaeSlider)b).UpdateVisuals());
    public bool ExactEnabled
    {
        get => (bool)GetValue(ExactEnabledProperty);
        set => SetValue(ExactEnabledProperty, value);
    }

    public event EventHandler<int>? ValueChanged;
    private void BuildPips()
    {
        PipsGrid.ColumnDefinitions.Clear();
        PipsGrid.Children.Clear();
        _pips.Clear();

        for (int i = 0; i < PipCount; i++)
        {
            PipsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var pip = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6) },
                BackgroundColor = Colors.DimGray,
                HeightRequest = 34,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                Shadow = new Shadow
                {
                    Opacity = 0.0f,
                    Radius = 10,
                    Offset = new Point(0, 0)
                }
            };

            Grid.SetColumn(pip, i);
            PipsGrid.Children.Add(pip);
            _pips.Add(pip);
        }
    }

    private static int CoerceValue(int v)
    {
        if (v < 0) return 0;
        if (v > 100) return 100;
        return v;
    }

    private void OnValueChanged(int oldValue, int newValue)
    {
        UpdateVisuals();
        ValueChanged?.Invoke(this, newValue);
    }

    private int ApplyQuantization(int raw)
    {
        raw = CoerceValue(raw);
        if (ExactEnabled) return raw;
        if (raw == 0) return 0;

        int snapped = (int)Math.Round(raw / 10.0, MidpointRounding.AwayFromZero) * 10;
        snapped = CoerceValue(snapped);
        if (snapped == 0) snapped = 10;
        return snapped;
    }

    private Color GetRangeColor(int value)
    {
        if (value >= 80) return Color.FromArgb("#2ECC71");
        if (value >= 61) return Color.FromArgb("#B6E637");
        if (value >= 41) return Color.FromArgb("#F1C40F");
        if (value >= 21) return Color.FromArgb("#E67E22");
        if (value >= 1) return Color.FromArgb("#E74C3C");
        return Colors.DimGray;
    }

    private void UpdateVisuals()
    {
        int displayValue = CoerceValue(Value);
        ValueLabel.Text = displayValue.ToString();
        int litCount = displayValue == 0
            ? 0
            : (int)Math.Ceiling(displayValue / 10.0);

        var litColor = GetRangeColor(displayValue);
        var unlitColor = Color.FromArgb("#6B6B6B");

        for (int i = 0; i < PipCount; i++)
        {
            bool lit = i < litCount;

            var pip = _pips[i];
            pip.BackgroundColor = lit ? litColor : unlitColor;
            if (pip.Shadow is Shadow sh)
            {
                sh.Opacity = lit ? 0.55f : 0.0f;
                sh.Radius = lit ? 14 : 0;
            }
        }
    }

    // --- Gestures (tap + pan) ---

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var p = e.GetPosition(PipsGrid);
        if (p is null) return;

        SetValueFromX(p.Value.X);
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartValue = Value;
                _panStartX = e.TotalX;
                break;

            case GestureStatus.Running:
                double width = Math.Max(1, PipsGrid.Width);
                double deltaX = e.TotalX - _panStartX;
                double deltaPercent = (deltaX / width) * 100.0;

                int raw = (int)Math.Round(_panStartValue + deltaPercent);
                Value = ApplyQuantization(raw);
                break;
        }
    }

    private void SetValueFromX(double x)
    {
        double width = Math.Max(1, PipsGrid.Width);
        double t = x / width;
        int raw = (int)Math.Round(t * 100.0);
        Value = ApplyQuantization(raw);
    }

}