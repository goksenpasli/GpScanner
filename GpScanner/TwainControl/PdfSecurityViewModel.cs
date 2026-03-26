using Extensions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace TwainControl
{
    public class PdfFileItem : InpcBase
    {
        public PdfFileItem()
        {
            DecryptCommand = new RelayCommand<object>(
                parameter =>
                {
                    string outputpath = Path.Combine(Path.GetDirectoryName(InputPath), $"{Path.GetFileNameWithoutExtension(InputPath)}_decrypted.pdf");
                    if (!PdfSecurityService.ValidatePaths(InputPath, outputpath, out string validationError))
                    {
                        Status = validationError;
                        IsSuccess = false;
                        return;
                    }
                    bool result = PdfSecurityService.Decrypt(InputPath, outputpath, UserPassword, out string error);

                    Status = result ? Translation.GetResStringValue("SUCCESS") : error;
                    IsSuccess = result;
                },
                parameter => !string.IsNullOrWhiteSpace(UserPassword) && File.Exists(InputPath));

            EncryptCommand = new RelayCommand<object>(
                parameter =>
                {
                    string outputpath = Path.Combine(Path.GetDirectoryName(InputPath), $"{Path.GetFileNameWithoutExtension(InputPath)}_encrypted.pdf");
                    if (!PdfSecurityService.ValidatePaths(InputPath, outputpath, out string validationError))
                    {
                        Status = validationError;
                        IsSuccess = false;
                        return;
                    }
                    bool result = PdfSecurityService.Encrypt(InputPath, outputpath, null, UserPassword, out string error);

                    Status = result ? $"{Translation.GetResStringValue("ENCRYPT")} {Translation.GetResStringValue("SUCCESS")}" : error;
                    IsSuccess = result;
                },
                parameter => !string.IsNullOrWhiteSpace(UserPassword) && File.Exists(InputPath));
        }

        public ICommand DecryptCommand { get; }

        public ICommand EncryptCommand { get; }

        public string InputPath { get; set; }

        public bool IsChecked
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public bool? IsSuccess
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(IsSuccess));
                }
            }
        }

        public string Status
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string UserPassword
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(UserPassword));
                }
            }
        }
    }

    public class PdfSecurityViewModel : InpcBase
    {
        public PdfSecurityViewModel()
        {
            BrowseCommand = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        string pdffolder = FolderDialog.SelectFolder($"PDF {Translation.GetResStringValue("SRC")}", null, null);
                        if (string.IsNullOrEmpty(pdffolder))
                        {
                            return;
                        }
                        foreach (string file in Directory.EnumerateFiles(pdffolder, "*.pdf", SearchOption.AllDirectories))
                        {
                            PdfFileItem item = new() { InputPath = file };
                            Files.Add(item);
                        }
                    }
                    catch (Exception)
                    {

                    }
                });

            CheckAllCommand = new RelayCommand<object>(
                parameter =>
                {
                    foreach (PdfFileItem item in Files)
                    {
                        item.IsChecked = true;
                    }
                },
                parameter => Files?.Any() == true);

            CheckNoneCommand = new RelayCommand<object>(
                parameter =>
                {
                    foreach (PdfFileItem item in Files)
                    {
                        item.IsChecked = false;
                    }

                },
                parameter => Files?.Any() == true);

            CheckReverseCommand = new RelayCommand<object>(
                parameter =>
                {
                    foreach (PdfFileItem item in Files)
                    {
                        item.IsChecked = !item.IsChecked;
                    }
                },
                parameter => Files?.Any() == true);

            DecryptAllCommand = new RelayCommand<object>(
                parameter =>
                {
                    foreach (PdfFileItem item in Files.Where(z => z.IsChecked))
                    {
                        bool result = PdfSecurityService.Decrypt(item.InputPath, Path.Combine(Path.GetDirectoryName(item.InputPath), $"{Path.GetFileNameWithoutExtension(item.InputPath)}_decrypted.pdf"), UserCommonPassword, out string error);
                        item.Status = result ? Translation.GetResStringValue("SUCCESS") : error;
                        item.IsSuccess = result;
                    }
                },
                parameter => Files?.Any(z => z.IsChecked) == true && !string.IsNullOrWhiteSpace(UserCommonPassword));

            EncryptAllCommand = new RelayCommand<object>(
                parameter =>
                {
                    foreach (PdfFileItem item in Files.Where(z => z.IsChecked))
                    {
                        bool result = PdfSecurityService.Encrypt(
                            item.InputPath,
                            Path.Combine(Path.GetDirectoryName(item.InputPath), $"{Path.GetFileNameWithoutExtension(item.InputPath)}_encrypted.pdf"),
                            null,
                            UserCommonPassword,
                            out string error,
                            PermitAccessibilityExtractContent,
                            PermitAnnotations,
                            PermitAssembleDocument,
                            PermitExtractContent,
                            PermitFormsFill,
                            PermitFullQualityPrint,
                            PermitModifyDocument,
                            PermitPrint);
                        item.Status = result ? Translation.GetResStringValue("SUCCESS") : error;
                        item.IsSuccess = result;
                    }
                },
                parameter => Files?.Any(z => z.IsChecked) == true && !string.IsNullOrWhiteSpace(UserCommonPassword));
        }

        public RelayCommand<object> BrowseCommand { get; }

        public RelayCommand<object> CheckAllCommand { get; }

        public RelayCommand<object> CheckNoneCommand { get; }

        public RelayCommand<object> CheckReverseCommand { get; }

        public RelayCommand<object> DecryptAllCommand { get; }

        public RelayCommand<object> EncryptAllCommand { get; }

        public ObservableCollection<PdfFileItem> Files
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Files));
                }
            }
        } = [];

        public bool PermitAccessibilityExtractContent
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitAccessibilityExtractContent));
                }
            }
        }

        public bool PermitAnnotations
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitAnnotations));
                }
            }
        }

        public bool PermitAssembleDocument
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitAssembleDocument));
                }
            }
        }

        public bool PermitExtractContent
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitExtractContent));
                }
            }
        }

        public bool PermitFormsFill
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitFormsFill));
                }
            }
        }

        public bool PermitFullQualityPrint
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitFullQualityPrint));
                }
            }
        }

        public bool PermitModifyDocument
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitModifyDocument));
                }
            }
        }

        public bool PermitPrint
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(PermitPrint));
                }
            }
        }

        public string UserCommonPassword
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(UserCommonPassword));
                }
            }
        }
    }

    internal static class PdfSecurityService
    {
        internal static bool Decrypt(string inputPath, string outputPath, string password, out string error)
        {
            error = null;

            try
            {
                using PdfDocument inputDocument = PdfReader.Open(inputPath, password, PdfDocumentOpenMode.Import);

                using PdfDocument outputDocument = new();

                foreach (PdfPage page in inputDocument.Pages)
                {
                    _ = outputDocument.AddPage(page);
                }

                outputDocument.Save(outputPath);
                return true;
            }
            catch (PdfReaderException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool Encrypt(string inputPath,
                                     string outputPath,
                                     string userPassword,
                                     string ownerPassword,
                                     out string error,
                                     bool PermitAccessibilityExtractContent = false,
                                     bool PermitAnnotations = false,
                                     bool PermitAssembleDocument = false,
                                     bool PermitExtractContent = false,
                                     bool PermitFormsFill = false,
                                     bool PermitFullQualityPrint = false,
                                     bool PermitModifyDocument = false,
                                     bool PermitPrint = false)
        {
            error = null;

            try
            {
                using PdfDocument document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
                document.SecuritySettings.UserPassword = userPassword ?? string.Empty;
                document.SecuritySettings.OwnerPassword = ownerPassword ?? string.Empty;
                document.SecuritySettings.PermitAccessibilityExtractContent = PermitAccessibilityExtractContent;
                document.SecuritySettings.PermitAnnotations = PermitAnnotations;
                document.SecuritySettings.PermitAssembleDocument = PermitAssembleDocument;
                document.SecuritySettings.PermitExtractContent = PermitExtractContent;
                document.SecuritySettings.PermitFormsFill = PermitFormsFill;
                document.SecuritySettings.PermitFullQualityPrint = PermitFullQualityPrint;
                document.SecuritySettings.PermitModifyDocument = PermitModifyDocument;
                document.SecuritySettings.PermitPrint = PermitPrint;
                document.Save(outputPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool ValidatePaths(string input, string output, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                error = "Input file not found";
                return false;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                error = "Output path is empty";
                return false;
            }

            if (Path.GetFullPath(input) == Path.GetFullPath(output))
            {
                error = "Input and output cannot be the same";
                return false;
            }

            return true;
        }
    }
}
