# HttpClient-with-HttpClientFactory
A .Net Framework practice of HttpClientFactory

Main idea comes from https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
which is implmented in .Net Core by MS.

## Useage
var proxy = new WebProxy("host", port);
CreateClient("HttpClientName", c => { }, proxy);
Build();

var client = GetClient("HttpClientName");// It will give you a HttpClient instance, use it as original HttpClinet.
