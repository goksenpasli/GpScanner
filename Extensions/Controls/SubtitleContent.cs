using System;
using System.Windows.Media;

namespace Extensions.Controls;

public class SubtitleContent : InpcBase
{
    public SolidColorBrush BackgroundColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
    }

    public TimeSpan EndTime
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EndTime));
            }
        }
    }

    public string Segment
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Segment));
            }
        }
    }

    public TimeSpan StartTime
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(StartTime));
            }
        }
    }

    public string Text
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }
}