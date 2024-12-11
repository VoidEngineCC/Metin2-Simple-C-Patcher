using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HtmlAgilityPack;

namespace Eldria_Patcher2
{
    public partial class MainWindow : Window
    {
        private const string UpdateUrl = "https://eldria2.mt2.ro/updates/update.zip";
        private const string ChangelogUrl = "https://eldria2.mt2.ro/updates/changelog.php";
        private const string GraphicsUrl = "https://eldria2.mt2.ro/updates/graphics.zip";
        private readonly string[] filesToCheck =
        {
            "d3d8.dll",
            "d3d9.dll",
            "d3dx9_26.dll",
            "ijl15.dll",
            "m2donusturur.ini",
            "m2graphic.ini"
        };

        private DispatcherTimer graphicsStatusTimer; // Timer for updating graphics status
        private DispatcherTimer imageSliderTimer; // Timer for image slider
        private readonly string[] imageUrls =
        {
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/items/550900/0f73b4070e4238fe757656ba68e24c44af64c970.jpg",
            "https://cdn.steamstatic.com/steamcommunity/public/images/items/550900/663858534e5c398bcb8bebcaab08f824d0eccc53.jpg",
            "https://image.board.gameforge.com/uploads/metin2/de/announcement_metin2_de_a64a88f7c50583536cddbecb5ad9e32b.jpg"
        };

        private int currentImageIndex = 0; // To keep track of the current image index
        private BitmapImage[] preloadedImages;
        private Stopwatch stopwatch = new Stopwatch(); // Stopwatch for download progress

        public MainWindow()
        {
            InitializeComponent();
            LoadPatchNotes();
            SetupGraphicsStatusTimer(); // Initialize and start the timer
            PreloadImages(); // Preload images
        }

        private void SetupGraphicsStatusTimer()
        {
            graphicsStatusTimer = new DispatcherTimer();
            graphicsStatusTimer.Interval = TimeSpan.FromSeconds(1); // Update every second
            graphicsStatusTimer.Tick += (sender, eventArgs) => UpdateGraphicsStatusAndPerformCheck();
            graphicsStatusTimer.Start(); // Start the timer
        }

        private void SetupImageSliderTimer()
        {
            imageSliderTimer = new DispatcherTimer();
            imageSliderTimer.Interval = TimeSpan.FromSeconds(3); // Change image every 3 seconds
            imageSliderTimer.Tick += (sender, eventArgs) => UpdateImageSlider();
            imageSliderTimer.Start(); // Start the timer
        }

        private async void PreloadImages()
        {
            preloadedImages = new BitmapImage[imageUrls.Length];
            for (int i = 0; i < imageUrls.Length; i++)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        var imageBytes = await client.DownloadDataTaskAsync(new Uri(imageUrls[i]));
                        var imageStream = new MemoryStream(imageBytes);
                        var bitmapImage = new BitmapImage();

                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = imageStream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();

                        preloadedImages[i] = bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to preload image: {ex.Message}", "Error");
                }
            }

