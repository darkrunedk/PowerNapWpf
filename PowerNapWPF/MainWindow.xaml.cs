using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PowerNapWPF.PowerNapService;

namespace PowerNapWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string _sharedFolderPlaceholderText, _filenamePlaceholderText;
        private string _sharedFolderPath;
        private readonly int _port;
        private readonly IPAddress _ip;
        private readonly TcpListener _serverListener;

        private bool _running = true;

        public MainWindow()
        {
            InitializeComponent();

            _sharedFolderPlaceholderText = SharedFolderTextBox.Text;
            _filenamePlaceholderText = FileNameTextBox.Text;

            _ip = Dns.GetHostAddresses(Dns.GetHostName())[0];
            _port = 8888;

            try
            {
                _serverListener = new TcpListener(_ip, _port);
                _serverListener.Start();

                AddMessage($"Your peer has started at {_ip} on port {_port}");

                Task.Run(() => AcceptClients());
            }
            catch
            {
                AddMessage("Error occured...");
            }
        }

        private void AcceptClients()
        {
            while (_running)
            {
                if (!_serverListener.Pending())
                {
                    Thread.Sleep(50);
                    continue;
                }

                var client = _serverListener.AcceptTcpClient();
                var stream = client.GetStream();

                Task.Run(() => SendFile(stream));
            }
        }

        private void SendFile(NetworkStream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                var req = streamReader.ReadLine();
                using (var fileStream = new FileStream(_sharedFolderPath + "/" + req, FileMode.Open))
                {
                    fileStream.CopyTo(stream);
                }

                stream.Flush();
                
                AddMessage($"{req} has been downloaded by another person.");
            }
        }

        private void SharedFolderTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SharedFolderTextBox.Text == _sharedFolderPlaceholderText)
            {
                SharedFolderTextBox.Text = "";
            }
        }

        private void SharedFolderTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SharedFolderTextBox.Text == string.Empty)
            {
                SharedFolderTextBox.Text = _sharedFolderPlaceholderText;
            }
        }

        private void FileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (FileNameTextBox.Text == string.Empty)
            {
                FileNameTextBox.Text = _filenamePlaceholderText;
            }
        }

        private void FileNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (FileNameTextBox.Text == _filenamePlaceholderText)
            {
                FileNameTextBox.Text = "";
            }
        }

        private void AddSharedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _sharedFolderPath = SharedFolderTextBox.Text;
            var files = Directory.GetFiles(_sharedFolderPath);
            var fileList = new List<string>();
            foreach (var file in files)
            {
                fileList.Add(System.IO.Path.GetFileName(file));
                AddMessage($"File in share: {file}");
            }

            using (var service = new Service1Client())
            {
                var count = service.AddAll(fileList.ToArray(), _ip.ToString(), _port);
                AddMessage($"You have added {count} to the file index.");
            }

            AddMessage($"Found {files.Length} files...");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _running = false;
            _serverListener.Stop();
            RemoveFiles();
        }

        private void RemoveFiles()
        {
            using (var service = new Service1Client())
            {
                service.RemoveAll(_ip.ToString(), _port);
            }
        }

        private void GetFileButton_Click(object sender, RoutedEventArgs e)
        {
            using (var service = new Service1Client())
            {
                try
                {
                    var fileName = FileNameTextBox.Text;
                    var resp = service.Get(fileName);

                    Task.Run(() => ReceiveFiles(resp, fileName));
                    AddMessage(resp[0].Host + " " + resp[0].Port);
                }
                catch
                {
                    AddMessage("File doesn't exist...");
                }
            }
        }

        private void ReceiveFiles(Destination[] resp, string fileName)
        {
            try
            {
                using (var tcpClient = new TcpClient(resp[0].Host, resp[0].Port))
                {
                    var stream = tcpClient.GetStream();

                    var writer = new StreamWriter(stream);
                    writer.WriteLine(fileName);
                    writer.Flush();

                    using (var fileStream = new FileStream(_sharedFolderPath + "/" + fileName, FileMode.Create))
                    {
                        stream.CopyTo(fileStream);
                        fileStream.Flush();
                    }
                }

                Dispatcher.Invoke(() => AddMessage($"{fileName} has been successfully copied to {_sharedFolderPath}"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AddMessage($"An error happened, while trying to receive {fileName}...\nMore info: {ex.Message}"));
            }
        }

        private void AddMessage(string message)
        {
            StatusTextBox.AppendText(message + "\n");
            StatusTextBox.ScrollToEnd();
        }
    }
}
