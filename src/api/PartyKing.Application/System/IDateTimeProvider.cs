namespace PartyKing.Application.System;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

internal class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}