using System.IO;
using System.Threading.Tasks;
using Plugin.Maui.Audio;
using Renci.SshNet;

namespace Adeptus_Mechanicus_Litanys
{
    public partial class MainPage : ContentPage
    {
        private readonly IAudioManager audioManager;
        private IAudioPlayer? currentPlayer;
        private FileStream? currentStream;

        private string litaniesPath = Path.Combine(AppContext.BaseDirectory, "Litanies");

        private string[] sampleLitanies = new[]
        {
        "Litany of Activation",
        "Chant of the Circuit",
        "Blessing of the Cog"
        };

        public MainPage()
        {
            InitializeComponent();
            LoadLitanyButtons();
            audioManager = AudioManager.Current;

            SetPointer(LActivation, "OLitanies/Для кнопки запуска.mp3");
            SetPointer(LRefresh, "OLitanies/Для кнопки обновления.mp3");
            SetPointer(LRandom, "OLitanies/Для выбора случайной литании.mp3");
        }

        private void SetPointer(Button btn, string path)
        {
            // создаём распознаватель указателя
            var pointer = new PointerGestureRecognizer();

            // Наведение
            pointer.PointerEntered += (s, e) =>
            {
                _ = PlayLitanyAsync(path);
            };

            // Уход курсора
            pointer.PointerExited += (s, e) =>
            {
                StopLitany();
            };

            // Привязываем к кнопке
            btn.GestureRecognizers.Add(pointer);
        }

        private void LoadLitanyButtons()
        {
            LitanyButtonsStack.Children.Clear();
            
            sampleLitanies = GetLitanyFiles().ToArray();

            foreach (var litany in sampleLitanies)
            {
                var btn = new Button
                {
                    Text = litany,
                    BackgroundColor = Color.FromHex("#660000"),
                    TextColor = Color.FromHex("#FFD700"),
                    CornerRadius = 8
                };
                btn.Clicked += OnLitanyClicked;
                LitanyButtonsStack.Children.Add(btn);
            }
        }


        private List<string> GetLitanyFiles()
        {
            if (!Directory.Exists(litaniesPath))
                Directory.CreateDirectory(litaniesPath);

            // Возвращаем список файлов (txt и mp3)
            return Directory.GetFiles(litaniesPath, "*.*")
                .Where(f => f.EndsWith(".txt") || f.EndsWith(".mp3"))
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Distinct() // чтобы txt и mp3 с одним именем не дублировались
                .ToList();
        }

        private async void OnLitanyClicked(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                string litany = LoadLitanyText(Path.Combine(litaniesPath, btn.Text + ".txt"));
                LitanyText.Text = $"{litany}";
                await PlayLitanyAsync(Path.Combine(litaniesPath, btn.Text + ".mp3"));
            }
        }

        private string LoadLitanyText(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {

            PlayLitanyAsync("OLitanies/Литания Благого Воззвания к Серверу.mp3");

            string host = "bakasenpai.ru";       // IP VPS
            int port = 22;
            string username = "litany";
            string password = "1234";
            string remotePath = "litanies";
            string localPath = Path.Combine(AppContext.BaseDirectory, "Litanies");

            LitanySync.SyncLitanies(host, port, username, password, remotePath, localPath);
            // Здесь в будущем: перечитать папку с литаниями
            LoadLitanyButtons();
            DisplayAlert("Omnissiah", "Список литаний был обновлён.", "Ave!");
            StopLitany();
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            LitanyText.Text = "";
        }

        private void OnRandomClicked(object sender, EventArgs e)
        {
            var random = new Random();
            var litany = sampleLitanies[random.Next(sampleLitanies.Length)];
            LitanyText.Text = $"You recite: {litany}\n\n+++ Praise the Omnissiah! +++";
        }

        private Task PlayLitanyAsync(string filePath)
        {
            // Останавливаем предыдущий плеер
            currentPlayer?.Stop();
            currentPlayer?.Dispose();
            currentStream?.Dispose();

            // Открываем новый поток (НЕ using!)
            currentStream = File.OpenRead(filePath);

            // Создаём плеер
            currentPlayer = audioManager.CreatePlayer(currentStream);
            currentPlayer.Volume = 1.0;

            // Воспроизведение
            currentPlayer.Play();

            return Task.CompletedTask;
        }

        private Task PlayLitanyBtnAsync(string filePath)
        {
            // Останавливаем предыдущий
            currentPlayer?.Stop();
            currentPlayer?.Dispose();
            currentStream?.Dispose();

            currentStream = File.OpenRead(filePath);
            currentPlayer = audioManager.CreatePlayer(currentStream);
            currentPlayer.Volume = 0.8;
            currentPlayer.Play();

            return Task.CompletedTask;
        }

        private void StopLitany()
        {
            currentPlayer?.Stop();
            currentPlayer?.Dispose();
            currentPlayer = null;

            currentStream?.Dispose();
            currentStream = null;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Это выполняется при показе окна (страницы)
            // Например, старт фонового звука или инициализация
            _ = PlayLitanyAsync("OLitanies/Литания при открытии программы с литаниями.mp3");
            LitanyText.Text = "+++ Литания запуска приложения +++" +
                "\n\r" +
                "Откройся, свод святых скрижалей,\r\nВ монитора нетленном свете.\r\nЯви свой код в тиши архива,\r\nДай доступ к мудрости в консоли этой.\r\n\r\nВо имя Духа и Омниссии,\r\nПрояви текст, что был сокрыт.\r\nДа снизойдёт благословение\r\nНа адепта, что к тебе явил.";
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Это выполняется при закрытии окна (страницы)
            StopLitany(); // останавливаем звук
        }

        public static class LitanySync
        {
            /// <summary>
            /// Синхронизация литаний с сервера по SFTP
            /// </summary>
            /// <param name="host">IP или домен сервера</param>
            /// <param name="port">Порт (обычно 22)</param>
            /// <param name="username">Пользователь SFTP</param>
            /// <param name="password">Пароль</param>
            /// <param name="remotePath">Папка на сервере (например "litanies")</param>
            /// <param name="localPath">Локальная папка, куда скачивать</param>
            public static void SyncLitanies(string host, int port, string username, string password,
                                            string remotePath, string localPath)
            {
                if (!Directory.Exists(localPath))
                    Directory.CreateDirectory(localPath);

                using var sftp = new SftpClient(host, port, username, password);
                sftp.Connect();

                // Получаем список файлов на сервере
                var files = sftp.ListDirectory(remotePath);

                foreach (var file in files)
                {
                    if (file.IsDirectory || file.IsSymbolicLink) continue; // пропускаем папки
                    if (!(file.Name.EndsWith(".mp3") || file.Name.EndsWith(".txt"))) continue; // только литании

                    string localFile = Path.Combine(localPath, file.Name);

                    bool needDownload = true;

                    if (File.Exists(localFile))
                    {
                        long localSize = new FileInfo(localFile).Length;
                        if (localSize == file.Attributes.Size)
                            needDownload = false; // совпадает, пропускаем
                    }

                    if (needDownload)
                    {
                        using var fs = File.Open(localFile, FileMode.Create, FileAccess.Write);
                        sftp.DownloadFile(file.FullName, fs);
                        Console.WriteLine($"[SYNC] Обновлён: {file.Name}");
                    }
                }

                sftp.Disconnect();
            }
        }
    }
}
