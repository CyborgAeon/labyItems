using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace labyItems.Controls
{
    public partial class DictionarySlider : ContentView
    {
        // -------- Step snapping --------

        public static readonly BindableProperty StepSizeProperty = BindableProperty.Create(
            nameof(StepSize),
            typeof(double),
            typeof(DictionarySlider),
            defaultValue: 1.0
        );

        public double StepSize
        {
            get => (double)GetValue(StepSizeProperty);
            set => SetValue(StepSizeProperty, value);
        }

        public static readonly BindableProperty SnapToStepProperty = BindableProperty.Create(
            nameof(SnapToStep),
            typeof(bool),
            typeof(DictionarySlider),
            defaultValue: true
        );

        public bool SnapToStep
        {
            get => (bool)GetValue(SnapToStepProperty);
            set => SetValue(SnapToStepProperty, value);
        }

        // -------- Formatting argument --------

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

        // -------- Items source (dictionary mode) --------

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

        // -------- Generated mode parameters --------

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

        // -------- Selection (index / count) --------

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

        // -------- Read-only selection info --------

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

        public static readonly BindableProperty KeyTextProperty = BindableProperty.Create(
            nameof(KeyText),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: string.Empty
        );

        public string KeyText
        {
            get => (string)GetValue(KeyTextProperty);
            private set => SetValue(KeyTextProperty, value);
        }

        public static readonly BindableProperty ValueTextProperty = BindableProperty.Create(
            nameof(ValueText),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: string.Empty
        );

        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            private set => SetValue(ValueTextProperty, value);
        }

        // -------- Label formats --------

        public static readonly BindableProperty KeyFormatProperty = BindableProperty.Create(
            nameof(KeyFormat),
            typeof(string),
            typeof(DictionarySlider),
            defaultValue: string.Empty,
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

        // -------- Events --------

        public event EventHandler<DictionarySelectionChangedEventArgs>? SelectionChanged;

        // -------- Internals --------

        private List<KeyValuePair<string, int>> _entries = new();
        private bool _usingGenerated => ItemsSource is null;

        public DictionarySlider()
        {
            InitializeComponent();
            RebuildEntries();
            UpdateFromGeneratedOrIndex(raiseEvent: false);
        }

        // -------- Helpers --------

        private static string SafeFormat(string fmt, params object?[] args)
        {
            if (fmt is null)
                return string.Empty;

            try
            {
                return string.Format(fmt, args);
            }
            catch
            {
                return args is { Length: > 0 } ? $"{args[0]}" : string.Empty;
            }
        }

        // -------- Property change plumbing --------

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

        // -------- Entry rebuilding --------

        private void RebuildEntries()
        {
            if (!_usingGenerated)
            {
                _entries = ItemsSource?.ToList() ?? new();
                Slider.Minimum = 0;
                Slider.Maximum = Math.Max(0, _entries.Count - 1);
                return;
            }

            // Generated mode: build keys = MinCount..MaxCount, values = count * Multiplier
            _entries = new List<KeyValuePair<string, int>>();
            var min = Math.Min(MinCount, MaxCount);
            var max = Math.Max(MinCount, MaxCount);

            for (int count = min; count <= max; count++)
            {
                _entries.Add(new KeyValuePair<string, int>(count.ToString(), count * Multiplier));
            }

            Slider.Minimum = min;
            Slider.Maximum = max;
        }

        // -------- Slider handler with snapping --------

        private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            double value = e.NewValue;

            if (SnapToStep && StepSize > 0)
            {
                double snapped = Math.Round(value / StepSize) * StepSize;

                if (Math.Abs(Slider.Value - snapped) > double.Epsilon)
                {
                    // Setting Value will re-fire ValueChanged, but the next call
                    // will find Slider.Value == snapped and won't set it again.
                    Slider.Value = snapped;
                }

                value = snapped;
            }

            int rounded = (int)Math.Round(value);

            if (_usingGenerated)
            {
                if (SelectedCount != rounded)
                    SelectedCount = rounded;
            }
            else
            {
                if (SelectedIndex != rounded)
                    SelectedIndex = rounded;
            }

            UpdateFromGeneratedOrIndex(raiseEvent: true);
        }

        private void OnSliderDragStarted(object sender, EventArgs e)
        {
        }

        private void OnSliderDragCompleted(object sender, EventArgs e)
        {
        }

        // -------- Core update logic --------

        private void UpdateFromGeneratedOrIndex(bool raiseEvent, int? previousIndex = null)
        {
            if (_entries.Count == 0)
            {
                RenderEmpty();
                return;
            }

            if (_usingGenerated)
            {
                int min = (int)Slider.Minimum;
                int max = (int)Slider.Maximum;
                int count = Math.Clamp(SelectedCount, min, max);

                if ((int)Math.Round(Slider.Value) != count)
                    Slider.Value = count;

                var kv = _entries[count - min];
                SelectedKey = kv.Key;
                SelectedValue = kv.Value;

                // Prefer Labels over KeyFormat if available
                string keyText;
                int labelIndex = count - min;

                if (Labels != null && labelIndex >= 0 && labelIndex < Labels.Count)
                {
                    keyText = Labels[labelIndex];
                }
                else
                {
                    keyText = SafeFormat(KeyFormat, kv.Key, FormatArg1);
                }

                KeyText = keyText;
                ValueText = SafeFormat(ValueFormat, kv.Value);

                if (raiseEvent)
                {
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
            }
            else
            {
                int idx = Math.Clamp(SelectedIndex, 0, _entries.Count - 1);

                if ((int)Math.Round(Slider.Value) != idx)
                    Slider.Value = idx;

                var kv = _entries[idx];
                SelectedKey = kv.Key;
                SelectedValue = kv.Value;

                string keyText;

                if (Labels != null && idx >= 0 && idx < Labels.Count)
                {
                    keyText = Labels[idx];
                }
                else
                {
                    keyText = string.Format(KeyFormat, kv.Key);
                }

                KeyText = keyText;
                ValueText = SafeFormat(ValueFormat, kv.Value);

                if (raiseEvent)
                {
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
        }

        private void RenderEmpty()
        {
            SelectedKey = string.Empty;
            SelectedValue = 0;
            KeyText = SafeFormat(KeyFormat, string.Empty, FormatArg1);
            ValueText = SafeFormat(ValueFormat, 0);
        }

        public static readonly BindableProperty CompactProperty = BindableProperty.Create(
            nameof(Compact),
            typeof(bool),
            typeof(DictionarySlider),
            defaultValue: false,
            propertyChanged: OnCompactChanged
        );

        public bool Compact
        {
            get => (bool)GetValue(CompactProperty);
            set => SetValue(CompactProperty, value);
        }

        public static readonly BindableProperty ShowLabelsProperty = BindableProperty.Create(
            nameof(ShowLabels),
            typeof(bool),
            typeof(DictionarySlider),
            defaultValue: true
        );

        public bool ShowLabels
        {
            get => (bool)GetValue(ShowLabelsProperty);
            set => SetValue(ShowLabelsProperty, value);
        }

        private static void OnCompactChanged(
            BindableObject bindable,
            object oldValue,
            object newValue
        )
        {
            var control = (DictionarySlider)bindable;
            bool compact = (bool)newValue;

            if (compact)
            {
                control.RootLayout.Padding = new Thickness(0, 0, 0, 0);
                control.RootLayout.Spacing = 0;
            }
            else
            {
                control.RootLayout.Padding = new Thickness(12);
                control.RootLayout.Spacing = 8;
            }
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
