using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using GrpcSampleApp;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace GrpcServiceApp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IConfiguration _configuration;
        private readonly GrpcChannel _channel;
        private readonly Employees.EmployeesClient _client;
        private readonly ObservableCollection<Employee> _employees = new ObservableCollection<Employee>();
        public MainWindow()
        {
            InitializeComponent();
            _configuration = CreateConfiguration();
            _channel = CreateGrpcChannel();
            _client = CreateEmployeesClient();
            listBoxEmployees.ItemsSource = _employees;
        }

        private Employees.EmployeesClient CreateEmployeesClient() => new Employees.EmployeesClient(_channel);

        private GrpcChannel CreateGrpcChannel() => GrpcChannel.ForAddress(_configuration.GetSection("ServerEndpoint").Value);

        private IConfiguration CreateConfiguration() => new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.development.json", true)
                .Build();

        private async void GetEmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            var r = await _client.GetEmployeesAsync(new Empty());
            _employees.Clear();
            foreach (var emp in r.Employees)
            {
                _employees.Add(emp);
            }
        }

        private async void AddEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxEmployeeName.Text))
            {
                MessageBox.Show("Name is required.");
                return;
            }

            var r = await _client.AddEmployeeAsync(new Employee { Name = textBoxEmployeeName.Text });
            if (r.Succeed)
            {
                MessageBox.Show($"{textBoxEmployeeName.Text} was added.");
                textBoxEmployeeName.Text = "";
            }
            else
            {
                MessageBox.Show($"{textBoxEmployeeName.Text} was not added.");
            }
        }

        private async void DeleteEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            var target = (Employee)((Button)sender).DataContext;
            var r = await _client.DeleteEmployeeAsync(new DeleteEmployeeRequest
            {
                Id = target.Id,
            });
            if (r.Succeed)
            {
                _employees.Remove(target);
            }
            else
            {
                MessageBox.Show($"Couldn't delete a employee. id: {target.Id}, name: {target.Name}");
            }
        }
    }
}
