using Certes;
using Certes.Acme;
using CsTools;
using CsTools.Async;
using CsTools.Extensions;
using CsTools.Functional;

using static System.Console;
using static CsTools.Core;
using static AspNetExtensions.LetsEncrypt; 

static class Authorizations
{
    public static AsyncResult<IOrderContext, string> ValidateAll(IOrderContext orderContext)
        => orderContext
            .Authorizations()
            .AsAsyncEnumerable()
            .AggregateAwaitAsync(
                Ok<Unit, string>(Unit.Value), 
                (acc, c) => Validate(acc, c).ToValueTask())
            .FromValueTask()
            .Select(_ => orderContext);

    static ValueTask<Result<T, E>> ToValueTask<T, E>(this AsyncResult<T, E> ae) 
        where T: notnull
        where E: notnull
        => new (ae.ToResult());

    static AsyncResult<T, E> FromValueTask<T, E>(this ValueTask<Result<T, E>> vt)
        where T: notnull
        where E: notnull
    {
        static async Task<Result<T, E>> FromValueTask(ValueTask<Result<T, E>> vt)
            => await vt;
        return FromValueTask(vt).ToAsyncResult();
    }
    static AsyncResult<Unit, string> Validate(Result<Unit, string> state, IAuthorizationContext auth) 
        => state
            .ToAsyncResult()
            .BindAwait(_ => Validate(auth));

    static AsyncResult<Unit, string> Validate(IAuthorizationContext auth) 
    {
        AsyncResult<Unit, string> Validate() 
            => auth
                .Http()
                .Select(Ok<IChallengeContext, string>)
                .ToAsyncResult()
                .SideEffectWhenOk(WriteKeyTokenFile)
                .BindAwait(ValidateChallenge);
        return AsyncResultExtensions.RepeatOnError(Validate, 7, TimeSpan.FromSeconds(3));
    }
    
    static AsyncResult<Unit, string> ValidateChallenge(IChallengeContext challenge)
        => challenge
            .Validate()
            .SideEffectAsync(c => WriteLine($"Challenge: {c.Error}, {c.Status}, {c.Validated}"))
            .Select(c =>
                c.Status.HasValue
                ? c.Status.Value == Certes.Acme.Resource.ChallengeStatus.Invalid
                    ? Error<Unit, string>("not valid".SideEffect(_ => WriteLine($"Could not validate LetsEncrypt token: {c.Token}")))
                    : c.Status.Value == Certes.Acme.Resource.ChallengeStatus.Valid
                    ? Ok<Unit, string>(Unit.Value)
                    : Error<Unit, string>("unknown")
                : Error<Unit, string>("unknown"))
            .ToAsyncResult();

    static void WriteKeyTokenFile(IChallengeContext challenge)
        => GetEncryptDirectory()
            .AppendPath(challenge
                            .Token
                            .SideEffect(t => WriteLine($"Validating LetsEncrypt token: {t}")))
            .WriteAllTextToFilePath(challenge.KeyAuthz);
}

