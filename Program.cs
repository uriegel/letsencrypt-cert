using CsTools.Extensions;
using CsTools.Functional;

using static System.Console;
using static AspNetExtensions.LetsEncrypt; 

WriteLine("Starting letsencrypt certificate handling");

if (string.IsNullOrEmpty(GetPfxPassword()))
    throw new Exception();

await (Parameters.Get() switch
{
    { Staging: true, Mode: OperationMode.Create } => 1.ToAsync().SideEffectAwait(_ => Account.Create()),
    _ when !Certificate.CheckValidationTime() => 2.ToAsync().SideEffectAwait( _ => Perform()),
    _ => 3.ToAsync().SideEffectAsync(_ => WriteLine("No further action needed"))
})
    .SideEffectAsync( _ => DeleteAllTokens());

WriteLine("Letsencrypt certificate handling finished");

static void DeleteAllTokens() 
    => GetEncryptDirectory()
        .GetFiles()
        .Where(n => string.IsNullOrEmpty(n.Extension))
        .ForEach(n => File.Delete(n.FullName));

static Task Perform()
    => Account
        .Get()
        .BindAwait(c => c.CreateNewOrder(
                                Account
                                    .ReadRequest()
                                    ?.Domains
                                    ?.SideEffectForAll(d => WriteLine($"Registering domain: {d}"))
                                    ?.ToAsyncEnumerable()
                                    ?.WhereAwait(async d => await HttpChecker.Check(d))
                                    ?.ToArrayAwait()))
        .SelectError(_ => "")
        .BindAwait(Authorizations.ValidateAll)
        .SelectAwait(Certificate.Order)
        .ToResult()
        .SideEffectAsync(t => t.Match(
                            _ => WriteLine("Certificate successfully retrieved"),
                            e => WriteLine($"An error has occurred: {e}")));
                            
                
                
                


