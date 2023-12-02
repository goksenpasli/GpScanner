using Extensions;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GpScanner.ViewModel
{
    public class ReminderData : InpcBase
    {
        private string açıklama;
        private string fileName;
        private int ıd;
        private bool seen;
        private DateTime tarih;

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

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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