using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;

namespace DeliveryNoteOcr.Api.Services.Extraction;

public class AzureDocumentIntelligenceOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-layout";
    public string CustomModelId { get; set; } = string.Empty;
}

public class AzureDocumentIntelligenceExtractor : IDocumentExtractor
{
    private static readonly Regex ProjectNumberRegex =
        new(@"\bPR\d{2}-\d{3,5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DateRegex =
        new(@"\b(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{2,4})\b", RegexOptions.Compiled);

    private static readonly Regex DeliveryNoteLabelRegex = new(
        @"(?:Lieferschein(?:-|\s)*Nr\.?|Liefer\s*Nr\.?|LS\s*-?\s*Nr\.?|Leistungsnachweis\s*Nr\.?)[\s:\-#\/]*(\d[\d\s\-\/]{3,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LseRegex = new(
        @"\bLSE\s*(\d{5,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AzureDocumentIntelligenceOptions _options;
    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<AzureDocumentIntelligenceExtractor> _logger;

    public AzureDocumentIntelligenceExtractor(
        IOptions<AzureDocumentIntelligenceOptions> options,
        ILogger<AzureDocumentIntelligenceExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException(
                "DocumentIntelligence:Endpoint and DocumentIntelligence:ApiKey must be configured.");

        _client = new DocumentIntelligenceClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));
    }

    public async Task<ExtractionResult> ExtractAsync(
        Stream pdfStream, string originalFileName, CancellationToken ct)
    {
        var modelId = !string.IsNullOrWhiteSpace(_options.CustomModelId)
            ? _options.CustomModelId
            : _options.ModelId;

        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        _logger.LogInformation("Analyzing {File} with model {ModelId}", originalFileName, modelId);

        var op = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            modelId,
            BinaryData.FromStream(ms),
            cancellationToken: ct);

        var analyzeResult = op.Value;

        var rawJson = SerializeSafely(analyzeResult);

        var result = new ExtractionResult
        {
            ModelIdUsed = modelId,
            RawResponseJson = rawJson
        };

        if (TryExtractFromCustomFields(analyzeResult, result))
            return result;

