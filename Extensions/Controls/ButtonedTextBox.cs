using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions
{
    public class ButtonedTextBox : TextBox, INotifyPropertyChanged
    {
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(ButtonedTextBox), new PropertyMetadata(string.Empty));

        static ButtonedTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonedTextBox), new FrameworkPropertyMetadata(typeof(ButtonedTextBox)));
        }

        public ButtonedTextBox()
        {
            _ = CommandBindings.Add(new CommandBinding(Reset, ResetCommand, CanExecute)); //handle reset
            _ = CommandBindings.Add(new CommandBinding(Copy, CopyCommand, CanExecute)); //handle copy
            _ = CommandBindings.Add(new CommandBinding(Open, OpenCommand, CanExecute)); //handle copy
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

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

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

        public ICommand Reset { get; } = new RoutedCommand();

        public Visibility ResetButtonVisibility
        {
            get => resetButtonVisibility; set

            {
                if (resetButtonVisibility != value)
                {
                    resetButtonVisibility = value;
                    OnPropertyChanged(nameof(ResetButtonVisibility));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Visibility copyButtonVisibility = Visibility.Visible;

        private Visibility openButtonVisibility = Visibility.Visible;

        private Visibility resetButtonVisibility = Visibility.Visible;

        private void CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Text))
            {
                e.CanExecute = true;
            }
        }

        private void CopyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Clipboard.SetText(Text);
        }

        private void OpenCommand(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start(Text);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ResetCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Text = string.Empty;
        }
    }
}