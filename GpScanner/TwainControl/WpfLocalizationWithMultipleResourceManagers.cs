using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xaml;
using TwainControl.Properties;

namespace TwainControl;

public class LocExtension : MarkupExtension
{
    public LocExtension(string stringName) { StringName = stringName; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        object targetObject = (serviceProvider as IProvideValueTarget)?.TargetObject;

        if(targetObject?.GetType().Name == "SharedDp")
        {
            return targetObject;
        }

        string baseName = GetResourceManager(targetObject)?.BaseName ?? string.Empty;

        if(string.IsNullOrEmpty(baseName))
        {
            object rootObject = (serviceProvider as IRootObjectProvider)?.RootObject;
            baseName = GetResourceManager(rootObject)?.BaseName ?? string.Empty;
        }

        if(string.IsNullOrEmpty(baseName) && targetObject is FrameworkElement frameworkElement)
        {
            baseName = GetResourceManager(frameworkElement.TemplatedParent)?.BaseName ?? string.Empty;
        }

        Binding binding = new() { Mode = BindingMode.OneWay, Path = new PropertyPath($"[{baseName}.{StringName}]"), Source = TranslationSource.Instance, FallbackValue = StringName };

        return binding.ProvideValue(serviceProvider);
    }

    public string StringName { get; }

    private ResourceManager GetResourceManager(object control)
    {
        if(control is DependencyObject dependencyObject)
        {
            object localValue = dependencyObject.ReadLocalValue(Translation.ResourceManagerProperty);

            if(localValue != DependencyProperty.UnsetValue && localValue is ResourceManager resourceManager)
            {
                TranslationSource.Instance.AddResourceManager(resourceManager);

                return resourceManager;
            }
        }

        return null;
    }
}

public class Translation : DependencyObject
{
    public static ResourceManager GetResourceManager(DependencyObject dependencyObject) { return (ResourceManager)dependencyObject.GetValue(ResourceManagerProperty); }

    public static string GetResStringValue(string resdata)
    {
        return string.IsNullOrEmpty(resdata)
            ? throw new ArgumentException($"'{nameof(resdata)}' cannot be null or empty.", nameof(resdata))
            : Resources.ResourceManager.GetString(resdata, TranslationSource.Instance.CurrentCulture);
    }

    public static void SetResourceManager(DependencyObject dependencyObject, ResourceManager value) { dependencyObject.SetValue(ResourceManagerProperty, value); }

    public static readonly DependencyProperty ResourceManagerProperty =
        DependencyProperty.RegisterAttached("ResourceManager", typeof(ResourceManager), typeof(Translation));
}

public class TranslationSource : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public string this[string key]
    {
        get
        {
            string translation = null;
            if(resourceManagerDictionary.ContainsKey(SplitName(key).Item1))
            {
                translation = resourceManagerDictionary[SplitName(key).Item1]
                    .GetString(SplitName(key).Item2, currentCulture);
            }

            return translation ?? key;
        }
    }

    public void AddResourceManager(ResourceManager resourceManager)
    {
        if(!resourceManagerDictionary.ContainsKey(resourceManager.BaseName))
        {
            resourceManagerDictionary.Add(resourceManager.BaseName, resourceManager);
        }
    }

    public static Tuple<string, string> SplitName(string name)
    {
        int idx = name.LastIndexOf('.');
        return Tuple.Create(name.Substring(0, idx), name.Substring(idx + 1));
    }

    public CultureInfo CurrentCulture
    {
        get => currentCulture;

        set
        {
            if(currentCulture != value)
            {
                currentCulture = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }
    }

    public static TranslationSource Instance { get; } = new();

    private CultureInfo currentCulture = CultureInfo.InstalledUICulture;

    private readonly Dictionary<string, ResourceManager> resourceManagerDictionary = new();
}