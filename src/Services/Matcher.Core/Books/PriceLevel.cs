namespace Matcher.Core.Books;

internal readonly struct PriceLevel(long price, int quantity)
{
    public long Price { get; } = price;
    public int Quantity { get; } = quantity;
}