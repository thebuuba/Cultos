using System.Windows;
using System.Windows.Input;

namespace Culto.Desktop;

// A click may keep logical focus for shortcuts, but only keyboard navigation
// should paint the keyboard cue. Selection/checked/playing states are separate.
public static class KeyboardFocusCue
{
    private static readonly DependencyPropertyKey IsVisiblePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsVisible", typeof(bool), typeof(KeyboardFocusCue), new PropertyMetadata(false));
    public static readonly DependencyProperty IsVisibleProperty = IsVisiblePropertyKey.DependencyProperty;
    public static bool GetIsVisible(DependencyObject element) => (bool)element.GetValue(IsVisibleProperty);
    private static UIElement? _focused;
    private static bool _keyboard = true;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        EventManager.RegisterClassHandler(typeof(UIElement), Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler((_, _) => SetMode(false)), true);
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler((_, _) => SetMode(true)), true);
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, e) => Update(e.NewFocus as UIElement)), true);
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, e) => Update(e.NewFocus as UIElement)), true);
    }

    private static void SetMode(bool keyboard)
    {
        _keyboard = keyboard;
        Update(Keyboard.FocusedElement as UIElement);
    }
    private static void Update(UIElement? element)
    {
        if (!ReferenceEquals(element, _focused)) _focused?.SetValue(IsVisiblePropertyKey, false);
        _focused = element;
        _focused?.SetValue(IsVisiblePropertyKey, _keyboard && _focused.IsKeyboardFocused);
    }
}
