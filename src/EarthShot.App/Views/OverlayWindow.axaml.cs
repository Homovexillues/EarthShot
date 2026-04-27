using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

namespace EarthShot.App.Views;

public partial class OverlayWindow : Window
{
    private readonly Rectangle _selectionBox;
    private readonly TextBlock _cursorLabel;

    private SelectionState _state = SelectionState.Idle;
    private Point _start;
    private Point _current;

    public OverlayWindow()
    {
        InitializeComponent();
        _selectionBox = this.FindControl<Rectangle>("SelectionBox")!;
        _cursorLabel = this.FindControl<TextBlock>("CursorLabel")!;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            Close();
            return;
        }

        if (!props.IsLeftButtonPressed)
            return;

        _start = e.GetPosition(this);
        _current = _start;
        _state = SelectionState.Selecting;
        _selectionBox.IsVisible = true;
        UpdateSelectionBox();
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _current = e.GetPosition(this);

        if (_state == SelectionState.Selecting)
        {
            UpdateSelectionBox();
            var rect = Normalize(_start, _current);
            _cursorLabel.Text =
                $"Selecting: {rect.X:F0}, {rect.Y:F0}  {rect.Width:F0} × {rect.Height:F0}";
        }
        else
        {
            _cursorLabel.Text = $"Cursor: {_current.X:F0}, {_current.Y:F0}";
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_state != SelectionState.Selecting)
            return;

        e.Pointer.Capture(null);
        _state = SelectionState.Selected;
        UpdateSelectionBox();

        var rect = Normalize(_start, _current);
        _cursorLabel.Text =
            $"Selected: {rect.X:F0}, {rect.Y:F0}  {rect.Width:F0} × {rect.Height:F0}";
    }

    private void UpdateSelectionBox()
    {
        var rect = Normalize(_start, _current);
        Canvas.SetLeft(_selectionBox, rect.X);
        Canvas.SetTop(_selectionBox, rect.Y);
        _selectionBox.Width = rect.Width;
        _selectionBox.Height = rect.Height;
    }

    private static Rect Normalize(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }
}

internal enum SelectionState
{
    Idle,
    Selecting,
    Selected,
}
