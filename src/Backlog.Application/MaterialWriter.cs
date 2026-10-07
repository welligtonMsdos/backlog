using Backlog.Domain;
using System.Text;

namespace Backlog.Application;

public static class MaterialWriter
{
    public const int MaxFiles = 5;

    public const int MaxFileBytes = 5 * 1024 * 1024;

    public static readonly string[] AllowedTypes = ["application/pdf", "image/png", "image/jpeg", "text/plain", "text/csv"];

    public static void Add(StageEntry stage, TaskMaterials materials, Actor actor, DateTimeOffset now)
    {
        if (materials.Note?.Length > 4000)
        {
            throw new DomainException(ErrorKind.Invalid, "Observação excede 4000 caracteres.");
        }

        if (stage.Attachments.Count + materials.Files.Count > MaxFiles)
        {
            throw new DomainException(ErrorKind.Invalid, "Limite de cinco arquivos por passagem.");
        }

        foreach (var file in materials.Files)
        {
            ValidateFile(file);
        }

        stage.AddNote(materials.Note, actor.Id, now);

        foreach (var file in materials.Files)
        {
            stage.AddAttachment(new StageAttachment(stage.Id, actor.Id, file.Name, file.ContentType, file.Content, now));
        }
    }

    public static void ValidateFile(UploadedFile file)
    {
        if (file.Content.Length > MaxFileBytes)
        {
            throw new DomainException(ErrorKind.TooLarge, "Arquivo excede 5 MiB.");
        }

        if (file.Content.Length == 0 || string.IsNullOrWhiteSpace(file.Name) || file.Name.Length > 200 ||
            file.Name.IndexOfAny(['/', '\\', '\r', '\n', '\0']) >= 0 || !AllowedTypes.Contains(file.ContentType))
        {
            throw new DomainException(ErrorKind.Invalid, "Nome ou tipo de arquivo inválido.");
        }

        var content = file.Content.AsSpan();

        var valid = file.ContentType switch
        {
            "application/pdf" => content.StartsWith("%PDF-"u8),
            "image/png" => content.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => content.StartsWith(new byte[] { 255, 216, 255 }),
            _ => ValidText(file.Content)
        };

        if (!valid)
        {
            throw new DomainException(ErrorKind.Invalid, "Conteúdo incompatível com o tipo informado.");
        }
    }

    private static bool ValidText(byte[] content)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(content);

            return !text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
