using System.Security.Cryptography.X509Certificates;
using AspNetExtensions;
using Certes;
using Certes.Acme;
using CsTools;
using CsTools.Extensions;

using static System.Console;

static class Certificate
{
    public static bool CheckValidationTime()
        => Parameters
            .GetPfxFile()
            .Pipe(p =>
                File.Exists(p)
                ? p
                : null)
            ?.Pipe(p => new X509Certificate2(p, LetsEncrypt.GetPfxPassword())
                            .SideEffect(c => WriteLine($"Certificate expires: {c.NotAfter}")))
            ?.Pipe(c => c.NotAfter > DateTime.Now + TimeSpan.FromDays(30)) == true;


    public static async Task<Unit> Order(IOrderContext order)
    {
        WriteLine("Ordering certificate");

        var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var cert = await order.Generate(new CsrInfo
        {
            CountryName = Account.ReadRequest()!.Data.CountryName,
            State = Account.ReadRequest()!.Data.State,
            Locality = Account.ReadRequest()!.Data.Locality,
            Organization = Account.ReadRequest()!.Data.Organization,
            OrganizationUnit = Account.ReadRequest()!.Data.OrganizationUnit,
            CommonName = Account.ReadRequest()!.Data.CommonName
        }, privateKey);

        WriteLine("Creating certificate");
        var certPem = cert.ToPem();
        var certKey = privateKey.ToPem();
        var x509 = X509Certificate2.CreateFromPem(certPem.ToCharArray(), certKey.ToCharArray());
        var pfxBytes = x509.Export(X509ContentType.Pfx, LetsEncrypt.GetPfxPassword());
        WriteLine("Saving certificate");
        File.WriteAllBytes(Parameters.GetPfxFile(), pfxBytes);
        return Unit.Value;
    }
}



