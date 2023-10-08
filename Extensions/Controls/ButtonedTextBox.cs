using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace Extensions;

[DefaultProperty("Description")]
[ContentProperty("Description")]
public class ButtonedTextBox : TextBox, INotifyPropertyChanged
{
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(object), typeof(ButtonedTextBox), new PropertyMetadata(null));
    private Visibility copyButtonVisibility = Visibility.Visible;
    private Visibility fontSizeButtonVisibility = Visibility.Collapsed;
    private Visibility openButtonVisibility = Visibility.Visible;
    private Visibility pasteButtonVisibility = Visibility.Visible;
    private Visibility remainingLengthVisibility = Visibility.Collapsed;
    private int remainingTextLength;
    private Visibility resetButtonVisibility = Visibility.Visible;
    private Visibility titleCaseMenuVisibility = Visibility.Collapsed;

    static ButtonedTextBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonedTextBox), new FrameworkPropertyMetadata(typeof(ButtonedTextBox))); }

    public ButtonedTextBox()
    {
        _ = CommandBindings.Add(new CommandBinding(Reset, ResetCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Copy, CopyCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Open, OpenCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(UpperCase, UpperCaseCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(TitleCase, TitleCaseCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(LowerCase, LowerCaseCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Paste, PasteCommand, PasteCanExecute));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public new ICommand Copy { get; } = new RoutedCommand();

    public Visibility CopyButtonVisibility
    {
        get => copyButtonVisibility;

        set
        {
            if (copyButtonVisibility != value)
            {
                copyButtonVisibility = value;
                OnPropertyChanged(nameof(CopyButtonVisibility));
            }
        }
    }

    public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public Visibility FontSizeButtonVisibility
    {
        get => fontSizeButtonVisibility;
        set
        {
            if (fontSizeButtonVisibility != value)
            {
                fontSizeButtonVisibility = value;
                OnPropertyChanged(nameof(FontSizeButtonVisibility));
            }
        }
    }

    public ICommand LowerCase { get; } = new RoutedCommand();

    public ICommand Open { get; } = new RoutedCommand();

    public Visibility OpenButtonVisibility
    {
        get => openButtonVisibility;

        set
        {
            if (openButtonVisibility != value)
            {
                openButtonVisibility = value;
                OnPropertyChanged(nameof(OpenButtonVisibility));
            }
        }
    }

    public new ICommand Paste { get; } = new RoutedCommand();

    public Visibility PasteButtonVisibility
    {
        get => pasteButtonVisibility;

        set
        {
            if (pasteButtonVisibility != value)
            {
                pasteButtonVisibility = value;
                OnPropertyChanged(nameof(PasteButtonVisibility));
            }
        }
    }

    public Visibility RemainingLengthVisibility
    {
        get => remainingLengthVisibility;
        set
        {
            if (remainingLengthVisibility != value)
            {
                remainingLengthVisibility = value;
                OnPropertyChanged(nameof(RemainingLengthVisibility));
            }
        }
    }

    public int RemainingTextLength
    {
        get => remainingTextLength;

        set
        {
            if (remainingTextLength != value)
            {
                remainingTextLength = value;
                OnPropertyChanged(nameof(RemainingTextLength));
            }
        }
    }

    public ICommand Reset { get; } = new RoutedCommand();

    public Visibility ResetButtonVisibility
    {
        get => resetButtonVisibility;

        set
        {
            if (resetButtonVisibility != value)
            {
                resetButtonVisibility = value;
                OnPropertyChanged(nameof(ResetButtonVisibility));
            }
        }
    }

    public ICommand TitleCase { get; } = new RoutedCommand();

    public Visibility TitleCaseMenuVisibility
    {
        get => titleCaseMenuVisibility;
        set
        {
            if (titleCaseMenuVisibility != value)
            {
                titleCaseMenuVisibility = value;
                OnPropertyChanged(nameof(TitleCaseMenuVisibility));
            }
        }
    }

    public ICommand UpperCase { get; } = new RoutedCommand();

    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        if (RemainingLengthVisibility == Visibility.Visible && MaxLength > 0)
        {
            RemainingTextLength = MaxLength - Text.Length;
        }
        base.OnTextChanged(e);
    }

    private void CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            e.CanExecute = true;
        }
    }

    private void CopyCommand(object sender, ExecutedRoutedEventArgs e) => Clipboard.SetText(Text);

    private void LowerCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.ToLower();

    private void OpenCommand(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            _ = Process.Start(Text);
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title);
        }
    }

    private void PasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Clipboard.ContainsText() && !IsReadOnly)
        {
            e.CanExecute = true;
        }
    }

    private void PasteCommand(object sender, ExecutedRoutedEventArgs e) => Text = Clipboard.GetText();

    private void ResetCommand(object sender, ExecutedRoutedEventArgs e) => Text = string.Empty;

    private void TitleCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(Text.ToLower());

    private void UpperCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.ToUpper();
}