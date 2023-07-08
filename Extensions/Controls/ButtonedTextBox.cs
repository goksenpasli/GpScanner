using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions;

public class ButtonedTextBox : TextBox, INotifyPropertyChanged
{
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(ButtonedTextBox), new PropertyMetadata(string.Empty));
    private Visibility copyButtonVisibility = Visibility.Visible;
    private Visibility openButtonVisibility = Visibility.Visible;
    private Visibility pasteButtonVisibility = Visibility.Visible;
    private Visibility resetButtonVisibility = Visibility.Visible;

    static ButtonedTextBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonedTextBox), new FrameworkPropertyMetadata(typeof(ButtonedTextBox))); }

    public ButtonedTextBox()
    {
        _ = CommandBindings.Add(new CommandBinding(Reset, ResetCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Copy, CopyCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Open, OpenCommand, CanExecute));
        _ = CommandBindings.Add(new CommandBinding(Paste, PasteCommand, PasteCanExecute));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public new ICommand Copy { get; } = new RoutedCommand();

    public Visibility CopyButtonVisibility
    {
        get => copyButtonVisibility;

        set
        {
            if(copyButtonVisibility != value)
            {
                copyButtonVisibility = value;
                OnPropertyChanged(nameof(CopyButtonVisibility));
            }
        }
    }

    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public ICommand Open { get; } = new RoutedCommand();

    public Visibility OpenButtonVisibility
    {
        get => openButtonVisibility;

        set
        {
            if(openButtonVisibility != value)
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
            if(pasteButtonVisibility != value)
            {
                pasteButtonVisibility = value;
                OnPropertyChanged(nameof(PasteButtonVisibility));
            }
        }
    }

    public ICommand Reset { get; } = new RoutedCommand();

    public Visibility ResetButtonVisibility
    {
        get => resetButtonVisibility;

        set
        {
            if(resetButtonVisibility != value)
            {
                resetButtonVisibility = value;
                OnPropertyChanged(nameof(ResetButtonVisibility));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    private void CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if(!string.IsNullOrWhiteSpace(Text))
        {
            e.CanExecute = true;
        }
    }

    private void CopyCommand(object sender, ExecutedRoutedEventArgs e) { Clipboard.SetText(Text); }

    private void OpenCommand(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            _ = Process.Start(Text);
        } catch(Exception ex)
        {
            _ = MessageBox.Show(ex.Message);
        }
    }

    private void PasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if(Clipboard.ContainsText() && !IsReadOnly)
        {
            e.CanExecute = true;
        }
    }

    private void PasteCommand(object sender, ExecutedRoutedEventArgs e) { Text = Clipboard.GetText(); }
    private void ResetCommand(object sender, ExecutedRoutedEventArgs e) { Text = string.Empty; }
}