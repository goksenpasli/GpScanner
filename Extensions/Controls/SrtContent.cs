using System;
using System.Windows.Media;

namespace Extensions.Controls;

public class SrtContent : InpcBase
{
    private SolidColorBrush backgroundColor;
    private TimeSpan endTime;
    private TimeSpan startTime;
    private string text;

    public SolidColorBrush BackgroundColor
    {
        get => backgroundColor;

        set
        {
            if (backgroundColor != value)
            {
                backgroundColor = value;
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

    public string Segment { get; set; }

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
        get => text;

        set
        {
            if (text != value)
            {
                text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }
}