        ExtractFromLayoutText(analyzeResult, result);
        return result;
    }

    private static bool TryExtractFromCustomFields(AnalyzeResult analyzeResult, ExtractionResult result)
    {
        if (analyzeResult.Documents is null || analyzeResult.Documents.Count == 0)
            return false;

        var doc = analyzeResult.Documents[0];
        if (doc.Fields is null || doc.Fields.Count == 0)
            return false;

        result.DeliveryNoteNo = FieldOrDefault(doc.Fields, "delivery_note_no", "DeliveryNoteNo");
        result.ProjectNumber = FieldOrDefault(doc.Fields, "project_number", "ProjectNumber");
        result.DeliveryDate = FieldOrDefault(doc.Fields, "delivery_date", "DeliveryDate");
        result.Assignee = FieldOrDefault(doc.Fields, "assignee", "Assignee");
        result.SupplierName = FieldOrDefault(doc.Fields, "supplier_name", "SupplierName");
        result.Site = FieldOrDefault(doc.Fields, "site", "Site");
        result.CostCentre = FieldOrDefault(doc.Fields, "cost_centre", "CostCentre");
        return true;
    }

    private static ExtractedField FieldOrDefault(
        IReadOnlyDictionary<string, DocumentField> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var f) && f is not null)
            {
                var value = f.Content ?? f.ValueString;
                return new ExtractedField(value, f.Confidence);
            }
        }
        return new ExtractedField(null, null);
    }

    private static void ExtractFromLayoutText(AnalyzeResult analyzeResult, ExtractionResult result)
    {
        var content = analyzeResult.Content ?? string.Empty;

        var project = ProjectNumberRegex.Match(content);
        if (project.Success)
            result.ProjectNumber = new ExtractedField(project.Value.ToUpperInvariant(), null);

        var deliveryNoteNo = FindDeliveryNoteNo(content);
        if (deliveryNoteNo is not null)
            result.DeliveryNoteNo = new ExtractedField(deliveryNoteNo, null);

        var dateMatch = DateRegex.Match(content);
        if (dateMatch.Success)
        {
            var parsed = TryParseDate(dateMatch.Value);
            if (parsed is not null)
                result.DeliveryDate = new ExtractedField(parsed.Value.ToString("yyyy-MM-dd"), null);
        }

        var topLeft = ExtractTopLeftBlock(analyzeResult);
        if (!string.IsNullOrWhiteSpace(topLeft))
            result.Assignee = new ExtractedField(topLeft, null);
    }

    private static readonly Regex LegalFormRegex = new(
        @"\b(GmbH(?:\s*&\s*Co\.?\s*KG)?|AG|KG|OG|OHG|e\.?\s*U\.?|Ges\.?\s*m\.?\s*b\.?\s*H\.?|Gesellschaft\s+m\.?b\.?H\.?|Societas\s+Europaea|SE|KEG)\b",
        RegexOptions.Compiled);

    private static readonly Regex NoiseLineRegex = new(
        @"^(?:\d+[\s.,-]*)+$|^[A-Z]{1,3}-?\d{3,}|^(?:Tel|Fax|E-?Mail|Mail|Web|Mobil|Mob|UID|ATU|FN|DVR|IBAN|BIC)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string? FindDeliveryNoteNo(string content)
    {
        var labelHit = DeliveryNoteLabelRegex.Match(content);
        if (labelHit.Success)
        {
            var digits = DigitsOnly(labelHit.Groups[1].Value);
            if (digits.Length >= 4) return digits;
        }

        var lse = LseRegex.Match(content);
        if (lse.Success) return lse.Groups[1].Value;

        return null;
    }

    private static string DigitsOnly(string s)
    {
        var buf = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) if (char.IsDigit(c)) buf.Append(c);
        return buf.ToString();
    }

    private static string? ExtractTopLeftBlock(AnalyzeResult analyzeResult)
    {
        var page = analyzeResult.Pages?.FirstOrDefault();
        if (page is null || page.Lines is null || page.Width is not > 0 || page.Height is not > 0)
            return null;

        var maxX = page.Width.Value * 0.55f;
        var maxY = page.Height.Value * 0.30f;

        var lines = page.Lines
            .Select(l => new { Line = l, Box = CenterOf(l.Polygon) })
            .Where(x => x.Box is not null && x.Box!.Value.X <= maxX && x.Box.Value.Y <= maxY)
            .OrderBy(x => x.Box!.Value.Y)
            .ThenBy(x => x.Box!.Value.X)
            .Select(x => x.Line.Content?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();

        if (lines.Count == 0) return null;

        var legalFormIndex = lines.FindIndex(l => LegalFormRegex.IsMatch(l));
        if (legalFormIndex >= 0)
        {
            var match = LegalFormRegex.Match(lines[legalFormIndex]);
            var tail = lines[legalFormIndex][..(match.Index + match.Length)].Trim();

            if (legalFormIndex == 0) return Clean(tail);

            var prev = lines[legalFormIndex - 1];
            if (!LooksLikeNoise(prev) && prev.Length > 2)
                return Clean($"{prev} {tail}");
            return Clean(tail);
        }

        var firstReal = lines.FirstOrDefault(l => !LooksLikeNoise(l) && l.Length >= 3);
        return firstReal is null ? null : Clean(firstReal);
    }

    private static string Clean(string s)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim().TrimEnd(',', ';', '.', ':');
        return s.Length > 80 ? s[..80] : s;
    }

    private static bool LooksLikeNoise(string line) => NoiseLineRegex.IsMatch(line);

    private static (float X, float Y)? CenterOf(IReadOnlyList<float>? polygon)
    {
        if (polygon is null || polygon.Count < 2 || polygon.Count % 2 != 0)
            return null;
        float sx = 0, sy = 0;
        var pts = polygon.Count / 2;
        for (var i = 0; i < polygon.Count; i += 2)
        {
            sx += polygon[i];
            sy += polygon[i + 1];
        }
        return (sx / pts, sy / pts);
    }

    private static string SerializeSafely(AnalyzeResult analyzeResult)
    {
        var projection = new
        {
            modelId = analyzeResult.ModelId,
            apiVersion = analyzeResult.ApiVersion,
            content = analyzeResult.Content,
            pageCount = analyzeResult.Pages?.Count ?? 0,
            documents = analyzeResult.Documents?.Select(d => new
            {
                d.DocumentType,
                d.Confidence,
                fields = d.Fields?.ToDictionary(
                    kv => kv.Key,
                    kv => new { kv.Value.Content, kv.Value.Confidence })
            })
        };
        return JsonSerializer.Serialize(projection, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static DateOnly? TryParseDate(string raw)
    {
        string[] formats =
        {
            "d.M.yyyy", "dd.MM.yyyy", "d.M.yy", "dd.MM.yy",
            "d/M/yyyy", "dd/MM/yyyy",
            "d-M-yyyy", "dd-MM-yyyy",
            "yyyy-MM-dd"
        };
        if (DateOnly.TryParseExact(raw, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
