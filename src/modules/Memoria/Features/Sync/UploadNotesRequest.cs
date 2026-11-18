namespace Sentium.Memoria.Features.Sync;

internal sealed record UploadNotesRequest(IReadOnlyList<NoteUpload> Notes);
