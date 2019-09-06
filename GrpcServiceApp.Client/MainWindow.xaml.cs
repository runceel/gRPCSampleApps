using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcSampleApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IPublicClientApplication _publicClientApplication;

        public bool IsInUsersRole { get; private set; }
        public bool IsInWriterRole { get; private set; }
        public bool IsInAdminsRole { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            _configuration = CreateConfiguration();
            _channel = CreateGrpcChannel();
            _client = CreateEmployeesClient();
            _publicClientApplication = CreatePublicClientApplication();
            listBoxEmployees.ItemsSource = _employees;
        }

        private IPublicClientApplication CreatePublicClientApplication()
        {
            var options = new PublicClientApplicationOptions();
            _configuration.Bind("AzureAd", options);
            return PublicClientApplicationBuilder.CreateWithApplicationOptions(options)
                .Build();
        }

        private Employees.EmployeesClient CreateEmployeesClient() => new Employees.EmployeesClient(_channel);

        private GrpcChannel CreateGrpcChannel() => GrpcChannel.ForAddress(_configuration.GetSection("ServerEndpoint").Value);

        private IConfiguration CreateConfiguration() => new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.development.json", true)
                .Build();

        private async void GetEmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            var headers = await CreateAuthMetadataAsync();
            if (headers == null)
            {
                MessageBox.Show("Couldn't get accessToken.");
                return;
            }

            var r = await _client.GetEmployeesAsync(new Empty(), headers);
            _employees.Clear();
            foreach (var emp in r.Employees)
            {
                _employees.Add(emp);
            }
        }

        private async Task<Metadata> CreateAuthMetadataAsync()
        {
            var r = await AcquireTokenAsync();
            if (r == null)
            {
                return null;
            }

            return new Metadata
            {
                { "Authorization", $"Bearer {r.AccessToken}" },
            };
        }

        private async void AddEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxEmployeeName.Text))
            {
                MessageBox.Show("Name is required.");
                return;
            }

            var headers = await CreateAuthMetadataAsync();
            if (headers == null)
            {
                MessageBox.Show("Couldn't get accessToken.");
                return;
            }

            var r = await _client.AddEmployeeAsync(new Employee { Name = textBoxEmployeeName.Text }, headers);
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
            var headers = await CreateAuthMetadataAsync();
            if (headers == null)
            {
                MessageBox.Show("Couldn't get accessToken.");
                return;
            }

            var r = await _client.DeleteEmployeeAsync(new DeleteEmployeeRequest
            {
                Id = target.Id,
            }, headers);
            if (r.Succeed)
            {
                _employees.Remove(target);
            }
            else
            {
                MessageBox.Show($"Couldn't delete a employee. id: {target.Id}, name: {target.Name}");
            }
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            var r = await AcquireTokenAsync();
            if (r is null)
            {
                MessageBox.Show("Sign in failed.");
                return;
            }

            var claims = new JwtSecurityToken(r.AccessToken);
            var roleName = claims.Claims.FirstOrDefault(x => x.Type == "roles")?.Value ?? "Users"; // default is users role
            textBlockSiginStatus.Text = $"Signed in: {r.Account.Username}(Role: {roleName})";

            tabItemAddEmployee.IsEnabled = roleName == "Writer" || roleName == "Administrators";
            IsInAdminsRole = roleName == "Administrators";
        }

        private async Task<AuthenticationResult> AcquireTokenAsync()
        {
            var scopes = _configuration.GetSection("Scopes").Get<string[]>();
            var account = (await _publicClientApplication.GetAccountsAsync()).FirstOrDefault();
            try
            {
                return await _publicClientApplication.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                Debug.WriteLine(ex);
                try
                {
                    using (var cancellationTokenSource = new CancellationTokenSource())
                    {
                        var signInTask = _publicClientApplication.AcquireTokenInteractive(scopes)
                            .WithSystemWebViewOptions(new SystemWebViewOptions
                            {
                                OpenBrowserAsync = SystemWebViewOptions.OpenWithChromeEdgeBrowserAsync,
                            })
                            .ExecuteAsync(cancellationTokenSource.Token);
                        if (signInTask != await Task.WhenAny(signInTask, Task.Delay(TimeSpan.FromMinutes(2))))
                        {
                            cancellationTokenSource.Cancel();
                            MessageBox.Show("Timeout.");
                            return null;
                        }

                        return await signInTask;
                    }
                }
                catch (MsalException msalEx)
                {
                    Debug.WriteLine(msalEx);
                    return null;
                }
            }
        }
    }
}
