// open Certes.Acme
using System.Text.Json;
using Certes;
using Certes.Acme;
using CsTools;
using CsTools.Extensions;
using CsTools.Functional;

using static System.Console;
using static CsTools.Functional.Memoization;
using static AspNetExtensions.LetsEncrypt; 

static class Account
{
    public static async Task Create()
    {
        WriteLine("Creating letsencrypt account");

        var certRequest = 
                "cert.json"                
                    .CheckIfFileExists()
                    ?.OpenFile()
                    ?.Use(f => JsonSerializer.Deserialize<CertRequest>(f, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
        if (certRequest == null)
        {
            WriteLine("You have to create a cert request json file, see https://www.nuget.org/packages/LetsencryptCert/");
            return;
        }

        GetEncryptDirectory()
            .EnsureDirectoryExists();

        File.Copy("cert.json", Parameters.GetCertFile(), true);

        var server = Parameters.Get().Staging
            ? WellKnownServers.LetsEncryptStagingV2
            : WellKnownServers.LetsEncryptV2;

        var acmeContext = new AcmeContext(server);
        await acmeContext.NewAccount(certRequest.Account, true);

        var pemKey = acmeContext.AccountKey.ToPem();
        await File.WriteAllTextAsync(Parameters.GetAccountFile(), pemKey);
        WriteLine("Letsencrypt account created");
    }

    public static AsyncResult<AcmeContext, Unit> Get()
    {
        WriteLine("Reading letsencrypt account");

        return (Parameters
            .GetAccountFile()
            .ReadAllTextFromFilePath()
            ?.Pipe(p => KeyFactory.FromPem(p))
            ?.Pipe(k => new AcmeContext(Parameters.GetAcmeUri(), k))
            ?.SideEffect(_ => WriteLine("Letsencrypt account read")))
            .FromNullable()
            .SideEffectWhenOkAsync(a => a.Account())
            .ToAsyncResult();
    }

    public static Func<CertRequest?> ReadRequest { get; }
        = MemoizeMaybe(InitReadRequest);

    static CertRequest? InitReadRequest()
        => Parameters
                .GetCertFile()
                ?.OpenFile()
                ?.Use(f => JsonSerializer.Deserialize<CertRequest>(f, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
}



