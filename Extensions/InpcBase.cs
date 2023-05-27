using System;
using System.ComponentModel;

namespace Extensions;

public abstract class InpcBase : INotifyPropertyChanged, INotifyPropertyChanging
{
    [field: NonSerialized] public event PropertyChangedEventHandler PropertyChanged;

    [field: NonSerialized] public event PropertyChangingEventHandler PropertyChanging;

    protected virtual void OnPropertyChanged(string propertyName)
    { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    protected virtual void OnPropertyChanged<T>(string propertyName, T oldvalue, T newvalue)
    { PropertyChanged?.Invoke(this, new PropertyChangedExtendedEventArgs<T>(propertyName, oldvalue, newvalue)); }

    protected virtual void OnPropertyChanging(string propertyName)
    { PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName)); }
}

public class PropertyChangedExtendedEventArgs<T> : PropertyChangedEventArgs
{
    public PropertyChangedExtendedEventArgs(string propertyName, T oldValue, T newValue) : base(propertyName)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public virtual T NewValue { get; private set; }

    public virtual T OldValue { get; private set; }
}