using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Certes;
using Certes.Acme;
using CsTools.Extensions;

using static System.Console;
using static CsTools.Functional.Memoization;

// Parameter: -prod: productive, without: staging (test)
// Parameter: -del: delete account
// Parameter: -create: read file cert.json

string encryptDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "letsencrypt-uweb");

string certRequestFile;
IAccountContext account;
string accountFile;

Func<string> GetPfxPassword = Memoize(InitGetPfxPassword);

try
{
    WriteLine($"Starting letsencrypt certificate handling");

    bool staging = !args.Contains("-prod");
    bool deleteAccount = args.Contains("-del");
    bool createAccount = args.Contains("-create");
    WriteLine(staging ? "Staging" : "!!! P R O D U C T I V E !!!");

    var certificateFile = Path.Combine(encryptDirectory, $"certificate{(staging ? "-staging" : "")}.pfx");
    accountFile = Path.Combine(encryptDirectory, $"access{(staging ? "-staging" : "")}.pem");
    certRequestFile = Path.Combine(encryptDirectory, "cert.json");

    if (deleteAccount)
    {
        DeleteAccount();
        return;
    }
    else if (createAccount)
    {
        await CreateAccountAsync(staging);
        return;
    }
    else
    {
        if (File.Exists(certificateFile))
        {
            var certificate = new X509Certificate2(certificateFile, GetPfxPassword());

            WriteLine($"Certificate expires: {certificate.NotAfter}");
            if (certificate.NotAfter > DateTime.Now + TimeSpan.FromDays(30))
            {
                WriteLine("No further action needed");
                return;
            }
        }
        (AcmeContext acmeContext, CertRequest? certRequest) = await ReadAccountAsync(staging);
        if (certRequest == null)
        {
            Error.WriteLine("Could not read cert request");
            return;
        }

        WriteLine($"Registering domains: {string.Join(", ", certRequest.Domains)}");

        foreach (var dom in certRequest.Domains)
        {
            if (!await CheckServer.Check(dom))
            {
                Error.WriteLine("Domain {dom} not prepared for Let's Encrypt");
                return;
            }
        }

        var order = await acmeContext.NewOrder(certRequest.Domains);
        var authorizations = (await order.Authorizations()).ToArray();
        foreach (var authorization in authorizations)
            await ValidateAsync(authorization);

        WriteLine($"Ordering certificate");
        var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var cert = await order.Generate(new CsrInfo
        {
            CountryName = certRequest.Data.CountryName,
            State = certRequest.Data.State,
            Locality = certRequest.Data.Locality,
            Organization = certRequest.Data.Organization,
            OrganizationUnit = certRequest.Data.OrganizationUnit,
            CommonName = certRequest.Data.CommonName,
        }, privateKey);

        WriteLine($"Creating certificate");
        var certPem = cert.ToPem();
        var pfxBuilder = cert.ToPfx(privateKey);

        var passwd = GetPfxPassword();
        var pfx = pfxBuilder.Build(certRequest.Data.CommonName, passwd);
        WriteLine($"Saving certificate");
        File.WriteAllBytes(certificateFile, pfx);
    }
}
catch (Exception e)
{
    Error.WriteLine($"Exception: {e}");
}
finally
{
    WriteLine("Letsencrypt certificate handling finished");
}


async Task CreateAccountAsync(bool staging)
{
    WriteLine("Creating letsencrypt account");

    var certRequest = ReadRequest("cert.json");
    if (certRequest == null)
    {
        Error.WriteLine("Could not create account from cert.json");
        return;
    }

    var fileInfo = new FileInfo(certRequestFile);
    if (fileInfo.Directory?.Exists != true)
        Directory.CreateDirectory(fileInfo.DirectoryName ?? "");

    File.Copy("cert.json", certRequestFile, true);
    var acmeContext = new AcmeContext(staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2);
    account = await acmeContext.NewAccount(certRequest.Account, true);
    var pemKey = acmeContext.AccountKey.ToPem();
    var fi = new FileInfo(accountFile);
    Directory.CreateDirectory(fi.DirectoryName ?? "");
    await File.WriteAllTextAsync(accountFile, pemKey);     
    WriteLine("Letsencrypt account created");
}

async Task<(AcmeContext, CertRequest?)> ReadAccountAsync(bool staging)
{
    WriteLine("Reading letsencrypt account");
    var pemKey = await File.ReadAllTextAsync(accountFile);
    var accountKey = KeyFactory.FromPem(pemKey);
    var acmeContext = new AcmeContext(staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2, accountKey);
    account = await acmeContext.Account();                 
    WriteLine("Letsencrypt account read");
    return (acmeContext, ReadRequest(certRequestFile));
}

void DeleteAccount()
{
    WriteLine("Deleting letsencrypt account");
    try 
    {
        File.Delete(certRequestFile);
    }
    catch {}
    try 
    {
        File.Delete(accountFile);
    }
    catch {}
    WriteLine("Letsencrypt account deleted");
}

CertRequest? ReadRequest(string requestFile)
{
    using var file = File.OpenRead(requestFile);
    return JsonSerializer.Deserialize<CertRequest>(file, Json.Defaults);
}

async Task ValidateAsync(IAuthorizationContext authorization)
{
    string token = "-";
    try
    {
        var httpChallenge = await authorization.Http();
        var keyAuthz = httpChallenge.KeyAuthz;
        token = httpChallenge.Token;
        WriteLine($"Validating LetsEncrypt token: {token}");
        await File.WriteAllTextAsync(Path.Combine(encryptDirectory, token), keyAuthz);

        while (true)
        {
            var challenge = await httpChallenge.Validate();
            WriteLine($"Challenge: {challenge.Error}, {challenge.Status} {challenge.Validated}");
            if (challenge.Status == Certes.Acme.Resource.ChallengeStatus.Invalid)
            {
                WriteLine($"Could not validate LetsEncrypt token: {token}");
                throw new Exception("Not valid");
            }
            if (challenge.Status == Certes.Acme.Resource.ChallengeStatus.Valid)
                break;
            await Task.Delay(2000);
        }
    }
    finally
    {
        try
        {
            File.Delete(Path.Combine(encryptDirectory, token));
        }
        catch { }
    }
}

string InitGetPfxPassword()
    => (OperatingSystem.IsLinux()
        ? "/etc"
        : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData))
        ?.AppendPath("letsencrypt-uweb")
        ?.ReadAllTextFromFilePath()
        ?.Trim() 
        ?? "".SideEffect(_ => WriteLine("!!!NO PASSWORD!!"));

