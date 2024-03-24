using Certes.Acme;
using CsTools.Extensions;

using static System.Console;
using static CsTools.Functional.Memoization;
using static AspNetExtensions.LetsEncrypt; 

record Parameters(
    bool Staging,
    OperationMode Mode
) {
    public static Func<Parameters> Get { get; }
        = Memoize(Init);

    public static Func<string> GetPfxFile { get; }
        = Memoize(InitGetPfxFile);

    public static Func<string> GetCertFile { get; }
        = Memoize(InitGetCertFile);

    public static Func<string> GetAccountFile { get; }
        = Memoize(InitGetAccountFile);

    public static Func<Uri> GetAcmeUri { get; }
        = Memoize(InitGetAcmeUri);

    static Parameters Init()
        => Environment.GetCommandLineArgs()
            .Pipe(args => new Parameters(
                args
                    .Contains("-prod")
                    .SideEffect(n => WriteLine(n ? "!!! P R O D U C T I V E !!!" : "Staging"))
                    != true,
                (args.Contains("-create"), args.Contains("-del")) switch
                {
                    (true, false) => OperationMode.Create,
                    (false, true) => OperationMode.Delete,
                    _ => OperationMode.Operate
                })
                    .SideEffect(WriteLine)
            );

    static string InitGetPfxFile()
        => GetEncryptDirectory()
            .AppendPath(Get().Staging ? "certificate-staging.pfx" : "certificate.pfx");

    static string InitGetCertFile()
        => GetEncryptDirectory()
            .AppendPath("cert.json");
            
    static string InitGetAccountFile()
        => GetEncryptDirectory()
            .AppendPath(Get().Staging ? "account-staging.pem" : "account.pem");

    static Uri InitGetAcmeUri()
        => Get().Staging
            ? WellKnownServers.LetsEncryptStagingV2
            : WellKnownServers.LetsEncryptV2;
}