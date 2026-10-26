using System.Collections;
using System.Collections.Specialized;

namespace labyItems.Controls.Specialisations;

public partial class CascadingSpecialisationView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(CascadingSpecialisationView),
        propertyChanged: OnItemsSourceChanged);
    public static readonly BindableProperty ItemTemplateSelectorProperty = BindableProperty.Create(nameof(ItemTemplateSelector), typeof(DataTemplateSelector), typeof(CascadingSpecialisationView));
    public static readonly BindableProperty AnimationsEnabledProperty = BindableProperty.Create(nameof(AnimationsEnabled), typeof(bool), typeof(CascadingSpecialisationView), true);
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public DataTemplateSelector? ItemTemplateSelector { get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty); set => SetValue(ItemTemplateSelectorProperty, value); }
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }
    private INotifyCollectionChanged? _observableItems;
    private CancellationTokenSource? _animationCts;

    public CascadingSpecialisationView()
    {
        InitializeComponent();
        Unloaded += (_, _) => CancelAnimation();
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (CascadingSpecialisationView)bindable;
        if (view._observableItems != null)
            view._observableItems.CollectionChanged -= view.OnCollectionChanged;

        view._observableItems = newValue as INotifyCollectionChanged;
        if (view._observableItems != null)
            view._observableItems.CollectionChanged += view.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!AnimationsEnabled)
            return;
        CancelAnimation();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;
        Dispatcher.Dispatch(async () =>
        {
            await Task.Yield();
            if (token.IsCancellationRequested)
                return;

            var children = SectionsHost.Children.OfType<VisualElement>().ToList();
            IEnumerable<VisualElement> entering = e.Action == NotifyCollectionChangedAction.Add
                && e.NewStartingIndex >= 0
                ? children.Skip(e.NewStartingIndex).Take(e.NewItems?.Count ?? 0)
                : children;

            foreach (var child in entering)
            {
                child.AbortAnimation("CascadeInsert");
                child.Opacity = 0;
                child.TranslationY = -12;
                var animation = new Animation(progress =>
                {
                    child.Opacity = progress;
                    child.TranslationY = -12 * (1 - progress);
                });
                animation.Commit(child, "CascadeInsert", 16, 240, Easing.CubicOut);
            }
        });
    }

    private void CancelAnimation()
    {
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
        foreach (var child in SectionsHost.Children.OfType<VisualElement>())
        {
            child.AbortAnimation("CascadeInsert");
            child.Opacity = 1;
            child.TranslationY = 0;
        }
    }
}
