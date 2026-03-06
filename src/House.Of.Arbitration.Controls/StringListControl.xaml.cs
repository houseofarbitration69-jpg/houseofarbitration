using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace House.Of.Arbitration.Controls;

public partial class StringListControl : ContentView
{
    // Collection pour le rendu visuel
    public ObservableCollection<string> InternalItems { get; } = new();

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), 
        typeof(IList), 
        typeof(StringListControl), 
        null,
        propertyChanged: OnItemsChanged);

    public IList Items
    {
        get => (IList)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (StringListControl)bindable;
        
        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnItemsCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnItemsCollectionChanged;

        control.UpdateInternalItems();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateInternalItems();
    }

    public static readonly BindableProperty ItemColorProperty = BindableProperty.Create(
        nameof(ItemColor), typeof(Color), typeof(StringListControl), Colors.Black);

    public Color ItemColor
    {
        get => (Color)GetValue(ItemColorProperty);
        set => SetValue(ItemColorProperty, value);
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(StringListControl), null);

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public StringListControl()
    {
        // INITIALISATION CRUCIALE : permet au XAML d'appeler .Add() sans crash
        Items = new ObservableCollection<string>();
        
        InitializeComponent();
    }

    private void UpdateInternalItems()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            InternalItems.Clear();
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    if (item != null)
                        InternalItems.Add(item.ToString());
                }
            }
        });
    }
}
