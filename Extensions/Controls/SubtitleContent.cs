using System;
using System.Windows.Media;

namespace Extensions.Controls;

public class SubtitleContent : InpcBase
{
    private SolidColorBrush backgroundColor;
    private TimeSpan endTime;
    private string segment;
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

    public string Segment
    {
        get => segment;
        set
        {
            if (segment != value)
            {
                segment = value;
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