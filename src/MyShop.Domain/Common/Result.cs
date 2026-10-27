namespace MyShop.Domain.Common;

public abstract record Result<T>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(Error Error) : Result<T>;

    public bool IsSuccess => this is Success;

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) => this switch
    {
        Success s => onSuccess(s.Value),
        Failure f => onFailure(f.Error),
        _ => throw new InvalidOperationException("Unreachable")
    };

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        Match(v => Result.Ok(map(v)), Result.Fail<TOut>);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        Match(bind, Result.Fail<TOut>);

    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> bind) =>
        Match(bind, e => Task.FromResult(Result.Fail<TOut>(e)));

    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        Bind(v => predicate(v) ? this : Result.Fail<T>(error));

    public Result<T> Tap(Action<T> action) =>
        Map(v => { action(v); return v; });

    public static implicit operator Result<T>(T value) => new Success(value);
    public static implicit operator Result<T>(Error error) => new Failure(error);
}

public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Success(value);
    public static Result<T> Fail<T>(Error error) => new Result<T>.Failure(error);

    public static Result<T> FromNullable<T>(T? value, Error whenNull) where T : class =>
        value is null ? Fail<T>(whenNull) : Ok(value);

    /// <summary>Turns a sequence of results into a result of a sequence, short-circuiting on the first failure.</summary>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results) =>
        results.Aggregate(
            Ok<IReadOnlyList<T>>([]),
            (acc, next) => acc.Bind(list => next.Map(IReadOnlyList<T> (v) => [.. list, v])));

    public static async Task<Result<TOut>> Map<T, TOut>(this Task<Result<T>> task, Func<T, TOut> map) =>
        (await task).Map(map);

    public static async Task<Result<TOut>> Bind<T, TOut>(this Task<Result<T>> task, Func<T, Result<TOut>> bind) =>
        (await task).Bind(bind);

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> task, Func<T, Task<Result<TOut>>> bind) =>
        await (await task).BindAsync(bind);

    // LINQ query syntax support: `from a in r1 from b in f(a) select g(a, b)`.
    public static Result<TOut> Select<T, TOut>(this Result<T> result, Func<T, TOut> map) => result.Map(map);

    public static Result<TOut> SelectMany<T, TMid, TOut>(
        this Result<T> result, Func<T, Result<TMid>> bind, Func<T, TMid, TOut> project) =>
        result.Bind(t => bind(t).Map(m => project(t, m)));
}
