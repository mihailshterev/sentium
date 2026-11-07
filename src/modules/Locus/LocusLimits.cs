namespace Sentium.Locus;

/// <summary>
/// Field limits shared by the EF model, the request validators and the portal's form schemas.
/// </summary>
public static class LocusLimits
{
    public const int LocationNameMax = 100;
    public const int LocationTextMax = 1000;
    public const int MaxLocationDepth = 12;

    public const int AssetNameMax = 200;
    public const int CategoryMax = 100;
    public const int DescriptionMax = 2000;
    public const int InstructionsMax = 4000;
    public const int ManufacturerMax = 200;
    public const int IdentifierMax = 100;
    public const int WarrantyMax = 2000;
    public const int AgentInstructionsMax = 2000;

    public const int TagMax = 32;
    public const int MaxTagsPerAsset = 20;

    public const int SearchQueryMax = 200;
    public const int MaxSearchTerms = 8;

    public const int FileNameMax = 255;
    public const long MaxAttachmentBytes = 25L * 1024 * 1024;
    public const int MaxAttachmentsPerAsset = 30;

    public const int UserNameMax = 256;
    public const int SyncErrorMax = 1024;
}
