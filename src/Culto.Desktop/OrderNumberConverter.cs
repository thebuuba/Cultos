using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Culto.Core;

namespace Culto.Desktop;

public sealed class OrderNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index && index >= 0 ? (index + 1).ToString("00", culture) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class OrderListBox : ListBox
{
    public static readonly DependencyProperty AbsoluteIndexProperty = DependencyProperty.RegisterAttached(
        "AbsoluteIndex", typeof(int), typeof(OrderListBox), new PropertyMetadata(-1));
    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.RegisterAttached(
        "IsLive", typeof(bool), typeof(OrderListBox), new PropertyMetadata(false));
    public static readonly DependencyProperty LiveCueIdProperty = DependencyProperty.Register(
        nameof(LiveCueId), typeof(Guid?), typeof(OrderListBox), new PropertyMetadata(null, OnLiveCueChanged));

    private readonly HashSet<ListBoxItem> realizedRows = [];

    public static int GetAbsoluteIndex(DependencyObject element) => (int)element.GetValue(AbsoluteIndexProperty);
    public static bool GetIsLive(DependencyObject element) => (bool)element.GetValue(IsLiveProperty);
    public static void SetIsLive(DependencyObject element, bool value) => element.SetValue(IsLiveProperty, value);
    public Guid? LiveCueId
    {
        get => (Guid?)GetValue(LiveCueIdProperty);
        set => SetValue(LiveCueIdProperty, value);
    }
    private static void OnLiveCueChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var list = (OrderListBox)element;
        foreach (var row in list.realizedRows) list.UpdateLiveMarker(row, row.Content);
    }
    private void UpdateLiveMarker(ListBoxItem row, object item) => SetIsLive(row, item is Cue cue && cue.Id == LiveCueId);

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is not ListBoxItem row) return;
        realizedRows.Add(row);
        var index = ItemContainerGenerator.IndexFromContainer(row);
        // A newly prepared container can precede the generator's final mapping.
        row.SetValue(AbsoluteIndexProperty, index >= 0 ? index : Items.IndexOf(item));
        UpdateLiveMarker(row, item);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is ListBoxItem row)
        {
            realizedRows.Remove(row);
            row.ClearValue(AbsoluteIndexProperty);
            row.ClearValue(IsLiveProperty);
        }
        base.ClearContainerForItemOverride(element, item);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        // WPF updates the generator before OnItemsChanged; only visible/cached rows need new numbers.
        foreach (var row in realizedRows)
        {
            row.SetValue(AbsoluteIndexProperty, ItemContainerGenerator.IndexFromContainer(row));
            UpdateLiveMarker(row, row.Content);
        }
    }
}
