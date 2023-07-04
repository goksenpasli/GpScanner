using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace TwainControl;

public class ZoomableInkCanvas : InkCanvas, INotifyPropertyChanged
{
    public ZoomableInkCanvas()
    {
        PropertyChanged += ZoomableInkCanvas_PropertyChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public double CurrentZoom {
        get => currentZoom;

        set {
            if (currentZoom != value)
            {
                currentZoom = value;
                OnPropertyChanged(nameof(CurrentZoom));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ApplyZoom()
    {
        ScaleTransform scaleTransform = new(CurrentZoom, CurrentZoom);
        LayoutTransform = scaleTransform;
    }

    private void ZoomableInkCanvas_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "CurrentZoom" && CurrentZoom is >= 0.1 and <= 3.0)
        {
            ApplyZoom();
        }
    }

    private double currentZoom = 1d;
}