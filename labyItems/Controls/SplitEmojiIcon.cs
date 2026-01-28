using System;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public sealed class SplitEmojiIcon : GraphicsView, IDrawable
{
    public static readonly BindableProperty LeftEmojiProperty =
        BindableProperty.Create(
            nameof(LeftEmoji),
            typeof(string),
            typeof(SplitEmojiIcon),
            string.Empty,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty RightEmojiProperty =
        BindableProperty.Create(
            nameof(RightEmoji),
            typeof(string),
            typeof(SplitEmojiIcon),
            string.Empty,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty LeftColorProperty =
        BindableProperty.Create(
            nameof(LeftColor),
            typeof(Color),
            typeof(SplitEmojiIcon),
            Colors.Transparent,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty RightColorProperty =
        BindableProperty.Create(
            nameof(RightColor),
            typeof(Color),
            typeof(SplitEmojiIcon),
            Colors.Transparent,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SplitEmojiIcon),
            Color.FromArgb("#E5E7EB"),
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty DiagonalColorProperty =
        BindableProperty.Create(
            nameof(DiagonalColor),
            typeof(Color),
            typeof(SplitEmojiIcon),
            Color.FromArgb("#374151"),
            propertyChanged: OnVisualPropertyChanged);

    public string LeftEmoji
    {
        get => (string)GetValue(LeftEmojiProperty);
        set => SetValue(LeftEmojiProperty, value);
    }

    public string RightEmoji
    {
        get => (string)GetValue(RightEmojiProperty);
        set => SetValue(RightEmojiProperty, value);
    }

    public Color LeftColor
    {
        get => (Color)GetValue(LeftColorProperty);
        set => SetValue(LeftColorProperty, value);
    }

    public Color RightColor
    {
        get => (Color)GetValue(RightColorProperty);
        set => SetValue(RightColorProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public Color DiagonalColor
    {
        get => (Color)GetValue(DiagonalColorProperty);
        set => SetValue(DiagonalColorProperty, value);
    }

    public SplitEmojiIcon()
    {
        Drawable = this;
        WidthRequest = 40;
        HeightRequest = 40;
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0)
            return;

        var rect = new RectF(
            dirtyRect.X + (dirtyRect.Width - size) / 2f,
            dirtyRect.Y + (dirtyRect.Height - size) / 2f,
            size,
            size);

        var radius = size / 2f;
        var cx = rect.Center.X;
        var cy = rect.Center.Y;

        canvas.SaveState();
        var clip = new PathF();
        clip.AppendCircle(cx, cy, radius);
        canvas.ClipPath(clip);

        var leftTriangle = new PathF();
        leftTriangle.MoveTo(rect.Left, rect.Top);
        leftTriangle.LineTo(rect.Right, rect.Top);
        leftTriangle.LineTo(rect.Left, rect.Bottom);
        leftTriangle.Close();

        canvas.FillColor = LeftColor;
        canvas.FillPath(leftTriangle);

        var rightTriangle = new PathF();
        rightTriangle.MoveTo(rect.Left, rect.Bottom);
        rightTriangle.LineTo(rect.Right, rect.Top);
        rightTriangle.LineTo(rect.Right, rect.Bottom);
        rightTriangle.Close();

        canvas.FillColor = RightColor;
        canvas.FillPath(rightTriangle);

        canvas.RestoreState();

        canvas.StrokeColor = DiagonalColor;
        canvas.StrokeSize = 1;
        canvas.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Top);

        canvas.StrokeColor = BorderColor;
        canvas.StrokeSize = 1;
        canvas.DrawCircle(cx, cy, radius - 0.5f);

        var fontSize = size * 0.42f;
        canvas.FontSize = fontSize;

        var leftCenter = new PointF(rect.Left + size * 0.32f, rect.Top + size * 0.32f);
        var rightCenter = new PointF(rect.Left + size * 0.68f, rect.Top + size * 0.68f);

        var textBoxSize = size * 0.5f;
        var leftBox = new RectF(
            leftCenter.X - textBoxSize / 2f,
            leftCenter.Y - textBoxSize / 2f,
            textBoxSize,
            textBoxSize);
        var rightBox = new RectF(
            rightCenter.X - textBoxSize / 2f,
            rightCenter.Y - textBoxSize / 2f,
            textBoxSize,
            textBoxSize);

        if (!string.IsNullOrWhiteSpace(LeftEmoji))
            canvas.DrawString(
                LeftEmoji,
                leftBox.X,
                leftBox.Y,
                leftBox.Width,
                leftBox.Height,
                HorizontalAlignment.Center,
                VerticalAlignment.Center,
                TextFlow.ClipBounds,
                1f);

        if (!string.IsNullOrWhiteSpace(RightEmoji))
            canvas.DrawString(
                RightEmoji,
                rightBox.X,
                rightBox.Y,
                rightBox.Width,
                rightBox.Height,
                HorizontalAlignment.Center,
                VerticalAlignment.Center,
                TextFlow.ClipBounds,
                1f);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SplitEmojiIcon icon)
            icon.Invalidate();
    }
}
