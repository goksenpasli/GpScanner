using System;
using System.Windows.Media;

namespace Extensions.Controls;

public class SubtitleContent : InpcBase
{
    private TimeSpan endTime;
    private TimeSpan startTime;

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
        get => endTime;

        set
        {
            if (endTime != value)
            {
                endTime = value;
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
        get => startTime;

        set
        {
            if (startTime != value)
            {
                startTime = value;
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