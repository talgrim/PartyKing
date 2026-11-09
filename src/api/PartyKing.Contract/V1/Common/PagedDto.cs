namespace PartyKing.Contract.V1.Common;

/// <summary>One page of a list: the page asked for (1-based), the size actually used and how many items there are in all.</summary>
public record PagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
