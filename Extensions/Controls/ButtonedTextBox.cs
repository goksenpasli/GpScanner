using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions
{
    public class ButtonedTextBox : TextBox
    {
        static ButtonedTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonedTextBox), new FrameworkPropertyMetadata(typeof(ButtonedTextBox)));
        }

        public ButtonedTextBox()
        {
            _ = CommandBindings.Add(new CommandBinding(Reset, ResetCommand)); //handle reset
        }

        public ICommand Reset { get; } = new RoutedCommand();

        private void ResetCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Text = string.Empty;
        }
    }
}