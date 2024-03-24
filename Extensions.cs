using Certes;
using Certes.Acme;
using CsTools;
using CsTools.Extensions;
using CsTools.Functional;

using static CsTools.Core;

static class Extensions
{
    public static FileInfo[] GetFiles(this string path)
        => new DirectoryInfo(path).GetFiles();

    public static string? CheckIfFileExists(this string path)
        => File.Exists(path)
            ? path
            : null;

    public static AsyncResult<IOrderContext, Unit> CreateNewOrder(this AcmeContext context, Task<string[]>? identifiersTask)
    {
        return
            (identifiersTask != null 
            ? CreateNewOrder(identifiersTask)
            : Error<IOrderContext, Unit>(Unit.Value).ToAsync())
                .ToAsyncResult();

        async Task<Result<IOrderContext, Unit>> CreateNewOrder(Task<string[]> identifiersTask)
        {
            var identifiers = await identifiersTask;
            return identifiers.Length > 0
                ? Ok<IOrderContext, Unit>(await context.NewOrder(identifiers))
                : Error<IOrderContext, Unit>(Unit.Value);
        }
    }
}    
 