using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Data.Entity;

namespace OAB
{
    public partial class Autorize : Window
    {
        private string captchaText;
        private Random random;
        private int loginAttempts = 0;
        private const int ATTEMPTS_BEFORE_CAPTCHA = 0;
        private DateTime? lockoutTime = null;
        private DispatcherTimer lockoutTimer;

        public Autorize()
        {
            InitializeComponent();
            random = new Random();

            lockoutTimer = new DispatcherTimer();
            lockoutTimer.Interval = TimeSpan.FromSeconds(1);
            lockoutTimer.Tick += LockoutTimer_Tick;
        }

        // Добавляем метод ValidateUserAsync
        private async Task<(bool isValid, string message)> ValidateUserAsync(string username, string password)
        {
            using (var context = new DB_OOO_Posuda_20Entities())
            {
                try
                {
                    // Проверка на наличие пользователей в таблице
                    if (!await context.Users.AnyAsync())
                    {
                        return (false, "No users found in the database.");
                    }

                    var user = await context.Users.FirstOrDefaultAsync(u => u.Login == username);

                    if (user == null)
                    {
                        return (false, "Invalid username or password.");
                    }

                    // В реальном приложении здесь должна быть проверка хеша пароля
                    if (user.Password == password)
                    {
                        return (true, "Login successful!");
                    }
                    else
                    {
                        return (false, "Invalid username or password.");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Database error: {ex.Message}");
                }
            }
        }

        private void LockoutTimer_Tick(object sender, EventArgs e)
        {
            if (lockoutTime.HasValue)
            {
                var remainingTime = lockoutTime.Value - DateTime.Now;
                if (remainingTime.TotalSeconds <= 0)
                {
                    lockoutTime = null;
                    LoginButton.IsEnabled = true;
                    StatusTextBlock.Text = "You can try again now.";
                    lockoutTimer.Stop();
                }
                else
                {
                    StatusTextBlock.Text = $"Please wait {remainingTime.Seconds} seconds before next attempt.";
                }
            }
        }

        private void GenerateCaptcha()
        {
            captchaCanvas.Children.Clear();

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            captchaText = "";
            for (int i = 0; i < 6; i++)
            {
                captchaText += chars[random.Next(chars.Length)];
            }

            // Добавляем помехи (линии)
            for (int i = 0; i < 20; i++)
            {
                Line line = new Line
                {
                    X1 = random.Next((int)captchaCanvas.Width),
                    Y1 = random.Next((int)captchaCanvas.Height),
                    X2 = random.Next((int)captchaCanvas.Width),
                    Y2 = random.Next((int)captchaCanvas.Height),
                    Stroke = new SolidColorBrush(Color.FromRgb(
                        (byte)random.Next(256),
                        (byte)random.Next(256),
                        (byte)random.Next(256))),
                    StrokeThickness = 1
                };
                captchaCanvas.Children.Add(line);
            }

            // Добавляем символы с перечеркиванием
            for (int i = 0; i < captchaText.Length; i++)
            {
                Canvas charCanvas = new Canvas
                {
                    Width = 30,
                    Height = 40
                };

                TextBlock charBlock = new TextBlock
                {
                    Text = captchaText[i].ToString(),
                    FontSize = 30 + random.Next(-5, 6),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Arial"),
                    Foreground = Brushes.Black
                };

                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(new RotateTransform(random.Next(-15, 16)));
                charBlock.RenderTransform = transformGroup;

                Line strikeLine = new Line
                {
                    X1 = 0,
                    Y1 = random.Next(5, 15),
                    X2 = 25,
                    Y2 = random.Next(25, 35),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                charCanvas.Children.Add(strikeLine);
                charCanvas.Children.Add(charBlock);

                Canvas.SetLeft(charCanvas, 20 + i * 30 + random.Next(-5, 6));
                Canvas.SetTop(charCanvas, 20 + random.Next(-5, 6));

                captchaCanvas.Children.Add(charCanvas);
            }

            // Добавляем точки для помех
            for (int i = 0; i < 50; i++)
            {
                Ellipse dot = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = Brushes.Black
                };
                Canvas.SetLeft(dot, random.Next((int)captchaCanvas.Width));
                Canvas.SetTop(dot, random.Next((int)captchaCanvas.Height));
                captchaCanvas.Children.Add(dot);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (lockoutTime.HasValue)
            {
                if (DateTime.Now < lockoutTime.Value)
                {
                    var remainingTime = lockoutTime.Value - DateTime.Now;
                    StatusTextBlock.Text = $"Please wait {remainingTime.Seconds} seconds before next attempt.";
                    return;
                }
                else
                {
                    lockoutTime = null;
                }
            }

            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "Please enter both username and password.";
                return;
            }

            try
            {
                if (CaptchaPanel.Visibility == Visibility.Visible)
                {
                    if (string.IsNullOrWhiteSpace(captchaInput.Text))
                    {
                        StatusTextBlock.Text = "Please enter the captcha.";
                        return;
                    }

                    if (captchaInput.Text.ToUpper() != captchaText)
                    {
                        lockoutTime = DateTime.Now.AddSeconds(10);
                        LoginButton.IsEnabled = false;
                        lockoutTimer.Start();
                        StatusTextBlock.Text = "Invalid captcha! Please wait 10 seconds.";
                        GenerateCaptcha();
                        captchaInput.Text = "";
                        return;
                    }
                }

                (bool isValid, string message) = await ValidateUserAsync(username, password);

                if (isValid)
                {
                    StatusTextBlock.Text = "Login successful!";
                    this.Hide();
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                else
                {
                    loginAttempts++;

                    if (loginAttempts > ATTEMPTS_BEFORE_CAPTCHA)
                    {
                        if (CaptchaPanel.Visibility != Visibility.Visible)
                        {
                            CaptchaPanel.Visibility = Visibility.Visible;
                            GenerateCaptcha();
                        }
                        else
                        {
                            GenerateCaptcha();
                        }
                    }

                    StatusTextBlock.Text = message;
                    captchaInput.Text = "";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "An error occurred. Please try again.";
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void RefreshCaptcha_Click(object sender, RoutedEventArgs e)
        {
            if (!lockoutTime.HasValue)
            {
                GenerateCaptcha();
                captchaInput.Text = "";
            }
        }
    }
}