﻿using System.Windows;

namespace Ocr
{
    public class OcrData : InpcBase
    {
        public Rect Rect
        {
            get => rect;

            set
            {
                if (rect != value)
                {
                    rect = value;
                    OnPropertyChanged(nameof(Rect));
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

        private Rect rect;

        private string text;
    }
}