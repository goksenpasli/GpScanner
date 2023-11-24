using Extensions;
using System;
using System.Xml.Serialization;

namespace GpScanner.ViewModel
{
    [XmlRoot(ElementName = "ReminderData")]

    public class ReminderData : InpcBase
    {
        private string açıklama;
        private string fileName;
        private int ıd;
        private bool seen;
        private DateTime tarih;

        [XmlAttribute(AttributeName = "Açıklama")]
        public string Açıklama
        {
            get => açıklama;
            set
            {
                if (açıklama != value)
                {
                    açıklama = value;
                    OnPropertyChanged(nameof(Açıklama));
                }
            }
        }

        [XmlAttribute(AttributeName = "FileName")]
        public string FileName
        {
            get => fileName;
            set
            {
                if (fileName != value)
                {
                    fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        [XmlAttribute(AttributeName = "Id")]
        public int Id
        {
            get => ıd;
            set
            {
                if (ıd != value)
                {
                    ıd = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        [XmlAttribute(AttributeName = "Seen")]
        public bool Seen
        {
            get => seen;
            set
            {
                if (seen != value)
                {
                    seen = value;
                    OnPropertyChanged(nameof(Seen));
                }
            }
        }

        [XmlAttribute(AttributeName = "Tarih")]
        public DateTime Tarih
        {
            get => tarih;
            set
            {
                if (tarih != value)
                {
                    tarih = value;
                    OnPropertyChanged(nameof(Tarih));
                }
            }
        }
    }
}