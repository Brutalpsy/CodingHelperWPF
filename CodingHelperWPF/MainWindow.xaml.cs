using Microsoft.Win32;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net;
using Kernel = Microsoft.SemanticKernel.Kernel;
using System;

namespace CodingHelperWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private  ChatHistory _chatHistory;
        private  IChatCompletionService _chat;

        public MainWindow()
        {
            InitializeComponent();
            _ = InitAsync();
        }


        private async Task InitAsync()
        {
            string apiKey = Environment.GetEnvironmentVariable("CodingHelperAPIKey", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("API key not found. Please set it in the settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var kernelBuilder = Kernel.CreateBuilder().AddOpenAIChatCompletion("gpt-4o", apiKey);

            var app = kernelBuilder.Build();

            _chat = app.Services.GetRequiredService<IChatCompletionService>();
            _chatHistory = new ChatHistory();
            _chatHistory.AddSystemMessage("You are a helpful coding assistant and your task is to help with programming questions.");
        }


        private async void sendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(messageBox.Text))
            {
                chatDisplay.AppendText(messageBox.Text + Environment.NewLine);
                chatDisplay.ScrollToEnd();
                _chatHistory.AddUserMessage(messageBox.Text);


                var assistentResponse = string.Empty;

                await foreach (var result in _chat.GetStreamingChatMessageContentsAsync(_chatHistory))
                {
                    assistentResponse += result.Content;
                    chatDisplay.AppendText(result.Content);
                }
                MarkCodeBlocks();
                _chatHistory.AddAssistantMessage(assistentResponse);

                messageBox.Clear();
            }
        }

        private void messageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                sendButton_Click(sender, new RoutedEventArgs());
                e.Handled = true; // Prevents the newline character from being added
            }
        }

        private void MarkCodeBlocks()
        {
            string richText = new TextRange(chatDisplay.Document.ContentStart, chatDisplay.Document.ContentEnd).Text;

            if (richText.Contains("```"))
            {
                chatDisplay.Document.Blocks.Clear();

                string[] parts = richText.Split(new[] { "```" }, StringSplitOptions.None);

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i % 2 == 0) // Regular text
                    {
                        chatDisplay.AppendText(parts[i]);
                    }
                    else // Code block
                    {
                        // Remove any programming language label
                        string codeBlock = parts[i];
                        int firstLineEndIndex = codeBlock.IndexOf(Environment.NewLine);
                        if (firstLineEndIndex > 0 && codeBlock.Substring(0, firstLineEndIndex).Trim().All(char.IsLetter))
                        {
                            // The first line is a language label; remove it
                            codeBlock = codeBlock.Substring(firstLineEndIndex + Environment.NewLine.Length);
                        }
                        AppendCodeBlock(codeBlock);
                    }
                }
            }
        }

        private void AppendCodeBlock(string code)
        {
            // Highlight the code block
            TextRange textRange = new TextRange(chatDisplay.Document.ContentEnd, chatDisplay.Document.ContentEnd)
            {
                Text = code + Environment.NewLine
            };
            textRange.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Consolas"));
            textRange.ApplyPropertyValue(TextElement.FontSizeProperty, 14.0); // Increase font size
            textRange.ApplyPropertyValue(Paragraph.MarginProperty, new Thickness(30, 0, 0, 0)); // Add left margin for indentation

            // Insert a "Copy Code" button after the code block
            Paragraph paragraph = new Paragraph();
            Button copyButton = new Button
            {
                Content = "Copy Code",
                Tag = code
            };
            copyButton.Click += copyButton_Click;

            InlineUIContainer container = new InlineUIContainer(copyButton);
            paragraph.Inlines.Add(container);
            chatDisplay.Document.Blocks.Add(paragraph);
        }

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button copyButton && copyButton.Tag is string code)
            {
                copyButton.Content = "✔️ Copied";
                copyButton.Background = Brushes.LightGreen;

                Clipboard.SetText(code);
            }
        }
        private void attachButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Load the selected image
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.DecodePixelWidth = 100; // Resize to 100px width
                    bitmap.DecodePixelHeight = 100; // Resize to 100px height
                    bitmap.EndInit();

                    // Convert BitmapImage to an Image control
                    Image image = new Image();
                    image.Source = bitmap;
                    image.Width = 100;
                    image.Height = 100;

                    // Create an inline UI container for the image
                    InlineUIContainer container = new InlineUIContainer(image);
                    Paragraph paragraph = new Paragraph(container);
                    chatDisplay.Document.Blocks.Add(paragraph);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred while loading the image: " + ex.Message);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the existing API key from the environment variable
            string existingApiKey = Environment.GetEnvironmentVariable("CodingHelperAPIKey", EnvironmentVariableTarget.User);

            // Create a simple input dialog for API key
            Window settingsWindow = new Window
            {
                Title = "Set API Key",
                Height = 150,
                Width = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(20) };

            TextBlock instructions = new TextBlock
            {
                Text = "Enter your API key:",
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = Brushes.Black
            };

            TextBox apiKeyTextBox = new TextBox
            {
                Width = 350,
                Margin = new Thickness(0, 0, 0, 10),
                Text = existingApiKey // Display the existing API key if it exists
            };

            Button okButton = new Button
            {
                Content = "OK",
                Width = 75,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            okButton.Click += async (s, args) =>
            {
                // Set the API key as a system environment variable
                string apiKey = apiKeyTextBox.Text;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        Environment.SetEnvironmentVariable("CodingHelperAPIKey", apiKey, EnvironmentVariableTarget.User);
                        MessageBox.Show("API key has been set successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await InitAsync(); // Reinitialize with the new API key
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to set the API key. " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                settingsWindow.Close();
            };

            stackPanel.Children.Add(instructions);
            stackPanel.Children.Add(apiKeyTextBox);
            stackPanel.Children.Add(okButton);

            settingsWindow.Content = stackPanel;
            settingsWindow.ShowDialog();
        }
    }
}