using System;
using System.ComponentModel;

namespace Extensions;

public abstract class InpcBase : INotifyPropertyChanged, INotifyPropertyChanging
{
    [field: NonSerialized] public event PropertyChangedEventHandler PropertyChanged;

    [field: NonSerialized] public event PropertyChangingEventHandler PropertyChanging;
    [field: NonSerialized]
    public static event EventHandler<PropertyChangedEventArgs> StaticEventPropertyChanged = delegate
    {
    };

    protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    protected virtual void OnPropertyChanged<T>(string propertyName, T oldvalue, T newvalue)
    { PropertyChanged?.Invoke(this, new PropertyChangedExtendedEventArgs<T>(propertyName, oldvalue, newvalue)); }

    protected virtual void OnPropertyChanging(string propertyName) { PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName)); }

    protected static void StaticPropertyChanged(string propertyName) { StaticEventPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(propertyName))); }
}

public class PropertyChangedExtendedEventArgs<T>(string propertyName, T oldValue, T newValue) : PropertyChangedEventArgs(propertyName)
{
    public virtual T NewValue { get; private set; } = newValue;

    public virtual T OldValue { get; private set; } = oldValue;
}