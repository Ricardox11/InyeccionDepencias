using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Extensions.Logging;
using Serilog;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;


// package
// Microsft.extensions.hosting
// Serilog.skins.file
// Microsoft.Extensions.Http // cliente http para comunicacion con endpoint
// Microsoft.Extensions.Http.Polly // httpclient con manejo de errores - incluye Microsoft.Extensions.Http 

namespace InyeccionDependecias
{
    class Program
    {
        static async Task Main(string[] args)
        {

            // crear politica para http polly - si recibe error de comunicacion si no existe o reintentar si falla
            static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
            {
                return HttpPolicyExtensions
                    .HandleTransientHttpError() // error
                    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound) // si no existe
                    .WaitAndRetryAsync(7, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // reinteta 7 veces cada 2 exponencial
            }

            // configurar el log
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext() // texto
                .WriteTo.File("./test1.log") // archivo
                .CreateLogger();
           

            // instancia hostbuilder
            var hostBuilder = new HostBuilder();



            // configurar contenedor de inyeccion dependencias

            // configurar servicio - objetos que se utilizen por la inyeccion
            hostBuilder.ConfigureServices((context, services) =>
            {
                // Singleton una sola instancia en todo el ciclo de vida
                services.AddSingleton<ITest, Test>(); // cuando se solicite Itest se inyecta Test

                // funcion que usa la inyeccion
                services.AddSingleton<IHostedService, MyService>();

                // configuracion inyeccion httpclient
                services.AddHttpClient<ITest, Test>(client => {
                    client.BaseAddress = new Uri("https://swapi.dev"); // define url endpoint
                }).AddPolicyHandler(GetRetryPolicy()); // politica para usar en consumo url polly
            });

            // configurar configure - componentes o proveedores de configuracion
            hostBuilder.ConfigureAppConfiguration((context, configuration) =>
            {
                // componente para recibir parametros de configuracion
                configuration.AddCommandLine(args); // permite inyectar Iconfiguration
                configuration.AddEnvironmentVariables(); // variables de ambiente

            });

            // configurar loggin
            hostBuilder.ConfigureLogging((context , logging) =>
            {
                logging.AddConsole(); // proveedor - envia a consola
                logging.AddSerilog(); // proveedor serilog - envia a file

            });

           
            Log.Logger.Information("mensaje1");

            // ejecucion metodo para console
            await hostBuilder.RunConsoleAsync();
        }
    }

    // Interface test para inyeccion

    interface ITest
    {
        void Run(string message);

        Task InvokeEndPointAsync(string endpoint); // metodo para uso de httpclient
    }

    // clase que implementa la interface

    class Test : ITest
    {
        private readonly HttpClient client;

        public Test(HttpClient client) // inyeccion httpclient
        {
            this.client = client;
        }

        public async Task InvokeEndPointAsync(string endpoint)
        {
            // httpclient
            var result = await client.GetAsync(endpoint); // consumir uri
            System.Console.WriteLine(await result.Content.ReadAsStringAsync()); // imprimir resultado
        }

        void ITest.Run(string message)
        {
            System.Console.WriteLine(message);
        }
    }


    class MyService : IHostedService
    {
        // Fiedl

        private readonly ITest test; // define variable
        private readonly IConfiguration configuration;
        private readonly ILogger<MyService> logger;


      


        // inyeccion instancia Test, y Iconfiguration
        public MyService(ITest test, IConfiguration configuration, ILogger<MyService> logger)
        {
            this.test = test; // asiga valor de la inyeccion
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            // obtiene parametro para inyeccion configuration dela variable "message" dotnet run "message=mensaje1" 
            //  var message = configuration.GetValue<string>("message"); // recibe por consola
            var message = configuration.GetValue<string>("PATH"); // variable de ambiente
            // usa log
            logger?.LogInformation("Log del servicio");
            logger.LogError("prueba");
            // usa valor
            test.Run(message);
            // uso httpclient
            await test.InvokeEndPointAsync("api/people/1/"); // ruta


            // return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

}
