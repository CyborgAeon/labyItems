using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace labyItems.Controls
{
    public partial class DictionarySlider : ContentView
    {
        public static readonly BindableProperty FormatArg1Property = BindableProperty.Create(
            nameof(FormatArg1),
            typeof(object),
            typeof(DictionarySlider),
            defaultValue: null,
            propertyChanged: OnFormatChanged
        );

        public object? FormatArg1
        {
            get => GetValue(FormatArg1Property);
            set => SetValue(FormatArg1Property, value);
        }

        // Optional: use a dictionary (keys shown; values used for cost)
        public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IDictionary<string, int>),
            typeof(DictionarySlider),
            defaultValue: null,
            propertyChanged: OnItemsSourceChanged
        );

        public IDictionary<string, int>? ItemsSource
        {
            get => (IDictionary<string, int>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // Generated-mode: Min/Max count and multiplier (used when ItemsSource == null)
        public static readonly BindableProperty MinCountProperty = BindableProperty.Create(
            nameof(MinCount),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 0,
            propertyChanged: OnGeneratedParamsChanged
        );

        public int MinCount
        {
            get => (int)GetValue(MinCountProperty);
            set => SetValue(MinCountProperty, value);
        }

        public static readonly BindableProperty MaxCountProperty = BindableProperty.Create(
            nameof(MaxCount),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 12,
            propertyChanged: OnGeneratedParamsChanged
        );

        public int MaxCount
        {
            get => (int)GetValue(MaxCountProperty);
            set => SetValue(MaxCountProperty, value);
        }

        public static readonly BindableProperty MultiplierProperty = BindableProperty.Create(
            nameof(Multiplier),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 4,
            propertyChanged: OnGeneratedParamsChanged
        );

        public int Multiplier
        {
            get => (int)GetValue(MultiplierProperty);
            set => SetValue(MultiplierProperty, value);
        }

        // Selection by index (dictionary mode) or by count (generated mode)
        public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 0,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnSelectedIndexChanged
        );

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public static readonly BindableProperty SelectedCountProperty = BindableProperty.Create(
            nameof(SelectedCount),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 0,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnSelectedCountChanged
        );

        /// <summary>
        /// In generated mode, bind this to your VM (e.g., GeneralSpiritStore / SphereSpiritStore).
        /// Ignored in dictionary mode.
        /// </summary>
        public int SelectedCount
        {
            get => (int)GetValue(SelectedCountProperty);
            set => SetValue(SelectedCountProperty, value);
        }

        // helper so bad format strings won’t crash
        private static string SafeFormat(string fmt, params object?[] args)
        {
            if (fmt is null)
                return string.Empty;
            try
            {
                return string.Format(fmt ?? "{0}", args);
            }
            catch
            {
                return args is { Length: > 0 } ? $"{args[0]}" : string.Empty;
            }
        }

        // Read-only conveniences for UI
        public static readonly BindableProperty SelectedKeyProperty = BindableProperty.Create(
            nameof(SelectedKey),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: string.Empty
        );

        public string SelectedKey
        {
            get => (string)GetValue(SelectedKeyProperty);
            private set => SetValue(SelectedKeyProperty, value);
        }

        public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
            nameof(SelectedValue),
            typeof(int),
            typeof(DictionarySlider),
            defaultValue: 0
        );

        public int SelectedValue
        {
            get => (int)GetValue(SelectedValueProperty);
            private set => SetValue(SelectedValueProperty, value);
        }

        // Label formats
        public static readonly BindableProperty KeyFormatProperty = BindableProperty.Create(
            nameof(KeyFormat),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: "Key: {0}",
            propertyChanged: OnFormatChanged
        );

        public string KeyFormat
        {
            get => (string)GetValue(KeyFormatProperty);
            set => SetValue(KeyFormatProperty, value);
        }
        public static readonly BindableProperty LabelsProperty = BindableProperty.Create(
            nameof(Labels),
            typeof(IList<string>),
            typeof(DictionarySlider),
            default(IList<string>)
        );

        public IList<string> Labels
        {
            get => (IList<string>)GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }
        public static readonly BindableProperty ValueFormatProperty = BindableProperty.Create(
            nameof(ValueFormat),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: string.Empty,
            propertyChanged: OnFormatChanged
        );

        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        // ---------------- Events ----------------
        public event EventHandler<DictionarySelectionChangedEventArgs>? SelectionChanged;

        // ---------------- Internals ----------------
        private List<KeyValuePair<string, int>> _entries = new();
        private bool _usingGenerated => ItemsSource is null;

        public DictionarySlider()
        {
            InitializeComponent();
            RebuildEntries();
            UpdateFromGeneratedOrIndex(raiseEvent: false);
        }

        // ------- rebuild & property change plumbing -------

        private static void OnItemsSourceChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            control.RebuildEntries();
            control.UpdateFromGeneratedOrIndex(raiseEvent: false);
        }

        private static void OnGeneratedParamsChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            if (control._usingGenerated)
            {
                control.RebuildEntries();
                control.UpdateFromGeneratedOrIndex(raiseEvent: false);
            }
        }

        private static void OnSelectedIndexChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            if (control._usingGenerated)
                return; // index not used in generated mode
            control.UpdateFromGeneratedOrIndex(raiseEvent: true, previousIndex: (int)oldValue);
        }

        private static void OnSelectedCountChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            if (!control._usingGenerated)
                return; // count not used in dictionary mode
            control.UpdateFromGeneratedOrIndex(raiseEvent: true);
        }

        private static void OnFormatChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            control.UpdateFromGeneratedOrIndex(raiseEvent: false);
        }

        private void RebuildEntries()
        {
            if (!_usingGenerated)
            {
                _entries = ItemsSource?.ToList() ?? new();
                Slider.Minimum = 0;
                Slider.Maximum = Math.Max(0, _entries.Count - 1);
                return;
            }

            // Generated mode: build keys = MinCount..MaxCount (as strings), values = count * Multiplier
            _entries = new List<KeyValuePair<string, int>>();
            var min = Math.Min(MinCount, MaxCount);
            var max = Math.Max(MinCount, MaxCount);
            for (int count = min; count <= max; count++)
            {
                _entries.Add(new KeyValuePair<string, int>(count.ToString(), count * Multiplier));
            }

            // In generated mode we drive via SelectedCount
            Slider.Minimum = min;
            Slider.Maximum = max;
        }

        // ------- Slider handler -------

        private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            int rounded = (int)Math.Round(e.NewValue);

            if (_usingGenerated)
            {
                if (SelectedCount != rounded)
                    SelectedCount = rounded; // triggers UI update
            }
            else
            {
                if (SelectedIndex != rounded)
                    SelectedIndex = rounded; // triggers UI update
            }
        }

        // ------- Core updater -------

        private void UpdateFromGeneratedOrIndex(bool raiseEvent, int? previousIndex = null)
{
    if (_entries.Count == 0)
    {
        RenderEmpty();
        return;
    }

    if (_usingGenerated)
    {
        // clamp to slider range
        int min = (int)Slider.Minimum;
        int max = (int)Slider.Maximum;
        int count = Math.Clamp(SelectedCount, min, max);

        // keep slider in sync
        if ((int)Math.Round(Slider.Value) != count)
            Slider.Value = count;

        var kv = _entries[count - min]; // entries are sequential
        SelectedKey = kv.Key;
        SelectedValue = kv.Value;

        // --- NEW: prefer Labels over KeyFormat if available ---
        string keyText;
        int labelIndex = count - min; // logical index from slider position

        if (Labels != null &&
            labelIndex >= 0 &&
            labelIndex < Labels.Count)
        {
            keyText = Labels[labelIndex];
        }
        else
        {
            keyText = SafeFormat(KeyFormat, kv.Key, FormatArg1);
        }

        KeyLabel.Text = keyText;
        // ------------------------------------------------------

        ValueLabel.Text = SafeFormat(ValueFormat, kv.Value);

        if (raiseEvent)
            SelectionChanged?.Invoke(
                this,
                new DictionarySelectionChangedEventArgs(
                    previousIndex ?? count,
                    count,
                    kv.Key,
                    kv.Value
                )
            );
    }
    else
    {
        int idx = Math.Clamp(SelectedIndex, 0, _entries.Count - 1);

        if ((int)Math.Round(Slider.Value) != idx)
            Slider.Value = idx;

        var kv = _entries[idx];
        SelectedKey = kv.Key;
        SelectedValue = kv.Value;

        // --- NEW: prefer Labels over KeyFormat if available ---
        string keyText;

        if (Labels != null &&
            idx >= 0 &&
            idx < Labels.Count)
        {
            keyText = Labels[idx];
        }
        else
        {
            keyText = string.Format(KeyFormat, kv.Key);
        }

        KeyLabel.Text = keyText;
        // ------------------------------------------------------

        ValueLabel.Text = string.Format(ValueFormat, kv.Value);

        if (raiseEvent)
            SelectionChanged?.Invoke(
                this,
                new DictionarySelectionChangedEventArgs(
                    previousIndex ?? idx,
                    idx,
                    kv.Key,
                    kv.Value
                )
            );
    }
}


        private void RenderEmpty()
        {
            SelectedKey = string.Empty;
            SelectedValue = 0;
            KeyLabel.Text = SafeFormat(KeyFormat, string.Empty, FormatArg1);
            ValueLabel.Text = SafeFormat(ValueFormat, 0);
        }
    }

    public sealed class DictionarySelectionChangedEventArgs : EventArgs
    {
        public int OldIndex { get; }
        public int NewIndex { get; }
        public string Key { get; }
        public int Value { get; }

        public DictionarySelectionChangedEventArgs(
            int oldIndex,
            int newIndex,
            string key,
            int value
        )
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            Key = key;
            Value = value;
        }
    }
}
