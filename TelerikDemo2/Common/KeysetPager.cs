using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace TelerikDemo2.Common;

// NextCursor is null on the last page.
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string NextCursor);

// Keyset pagination: a page is "the rows after the last one you saw", expressed as a WHERE on
// (sort column, id) instead of OFFSET. Cost doesn't grow with depth and there is no COUNT, but you
// can only go to the next/previous page. Sorts on one column with the unique id as tie-break;
// sort values must not be null.
public static class KeysetPager
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int NormalizePageSize(int requested) =>
        requested <= 0 ? DefaultPageSize : Math.Clamp(requested, 1, MaxPageSize);

    public static async Task<CursorPage<T>> ToPageAsync<T>(
        IQueryable<T> source, string sortProperty, string idProperty, bool descending,
        string cursor, int pageSize, CancellationToken ct = default)
    {
        pageSize = NormalizePageSize(pageSize);
        var rows = await Apply(source, sortProperty, idProperty, descending, cursor, pageSize).ToListAsync(ct);
        return ToPage(rows, sortProperty, idProperty, descending, pageSize);
    }

    // Fetches pageSize + 1 rows so ToPage can tell whether there is a next page.
    public static IQueryable<T> Apply<T>(
        IQueryable<T> source, string sortProperty, string idProperty, bool descending, string cursor, int pageSize)
    {
        if (Decode(cursor) is { } c && c.Sort == sortProperty && c.Desc == descending)
        {
            var predicate = After<T>(sortProperty, idProperty, descending, c);
            if (predicate is not null)
            {
                source = source.Where(predicate);
            }
        }

        var ordered = OrderBy(source, sortProperty, descending, first: true);
        if (sortProperty != idProperty)
        {
            ordered = OrderBy(ordered, idProperty, descending, first: false);
        }
        return ordered.Take(pageSize + 1);
    }

    public static CursorPage<T> ToPage<T>(
        IReadOnlyList<T> rowsPlusOne, string sortProperty, string idProperty, bool descending, int pageSize)
    {
        if (rowsPlusOne.Count <= pageSize)
        {
            return new CursorPage<T>(rowsPlusOne, null);
        }

        var items = rowsPlusOne.Take(pageSize).ToList();
        var last = items[^1];
        var sortValue = typeof(T).GetProperty(sortProperty)!.GetValue(last);
        var id = (int)typeof(T).GetProperty(idProperty)!.GetValue(last)!;
        return new CursorPage<T>(items, Encode(new Cursor(sortProperty, descending, JsonSerializer.SerializeToElement(sortValue), id)));
    }


    private sealed record Cursor(string Sort, bool Desc, JsonElement Value, int Id);

    private static string Encode(Cursor cursor) =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(cursor));

    private static Cursor Decode(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            return JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(token));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null; // bad cursor: start from the first page
        }
    }


    // desc: (sort < v) OR (sort == v AND id < lastId); asc uses >.
    private static Expression<Func<T, bool>> After<T>(string sortProperty, string idProperty, bool descending, Cursor c)
    {
        var x = Expression.Parameter(typeof(T), "x");
        var idMember = Expression.Property(x, idProperty);
        var idAfter = Compare(idMember, Constant(c.Id, idMember.Type), descending);

        if (sortProperty == idProperty)
        {
            return Expression.Lambda<Func<T, bool>>(idAfter, x);
        }

        var sortMember = Expression.Property(x, sortProperty);
        object value;
        try
        {
            value = c.Value.Deserialize(sortMember.Type);
        }
        catch (JsonException)
        {
            return null;
        }
        if (value is null) return null;

        var sortAfter = Compare(sortMember, Constant(value, sortMember.Type), descending);
        var sortEqual = Expression.Equal(sortMember, Constant(value, sortMember.Type));
        var body = Expression.OrElse(sortAfter, Expression.AndAlso(sortEqual, idAfter));
        return Expression.Lambda<Func<T, bool>>(body, x);
    }

    private static Expression Compare(Expression left, Expression right, bool less)
    {
        var underlying = Nullable.GetUnderlyingType(left.Type) ?? left.Type;

        if (underlying == typeof(string))
        {
            var compare = Expression.Call(typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string) })!, left, right);
            return less
                ? Expression.LessThan(compare, Expression.Constant(0))
                : Expression.GreaterThan(compare, Expression.Constant(0));
        }

        if (underlying.IsEnum)
        {
            // Expression.LessThan is not defined for enums: compare their integer values.
            var intType = left.Type == underlying ? typeof(int) : typeof(int?);
            left = Expression.Convert(left, intType);
            right = Expression.Convert(right, intType);
        }

        return less ? Expression.LessThan(left, right) : Expression.GreaterThan(left, right);
    }

    // Wrapping the value in an object member makes EF send it as a SQL parameter instead of inlining a literal.
    private sealed class Box<TValue>(TValue value)
    {
        public TValue Value { get; } = value;
    }

    private static Expression Constant(object value, Type type)
    {
        var box = Activator.CreateInstance(typeof(Box<>).MakeGenericType(type), value)!;
        return Expression.Property(Expression.Constant(box), nameof(Box<int>.Value));
    }

    private static IOrderedQueryable<T> OrderBy<T>(IQueryable<T> source, string property, bool descending, bool first)
    {
        var x = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(x, property);
        var method = first
            ? (descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
            : (descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

        var call = Expression.Call(typeof(Queryable), method, new[] { typeof(T), member.Type },
            source.Expression, Expression.Quote(Expression.Lambda(member, x)));
        return (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
    }
}