            SetupImageSliderTimer(); // Initialize and start the image slider timer after preloading
        }

        private void UpdateImageSlider()
        {
            try
            {
                if (preloadedImages != null && preloadedImages.Length > 0)
                {
                    ImageSlider.Source = preloadedImages[currentImageIndex];
                    currentImageIndex = (currentImageIndex + 1) % preloadedImages.Length; // Move to the next image index
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update image slider: {ex.Message}", "Error");
            }
        }

        private bool CheckIfGameIsRunning()
        {
            return Process.GetProcessesByName("Eldria2").Any();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIfGameIsRunning()) // Check if the game is running
            {
                MessageBox.Show("The game client should be offline before starting the update.", "Error");
                return; // Exit the method if the game is running
            }

            try
            {
                StartButton.IsEnabled = false;
                StatusTextBlock.Text = "Downloading... (0% - 00:00:00)";

                // Initialize and start the stopwatch
                stopwatch.Restart();

                using (var client = new WebClient())
                {
                    client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36";
                    client.DownloadProgressChanged += Client_DownloadProgressChanged;
                    client.DownloadFileCompleted += (s, args) =>
                    {
                        stopwatch.Stop();
                        StatusTextBlock.Text = "Download complete!";
                    };
                    await client.DownloadFileTaskAsync(new Uri(UpdateUrl), "update.zip");

                    using (var archive = ZipFile.OpenRead("update.zip"))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(Path.Combine(".", entry.FullName));
                            }
                            else
                            {
                                entry.ExtractToFile(Path.Combine(".", entry.FullName), true);
                            }
                        }
                    }

                    File.Delete("update.zip");
                    StatusTextBlock.Text = "Update successfully downloaded and installed.";
                    StartButton.IsEnabled = true;
                    Process.Start("Eldria2.exe");
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download and install update: {ex.Message}");
                StartButton.IsEnabled = true;
                StatusTextBlock.Text = "";
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs progressArgs)
        {
            ProgressBar.Value = progressArgs.ProgressPercentage;

            // Calculate elapsed time
            TimeSpan elapsedTime = stopwatch.Elapsed;

            // Estimate remaining time
            double downloadProgress = progressArgs.ProgressPercentage / 100.0;
            double estimatedTotalTime = elapsedTime.TotalSeconds / downloadProgress;
            TimeSpan estimatedRemainingTime = TimeSpan.FromSeconds(estimatedTotalTime - elapsedTime.TotalSeconds);

            // Update status text
            StatusTextBlock.Text = $"Downloading ({progressArgs.ProgressPercentage}% - {FormatTime(elapsedTime)} / {FormatTime(estimatedRemainingTime)})";
        }

        private string FormatTime(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        private async void ChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
                    var html = await client.GetStringAsync(ChangelogUrl);
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    var changelogNode = htmlDoc.DocumentNode.Descendants("ul").FirstOrDefault();
                    if (changelogNode != null)
                    {
                        var changelogText = changelogNode.InnerHtml
                            .Replace("<li>", "• ")
                            .Replace("</li>", "\n")
                            .Replace("<strong>", "")
                            .Replace("</strong>", "")
                            .Replace("<ul>", "")
                            .Replace("</ul>", "");

                        MessageBox.Show(changelogText, "Changelog");
                    }
                    else
                    {
                        MessageBox.Show("Changelog not found!", "Error");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Failed to retrieve changelog: {ex.Message}", "Error");
            }
        }

        private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem selectedItem = (ComboBoxItem)languageComboBox.SelectedItem;
            string selectedLanguage = selectedItem.Content.ToString().ToLower();

            string localeCode = "";
            switch (selectedLanguage)
            {
                case "english":
                    localeCode = "10002 1252 en";
                    break;
                case "magyar":
                    localeCode = "10021 1250 hu";
                    break;
                case "cestina":
                    localeCode = "10019 1250 cz";
                    break;
                case "deutsch":
                    localeCode = "10000 1252 de";
                    break;
                case "dansk":
                    localeCode = "10013 1252 dk";
                    break;
                case "espanol":
                    localeCode = "10004 1252 es";
                    break;
                case "francais":
                    localeCode = "10000 1252 fr";
                    break;
                case "ellinika":
                    localeCode = "0 1253 gr";
                    break;
                case "italiano":
                    localeCode = "10006 1252 it";
                    break;
                case "holland":
                    localeCode = "10018 1252 nl";
                    break;
                case "polski":
                    localeCode = "10012 1250 pl";
                    break;
                case "portugues":
                    localeCode = "10010 1252 pt";
                    break;
                case "romana":
                    localeCode = "10022 1250 ro";
                    break;
                case "russkiy":
                    localeCode = "10020 1251 ru";
                    break;
                case "turkce":
                    localeCode = "10012 1254 tr";
                    break;
                default:
                    MessageBox.Show("Unsupported language selection.");
                    return;
            }

            string configFile = "locale.cfg";
            try
            {
                UTF8Encoding utf8WithoutBom = new UTF8Encoding(false);

                using (StreamWriter writer = new StreamWriter(configFile, false, utf8WithoutBom))
                {
                    writer.WriteLine(localeCode);
                }

                MessageBox.Show("Language settings updated successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating language settings: {ex.Message}");
            }
        }

        private async void NewButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIfGameIsRunning()) // Check if the game is running
            {
                MessageBox.Show("The game client should be offline before making graphics changes.", "Error");
                return; // Exit the method if the game is running
            }

            bool anyFileExists = false;

            foreach (var file in filesToCheck)
            {
                if (File.Exists(file))
                {
                    anyFileExists = true;
                    File.Delete(file);
                }
            }

            if (anyFileExists)
            {
                SmallProgressBar.Value = 100; // Set progress bar to full
                MessageBox.Show("ENB Disabled");
                GraphicsModeStatusTextBlock.Text = "OFF"; // Update the graphics mode status
            }
            else
            {
                await DownloadAndExtractGraphicsZipAsync();
            }
        }

        private async Task DownloadAndExtractGraphicsZipAsync()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36";
                    client.DownloadProgressChanged += (sender, progressArgs) =>
                    {
                        SmallProgressBar.Value = progressArgs.ProgressPercentage;
                    };

                    await client.DownloadFileTaskAsync(new Uri(GraphicsUrl), "graphics.zip");

                    using (var archive = ZipFile.OpenRead("graphics.zip"))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string fullPath = Path.Combine(".", entry.FullName);
                            if (entry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(fullPath);
                            }
                            else
                            {
                                entry.ExtractToFile(fullPath, true);
                            }
                        }
                    }

                    File.Delete("graphics.zip");
                    SmallProgressBar.Value = 100; // Set progress bar to full
                    MessageBox.Show("ENB Enabled");
                    GraphicsModeStatusTextBlock.Text = "ON"; // Update the graphics mode status
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download and extract graphics: {ex.Message}", "Error");
            }
        }

        private async void LoadPatchNotes()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
                    var html = await client.GetStringAsync("https://eldria2.mt2.ro/updates/patchnotes.php");
                    PatchNotesWebBrowser.NavigateToString(html);
                }
            }
            catch (HttpRequestException ex)
            {
                PatchNotesWebBrowser.NavigateToString($"<p>Failed to retrieve patch notes: {ex.Message}</p>");
            }
        }

        private void UpdateGraphicsStatusAndPerformCheck()
        {
            bool anyFileExists = filesToCheck.Any(File.Exists);

            if (anyFileExists)
            {
                GraphicsModeStatusTextBlock.Text = "ON";
                GraphicsModeStatusTextBlock.Foreground = new SolidColorBrush(Colors.Green); // Set the text color to green
            }
            else
            {
                GraphicsModeStatusTextBlock.Text = "OFF";
                GraphicsModeStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red); // Set the text color to red
            }
        }
    }
}
