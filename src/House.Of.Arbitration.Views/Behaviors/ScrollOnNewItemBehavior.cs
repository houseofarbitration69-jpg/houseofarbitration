using Microsoft.Maui.Controls;
using System.Collections.Specialized;
using System.ComponentModel;

namespace House.Of.Arbitration.Views.Behaviors;

/// <summary>
/// A Behavior that scrolls a <see cref="CollectionView"/> to the newest item (inserted at index 0)
/// whenever the bound <see cref="ObservableCollection{T}"/> changes.
/// This class can be used directly in XAML:
///   <CollectionView>
///       <CollectionView.Behaviors>
///           <behaviors:ScrollOnNewItemBehavior/>
///       </CollectionView.Behaviors>
///   </CollectionView>
/// No code‑behind required.
/// </summary>
public class ScrollOnNewItemBehavior : Behavior<CollectionView>
{
    // Keep a reference to the currently observed collection so we can detach later.
    private INotifyCollectionChanged? _currentCollection;

    protected override void OnAttachedTo(CollectionView bindable)
    {
        base.OnAttachedTo(bindable);
        // Subscribe to the CollectionView's ItemsSource changes.
        bindable.PropertyChanged += CollectionView_PropertyChanged;
        AttachToCollection(bindable.ItemsSource as INotifyCollectionChanged, bindable);
    }

    protected override void OnDetachingFrom(CollectionView bindable)
    {
        base.OnDetachingFrom(bindable);
        // Clean up.
        bindable.PropertyChanged -= CollectionView_PropertyChanged;
        DetachFromCollection();
    }

    private void CollectionView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView) return;
        if (e.PropertyName != nameof(CollectionView.ItemsSource)) return;

        // Detach old collection and attach the new one.
        DetachFromCollection();
        AttachToCollection(collectionView.ItemsSource as INotifyCollectionChanged, collectionView);
    }

    private void AttachToCollection(INotifyCollectionChanged? collection, CollectionView collectionView)
    {
        if (collection == null) return;
        _currentCollection = collection;
        collection.CollectionChanged += (s, e) => OnCollectionChanged(collectionView, e);
    }

    private void DetachFromCollection()
    {
        if (_currentCollection != null)
        {
            // The lambda we added cannot be directly removed, so we simply stop tracking.
            // When the behavior is detached the CollectionView will be gone, so this is safe.
            _currentCollection = null;
        }
    }

    private void OnCollectionChanged(CollectionView collectionView, NotifyCollectionChangedEventArgs e)
    {
        // When a new item is inserted at index 0, scroll to it.
        if (e.NewItems != null && e.NewStartingIndex == 0)
        {
            var newItem = e.NewItems[0];

            // Ensure we run on the UI thread *after* the layout has updated
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Give the layout a moment to create the new visual container
                await Task.Yield();   // same as `await Task. Delay(0)` – no real delay
                collectionView.ScrollTo(newItem, position: ScrollToPosition.Start, animate: false);
            });
        }
    }
}
