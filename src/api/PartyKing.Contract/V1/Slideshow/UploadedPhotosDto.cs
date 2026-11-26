namespace PartyKing.Contract.V1.Slideshow;

/// <summary>What an upload did: how many photos were stored and why each of the others was not (it names the file).</summary>
public sealed record UploadedPhotosDto(int Stored, IReadOnlyList<string> Rejected);
