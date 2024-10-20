using Extensions.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

namespace Extensions;

[DefaultProperty("Description")]
[ContentProperty("Description")]
public class ButtonedTextBox : TextBox, INotifyPropertyChanged
{
    public static readonly DependencyProperty CancelCommandParameterProperty = DependencyProperty.Register("CancelCommandParameter", typeof(object), typeof(ButtonedTextBox), new PropertyMetadata(null));
    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register("CancelCommand", typeof(ICommand), typeof(ButtonedTextBox));
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(ButtonedTextBox), new PropertyMetadata(null));
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(ButtonedTextBox));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(object), typeof(ButtonedTextBox), new PropertyMetadata(null));
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(string), typeof(ButtonedTextBox), new PropertyMetadata(string.Empty));

    static ButtonedTextBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonedTextBox), new FrameworkPropertyMetadata(typeof(ButtonedTextBox))); }

    public ButtonedTextBox()
    {
        _ = CommandBindings.Add(new CommandBinding(Reset, ResetCommand, ResetCanExecute));
        _ = CommandBindings.Add(new CommandBinding(Copy, CopyCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Print, PrintCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Open, OpenCommand, OpenCanExecute));
        _ = CommandBindings.Add(new CommandBinding(UpperCase, UpperCaseCommand, CanCaseExecute));
        _ = CommandBindings.Add(new CommandBinding(TitleCase, TitleCaseCommand, CanCaseExecute));
        _ = CommandBindings.Add(new CommandBinding(LowerCase, LowerCaseCommand, CanCaseExecute));
        _ = CommandBindings.Add(new CommandBinding(UpperLowerCase, UpperLowerCaseCaseCommand, CanCaseExecute));
        _ = CommandBindings.Add(new CommandBinding(Paste, PasteCommand, PasteCanExecute));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ICommand CancelCommand { get => (ICommand)GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }

    public object CancelCommandParameter { get => GetValue(CancelCommandParameterProperty); set => SetValue(CancelCommandParameterProperty, value); }

    public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public new ICommand Copy { get; } = new RoutedCommand();

    public Visibility CopyButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CopyButtonVisibility));
            }
        }
    } = Visibility.Visible;

    public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public Visibility FontSizeButtonVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FontSizeButtonVisibility));
            }
        }
    } = Visibility.Collapsed;

    public Visibility KeyBoardButtonVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(KeyBoardButtonVisibility));
            }
        }
    } = Visibility.Collapsed;

    public ICommand LowerCase { get; } = new RoutedCommand();

    public ICommand Open { get; } = new RoutedCommand();

    public Visibility OpenButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OpenButtonVisibility));
            }
        }
    } = Visibility.Visible;

    public new ICommand Paste { get; } = new RoutedCommand();

    public Visibility PasteButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PasteButtonVisibility));
            }
        }
    } = Visibility.Visible;

    public ICommand Print { get; } = new RoutedCommand();

    public Visibility PrintButtonVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PrintButtonVisibility));
            }
        }
    } = Visibility.Collapsed;

    public Visibility RemainingLengthVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RemainingLengthVisibility));
            }
        }
    } = Visibility.Collapsed;

    public int RemainingTextLength
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RemainingTextLength));
            }
        }
    }

    public ICommand Reset { get; } = new RoutedCommand();

    public Visibility ResetButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ResetButtonVisibility));
            }
        }
    } = Visibility.Visible;

    public Visibility TextBoxVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TextBoxVisibility));
            }
        }
    } = Visibility.Visible;

    public ICommand TitleCase { get; } = new RoutedCommand();

    public Visibility TitleCaseMenuVisibility
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TitleCaseMenuVisibility));
            }
        }
    } = Visibility.Collapsed;

    public ICommand UpperCase { get; } = new RoutedCommand();

    public ICommand UpperLowerCase { get; } = new RoutedCommand();

    public string Watermark { get => (string)GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        if (RemainingLengthVisibility == Visibility.Visible && MaxLength > 0)
        {
            RemainingTextLength = MaxLength - Text.Length;
        }
        base.OnTextChanged(e);
    }

    private void CanCaseExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Text) && SelectedText.Length > 0 && !IsReadOnly)
        {
            e.CanExecute = true;
        }
    }

    private void CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            e.CanExecute = true;
        }
    }

    private void CopyCommand(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Text);
        }
        catch (COMException ex)
        {
            const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
            if ((uint)ex.ErrorCode != CLIPBRD_E_CANT_OPEN)
            {
                throw;
            }
        }
    }

    private void LowerCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, SelectedText.ToLower());

    private void OpenCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            e.CanExecute = true;
            return;
        }
        if (!string.IsNullOrWhiteSpace(Text))
        {
            e.CanExecute = true;
        }
    }

    private void OpenCommand(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
                return;
            }
            _ = Process.Start(Text);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
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

    private void PrintCommand(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            Window printwindow = new()
            {
                Owner = Window.GetWindow(this),
                WindowState = WindowState.Maximized,
                ShowInTaskbar = false,
                Title = Window.GetWindow(this)?.Title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                UseLayoutRounding = true
            };
            XpsViewer xpsviewer = new();
            printwindow.Content = xpsviewer;
            FlowDocument fd = new(new Paragraph(new Run(Text))) { IsOptimalParagraphEnabled = true, ColumnWidth = double.MaxValue };
            xpsviewer.Document = xpsviewer.WriteXPS(fd);
            printwindow.Show();
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message);
        }
    }

    private void ResetCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (CancelCommand?.CanExecute(CancelCommandParameter) == true)
        {
            e.CanExecute = true;
            return;
        }
        if (!string.IsNullOrWhiteSpace(Text) && !IsReadOnly)
        {
            e.CanExecute = true;
        }
    }

    private void ResetCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (CancelCommand?.CanExecute(CancelCommandParameter) == true)
        {
            CancelCommand.Execute(CancelCommandParameter);
            return;
        }
        Text = string.Empty;
    }

    private void TitleCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(SelectedText.ToLower()));

    private string ToggleTextCase(string text) => new(text.Select(z => char.IsLower(z) ? char.ToUpper(z) : char.IsUpper(z) ? char.ToLower(z) : z).ToArray());

    private void UpperCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, SelectedText.ToUpper());

    private void UpperLowerCaseCaseCommand(object sender, ExecutedRoutedEventArgs e) => Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, ToggleTextCase(SelectedText));
}