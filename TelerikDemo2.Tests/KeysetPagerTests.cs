using TelerikDemo2.Common;
using Xunit;

namespace TelerikDemo2.Tests;

public class KeysetPagerTests
{
    public enum Kind { A = 0, B = 1, C = 2 }
    public record Row(int Id, string Name, decimal Amount, DateTime Date, Kind Kind);

    // 57 rows with lots of ties on every sortable column, in shuffled insertion order.
    private static readonly List<Row> Data = Enumerable.Range(1, 57)
        .Select(i => new Row(i, "Name" + (i % 5), (i % 7) * 10m, new DateTime(2026, 1, 1).AddDays(i % 4), (Kind)(i % 3)))
        .OrderBy(r => (r.Id * 31) % 57)
        .ToList();

    private static List<int> WalkAll(string sort, bool desc, int pageSize, out int pages)
    {
        var ids = new List<int>();
        string cursor = null;
        pages = 0;
        do
        {
            var q = KeysetPager.Apply(Data.AsQueryable(), sort, nameof(Row.Id), desc, cursor, pageSize);
            var page = KeysetPager.ToPage(q.ToList(), sort, nameof(Row.Id), desc, pageSize);
            ids.AddRange(page.Items.Select(r => r.Id));
            cursor = page.NextCursor;
            pages++;
        } while (cursor is not null);
        return ids;
    }

    [Theory]
    [InlineData(nameof(Row.Id), false)]
    [InlineData(nameof(Row.Id), true)]
    [InlineData(nameof(Row.Name), false)]
    [InlineData(nameof(Row.Name), true)]
    [InlineData(nameof(Row.Amount), false)]
    [InlineData(nameof(Row.Amount), true)]
    [InlineData(nameof(Row.Date), true)]
    [InlineData(nameof(Row.Kind), false)]
    [InlineData(nameof(Row.Kind), true)]
    public void Walking_every_page_returns_every_row_once_in_sorted_order(string sort, bool desc)
    {
        var ids = WalkAll(sort, desc, pageSize: 10, out var pages);

        Assert.Equal(6, pages);                        // 57 rows / 10 per page
        Assert.Equal(57, ids.Count);
        Assert.Equal(57, ids.Distinct().Count());      // no duplicates, no gaps despite the ties

        var expected = desc
            ? Data.AsQueryable().OrderByDescending(r => typeof(Row).GetProperty(sort)!.GetValue(r)).ThenByDescending(r => r.Id)
            : Data.AsQueryable().OrderBy(r => typeof(Row).GetProperty(sort)!.GetValue(r)).ThenBy(r => r.Id);
        // string.Compare (culture) and ordinal can differ in theory; the data here is ASCII so they agree.
        Assert.Equal(expected.Select(r => r.Id), ids);
    }

    [Fact]
    public void Last_page_has_no_next_cursor_and_exact_multiple_does_not_add_an_empty_page()
    {
        WalkAll(nameof(Row.Id), false, pageSize: 19, out var pages);   // 57 = 3 x 19
        Assert.Equal(3, pages);
    }

    [Fact]
    public void Cursor_from_a_different_sort_is_ignored_and_garbage_starts_at_page_one()
    {
        var first = KeysetPager.ToPage(
            KeysetPager.Apply(Data.AsQueryable(), nameof(Row.Amount), nameof(Row.Id), false, null, 10).ToList(),
            nameof(Row.Amount), nameof(Row.Id), false, 10);

        var wrongSort = KeysetPager.Apply(Data.AsQueryable(), nameof(Row.Name), nameof(Row.Id), false, first.NextCursor, 10).ToList();
        var garbage = KeysetPager.Apply(Data.AsQueryable(), nameof(Row.Id), nameof(Row.Id), false, "not-a-cursor", 10).ToList();

        Assert.Equal(Data.OrderBy(r => r.Name).ThenBy(r => r.Id).First().Id, wrongSort[0].Id);
        Assert.Equal(1, garbage[0].Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(10, 10)]
    [InlineData(100000, 100)]
    public void Page_size_is_clamped(int requested, int expected) =>
        Assert.Equal(expected, KeysetPager.NormalizePageSize(requested));
}
