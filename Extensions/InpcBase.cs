using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Extensions;

public abstract class InpcBase : INotifyPropertyChanged, INotifyPropertyChanging
{
    public static event EventHandler<PropertyChangedEventArgs> StaticEventPropertyChanged = delegate
    {
    };
    [field: NonSerialized]
    public event PropertyChangedEventHandler PropertyChanged;
    [field: NonSerialized]
    public event PropertyChangingEventHandler PropertyChanging;

    public bool IsValid(object data)
    {
        if (data == null)
        {
            return false;
        }

        ValidationContext validationContext = new(data);
        List<ValidationResult> results = [];

        return Validator.TryValidateObject(data, validationContext, results, validateAllProperties: true);
    }

    protected static void StaticPropertyChanged(string propertyName) => StaticEventPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    protected virtual void OnPropertyChanged<T>(string propertyName, T oldValue, T newValue) => PropertyChanged?.Invoke(this, new PropertyChangedExtendedEventArgs<T>(propertyName, oldValue, newValue));

    protected virtual void OnPropertyChanging(string propertyName) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class PropertyChangedExtendedEventArgs<T>(string propertyName, T oldValue, T newValue) : PropertyChangedEventArgs(propertyName)
{
    public T NewValue { get; } = newValue;

    public T OldValue { get; } = oldValue;
}