using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Timeout;
using Polly.Wrap;


// https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
// https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory

namespace PacificEpoch.Lib.Net
{
	public class HttpClientNG
	{
		private static IHttpClientFactory factory;
		private static ServiceCollection service;
		private static readonly string VERSION = "2021/07/01 18:00";
		public static bool debug { get; set; }

		static HttpClientNG()
		{
			Console.WriteLine($"[HttpClientNG]: Current Ver {VERSION}");
		}


		public static void CreateClient(string name, Action<System.Net.Http.HttpClient> config, IWebProxy proxy,
			bool redirect = true, double timeout = 20d)
		{
			service = new ServiceCollection();

			var client = service.AddHttpClient(name, config)
				//.AddPolicyHandler(Policy.BulkheadAsync<HttpResponseMessage>(10, 50))
				.AddTransientHttpErrorPolicy(p => TransientHandler(p, timeout / 4))
				.AddPolicyHandler(WaitAndRetryNTimesWithTimeoutPerTry(2, timeout / 4, timeout))
				.SetHandlerLifetime(TimeSpan.FromSeconds(10d));

			var handler = new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				AllowAutoRedirect = redirect,
				UseCookies = false
			};

			if (proxy != null)
			{
				handler.Proxy = proxy;
				handler.UseProxy = true;
			}

			client.ConfigurePrimaryHttpMessageHandler(() => handler);
		}

		public static AsyncPolicyWrap<HttpResponseMessage> WaitAndRetryNTimesWithTimeoutPerTry(int retries,
			double delay, double timeout)
		{
			var waitAndRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
				retries,
				attempt => TimeSpan.FromSeconds(delay), (exception, waitDuration, ctx) =>
				{
					if (debug)
					{
						Console.WriteLine(
							$"[Polly.OnRetry due Exception: '{exception.Message}'; waiting for {waitDuration.TotalMilliseconds} ms before retrying.");
					}
				}
			);

			var timeoutPerTryPolicy =
				Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeout), TimeoutStrategy.Optimistic,
					(context, span, task, exception) =>
					{
						if (debug)
						{
							Console.WriteLine(
								$"[Polly.OnTimeoutAsync due Exception: '{(exception == null ? "None" : exception.Message)}' @ {DateTime.Now}; Timespan: {span.TotalMilliseconds} ms.");
						}

						return Task.FromResult(default(HttpResponseMessage));
					});

			return waitAndRetryPolicy.WrapAsync(timeoutPerTryPolicy);
		}

		private static IAsyncPolicy<HttpResponseMessage> TransientHandler(PolicyBuilder<HttpResponseMessage> builder,
			double timeout)
		{
			return builder.WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(timeout), (msg, waitDuration, ctx) =>
			{
				if (debug)
				{
					Console.WriteLine(
						$"[Polly.OnRetry due TransientHttpError '{msg.Result}'; waiting for {waitDuration.TotalMilliseconds} ms before retrying.");
				}
			});
		}

		public static void Build()
		{
			try
			{
				factory = service.BuildServiceProvider().GetService<IHttpClientFactory>();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		public static System.Net.Http.HttpClient GetClient(string name)
		{
			var client = factory.CreateClient(name);
			return client;
		}
	}
