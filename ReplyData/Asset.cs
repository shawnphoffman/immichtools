namespace ImmichTools.ReplyData;

internal class Asset
{
    public required string Id { get; set; }

    public required string OriginalFileName { get; set; }

    public required string OriginalPath { get; set; }

    public required DateTime LocalDateTime { get; set; }

    public required ExifInfo ExifInfo { get; set; }
}
