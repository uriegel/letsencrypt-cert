using CsTools.HttpRequest;

using static System.Console; 
using static CsTools.HttpRequest.Core;

static class CheckServer
{
    public static async Task<bool> Check(string domain)
    {
        try
        {
            using var msg = await Request.RunAsync(DefaultSettings with
            {
                Method = HttpMethod.Get,
                BaseUrl = $"http://{domain}",
                Url = "/.well-known/acme-challenge/check"
            });
            var result = await msg.Content.ReadAsStringAsync();
            return result == "checked";
        }
        catch (Exception e)
        {
            Error.WriteLine($"Could not check web server {domain}: {e}");
            return false;
        }
    }
}