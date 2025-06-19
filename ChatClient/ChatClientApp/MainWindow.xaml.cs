using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChatClientApp
{
    public partial class MainWindow : Window
    {
        private HubConnection _connection;
        private string _username;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            _username = UsernameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(_username)) return;

            string port = PortTextBox.Text.Trim();
            if (string.IsNullOrEmpty(port)) port = "5000";

            _connection = new HubConnectionBuilder()
                .WithUrl($"http://192.168.1.6:{port}/chatHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AddMessageToChat(user, message);
                });
            });

            try
            {
                await _connection.StartAsync();
                SendButton.IsEnabled = true;
                FileButton.IsEnabled = true;

                
                MessageBox.Show("✅ Đã kết nối đến server thành công!", "Kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Kết nối thất bại: {ex.Message}");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                await _connection.InvokeAsync("SendMessage", _username, message);
                MessageTextBox.Clear();
            }
        }

        private void AddMessageToChat(string user, string message)
        {
            bool isOwnMessage = (user == _username);

            Border bubble = new Border
            {
                Background = isOwnMessage
                    ? new SolidColorBrush(Color.FromRgb(220, 248, 198))
                    : new SolidColorBrush(Color.FromRgb(230, 230, 255)),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                MaxWidth = 250,
                HorizontalAlignment = isOwnMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            StackPanel messageStack = new StackPanel();

            
            if (!isOwnMessage)
            {
                messageStack.Children.Add(new TextBlock
                {
                    Text = user,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Blue,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            
            if (message.EndsWith(".jpg") || message.EndsWith(".png") || message.EndsWith(".jpeg"))
            {
                Image img = new Image
                {
                    Source = new BitmapImage(new Uri(message, UriKind.Absolute)),
                    Width = 150,
                    Height = 150,
                    Stretch = Stretch.UniformToFill,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                messageStack.Children.Add(img);
            }
            
            else if (message.Contains("http") && (message.Contains("/uploads/") || message.Contains(".zip") || message.Contains(".pdf")))
            {
                string fileName = Path.GetFileName(message);
                TextBlock fileLink = new TextBlock
                {
                    Text = $"📎 {fileName}",
                    Foreground = Brushes.Blue,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                fileLink.MouseLeftButtonUp += async (s, e) =>
                {
                    TextBlock status = new TextBlock
                    {
                        Text = "⏳ Downloading...",
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    messageStack.Children.Add(status);

                    try
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var fileBytes = await httpClient.GetByteArrayAsync(message);
                            string downloadPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                "Downloads",
                                fileName
                            );
                            await File.WriteAllBytesAsync(downloadPath, fileBytes);

                            status.Text = $"✅ Downloaded to Downloads";
                            status.Foreground = Brushes.Green;
                        }
                    }
                    catch (Exception ex)
                    {
                        status.Text = $"❌ Download failed: {ex.Message}";
                        status.Foreground = Brushes.Red;
                    }
                };

                messageStack.Children.Add(fileLink);
            }
            
            else
            {
                messageStack.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }

            
            messageStack.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                Foreground = Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            });

            bubble.Child = messageStack;
            ChatPanel.Children.Add(bubble);

            
            ScrollViewer scrollViewer = GetParentScrollViewer(ChatPanel);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToEnd();
            }
        }

        private async void FileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string fileName = Path.GetFileName(filePath);
                string port = PortTextBox.Text.Trim();
                string uploadsUrl = $"http://192.168.1.6:{port}/upload";

                using (var client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                    content.Add(fileContent, "file", fileName);

                    try
                    {
                        var response = await client.PostAsync(uploadsUrl, content);
                        if (response.IsSuccessStatusCode)
                        {
                            string fileUrl = $"http://192.168.1.6:{port}/uploads/{fileName}";
                            await _connection.InvokeAsync("SendMessage", _username, fileUrl);
                        }
                        else
                        {
                            MessageBox.Show("Upload failed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Upload error: {ex.Message}");
                    }
                }
            }
        }

        private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UsernameTextBox.Text == "Username")
                UsernameTextBox.Text = "";
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                UsernameTextBox.Text = "Username";
        }

        private ScrollViewer GetParentScrollViewer(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                    return scrollViewer;

                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
