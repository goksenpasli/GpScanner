using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions;

public class MaskedTextBox : TextBox
{
    public static readonly DependencyProperty IncludeLiteralsProperty = DependencyProperty.Register("IncludeLiterals", typeof(bool), typeof(MaskedTextBox), new UIPropertyMetadata(true, OnIncludeLiteralsPropertyChanged));
    public static readonly DependencyProperty IncludePromptProperty = DependencyProperty.Register("IncludePrompt", typeof(bool), typeof(MaskedTextBox), new UIPropertyMetadata(false, OnIncludePromptPropertyChanged));
    public static readonly DependencyProperty MaskProperty = DependencyProperty.Register("Mask", typeof(string), typeof(MaskedTextBox), new UIPropertyMetadata("<>", OnMaskPropertyChanged));
    public static readonly DependencyProperty PromptCharProperty = DependencyProperty.Register("PromptChar", typeof(char), typeof(MaskedTextBox), new UIPropertyMetadata('_', OnPromptCharChanged));
    public static readonly DependencyProperty SelectAllOnGotFocusProperty = DependencyProperty.Register("SelectAllOnGotFocus", typeof(bool), typeof(MaskedTextBox), new PropertyMetadata(false));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent("ValueChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<object>), typeof(MaskedTextBox));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(MaskedTextBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty ValueTypeProperty = DependencyProperty.Register("ValueType", typeof(Type), typeof(MaskedTextBox), new UIPropertyMetadata(typeof(string), OnValueTypeChanged));
    private bool _convertException;
    private bool _isInitialized;
    private bool _isSyncing;

    static MaskedTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MaskedTextBox), new FrameworkPropertyMetadata(typeof(MaskedTextBox)));
        TextProperty.OverrideMetadata(typeof(MaskedTextBox), new FrameworkPropertyMetadata(OnTextChanged));
    }

    public MaskedTextBox()
    {
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, Paste));
        _ = CommandBindings.Add(
            new CommandBinding(
                ApplicationCommands.Cut,
                null,
                (s, e) =>
                {
                    e.CanExecute = false;
                    e.Handled = true;
                }));
        Loaded += MaskedTextBox_Loaded;
    }

    public event RoutedPropertyChangedEventHandler<object> ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }

    public bool IncludeLiterals { get => (bool)GetValue(IncludeLiteralsProperty); set => SetValue(IncludeLiteralsProperty, value); }

    public bool IncludePrompt { get => (bool)GetValue(IncludePromptProperty); set => SetValue(IncludePromptProperty, value); }

    public string Mask { get => (string)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

    public char PromptChar { get => (char)GetValue(PromptCharProperty); set => SetValue(PromptCharProperty, value); }

    public bool SelectAllOnGotFocus { get => (bool)GetValue(SelectAllOnGotFocusProperty); set => SetValue(SelectAllOnGotFocusProperty, value); }

    public object Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public Type ValueType { get => (Type)GetValue(ValueTypeProperty); set => SetValue(ValueTypeProperty, value); }

    protected MaskedTextProvider MaskProvider { get; set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        InitializeMask();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        if (SelectAllOnGotFocus)
        {
            SelectAll();
        }

        base.OnGotKeyboardFocus(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!e.Handled)
        {
            HandleKeyInput(e);
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (!IsReadOnly)
        {
            InsertText(e.Text);
        }

        e.Handled = true;
        base.OnPreviewTextInput(e);
    }

    private static void OnIncludeLiteralsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).InitializeMask();

    private static void OnIncludePromptPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).InitializeMask();

    private static void OnMaskPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).InitializeMask();

    private static void OnPromptCharChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).InitializeMask();

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).SyncTextAndValue(TextProperty, e.NewValue);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).RaiseValueChanged(e);

    private static void OnValueTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MaskedTextBox)d).SyncTextAndValue(ValueProperty, ((MaskedTextBox)d).Value);

    private object ConvertTextToValue()
    {
        try
        {
            string raw = MaskProvider?.ToString()?.Trim();
            Type target = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
            return string.IsNullOrWhiteSpace(raw) ? null : Convert.ChangeType(raw, target);
        }
        catch
        {
            _convertException = true;
            return Value;
        }
    }

    private string ConvertValueToText(object value)
    {
        if (_convertException)
        {
            _convertException = false;
            value = Value;
        }
        if (MaskProvider == null)
        {
            return value?.ToString() ?? string.Empty;
        }

        _ = MaskProvider.Set(value?.ToString() ?? string.Empty);
        return MaskProvider.ToDisplayString();
    }

    private void HandleKeyInput(KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        int position = SelectionStart;
        bool textChanged = false;

        switch (e.Key)
        {
            case Key.Back:
                if (!RemoveSelectedText() && position > 0)
                {
                    position--;
                    textChanged = MaskProvider.RemoveAt(position, position);
                }
                else
                {
                    textChanged = true;
                }
                break;
            case Key.Delete:
                textChanged = RemoveSelectedText() || MaskProvider.RemoveAt(position, position);
                break;
        }

        if (textChanged)
        {
            UpdateText();
            SelectionStart = position;
            e.Handled = true;
        }
    }

    private void InitializeMask()
    {
        if (string.IsNullOrEmpty(Mask))
        {
            return;
        }

        MaskProvider = new MaskedTextProvider(Mask) { IncludeLiterals = IncludeLiterals, IncludePrompt = IncludePrompt, PromptChar = PromptChar };
        UpdateText();
    }

    private void InsertText(string input)
    {
        int pos = SelectionStart;
        if (RemoveSelectedText())
        {
            pos = SelectionStart;
        }

        foreach (char ch in input)
        {
            int insertPos = MaskProvider.FindEditPositionFrom(pos, true);
            if (insertPos < 0)
            {
                break;
            }

            if (MaskProvider.InsertAt(ch, insertPos))
            {
                pos = insertPos + 1;
            }
        }

        UpdateText();
        SelectionStart = pos;
    }

    private void MaskedTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            SyncTextAndValue(ValueProperty, Value);
        }
    }

    private void Paste(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        string paste = Clipboard.GetText()?.Trim();
        if (!string.IsNullOrEmpty(paste))
        {
            InsertText(paste);
        }
    }

    private void RaiseValueChanged(DependencyPropertyChangedEventArgs e)
    {
        SyncTextAndValue(ValueProperty, e.NewValue);
        RaiseEvent(new RoutedPropertyChangedEventArgs<object>(e.OldValue, e.NewValue) { RoutedEvent = ValueChangedEvent });
    }

    private bool RemoveSelectedText()
    {
        int len = SelectionLength;
        return len != 0 && MaskProvider.RemoveAt(SelectionStart, SelectionStart + len - 1);
    }

    private void SyncTextAndValue(DependencyProperty source, object newValue)
    {
        if (_isSyncing || !_isInitialized)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            if (source == TextProperty)
            {
                SetCurrentValue(ValueProperty, ConvertTextToValue());
            }
            else if (source == ValueProperty)
            {
                SetCurrentValue(TextProperty, ConvertValueToText(newValue));
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void UpdateText()
    {
        Text = MaskProvider?.ToDisplayString() ?? string.Empty;
        SelectionStart = Text.Length;
    }
}
