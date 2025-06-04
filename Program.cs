using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using CsTools.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using static CsTools.Functional.Memoization;

// Parameter: -prod: productive, without: staging (test)
// Parameter: -del: delete account
// Parameter: -create: read file cert.json

// To build: 
// dotnet publish -c Release

string encryptDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "letsencrypt-uweb");
//const string encryptDirectory = "/etc/letsencrypt-uweb";

// TODO modernize
// TODO eliminate Newtonsoft
// TODO check mode for WebServer

string certRequestFile;
AcmeContext acmeContext = null;
IAccountContext account;
string accountFile;

Func<string> GetPfxPassword = Memoize(InitGetPfxPassword);

try
{
    Console.WriteLine($"Starting letsencrypt certificate handling");

    bool staging = !args.Contains("-prod");
    bool deleteAccount = args.Contains("-del");
    bool createAccount = args.Contains("-create");
    Console.WriteLine(staging ? "Staging" : "!!! P R O D U C T I V E !!!");

    var certificateFile = Path.Combine(encryptDirectory, $"certificate{(staging ? "-staging" : "")}.pfx");
    accountFile = Path.Combine(encryptDirectory, $"access{(staging ? "-staging" : "")}.pem");
    certRequestFile = Path.Combine(encryptDirectory, $"cert{(staging ? "-staging" : "")}.json");

    CertRequest certRequest = null;
    if (deleteAccount)
    {
        DeleteAccount();
        return;
    }
    else if (createAccount)
        certRequest = await CreateAccountAsync(staging);
    else
    {
        if (File.Exists(certificateFile))
        {
            var certificate = new X509Certificate2(certificateFile, GetPfxPassword());

            Console.WriteLine($"Certificate expires: {certificate.NotAfter}");
            if (certificate.NotAfter > DateTime.Now + TimeSpan.FromDays(30))
            {
                Console.WriteLine("No further action needed");
                return;
            }
        }
        certRequest = await ReadAccountAsync(staging);
    }

    Console.WriteLine($"Registering domains: {String.Join(", ", certRequest.Domains)}");

    var order = await acmeContext.NewOrder(certRequest.Domains);
    var authorizations = (await order.Authorizations()).ToArray();
    foreach (var authorization in authorizations)
        await ValidateAsync(authorization);

    Console.WriteLine($"Ordering certificate");
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

    Console.WriteLine($"Creating certificate");
    var certPem = cert.ToPem();
    var pfxBuilder = cert.ToPfx(privateKey);

    //var passwordFile = Path.Combine(encryptDirectory, $"passwd{(staging ? "-staging" : "")}");
    //var passwd = Guid.NewGuid().ToString();
    // File.WriteAllText(passwordFile, passwd);
    var passwd = GetPfxPassword();
    var pfx = pfxBuilder.Build(certRequest.Data.CommonName, passwd);
    Console.WriteLine($"Saving certificate");
    File.WriteAllBytes(certificateFile, pfx);
}
catch (Exception e)
{
    Console.Error.WriteLine($"Exception: {e}");
}
finally
{
    Console.WriteLine("Letsencrypt certificate handling finished");
}


async Task<CertRequest> CreateAccountAsync(bool staging)
{
    Console.WriteLine("Creating letsencrypt account");

    var certRequest = ReadRequest("cert.json");

    var fileInfo = new FileInfo(certRequestFile);
    if (!fileInfo.Directory.Exists)
        Directory.CreateDirectory(fileInfo.DirectoryName);

    File.Copy("cert.json", certRequestFile, true);
    acmeContext = new AcmeContext(staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2);
    account = await acmeContext.NewAccount(certRequest.Account, true);
    var pemKey = acmeContext.AccountKey.ToPem();
    var fi = new FileInfo(accountFile);
    Directory.CreateDirectory(fi.DirectoryName);
    await File.WriteAllTextAsync(accountFile, pemKey);     
    Console.WriteLine("Letsencrypt account created");
    return certRequest;
}

async Task<CertRequest> ReadAccountAsync(bool staging)
{
    Console.WriteLine("Reading letsencrypt account");
    var pemKey = await File.ReadAllTextAsync(accountFile);
    var accountKey = KeyFactory.FromPem(pemKey);
    acmeContext = new AcmeContext(staging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2, accountKey);
    account = await acmeContext.Account();                 
    Console.WriteLine("Letsencrypt account read");
    return ReadRequest(certRequestFile);
}

void DeleteAccount()
{
    Console.WriteLine("Deleting letsencrypt account");
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
    Console.WriteLine("Letsencrypt account deleted");
}

CertRequest ReadRequest(string requestFile)
{
    using var file = File.OpenText(requestFile);
    var serializer = new JsonSerializer{
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        DefaultValueHandling = DefaultValueHandling.Ignore
    };
    return serializer.Deserialize(file, typeof(CertRequest)) as CertRequest;
}

async Task ValidateAsync(IAuthorizationContext authorization)
{
    string token = "-";
    try
    {
        var httpChallenge = await authorization.Http();
        var keyAuthz = httpChallenge.KeyAuthz;
        token = httpChallenge.Token;
        Console.WriteLine($"Validating LetsEncrypt token: {token}");
        await File.WriteAllTextAsync(Path.Combine(encryptDirectory, token), keyAuthz);

        while (true)
        {
            var challenge = await httpChallenge.Validate();
            Console.WriteLine($"Challenge: {challenge.Error}, {challenge.Status} {challenge.Validated}");
            if (challenge.Status == Certes.Acme.Resource.ChallengeStatus.Invalid)
            {
                Console.WriteLine($"Could not validate LetsEncrypt token: {token}");
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
        ?? "".SideEffect(_ => Console.WriteLine("!!!NO PASSWORD!!"));

