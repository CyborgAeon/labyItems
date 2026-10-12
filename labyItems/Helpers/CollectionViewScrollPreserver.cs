using Microsoft.Maui.Controls;

namespace labyItems.Helpers;

/// <summary>
/// Preserves the scroll position of a CollectionView across page navigations.
/// When a user taps a card to view details and returns via back navigation,
/// this helper ensures the scroll position remains unchanged.
/// </summary>
public sealed class CollectionViewScrollPreserver
{
    public static readonly BindableProperty PreserveScrollProperty =
        BindableProperty.CreateAttached(
            "PreserveScroll",
            typeof(bool),
            typeof(CollectionViewScrollPreserver),
            false,
            propertyChanged: OnPreserveScrollChanged);

    public static bool GetPreserveScroll(BindableObject view) => (bool)view.GetValue(PreserveScrollProperty);
    public static void SetPreserveScroll(BindableObject view, bool value) => view.SetValue(PreserveScrollProperty, value);

    private static readonly BindableProperty PreserverInstanceProperty =
        BindableProperty.CreateAttached(
            "PreserverInstance",
            typeof(CollectionViewScrollPreserver),
            typeof(CollectionViewScrollPreserver),
            null);

    private readonly CollectionView _collectionView;
    private Page? _parentPage;
    private int _firstVisibleIndex = -1;
    private object? _lastTappedItem;
    private bool _hasSavedPosition;
    private bool _isRestoring;

    public CollectionViewScrollPreserver(CollectionView collectionView, Page? parentPage = null)
    {
        _collectionView = collectionView ?? throw new ArgumentNullException(nameof(collectionView));
        _parentPage = parentPage;

        _collectionView.Scrolled += OnScrolled;
        _collectionView.Loaded += OnCollectionViewLoaded;
        _collectionView.Unloaded += OnCollectionViewUnloaded;

        if (_parentPage != null)
        {
            _parentPage.Appearing += OnParentPageAppearing;
        }
        else
        {
            HookParentPage();
        }
    }

    public static CollectionViewScrollPreserver GetOrCreate(CollectionView collectionView, Page? parentPage = null)
    {
        if (collectionView.GetValue(PreserverInstanceProperty) is CollectionViewScrollPreserver existing)
        {
            if (parentPage != null && existing._parentPage == null)
            {
                existing.SetParentPage(parentPage);
            }
            return existing;
        }

        var preserver = new CollectionViewScrollPreserver(collectionView, parentPage);
        collectionView.SetValue(PreserverInstanceProperty, preserver);
        return preserver;
    }

    private static void OnPreserveScrollChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CollectionView cv)
            return;

        if ((bool)newValue)
        {
            GetOrCreate(cv);
        }
    }

    public void SetParentPage(Page page)
    {
        if (_parentPage != null)
            _parentPage.Appearing -= OnParentPageAppearing;

        _parentPage = page;
        if (_parentPage != null)
            _parentPage.Appearing += OnParentPageAppearing;
    }

    private void OnCollectionViewLoaded(object? sender, EventArgs e)
    {
        if (_parentPage == null)
            HookParentPage();
    }

    private void OnCollectionViewUnloaded(object? sender, EventArgs e)
    {
        // Don't unhook parent page here, as unloading can happen during push navigation
    }

    private void HookParentPage()
    {
        Element? current = _collectionView.Parent;
        while (current != null && current is not Page)
        {
            current = current.Parent;
        }

        if (current is Page page)
        {
            SetParentPage(page);
        }
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (_isRestoring)
            return;

        if (e.FirstVisibleItemIndex >= 0)
        {
            _firstVisibleIndex = e.FirstVisibleItemIndex;
            _hasSavedPosition = true;
        }
    }

    public void RecordItem(object? item)
    {
        if (item != null)
        {
            _lastTappedItem = item;
            _hasSavedPosition = true;
        }
    }

    private void OnParentPageAppearing(object? sender, EventArgs e)
    {
        RestoreScrollPosition();
    }

    public void RestoreScrollPosition()
    {
        if (!_hasSavedPosition && _lastTappedItem == null)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _isRestoring = true;
            try
            {
                await Task.Yield();
                PerformScroll();

                // Secondary attempt after layout pass in case CollectionView is still measuring
                await Task.Delay(60);
                PerformScroll();
            }
            finally
            {
                _isRestoring = false;
            }
        });
    }

    private void PerformScroll()
    {
        try
        {
            if (_firstVisibleIndex >= 0)
            {
                _collectionView.ScrollTo(_firstVisibleIndex, position: ScrollToPosition.Start, animate: false);
            }
            else if (_lastTappedItem != null)
            {
                _collectionView.ScrollTo(_lastTappedItem, position: ScrollToPosition.MakeVisible, animate: false);
            }
        }
        catch
        {
            // Ignore if layout is in intermediate state
        }
    }

    public void Detach()
    {
        _collectionView.Scrolled -= OnScrolled;
        _collectionView.Loaded -= OnCollectionViewLoaded;
        _collectionView.Unloaded -= OnCollectionViewUnloaded;
        if (_parentPage != null)
        {
            _parentPage.Appearing -= OnParentPageAppearing;
            _parentPage = null;
        }
    }
}
