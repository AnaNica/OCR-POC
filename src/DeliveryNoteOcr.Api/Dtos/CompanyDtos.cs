namespace DeliveryNoteOcr.Api.Dtos;

public record CompanyDto(
    Guid Id,
    string Name,
    string? ExternalCode,
    bool IsActive,
    List<string> Aliases);

public record CreateCompanyDto(string Name, string? ExternalCode, List<string>? Aliases);

public record UpdateCompanyDto(string? Name, string? ExternalCode, bool? IsActive, List<string>? Aliases);
