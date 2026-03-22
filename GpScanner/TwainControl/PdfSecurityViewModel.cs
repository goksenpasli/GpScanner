using Extensions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.ObjectModel;
using System.IO;
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

        public bool? IsSuccess
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string UserPassword
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public class PdfSecurityViewModel : InpcBase
    {
        public PdfSecurityViewModel()
        {
            BrowseCommand = new RelayCommand(
                () =>
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
                });
        }

        public RelayCommand BrowseCommand { get; }

        public ObservableCollection<PdfFileItem> Files { get; set; } = [];
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

        internal static bool Encrypt(string inputPath, string outputPath, string userPassword, string ownerPassword, out string error)
        {
            error = null;

            try
            {
                using PdfDocument document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
                document.SecuritySettings.UserPassword = userPassword ?? string.Empty;
                document.SecuritySettings.OwnerPassword = ownerPassword ?? string.Empty;
                document.SecuritySettings.PermitAccessibilityExtractContent = false;
                document.SecuritySettings.PermitAnnotations = false;
                document.SecuritySettings.PermitAssembleDocument = false;
                document.SecuritySettings.PermitExtractContent = false;
                document.SecuritySettings.PermitFormsFill = false;
                document.SecuritySettings.PermitFullQualityPrint = false;
                document.SecuritySettings.PermitModifyDocument = false;
                document.SecuritySettings.PermitPrint = false;
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
