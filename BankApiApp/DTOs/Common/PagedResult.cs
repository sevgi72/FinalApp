namespace BankApiApp.DTOs.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }       // umumi element sayi
    public int TotalPages { get; set; }       // umumi sehife sayi
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}