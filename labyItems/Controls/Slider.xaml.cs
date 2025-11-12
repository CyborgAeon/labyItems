using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace labyItems.Controls;
    public partial class DictionarySlider : ContentView
    {
        // ============= Bindable Properties =============

        // Accept any IDictionary<string,int> (Dictionary works fine)
        public static readonly BindableProperty ItemsSourceProperty =
            BindableProperty.Create(
                nameof(ItemsSource),
                typeof(IDictionary<string, int>),
                typeof(DictionarySlider),
                defaultValue: null,
                propertyChanged: OnItemsSourceChanged);

        public IDictionary<string, int>? ItemsSource
        {
            get => (IDictionary<string, int>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly BindableProperty SelectedIndexProperty =
            BindableProperty.Create(
                nameof(SelectedIndex),
                typeof(int),
                typeof(DictionarySlider),
                defaultValue: 0,
                defaultBindingMode: BindingMode.TwoWay,
                propertyChanged: OnSelectedIndexChanged);

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        // Read-only conveniences (bind OneWay)
        public static readonly BindableProperty SelectedKeyProperty =
            BindableProperty.Create(
                nameof(SelectedKey),
                typeof(string),
                typeof(DictionarySlider),
                defaultValue: string.Empty);

        public string SelectedKey
        {
            get => (string)GetValue(SelectedKeyProperty);
            private set => SetValue(SelectedKeyProperty, value);
        }

        public static readonly BindableProperty SelectedValueProperty =
            BindableProperty.Create(
                nameof(SelectedValue),
                typeof(int),
                typeof(DictionarySlider),
                defaultValue: 0);

        public int SelectedValue
        {
            get => (int)GetValue(SelectedValueProperty);
            private set => SetValue(SelectedValueProperty, value);
        }

        // Label formats
        public static readonly BindableProperty KeyFormatProperty =
            BindableProperty.Create(
                nameof(KeyFormat),
                typeof(string),
                typeof(DictionarySlider),
                defaultValue: "Key: {0}",
                propertyChanged: OnFormatChanged);

        public string KeyFormat
        {
            get => (string)GetValue(KeyFormatProperty);
            set => SetValue(KeyFormatProperty, value);
        }

        public static readonly BindableProperty ValueFormatProperty =
            BindableProperty.Create(
                nameof(ValueFormat),
                typeof(string),
                typeof(DictionarySlider),
                defaultValue: "Value: {0}",
                propertyChanged: OnFormatChanged);

        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        // ============= Events =============
        public event EventHandler<DictionarySelectionChangedEventArgs>? SelectionChanged;

        // ============= Internals =============
        // Keep an ordered list for index-based access
        private List<KeyValuePair<string, int>> _entries = new();

        public DictionarySlider()
        {
            InitializeComponent();
            UpdateUIFromIndex(SelectedIndex, raiseEvent: false);
        }

        // ---------- Property changed handlers ----------

        private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (DictionarySlider)bindable;
            var src = newValue as IDictionary<string, int>;

            // Preserve insertion order if provided; otherwise this is just enumeration order
            control._entries = src?.ToList() ?? new List<KeyValuePair<string, int>>();

            // Configure slider range
            control.Slider.Minimum = 0;
            control.Slider.Maximum = Math.Max(0, control._entries.Count - 1);

            // Clamp index and update UI
            if (control._entries.Count == 0)
            {
                control.SelectedIndex = 0;
                control.Slider.Value = 0;
                control.RenderEmpty();
                return;
            }

            control.SelectedIndex = Math.Clamp(control.SelectedIndex, 0, control._entries.Count - 1);
            control.Slider.Value = control.SelectedIndex;
            control.UpdateUIFromIndex(control.SelectedIndex, raiseEvent: false);
        }

        private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (DictionarySlider)bindable;
            var oldIndex = (int)oldValue;
            var newIndex = (int)newValue;

            if (control._entries.Count == 0)
            {
                control.RenderEmpty();
                return;
            }

            newIndex = Math.Clamp(newIndex, 0, control._entries.Count - 1);

            // Keep the Slider in sync if the change came from a binding
            if ((int)Math.Round(control.Slider.Value) != newIndex)
                control.Slider.Value = newIndex;

            control.UpdateUIFromIndex(newIndex, raiseEvent: true, previousIndex: oldIndex);
        }

        private static void OnFormatChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (DictionarySlider)bindable;
            control.UpdateUIFromIndex(control.SelectedIndex, raiseEvent: false);
        }

        // ---------- Slider handler ----------

        private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            int idx = (int)Math.Round(e.NewValue);

            if (SelectedIndex != idx)
                SelectedIndex = idx; // triggers UI + event
        }

        // ---------- Core updaters ----------

        private void RenderEmpty()
        {
            SelectedKey = string.Empty;
            SelectedValue = 0;
            KeyLabel.Text = string.Format(KeyFormat, string.Empty);
            ValueLabel.Text = string.Format(ValueFormat, 0);
        }

        private void UpdateUIFromIndex(int index, bool raiseEvent, int? previousIndex = null)
        {
            if (_entries.Count == 0)
            {
                RenderEmpty();
                return;
            }

            var clamped = Math.Clamp(index, 0, _entries.Count - 1);
            var kv = _entries[clamped];

            SelectedKey = kv.Key;
            SelectedValue = kv.Value;

            KeyLabel.Text   = string.Format(KeyFormat, kv.Key);
            ValueLabel.Text = string.Format(ValueFormat, kv.Value);

            if (raiseEvent)
            {
                var args = new DictionarySelectionChangedEventArgs(
                    previousIndex ?? clamped, clamped, kv.Key, kv.Value);
                SelectionChanged?.Invoke(this, args);
            }
        }
    }

    public sealed class DictionarySelectionChangedEventArgs : EventArgs
    {
        public int OldIndex { get; }
        public int NewIndex { get; }
        public string Key { get; }
        public int Value { get; }

        public DictionarySelectionChangedEventArgs(int oldIndex, int newIndex, string key, int value)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            Key = key;
            Value = value;
        }
    }
