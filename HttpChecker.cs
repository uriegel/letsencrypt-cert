using CsTools.Extensions;
using CsTools.HttpRequest;

using static System.Console;
using static CsTools.HttpRequest.Core;

static class HttpChecker
{
    public static async Task<bool> Check(string domain)
    {
        try
        {
            var msg = await Request.RunAsync(DefaultSettings with
                {
                    Method = HttpMethod.Get,
                    BaseUrl = $"HTTP://{domain}",
                    Url = "/.well-known/acme-challenge/check"
                });
            return 
                await msg.Content.ReadAsStringAsync() == "checked"
                || false.SideEffect(_ => WriteLine("HTTP server not ready for Lets Encrypt!"));
        }
        catch (HttpException he) when (he.InnerException is System.Net.Http.HttpRequestException hre && hre.HttpRequestError == HttpRequestError.ConnectionError)
        {
            WriteLine("HTTP server not running!");
            return false;
        }
        catch (HttpException he) when (he.InnerException is System.Net.Http.HttpRequestException hre && hre.HttpRequestError == HttpRequestError.NameResolutionError)
        {
            WriteLine($"Unknown domain: {domain}!");
            return false;
        }
        catch (Exception e)
        {
            WriteLine(e);
            return false;
        }
    }
}